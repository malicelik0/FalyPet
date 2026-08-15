namespace FalyPet.Core.Persistence;

/// <summary>
/// Diske yazılan tüm kalıcı durum. Şema değişirse <see cref="CurrentVersion"/> artırılır
/// ve <see cref="SaveStore"/> içine göç (migration) eklenir.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Pencerenin en son bırakıldığı yer.</summary>
    public WindowSave Window { get; set; } = new();

    /// <summary>
    /// Uygulamanın en son kayıt yaptığı an (UTC). Offline ihtiyaç telafisinin dayanağı budur;
    /// yerel saat DEĞİL — kullanıcı saat dilimi değiştirince simülasyon bozulmasın diye.
    /// </summary>
    public DateTimeOffset LastSavedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Pet henüz yaratılmadıysa null. Onboarding'in yapılıp yapılmadığının tek göstergesi.</summary>
    public PetSave? Pet { get; set; }
}

public sealed class WindowSave
{
    /// <summary>
    /// DIP cinsinden sol kenar. null = pet hiç konumlanmadı, varsayılan köşeye yerleştirilecek.
    ///
    /// Bilerek nullable, NaN değil: System.Text.Json NaN'ı serileştiremez ve fırlatır.
    /// Sentinel olarak NaN kullanmak, pencere konumlanmadan kapanan bir oturumda
    /// kaydın çökmesine yol açıyordu.
    /// </summary>
    public double? X { get; set; }

    /// <summary>DIP cinsinden üst kenar. null = hiç konumlanmadı.</summary>
    public double? Y { get; set; }

    /// <summary>Kullanıcı tepsiden gizlediyse true; sonraki açılışta gizli başlar.</summary>
    public bool Hidden { get; set; }
}

/// <summary>
/// Pet'in tüm kalıcı durumu. <see cref="Simulation.PetSimulation"/> bu nesnenin
/// üstünde çalışır — simülasyonun kendi ayrı bir durumu yoktur, böylece
/// "kaydedilen" ile "simüle edilen" asla ayrışamaz.
/// </summary>
public sealed class PetSave
{
    public string SpeciesId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset BornAtUtc { get; set; }

    /// <summary>Simülasyonun en son ilerletildiği an. Offline telafinin dayanağı.</summary>
    public DateTimeOffset LastTickUtc { get; set; }

    public Model.GrowthStage Stage { get; set; } = Model.GrowthStage.Egg;

    /// <summary>Yumurta aşamasındaki çatlak sayısı (0-3).</summary>
    public int EggCracks { get; set; }

    /// <summary>Son çatlağın zamanı — çatlaklar arası bekleme süresi için.</summary>
    public DateTimeOffset? LastCrackUtc { get; set; }

    /// <summary>Toplam kazanılmış bakım puanı. Büyüme eşikleri kümülatiftir.</summary>
    public int CarePoints { get; set; }

    public Model.Needs Needs { get; set; } = new();

    public bool IsSleeping { get; set; }

    /// <summary>Uykuya yorgun başlandıysa uyanışta bakım puanı verilir.</summary>
    public bool SleepStartedTired { get; set; }

    /// <summary>Kesintisiz hastalığın başladığı an; sağlıklıysa null.</summary>
    public DateTimeOffset? SickSinceUtc { get; set; }

    public bool IsSulking { get; set; }

    /// <summary>Okşama günlük sayacı ve hangi güne ait olduğu (UTC tarihi).</summary>
    public int PetsToday { get; set; }
    public DateTimeOffset? PetsTodayDateUtc { get; set; }

    public int Coins { get; set; }

    /// <summary>Mini oyunlardan bugün kazanılan coin ve hangi güne ait olduğu (UTC).</summary>
    public int GameCoinsToday { get; set; }
    public DateTimeOffset? GameCoinsDateUtc { get; set; }

    /// <summary>Mini oyundaki en yüksek skor — kullanıcının kendisiyle yarışması için.</summary>
    public int CatchHighScore { get; set; }

    /// <summary>Satın alınmış eşya kimlikleri.</summary>
    public List<string> OwnedItems { get; set; } = [];

    /// <summary>Pet'in üstünde takılı olan kostüm; yoksa null.</summary>
    public string? EquippedCostumeId { get; set; }
}
