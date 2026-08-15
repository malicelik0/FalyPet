using FalyPet.Core.Model;

namespace FalyPet.Core.Simulation;

public enum AlertLevel { None = 0, Low = 1, Critical = 2 }

public sealed record NeedAlert(NeedKind Kind, AlertLevel Level, string Message);

/// <summary>
/// "Pet'in bir şeye ihtiyacı var" uyarılarına ne zaman izin verileceğine karar verir.
///
/// Bu sınıf olmadan uygulamada işlevsel bir boşluk var: pet gizliyken ya da kullanıcı
/// başka bir pencereyle çalışırken ihtiyaçlar sessizce dibe vuruyor. Büyüme bakım
/// EYLEMLERİNE bağlı olduğu için (geçen süreye değil), haber verilmeyen bir ihtiyaç
/// doğrudan büyümenin durması demek.
///
/// Aynı ölçüde önemli olan öbür taraf: 7/24 açık duran bir uygulama rahatsız edici
/// olursa silinir. O yüzden üç fren var:
///   1. Her ihtiyaç, her seviye için YALNIZCA BİR KEZ uyarır
///   2. Uyarı ancak ihtiyaç gerçekten düzelince (Reset eşiği) sıfırlanır
///   3. Uyarılar arasında genel bir bekleme süresi var
/// </summary>
public sealed class NeedAlertTracker
{
    /// <summary>Bu değerin altına düşen ihtiyaç için nazik bir hatırlatma.</summary>
    public const double LowThreshold = 35.0;

    /// <summary>Bu değerin altı acil: pet hastalanmak üzere.</summary>
    public const double CriticalThreshold = 15.0;

    /// <summary>İhtiyaç bu değerin üstüne çıkınca uyarı hakkı yenilenir.</summary>
    public const double ResetThreshold = 55.0;

    /// <summary>İki uyarı arasında en az bu kadar beklenir.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    private readonly Dictionary<NeedKind, AlertLevel> _announced = [];
    private DateTimeOffset? _lastAlertUtc;

    /// <summary>
    /// Şu an gösterilmesi gereken bir uyarı varsa döner, yoksa null.
    /// Uyarı döndüğünde "duyuruldu" olarak işaretlenir — çağıran taraf onu
    /// gerçekten göstermekle yükümlü.
    /// </summary>
    public NeedAlert? Poll(PetSimulation sim, DateTimeOffset now)
    {
        // Yumurtanın ihtiyacı yok; uyuyan pet'i de rahatsız etmiyoruz.
        if (sim.Stage == GrowthStage.Egg || sim.IsSleeping) return null;

        ForgetRecoveredNeeds(sim);

        if (_lastAlertUtc is { } last && now - last < Cooldown) return null;

        // En kötü durumdaki ihtiyaçtan başla: aynı anda iki şey eksikse
        // kullanıcıya önce daha acil olanı söylenmeli.
        foreach (var kind in Ordered(sim))
        {
            var level = LevelFor(sim.Needs.Get(kind));
            if (level == AlertLevel.None) continue;

            // Bu seviye (ya da daha kötüsü) zaten duyurulduysa tekrar etme.
            if (_announced.TryGetValue(kind, out var already) && already >= level) continue;

            _announced[kind] = level;
            _lastAlertUtc = now;

            return new NeedAlert(kind, level, Compose(sim, kind, level));
        }

        return null;
    }

    private IEnumerable<NeedKind> Ordered(PetSimulation sim) =>
        Needs.All.OrderBy(sim.Needs.Get);

    private void ForgetRecoveredNeeds(PetSimulation sim)
    {
        foreach (var kind in Needs.All)
        {
            if (_announced.ContainsKey(kind) && sim.Needs.Get(kind) >= ResetThreshold)
                _announced.Remove(kind);
        }
    }

    private static AlertLevel LevelFor(double value) => value switch
    {
        < CriticalThreshold => AlertLevel.Critical,
        < LowThreshold => AlertLevel.Low,
        _ => AlertLevel.None,
    };

    private static string Compose(PetSimulation sim, NeedKind kind, AlertLevel level)
    {
        var name = sim.State.Name;

        return level == AlertLevel.Critical
            ? $"{name} {PetSimulation.NeedLabel(kind)}!"
            : $"{name} {Request(kind)}";
    }

    private static string Request(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => "acıktı.",
        NeedKind.Thirst => "susadı.",
        NeedKind.Energy => "yoruldu.",
        NeedKind.Happiness => "canı sıkıldı.",
        NeedKind.Cleanliness => "yıkanmak istiyor.",
        _ => "bir şey istiyor.",
    };

    /// <summary>Pet sıfırlandığında ya da yeni bir pet yaratıldığında geçmişi temizler.</summary>
    public void Reset()
    {
        _announced.Clear();
        _lastAlertUtc = null;
    }
}
