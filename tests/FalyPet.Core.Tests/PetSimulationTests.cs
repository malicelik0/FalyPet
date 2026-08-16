using FalyPet.Core.Model;
using FalyPet.Core.Simulation;

namespace FalyPet.Core.Tests;

/// <summary>
/// Simülasyonun testleri. Buradaki her senaryo gerçek zamanda günler sürerdi;
/// simülasyon saati kendisi okumadığı (her metot <c>now</c> aldığı) için
/// milisaniyelerde koşuyor. Zamanı dışarıdan vermenin asıl sebebi bu.
/// </summary>
public sealed class PetSimulationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Yumurtayı çatlatıp bebek aşamasına geçmiş, tüm ihtiyaçları tam bir pet.</summary>
    private static PetSimulation NewBaby(out DateTimeOffset now)
    {
        var sim = new PetSimulation(PetSimulation.CreateNew("kedi", "Momo", T0));
        now = T0;

        for (var i = 0; i < SimulationRules.EggCracksRequired; i++)
        {
            now += SimulationRules.EggCrackCooldown;
            sim.Apply(CareAction.Pet, now);
        }

        Assert.Equal(GrowthStage.Baby, sim.Stage);
        return sim;
    }

    /// <summary>
    /// Zamanı KÜÇÜK ADIMLARLA ilerletir — açık duran bir uygulamanın tick'i gibi.
    /// Tek büyük Advance çağrısı 8 saatlik offline tavanına takılır ve ihtiyaçlar
    /// hiçbir zaman dibe vurmaz; testlerin bunu taklit etmesi şart.
    /// </summary>
    private static void Ilerlet(PetSimulation sim, ref DateTimeOffset now, TimeSpan toplam)
    {
        var step = TimeSpan.FromMinutes(15);
        var hedef = now + toplam;
        while (now < hedef)
        {
            now = now + step > hedef ? hedef : now + step;
            sim.Advance(now);
        }
    }

    private static void Kuskunlestir(PetSimulation sim, ref DateTimeOffset now)
    {
        var limit = now + TimeSpan.FromDays(3);
        while (sim.Mood != PetMood.Sulking && now < limit)
            Ilerlet(sim, ref now, TimeSpan.FromMinutes(30));

        Assert.Equal(PetMood.Sulking, sim.Mood);
    }

    // ------------------------------------------------------------------ zaman

    [Fact]
    public void Offline_kalma_8_saatle_sinirli()
    {
        var sim = NewBaby(out var now);

        // Üç gün kapalı kal. Tavan olmasaydı her ihtiyaç sıfırlanırdı.
        sim.Advance(now + TimeSpan.FromDays(3));

        Assert.Equal(100 - 8 * 9, sim.Needs.Thirst, precision: 1);
        Assert.Equal(100 - 8 * 7, sim.Needs.Hunger, precision: 1);
    }

    [Fact]
    public void Saat_geri_alinirsa_hicbir_sey_degismez()
    {
        var sim = NewBaby(out var now);
        var before = sim.Needs.Clone();

        sim.Advance(now - TimeSpan.FromHours(5));

        Assert.Equal(before.Hunger, sim.Needs.Hunger);
        Assert.Equal(before.Thirst, sim.Needs.Thirst);
    }

    [Fact]
    public void Yumurtanin_ihtiyaci_azalmaz()
    {
        var sim = new PetSimulation(PetSimulation.CreateNew("kedi", "Momo", T0));

        // Çıkma süresinin altında kalınıyor; amaç ihtiyaçların azalmadığını görmek.
        sim.Advance(T0 + SimulationRules.EggHatchTimeout - TimeSpan.FromSeconds(1));

        Assert.Equal(100, sim.Needs.Hunger);
        Assert.Equal(GrowthStage.Egg, sim.Stage);
    }

    [Fact]
    public void Yumurta_hic_oksanmasa_da_sure_dolunca_cikar()
    {
        var sim = new PetSimulation(PetSimulation.CreateNew("kedi", "Momo", T0));

        sim.Advance(T0 + SimulationRules.EggHatchTimeout);

        Assert.Equal(GrowthStage.Baby, sim.Stage);
    }

    [Fact]
    public void Yumurta_20_oksamada_beklemeden_cikar()
    {
        var sim = new PetSimulation(PetSimulation.CreateNew("kedi", "Momo", T0));
        var now = T0;

        // Sabırsız kullanıcı: art arda tıklıyor, 5 dakika beklemiyor.
        for (var i = 0; i < SimulationRules.EggCracksRequired; i++)
        {
            now += SimulationRules.EggCrackCooldown;
            sim.Apply(CareAction.Pet, now);
        }

        Assert.Equal(GrowthStage.Baby, sim.Stage);

        // Toplam süre 5 dakikanın çok altında olmalı — yoksa "veya" değil "ve" olurdu.
        Assert.True(now - T0 < TimeSpan.FromSeconds(10), $"gecen sure: {now - T0}");
    }

    // ------------------------------------------------------------------ yumurta

    [Fact]
    public void Yumurta_uc_catlakta_cikar()
    {
        var sim = new PetSimulation(PetSimulation.CreateNew("kedi", "Momo", T0));
        var now = T0;

        for (var i = 0; i < SimulationRules.EggCracksRequired - 1; i++)
        {
            now += SimulationRules.EggCrackCooldown;
            Assert.True(sim.Apply(CareAction.Pet, now).Accepted);
            Assert.Equal(GrowthStage.Egg, sim.Stage);
        }

        now += SimulationRules.EggCrackCooldown;
        sim.Apply(CareAction.Pet, now);

        Assert.Equal(GrowthStage.Baby, sim.Stage);
    }

    [Fact]
    public void Ayni_tiklama_cift_saymaz()
    {
        // Bekleme süresi artık yalnızca tek tıklamanın iki kez işlenmesini
        // engelliyor; hızlı tıklamayı yavaşlatmak gibi bir amacı yok.
        var sim = new PetSimulation(PetSimulation.CreateNew("kedi", "Momo", T0));
        var now = T0 + SimulationRules.EggCrackCooldown;

        sim.Apply(CareAction.Pet, now);
        var ikinci = sim.Apply(CareAction.Pet, now + TimeSpan.FromMilliseconds(20));

        Assert.False(ikinci.Accepted);
        Assert.Equal(1, sim.State.EggCracks);
    }

    // ------------------------------------------------------------- bakım eylemleri

    [Fact]
    public void Tok_pet_yemegi_reddeder()
    {
        var sim = NewBaby(out var now);

        var result = sim.Apply(CareAction.Feed, now);

        Assert.False(result.Accepted);
        Assert.Equal(0, sim.CarePoints);
    }

    [Fact]
    public void Ac_pet_beslenince_bakim_puani_kazanir()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(7)); // açlık 100 -> 51, ödül eşiği 60'ın altı

        var result = sim.Apply(CareAction.Feed, now);

        Assert.True(result.Accepted);
        Assert.Equal(SimulationRules.CarePoints(CareAction.Feed), result.CarePointsGained);
    }

    [Fact]
    public void Az_ac_pet_beslenir_ama_buyumeye_saymaz()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(2)); // açlık 86: reddetme eşiğinin altı, ödül eşiğinin üstü

        var result = sim.Apply(CareAction.Feed, now);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.CarePointsGained);
    }

    [Fact]
    public void Beslemeyi_spamlamak_buyutmez()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(10));

        // Aynı dakika içinde 50 kez besle. İlki açlığı doyurur, gerisi reddedilir.
        for (var i = 0; i < 50; i++) sim.Apply(CareAction.Feed, now + TimeSpan.FromSeconds(i));

        Assert.Equal(SimulationRules.CarePoints(CareAction.Feed), sim.CarePoints);
    }

    [Fact]
    public void Oksama_gunde_bes_kezle_sinirli()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(1));

        var kabul = 0;
        for (var i = 0; i < 20; i++)
            if (sim.Apply(CareAction.Pet, now + TimeSpan.FromSeconds(i * 10)).Accepted) kabul++;

        Assert.Equal(SimulationRules.MaxPetsPerDay, kabul);
    }

    [Fact]
    public void Oyun_yorar()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(8));
        var enerjiOncesi = sim.Needs.Energy;

        sim.Apply(CareAction.Play, now);

        Assert.True(sim.Needs.Energy < enerjiOncesi);
    }

    [Fact]
    public void Ihtiyac_karsilamak_mutlulugu_da_artirir()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(8));
        var mutlulukOncesi = sim.Needs.Happiness;

        sim.Apply(CareAction.Feed, now);

        Assert.Equal(mutlulukOncesi + SimulationRules.HappinessBonusOnCare, sim.Needs.Happiness, precision: 1);
    }

    // ------------------------------------------------------------------ uyku

    [Fact]
    public void Yorgun_uyku_puan_verir_enerjiyi_doldurur()
    {
        var sim = NewBaby(out var now);
        sim.State.Needs.Energy = 25; // yorgun ama diğer ihtiyaçları tam — hasta değil
        now += TimeSpan.FromMinutes(1);

        sim.Apply(CareAction.Sleep, now);
        Assert.True(sim.IsSleeping);

        var puanOncesi = sim.CarePoints;
        Ilerlet(sim, ref now, TimeSpan.FromHours(4)); // 25/saat ile dolar, kendiliğinden uyanır

        Assert.False(sim.IsSleeping);
        Assert.Equal(puanOncesi + SimulationRules.CarePoints(CareAction.Sleep), sim.CarePoints);
    }

    [Fact]
    public void Uyut_uyandir_dongusu_puan_vermez()
    {
        var sim = NewBaby(out var now);
        sim.State.Needs.Energy = 25;
        now += TimeSpan.FromMinutes(1);

        // İstismar denemesi: yorgunken uyut, hemen uyandır, tekrarla.
        for (var i = 0; i < 20; i++)
        {
            sim.Apply(CareAction.Sleep, now + TimeSpan.FromSeconds(i * 2));
            sim.Apply(CareAction.Sleep, now + TimeSpan.FromSeconds(i * 2 + 1));
        }

        Assert.Equal(0, sim.CarePoints);
    }

    [Fact]
    public void Uykudayken_baska_eylem_kabul_edilmez()
    {
        var sim = NewBaby(out var now);
        sim.State.Needs.Energy = 25;
        now += TimeSpan.FromMinutes(1);
        sim.Apply(CareAction.Sleep, now);

        var result = sim.Apply(CareAction.Feed, now + TimeSpan.FromMinutes(1));

        Assert.False(result.Accepted);
    }

    // ------------------------------------------------------------ hastalık / küskünlük

    [Fact]
    public void Ihtiyac_dibe_vurunca_hastalanir()
    {
        var sim = NewBaby(out var now);

        Ilerlet(sim, ref now, TimeSpan.FromHours(10)); // susuzluk 100 -> 10

        Assert.Equal(PetMood.Sick, sim.Mood);
    }

    [Fact]
    public void Uzun_sure_hasta_kalirsa_kuser()
    {
        var sim = NewBaby(out var now);

        Ilerlet(sim, ref now, TimeSpan.FromHours(10));
        Assert.Equal(PetMood.Sick, sim.Mood);

        Ilerlet(sim, ref now, SimulationRules.SickToSulking + TimeSpan.FromMinutes(30));

        Assert.Equal(PetMood.Sulking, sim.Mood);
    }

    [Fact]
    public void Kuskun_pet_oyunu_reddeder_ama_oksanmayi_kabul_eder()
    {
        var sim = NewBaby(out var now);
        Kuskunlestir(sim, ref now);

        Assert.False(sim.Apply(CareAction.Play, now).Accepted);
        Assert.True(sim.Apply(CareAction.Pet, now).Accepted);   // barışma yolu açık kalmalı
        Assert.True(sim.Apply(CareAction.Feed, now).Accepted);
    }

    [Fact]
    public void Kuskunken_bakim_puani_yariya_duser()
    {
        var sim = NewBaby(out var now);
        Kuskunlestir(sim, ref now);

        var result = sim.Apply(CareAction.Feed, now);

        Assert.Equal(SimulationRules.CarePoints(CareAction.Feed) / 2, result.CarePointsGained);
    }

    [Fact]
    public void Kuskunlukten_cikmak_fiziksel_ihtiyaclarin_duzelmesini_ister()
    {
        var sim = NewBaby(out var now);
        Kuskunlestir(sim, ref now);

        // Sadece beslemek yetmez — susuzluk ve temizlik hâlâ dipte.
        sim.Apply(CareAction.Feed, now);
        Assert.Equal(PetMood.Sulking, sim.Mood);

        for (var i = 0; i < 4; i++)
        {
            now += TimeSpan.FromSeconds(1);
            sim.Apply(CareAction.Feed, now);
            sim.Apply(CareAction.Water, now);
            sim.Apply(CareAction.Wash, now);
        }

        // Enerji hâlâ dipte ve onu yalnızca uyku geri getirir; dördü de düzelmeden barışma yok.
        Assert.Equal(PetMood.Sulking, sim.Mood);

        sim.Apply(CareAction.Sleep, now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(5));

        Assert.NotEqual(PetMood.Sulking, sim.Mood);
    }

    /// <summary>
    /// REGRESYON: küskünlük bir zamanlar kilitlenme üretiyordu. Küskünlükten çıkmak
    /// mutluluk şart koşuyordu ama küskünken hem oyun hem okşama engelliydi — pet
    /// kalıcı olarak küskün kalıyordu. İlgilenen kullanıcı HER ZAMAN barışabilmeli.
    /// </summary>
    [Fact]
    public void Kuskun_pet_asla_kilitlenmez()
    {
        var sim = NewBaby(out var now);
        Kuskunlestir(sim, ref now);

        for (var i = 0; i < 100 && sim.Mood == PetMood.Sulking; i++)
        {
            now += TimeSpan.FromMinutes(5);
            sim.Advance(now);
            IhtiyaciKarsila(sim, now);
        }

        Assert.NotEqual(PetMood.Sulking, sim.Mood);
    }

    [Fact]
    public void Buyume_durunca_kullaniciya_sebep_gosterilir()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(10));

        var reason = sim.GrowthStallReason;

        Assert.NotNull(reason);
        Assert.Contains("Momo", reason);
    }

    // ------------------------------------------------------------------ ekonomi

    [Fact]
    public void Bakim_puani_kazanmak_coin_de_kazandirir()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(7));

        var result = sim.Apply(CareAction.Feed, now);

        Assert.Equal(result.CarePointsGained * SimulationRules.CoinsPerCarePoint, sim.Coins);
    }

    [Fact]
    public void Puan_getirmeyen_bakim_coin_de_getirmez()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(2)); // aç ama ödül eşiğinin üstünde

        sim.Apply(CareAction.Feed, now);

        Assert.Equal(0, sim.Coins);
    }

    [Fact]
    public void Parasi_yetmeyen_alisveris_hicbir_seyi_degistirmez()
    {
        var sim = NewBaby(out var now);
        Ilerlet(sim, ref now, TimeSpan.FromHours(7));
        sim.Apply(CareAction.Feed, now);

        var oncesi = sim.Coins;

        Assert.False(sim.TrySpendCoins(oncesi + 1));
        Assert.Equal(oncesi, sim.Coins);

        Assert.True(sim.TrySpendCoins(oncesi));
        Assert.Equal(0, sim.Coins);
    }

    [Fact]
    public void Mini_oyun_coinleri_gunluk_tavana_takilir()
    {
        var sim = NewBaby(out var now);

        // Tavanın üstünde bir ödül iste; yalnızca tavan kadarı verilmeli.
        var ilk = sim.AwardGameCoins(SimulationRules.MaxGameCoinsPerDay + 100, now);
        Assert.Equal(SimulationRules.MaxGameCoinsPerDay, ilk);

        // Aynı gün ikinci oyun hiç coin getirmez.
        Assert.Equal(0, sim.AwardGameCoins(50, now));
        Assert.Equal(0, sim.RemainingGameCoinsToday(now));

        // Ertesi gün sayaç sıfırlanır.
        var yarin = now.AddDays(1);
        Assert.Equal(SimulationRules.MaxGameCoinsPerDay, sim.RemainingGameCoinsToday(yarin));
        Assert.Equal(30, sim.AwardGameCoins(30, yarin));
    }

    [Fact]
    public void Mini_oyun_coinleri_bakim_coinlerinden_ayri_sayilir()
    {
        var sim = NewBaby(out var now);

        sim.AwardGameCoins(SimulationRules.MaxGameCoinsPerDay, now);
        var oyundanSonra = sim.Coins;

        // Oyun tavanı dolmuş olsa bile bakım coin vermeye devam etmeli:
        // tavan oyunu sınırlar, ilgilenmeyi değil.
        Ilerlet(sim, ref now, TimeSpan.FromHours(7));
        sim.Apply(CareAction.Feed, now);

        Assert.True(sim.Coins > oyundanSonra);
    }

    // ------------------------------------------------------------------ büyüme

    [Fact]
    public void Asama_esikleri_kumulatif_ilerler()
    {
        var sim = NewBaby(out var now);

        sim.State.CarePoints = SimulationRules.CarePointsToReach(GrowthStage.Child) - 1;
        Assert.Equal(GrowthStage.Baby, sim.Stage);

        Ilerlet(sim, ref now, TimeSpan.FromHours(1));
        sim.Apply(CareAction.Pet, now); // tek puan eşiği aşmalı

        Assert.Equal(GrowthStage.Child, sim.Stage);
    }

    /// <summary>
    /// PLANIN ASIL İDDİASI: dikkatli bir kullanıcı yaklaşık 2 haftada yetişkine ulaşır.
    /// Denge sayıları değişirse bu test kırılır ve haber verir.
    /// </summary>
    [Fact]
    public void Dikkatli_kullanici_iki_haftada_yetiskine_ulasir()
    {
        var sim = NewBaby(out var now);
        var gun = 0;

        // Günde 16 saat uyanık, her 30 dakikada bir pete bakan bir kullanıcı.
        for (; gun < 60 && sim.Stage != GrowthStage.Adult; gun++)
        {
            for (var yarimSaat = 0; yarimSaat < 32; yarimSaat++)
            {
                Ilerlet(sim, ref now, TimeSpan.FromMinutes(30));
                IhtiyaciKarsila(sim, now);
            }

            now += TimeSpan.FromHours(8); // gece: uygulama kapalı
            sim.Advance(now);
        }

        Assert.Equal(GrowthStage.Adult, sim.Stage);
        Assert.InRange(gun, 8, 20); // ~2 hafta
    }

    [Fact]
    public void Ilgilenmeyen_kullanicinin_peti_hic_buyumez()
    {
        var sim = NewBaby(out var now);

        for (var i = 0; i < 30; i++)
        {
            now += TimeSpan.FromDays(1);
            sim.Advance(now);
        }

        Assert.Equal(GrowthStage.Baby, sim.Stage);
        Assert.Equal(0, sim.CarePoints);
        Assert.Equal(PetMood.Sulking, sim.Mood);
    }

    /// <summary>
    /// En düşük ihtiyacı olan eylemden başlayarak dener; reddedilirse sıradakine geçer.
    /// Reddedilince pes etmemesi önemli — küskünken oyun reddedilir ve
    /// "hiçbir şey yapmadan dön" davranışı testi sonsuza kadar takar.
    /// </summary>
    private static void IhtiyaciKarsila(PetSimulation sim, DateTimeOffset now)
    {
        if (sim.IsSleeping) return;

        if (sim.Needs.Energy < SimulationRules.RewardThreshold(CareAction.Sleep))
        {
            sim.Apply(CareAction.Sleep, now);
            return;
        }

        var adaylar = new[]
        {
            (need: NeedKind.Hunger, action: CareAction.Feed),
            (need: NeedKind.Thirst, action: CareAction.Water),
            (need: NeedKind.Cleanliness, action: CareAction.Wash),
            (need: NeedKind.Happiness, action: CareAction.Play),
        }.OrderBy(x => sim.Needs.Get(x.need));

        foreach (var (need, action) in adaylar)
        {
            if (sim.Needs.Get(need) >= SimulationRules.RewardThreshold(action)) continue;
            if (sim.Apply(action, now).Accepted) return;
        }

        sim.Apply(CareAction.Pet, now);
    }
}
