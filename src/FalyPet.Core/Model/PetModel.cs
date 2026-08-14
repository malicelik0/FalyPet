namespace FalyPet.Core.Model;

/// <summary>
/// Pet'in ihtiyaçları.
/// DİKKAT: değerler "doyum"dur, "eksiklik" değil — 100 = ihtiyaç tamamen karşılanmış,
/// 0 = dibe vurmuş. <c>Hunger = 100</c> "pet tok" demektir, "çok aç" değil.
/// </summary>
public enum NeedKind
{
    Hunger,
    Thirst,
    Energy,
    Happiness,
    Cleanliness,
}

public enum GrowthStage
{
    Egg = 0,
    Baby = 1,
    Child = 2,
    Teen = 3,
    Adult = 4,
}

public enum CareAction
{
    Feed,
    Water,
    Play,
    Wash,
    Sleep,
    Pet,
}

public enum PetMood
{
    Happy,
    Neutral,
    /// <summary>Bir ihtiyaç dibe vurdu. Yavaşlar, oyun oynamaz.</summary>
    Sick,
    /// <summary>Uzun süre hasta bırakıldı. Köşeye çekilir, bakım puanı yarıya düşer.</summary>
    Sulking,
    Sleeping,
}

/// <summary>Beş ihtiyacın tutulduğu yer. Tüm değerler 0-100 arasına kıstırılır.</summary>
public sealed class Needs
{
    public double Hunger { get; set; } = 100;
    public double Thirst { get; set; } = 100;
    public double Energy { get; set; } = 100;
    public double Happiness { get; set; } = 100;
    public double Cleanliness { get; set; } = 100;

    public double Get(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => Hunger,
        NeedKind.Thirst => Thirst,
        NeedKind.Energy => Energy,
        NeedKind.Happiness => Happiness,
        NeedKind.Cleanliness => Cleanliness,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public void Set(NeedKind kind, double value)
    {
        value = Math.Clamp(value, 0, 100);
        switch (kind)
        {
            case NeedKind.Hunger: Hunger = value; break;
            case NeedKind.Thirst: Thirst = value; break;
            case NeedKind.Energy: Energy = value; break;
            case NeedKind.Happiness: Happiness = value; break;
            case NeedKind.Cleanliness: Cleanliness = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public void Add(NeedKind kind, double delta) => Set(kind, Get(kind) + delta);

    public static readonly NeedKind[] All =
        [NeedKind.Hunger, NeedKind.Thirst, NeedKind.Energy, NeedKind.Happiness, NeedKind.Cleanliness];

    /// <summary>
    /// Mutluluk hariç ihtiyaçlar. Küskünlükten çıkış bunlara bakar: mutluluğu da
    /// şart koşmak kilitlenme üretiyordu, çünkü küskünken oyun engelli ve okşamanın
    /// günlük sınırı mutluluğun saatlik azalmasını karşılamıyor.
    /// </summary>
    public static readonly NeedKind[] Physical =
        [NeedKind.Hunger, NeedKind.Thirst, NeedKind.Energy, NeedKind.Cleanliness];

    public NeedKind LowestKind
    {
        get
        {
            var lowest = All[0];
            foreach (var kind in All)
                if (Get(kind) < Get(lowest)) lowest = kind;
            return lowest;
        }
    }

    public double Lowest => Get(LowestKind);

    /// <summary>Mutluluk hariç en düşük ihtiyaç.</summary>
    public double LowestPhysical
    {
        get
        {
            var lowest = 100.0;
            foreach (var kind in Physical) lowest = Math.Min(lowest, Get(kind));
            return lowest;
        }
    }

    public Needs Clone() => new()
    {
        Hunger = Hunger, Thirst = Thirst, Energy = Energy,
        Happiness = Happiness, Cleanliness = Cleanliness,
    };
}

/// <summary>Bir bakım eyleminin sonucu. Reddedildiyse <see cref="Reason"/> kullanıcıya gösterilir.</summary>
public sealed record CareResult(bool Accepted, int CarePointsGained, string Reason)
{
    public static CareResult Reject(string reason) => new(false, 0, reason);
    public static CareResult Accept(int points, string reason = "") => new(true, points, reason);
}
