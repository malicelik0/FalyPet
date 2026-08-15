using FalyPet.Core.Model;

namespace FalyPet.Core.Simulation;

/// <summary>
/// Oyunun bütün ayarlanabilir sayıları tek yerde. Denge değişikliği için başka
/// hiçbir dosyaya dokunulmamalı — sayılar koda dağılırsa denge ayarlamak imkânsızlaşır.
/// </summary>
public static class SimulationRules
{
    // ------------------------------------------------------------------ zaman

    /// <summary>
    /// Uygulama kapalıyken en fazla bu kadarlık ihtiyaç düşüşü uygulanır.
    /// Tavan olmasaydı tatilden dönen kullanıcı her ihtiyacı sıfırlanmış bir pet bulurdu.
    /// </summary>
    public static readonly TimeSpan MaxOfflineCatchUp = TimeSpan.FromHours(8);

    // ------------------------------------------------------- ihtiyaç azalması

    /// <summary>Saatlik doyum kaybı. Susuzluk en hızlı, temizlik en yavaş azalır.</summary>
    public static double DecayPerHour(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => 7.0,        // ~14 saatte dibe vurur, günde ~3 besleme
        NeedKind.Thirst => 9.0,        // ~11 saat, günde ~4 su
        NeedKind.Energy => 6.0,        // ~16 saat, günde 1 uyku
        NeedKind.Happiness => 5.0,     // ~20 saat, günde ~2 oyun
        NeedKind.Cleanliness => 3.0,   // ~33 saat, günde ~1 yıkama
        _ => 0.0,
    };

    /// <summary>Uyurken diğer ihtiyaçlar bu oranda azalır. Uyku bir mola olmalı, ceza değil.</summary>
    public const double SleepDecayMultiplier = 0.4;

    /// <summary>Uyurken saatlik enerji kazancı. ~4 saatte tam dolar.</summary>
    public const double EnergyRecoveryPerHour = 25.0;

    /// <summary>Bu enerjinin üstünde pet kendiliğinden uyanır.</summary>
    public const double WakeUpEnergy = 95.0;

    // --------------------------------------------------------- bakım eylemleri

    /// <summary>Eylem hangi ihtiyaca dokunuyor.</summary>
    public static NeedKind TargetNeed(CareAction action) => action switch
    {
        CareAction.Feed => NeedKind.Hunger,
        CareAction.Water => NeedKind.Thirst,
        CareAction.Play => NeedKind.Happiness,
        CareAction.Wash => NeedKind.Cleanliness,
        CareAction.Sleep => NeedKind.Energy,
        CareAction.Pet => NeedKind.Happiness,
        _ => NeedKind.Happiness,
    };

    /// <summary>Bu doyumun ÜSTÜNDEyken eylem bakım puanı vermez — tok pete yemek vermek büyütmez.</summary>
    public static double RewardThreshold(CareAction action) => action switch
    {
        CareAction.Feed => 60,
        CareAction.Water => 60,
        CareAction.Play => 70,
        CareAction.Wash => 50,
        CareAction.Sleep => 30,
        CareAction.Pet => 100,   // okşama her zaman sayılır, ama günlük sınırı var
        _ => 100,
    };

    /// <summary>Bu doyumun üstündeyken eylem tamamen REDDEDİLİR (pet tok/temiz, istemiyor).</summary>
    public static double RefuseThreshold(CareAction action) => action switch
    {
        CareAction.Feed => 92,
        CareAction.Water => 92,
        CareAction.Wash => 90,
        _ => double.MaxValue,
    };

    public static int CarePoints(CareAction action) => action switch
    {
        CareAction.Feed => 3,
        CareAction.Water => 3,
        CareAction.Play => 5,
        CareAction.Wash => 4,
        CareAction.Sleep => 6,
        CareAction.Pet => 1,
        _ => 0,
    };

    public static double Restore(CareAction action) => action switch
    {
        CareAction.Feed => 40,
        CareAction.Water => 45,
        CareAction.Play => 35,
        CareAction.Wash => 60,
        CareAction.Pet => 8,
        _ => 0,
    };

    /// <summary>Oyun yorar. Bedelsiz mutluluk olursa diğer ihtiyaçlar anlamsızlaşır.</summary>
    public const double PlayEnergyCost = 10.0;

    /// <summary>
    /// Gerçek bir ihtiyacı karşılamak mutluluğu da bu kadar artırır.
    /// Küskünlük kilitlenmesine karşı savunma: mutluluk tek bir eyleme bağlı kalmamalı.
    /// </summary>
    public const double HappinessBonusOnCare = 5.0;

    /// <summary>Okşama günlük sınırı. Olmasaydı büyüme tıklama hızına bağlı olurdu.</summary>
    public const int MaxPetsPerDay = 5;

    // ------------------------------------------------------------ sağlık/ruh hali

    /// <summary>Herhangi bir ihtiyaç bunun altına düşerse pet hastalanır.</summary>
    public const double SickThreshold = 20.0;

    /// <summary>Bunun üstünde hiçbir ihtiyaç yoksa pet mutlu görünür.</summary>
    public const double HappyThreshold = 70.0;

    /// <summary>Bu kadar süre kesintisiz hasta kalırsa küser.</summary>
    public static readonly TimeSpan SickToSulking = TimeSpan.FromHours(6);

    /// <summary>Küskünlükten çıkmak için TÜM ihtiyaçların bu değerin üstüne çıkması gerekir.</summary>
    public const double SulkRecoveryThreshold = 50.0;

    /// <summary>Küskünken bakım puanı bu oranla çarpılır. Büyümenin durması asıl cezadır.</summary>
    public const double SulkingCarePointMultiplier = 0.5;

    // ------------------------------------------------------------------ büyüme

    // ------------------------------------------------------------------ ekonomi

    /// <summary>
    /// Bakım puanı başına kazanılan coin. Coin ayrı bir kaynak değil, bakımın
    /// yan ürünü: ilgilenen kullanıcı zaten kazanır, ayrıca uğraşması gerekmez.
    /// </summary>
    public const int CoinsPerCarePoint = 2;

    /// <summary>
    /// Mini oyunlardan bir günde kazanılabilecek en fazla coin.
    ///
    /// Tavan olmasaydı oyun oynayan biri dakikalar içinde bütün dükkanı alırdı ve
    /// "coin bakımın yan ürünüdür" ilkesi çökerdi. Dikkatli bir kullanıcı bakımdan
    /// günde ~80 coin kazanıyor; oyun onun altında kalmalı, alternatifi değil takviyesi.
    /// </summary>
    public const int MaxGameCoinsPerDay = 60;

    // ------------------------------------------------------------------ büyüme

    /// <summary>Yumurtanın çatlaması için gereken okşama sayısı.</summary>
    public const int EggCracksRequired = 3;

    /// <summary>İki çatlak arasında beklenmesi gereken en az süre. Yumurta ~10 dakikada çıkar.</summary>
    public static readonly TimeSpan EggCrackCooldown = TimeSpan.FromMinutes(3);

    /// <summary>Bu aşamaya geçmek için gereken TOPLAM bakım puanı (kümülatif).</summary>
    public static int CarePointsToReach(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => 0,
        GrowthStage.Baby => 0,
        GrowthStage.Child => 60,
        GrowthStage.Teen => 210,   // 60 + 150
        GrowthStage.Adult => 510,  // 210 + 300
        _ => int.MaxValue,
    };

    public static GrowthStage? NextStage(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => GrowthStage.Baby,
        GrowthStage.Baby => GrowthStage.Child,
        GrowthStage.Child => GrowthStage.Teen,
        GrowthStage.Teen => GrowthStage.Adult,
        _ => null,
    };
}
