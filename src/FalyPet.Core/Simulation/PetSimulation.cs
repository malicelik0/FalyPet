using FalyPet.Core.Model;
using FalyPet.Core.Persistence;

namespace FalyPet.Core.Simulation;

/// <summary>
/// Pet'in bütün kuralları. UI bağımlılığı yoktur ve zamanı asla kendisi okumaz —
/// her metot <c>now</c> alır. Bunun sebebi test edilebilirlik: 30 günlük bir yaşamı
/// saniyeler içinde simüle edebilmek, gerçekten 30 gün beklemekten farklı olmalı.
/// </summary>
public sealed class PetSimulation(PetSave state)
{
    private readonly PetSave _state = state ?? throw new ArgumentNullException(nameof(state));

    public PetSave State => _state;
    public GrowthStage Stage => _state.Stage;
    public Needs Needs => _state.Needs;
    public int CarePoints => _state.CarePoints;
    public int Coins => _state.Coins;
    public bool IsSleeping => _state.IsSleeping;

    /// <summary>Dükkandan alışveriş. Parası yetmezse hiçbir şey değişmez.</summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || _state.Coins < amount) return false;
        _state.Coins -= amount;
        return true;
    }

    /// <summary>
    /// Mini oyun ödülü. Günlük tavan uygulanır ve GERÇEKTEN verilen miktar döner —
    /// çağıran taraf kullanıcıya doğru sayıyı gösterebilsin diye. Tavan olmasaydı
    /// oyun, bakımın yerine geçen bir coin kaynağı olurdu.
    /// </summary>
    public int AwardGameCoins(int amount, DateTimeOffset now)
    {
        if (amount <= 0) return 0;

        var today = now.UtcDateTime.Date;
        if (_state.GameCoinsDateUtc?.UtcDateTime.Date != today)
        {
            _state.GameCoinsDateUtc = new DateTimeOffset(today, TimeSpan.Zero);
            _state.GameCoinsToday = 0;
        }

        var remaining = SimulationRules.MaxGameCoinsPerDay - _state.GameCoinsToday;
        var granted = Math.Min(amount, Math.Max(0, remaining));

        _state.GameCoinsToday += granted;
        _state.Coins += granted;
        return granted;
    }

    /// <summary>Bugün mini oyunlardan daha ne kadar kazanılabilir.</summary>
    public int RemainingGameCoinsToday(DateTimeOffset now)
    {
        var today = now.UtcDateTime.Date;
        if (_state.GameCoinsDateUtc?.UtcDateTime.Date != today) return SimulationRules.MaxGameCoinsPerDay;
        return Math.Max(0, SimulationRules.MaxGameCoinsPerDay - _state.GameCoinsToday);
    }

    /// <summary>Yeni bir pet yaratır. Yumurta aşamasından başlar.</summary>
    public static PetSave CreateNew(string speciesId, string name, DateTimeOffset now) => new()
    {
        SpeciesId = speciesId,
        Name = name,
        BornAtUtc = now,
        LastTickUtc = now,
        Stage = GrowthStage.Egg,
        Needs = new Needs(),
    };

    // ---------------------------------------------------------------- zaman ilerletme

    /// <summary>
    /// Simülasyonu <paramref name="now"/> anına kadar ilerletir.
    /// Hem saniyelik canlı tick hem de açılıştaki offline telafi aynı yoldan geçer —
    /// iki ayrı kod yolu olsaydı ikisi kaçınılmaz olarak ayrışırdı.
    /// </summary>
    public void Advance(DateTimeOffset now)
    {
        var elapsed = now - _state.LastTickUtc;

        // Kullanıcı saati geri aldı ya da saat dilimi değişti. Ne cezalandır ne ödüllendir.
        if (elapsed <= TimeSpan.Zero)
        {
            _state.LastTickUtc = now;
            return;
        }

        // Yumurtanın ihtiyacı yoktur. Olsaydı bir gece unutulan yumurta, çatladığı anda
        // zaten hasta bir bebek verirdi — kullanıcı daha pet'ini görmeden cezalanırdı.
        if (_state.Stage == GrowthStage.Egg)
        {
            _state.LastTickUtc = now;

            // Süre dolduysa okşanmasa da çıkar. Okşama onu HIZLANDIRIR, şart değildir.
            if (now - _state.BornAtUtc >= SimulationRules.EggHatchTimeout) Hatch(now);
            return;
        }

        if (elapsed > SimulationRules.MaxOfflineCatchUp)
            elapsed = SimulationRules.MaxOfflineCatchUp;

        var hours = elapsed.TotalHours;

        if (_state.IsSleeping)
        {
            _state.Needs.Add(NeedKind.Energy, SimulationRules.EnergyRecoveryPerHour * hours);

            foreach (var kind in Needs.All)
            {
                if (kind == NeedKind.Energy) continue;
                _state.Needs.Add(kind, -SimulationRules.DecayPerHour(kind) * hours * SimulationRules.SleepDecayMultiplier);
            }

            if (_state.Needs.Energy >= SimulationRules.WakeUpEnergy) WakeUp();
        }
        else
        {
            foreach (var kind in Needs.All)
                _state.Needs.Add(kind, -SimulationRules.DecayPerHour(kind) * hours);
        }

        UpdateHealth(now);
        _state.LastTickUtc = now;
    }

    private void UpdateHealth(DateTimeOffset now)
    {
        var isSick = _state.Needs.Lowest < SimulationRules.SickThreshold;

        if (isSick)
        {
            _state.SickSinceUtc ??= now;

            if (!_state.IsSulking && now - _state.SickSinceUtc.Value >= SimulationRules.SickToSulking)
                _state.IsSulking = true;
        }
        else
        {
            _state.SickSinceUtc = null;
        }

        // Küskünlükten çıkmak tek bir öğünle olmaz: tüm FİZİKSEL ihtiyaçların düzelmesi
        // gerekir. Mutluluk bilerek dışarıda — şart koşulsaydı pet kilitlenirdi, çünkü
        // küskünken oyun engelli ve okşamanın günlük sınırı mutluluğun saatlik
        // azalmasını karşılamıyor. Kural şu: "önce karnımı doyur, sonra oynarız."
        if (_state.IsSulking && _state.Needs.LowestPhysical >= SimulationRules.SulkRecoveryThreshold)
            _state.IsSulking = false;
    }

    // ---------------------------------------------------------------- bakım eylemleri

    /// <summary>Bir bakım eylemi uygular. Reddedilirse durum hiç değişmez.</summary>
    public CareResult Apply(CareAction action, DateTimeOffset now)
    {
        Advance(now);

        if (_state.Stage == GrowthStage.Egg)
            return action == CareAction.Pet ? CrackEgg(now) : CareResult.Reject("Henüz yumurtada.");

        if (_state.IsSleeping && action != CareAction.Sleep)
            return CareResult.Reject($"{_state.Name} uyuyor.");

        if (action == CareAction.Sleep) return ToggleSleep(now);

        // Küskün pet OYUNU reddeder ama okşanmayı reddetmez — barışma yolu okşamadır.
        // Okşama da engellenirse kilitlenme olur: küskünlükten çıkmak mutluluk ister,
        // mutluluğu yükselten iki eylem de engelliyse pet kalıcı olarak küskün kalır.
        if (_state.IsSulking && action == CareAction.Play)
            return CareResult.Reject($"{_state.Name} küskün, önce ihtiyaçlarını gider.");

        var need = SimulationRules.TargetNeed(action);
        var current = _state.Needs.Get(need);

        if (current >= SimulationRules.RefuseThreshold(action))
            return CareResult.Reject($"{_state.Name} şu an istemiyor.");

        if (action == CareAction.Pet && !TryConsumeDailyPet(now))
            return CareResult.Reject($"{_state.Name} bugünlük yeterince sevildi.");

        _state.Needs.Add(need, SimulationRules.Restore(action));
        if (action == CareAction.Play) _state.Needs.Add(NeedKind.Energy, -SimulationRules.PlayEnergyCost);

        var deservedReward = current < SimulationRules.RewardThreshold(action);

        // Gerçek bir ihtiyacı karşılamak pet'i biraz da mutlu eder. Kilitlenmeye karşı
        // ikinci savunma: mutluluk yalnızca oyuna bağlı kalmasın, ilgilenmenin kendisi
        // de mutluluk üretsin. Ayrıca gerçekçi — doyan hayvan keyiflenir.
        if (deservedReward && need != NeedKind.Happiness)
            _state.Needs.Add(NeedKind.Happiness, SimulationRules.HappinessBonusOnCare);

        var points = ApplySulkPenalty(deservedReward ? SimulationRules.CarePoints(action) : 0);

        AwardCarePoints(points);
        UpdateHealth(now);

        return CareResult.Accept(points, points == 0 ? "İhtiyacı yoktu, büyümeye saymadı." : "");
    }

    private CareResult CrackEgg(DateTimeOffset now)
    {
        if (_state.LastCrackUtc is { } last && now - last < SimulationRules.EggCrackCooldown)
            return CareResult.Reject("Yumurta ısınıyor, biraz bekle.");

        _state.EggCracks++;
        _state.LastCrackUtc = now;

        if (_state.EggCracks >= SimulationRules.EggCracksRequired)
        {
            Hatch(now);
            return CareResult.Accept(0, $"{_state.Name} yumurtadan çıktı!");
        }

        var remaining = SimulationRules.EggCracksRequired - _state.EggCracks;
        return CareResult.Accept(0, $"Yumurta çatladı! ({remaining} okşama kaldı)");
    }

    private void Hatch(DateTimeOffset now)
    {
        if (_state.Stage != GrowthStage.Egg) return;

        _state.Stage = GrowthStage.Baby;
        _state.EggCracks = SimulationRules.EggCracksRequired;
        _state.BornAtUtc = now;
    }

    /// <summary>Yumurtanın kendiliğinden çıkmasına kalan süre; yumurtada değilse null.</summary>
    public TimeSpan? EggTimeRemaining(DateTimeOffset now)
    {
        if (_state.Stage != GrowthStage.Egg) return null;
        var kalan = SimulationRules.EggHatchTimeout - (now - _state.BornAtUtc);
        return kalan > TimeSpan.Zero ? kalan : TimeSpan.Zero;
    }

    private CareResult ToggleSleep(DateTimeOffset now)
    {
        if (_state.IsSleeping)
        {
            var points = WakeUp();
            return CareResult.Accept(points, $"{_state.Name} uyandı.");
        }

        _state.IsSleeping = true;
        _state.SleepStartedTired = _state.Needs.Energy < SimulationRules.RewardThreshold(CareAction.Sleep);
        return CareResult.Accept(0, $"{_state.Name} uyuyor...");
    }

    /// <summary>Uyandırır ve hak edilmişse bakım puanını verir.</summary>
    private int WakeUp()
    {
        _state.IsSleeping = false;

        // Puan yalnızca uyku GERÇEKTEN dinlendirdiyse verilir: yorgun başlanmış ve
        // enerji toparlanmış olmalı. Yoksa uyut-uyandır döngüsü büyümeyi sömürürdü.
        var earned = _state.SleepStartedTired && _state.Needs.Energy >= SimulationRules.WakeUpEnergy;
        _state.SleepStartedTired = false;

        if (!earned) return 0;

        var points = ApplySulkPenalty(SimulationRules.CarePoints(CareAction.Sleep));
        AwardCarePoints(points);
        return points;
    }

    private int ApplySulkPenalty(int points) =>
        _state.IsSulking ? (int)Math.Floor(points * SimulationRules.SulkingCarePointMultiplier) : points;

    private bool TryConsumeDailyPet(DateTimeOffset now)
    {
        var today = now.UtcDateTime.Date;

        if (_state.PetsTodayDateUtc?.UtcDateTime.Date != today)
        {
            _state.PetsTodayDateUtc = new DateTimeOffset(today, TimeSpan.Zero);
            _state.PetsToday = 0;
        }

        if (_state.PetsToday >= SimulationRules.MaxPetsPerDay) return false;

        _state.PetsToday++;
        return true;
    }

    // ---------------------------------------------------------------- büyüme

    private void AwardCarePoints(int points)
    {
        if (points <= 0) return;

        _state.CarePoints += points;
        _state.Coins += points * SimulationRules.CoinsPerCarePoint;

        // Tek seferde birden çok aşama atlanabilir (uzun bir birikim sonrası).
        while (SimulationRules.NextStage(_state.Stage) is { } next
               && next != GrowthStage.Baby
               && _state.CarePoints >= SimulationRules.CarePointsToReach(next))
        {
            _state.Stage = next;
        }
    }

    // ---------------------------------------------------------------- görüntüleme

    public PetMood Mood
    {
        get
        {
            if (_state.IsSleeping) return PetMood.Sleeping;
            if (_state.IsSulking) return PetMood.Sulking;
            if (_state.Needs.Lowest < SimulationRules.SickThreshold) return PetMood.Sick;
            if (_state.Needs.Lowest >= SimulationRules.HappyThreshold) return PetMood.Happy;
            return PetMood.Neutral;
        }
    }

    /// <summary>Bir sonraki aşamaya ilerleme (0-1). Yetişkinse 1.</summary>
    public double GrowthProgress
    {
        get
        {
            if (SimulationRules.NextStage(_state.Stage) is not { } next) return 1.0;
            if (_state.Stage == GrowthStage.Egg)
                return (double)_state.EggCracks / SimulationRules.EggCracksRequired;

            var from = SimulationRules.CarePointsToReach(_state.Stage);
            var to = SimulationRules.CarePointsToReach(next);
            if (to <= from) return 1.0;

            return Math.Clamp((_state.CarePoints - from) / (double)(to - from), 0, 1);
        }
    }

    /// <summary>
    /// Büyüme neden ilerlemiyor? null = sorun yok.
    /// Bu metin kullanıcıya gösterilecek: sessizce duran bir ilerleme çubuğu
    /// "uygulama bozuk" demektir.
    /// </summary>
    public string? GrowthStallReason
    {
        get
        {
            if (_state.Stage == GrowthStage.Adult) return null;
            if (_state.IsSulking) return $"{_state.Name} küskün — büyüme yarı hızda.";

            var lowest = _state.Needs.LowestKind;
            if (_state.Needs.Get(lowest) < SimulationRules.SickThreshold)
                return $"{_state.Name} {NeedLabel(lowest)} — ilgilenmen lazım.";

            return null;
        }
    }

    public static string NeedLabel(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => "çok aç",
        NeedKind.Thirst => "çok susamış",
        NeedKind.Energy => "çok yorgun",
        NeedKind.Happiness => "çok mutsuz",
        NeedKind.Cleanliness => "çok kirli",
        _ => "iyi değil",
    };
}
