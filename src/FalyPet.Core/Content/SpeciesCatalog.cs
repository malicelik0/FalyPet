namespace FalyPet.Core.Content;

public enum BodyShape { Round, Tall, Wide, Blob }
public enum EarType { None, Pointed, Floppy, Round, Horns, Tufts, Antennae }
public enum TailType { None, Thin, Bushy, Curl, Tentacle }
public enum MarkingType { None, Belly, Stripes, Spots, Patch, LadybugShell }

/// <summary>
/// Bir pet türünün tanımı. Sprite'lar bu tanımdan üretilir — yani yeni bir tür
/// eklemek bir satır veri demek, yeni bir kod yolu değil.
///
/// Renkler 0xRRGGBB.
/// </summary>
/// <summary>
/// Bir hayvanı tanınır kılan yüz/gövde ayrıntıları.
///
/// Kulak ve kuyruk tek başına yetmiyordu: pet'lerin hepsi aynı gövdeye takılmış
/// farklı kulaklar gibi duruyordu. Bir kediyi kedi yapan şey burnu ve bıyığı,
/// bir pandayı panda yapan şey göz çevresindeki siyah lekeler.
/// </summary>
[Flags]
public enum PetTrait
{
    None = 0,
    /// <summary>Öne çıkık ağız/burun bölgesi (kedi, köpek, tilki).</summary>
    Muzzle = 1 << 0,
    /// <summary>Üçgen burun (kedi, tilki).</summary>
    TriangleNose = 1 << 1,
    /// <summary>Yuvarlak ıslak burun (köpek, panda).</summary>
    RoundNose = 1 << 2,
    /// <summary>Gaga (baykuş).</summary>
    Beak = 1 << 3,
    Whiskers = 1 << 4,
    /// <summary>Gözü çevreleyen koyu leke (panda).</summary>
    EyePatch = 1 << 5,
    /// <summary>Bacak çizilmez (hayalet, jöle, ahtapot).</summary>
    NoLegs = 1 << 6,
    /// <summary>Alt kenarı dalgalı (hayalet).</summary>
    WavyBottom = 1 << 7,
    /// <summary>Sırt kanatları (ejderha).</summary>
    Wings = 1 << 8,
    /// <summary>Ön dişler (tavşan).</summary>
    BuckTeeth = 1 << 9,
    /// <summary>Açık renk yanaklar (tilki, baykuş).</summary>
    Cheeks = 1 << 10,
    /// <summary>
    /// İri yuvarlak gözler ve çevresinde açık renk disk (baykuş).
    /// Bu olmadan baykuş, uzun tüyleri yüzünden tavşandan ayırt edilemiyordu.
    /// </summary>
    BigEyes = 1 << 11,
}

public sealed record SpeciesDefinition(
    string Id,
    string DisplayName,
    BodyShape Body,
    EarType Ears,
    TailType Tail,
    MarkingType Marking,
    uint BaseColor,
    uint AccentColor,
    PetTrait Traits = PetTrait.None);

/// <summary>
/// İlk sürümün 10 türü.
///
/// Katalog Core'da duruyor çünkü hem onboarding (tür seçimi) hem de çizim katmanı
/// aynı listeye bakmalı. İkisi ayrı liste tutsaydı biri diğerine eklenmeyen bir
/// tür yüzünden sessizce bozulurdu.
/// </summary>
public static class SpeciesCatalog
{
    public static readonly IReadOnlyList<SpeciesDefinition> All =
    [
        new("kedi",     "Kedi",     BodyShape.Round, EarType.Pointed, TailType.Thin,     MarkingType.Stripes, 0xE8A24C, 0xFFF0D8,
            PetTrait.Muzzle | PetTrait.TriangleNose | PetTrait.Whiskers),

        new("kopek",    "Köpek",    BodyShape.Wide,  EarType.Floppy,  TailType.Curl,     MarkingType.Patch,   0xB07A46, 0xFFF0D8,
            PetTrait.Muzzle | PetTrait.RoundNose),

        new("tavsan",   "Tavşan",   BodyShape.Round, EarType.Tufts,   TailType.None,     MarkingType.Belly,   0xF2EDE4, 0xF6B8C8,
            PetTrait.Muzzle | PetTrait.TriangleNose | PetTrait.BuckTeeth | PetTrait.Whiskers),

        new("ejderha",  "Ejderha",  BodyShape.Tall,  EarType.Horns,   TailType.Thin,     MarkingType.Belly,   0x6FBF73, 0xF2D06B,
            PetTrait.Wings | PetTrait.Muzzle),

        new("jole",     "Jöle",     BodyShape.Blob,  EarType.None,    TailType.None,     MarkingType.None,    0x6FC7D6, 0xE8FBFF,
            PetTrait.NoLegs),

        new("baykus",   "Baykuş",   BodyShape.Round, EarType.Tufts,   TailType.None,     MarkingType.Spots,   0x9B7BC4, 0xFFF0D8,
            PetTrait.Beak | PetTrait.BigEyes),

        new("tilki",    "Tilki",    BodyShape.Round, EarType.Pointed, TailType.Bushy,    MarkingType.Belly,   0xE0703C, 0xFFF4E6,
            PetTrait.Muzzle | PetTrait.TriangleNose | PetTrait.Cheeks | PetTrait.Whiskers),

        new("panda",    "Panda",    BodyShape.Wide,  EarType.Round,   TailType.Thin,     MarkingType.None,    0xF4F4F0, 0x3A3A44,
            PetTrait.EyePatch | PetTrait.Muzzle | PetTrait.RoundNose),

        new("ahtapot",  "Ahtapot",  BodyShape.Blob,  EarType.None,    TailType.Tentacle, MarkingType.Spots,   0xE87FA8, 0xA85B96,
            PetTrait.NoLegs),

        new("hayalet",  "Hayalet",  BodyShape.Blob,  EarType.None,    TailType.None,     MarkingType.None,    0xC9D8F0, 0xFFFFFF,
            PetTrait.NoLegs | PetTrait.WavyBottom),

        new("ugurbocegi", "Uğur Böceği", BodyShape.Round, EarType.Antennae, TailType.None, MarkingType.LadybugShell, 0xD9342B, 0x241E22),
    ];

    public static SpeciesDefinition ById(string id) =>
        All.FirstOrDefault(s => s.Id == id) ?? All[0];

    public static bool Exists(string id) => All.Any(s => s.Id == id);
}
