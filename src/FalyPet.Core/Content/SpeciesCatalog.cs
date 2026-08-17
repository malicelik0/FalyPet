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
public sealed record SpeciesDefinition(
    string Id,
    string DisplayName,
    BodyShape Body,
    EarType Ears,
    TailType Tail,
    MarkingType Marking,
    uint BaseColor,
    uint AccentColor);

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
        new("kedi",     "Kedi",     BodyShape.Round, EarType.Pointed, TailType.Thin,     MarkingType.Stripes, 0xE8A24C, 0xFFF0D8),
        new("kopek",    "Köpek",    BodyShape.Wide,  EarType.Floppy,  TailType.Curl,     MarkingType.Patch,   0xB07A46, 0xFFF0D8),
        new("tavsan",   "Tavşan",   BodyShape.Round, EarType.Tufts,   TailType.None,     MarkingType.Belly,   0xF2EDE4, 0xF6B8C8),
        new("ejderha",  "Ejderha",  BodyShape.Tall,  EarType.Horns,   TailType.Thin,     MarkingType.Belly,   0x6FBF73, 0xF2D06B),
        new("jole",     "Jöle",     BodyShape.Blob,  EarType.None,    TailType.None,     MarkingType.None,    0x6FC7D6, 0xE8FBFF),
        new("baykus",   "Baykuş",   BodyShape.Round, EarType.Tufts,   TailType.None,     MarkingType.Spots,   0x9B7BC4, 0xFFF0D8),
        new("tilki",    "Tilki",    BodyShape.Round, EarType.Pointed, TailType.Bushy,    MarkingType.Belly,   0xE0703C, 0xFFF4E6),
        new("panda",    "Panda",    BodyShape.Wide,  EarType.Round,   TailType.Thin,     MarkingType.Patch,   0xF4F4F0, 0x3A3A44),
        new("ahtapot",  "Ahtapot",  BodyShape.Blob,  EarType.None,    TailType.Tentacle, MarkingType.Spots,   0xE87FA8, 0xA85B96),
        new("hayalet",  "Hayalet",  BodyShape.Blob,  EarType.None,    TailType.None,     MarkingType.None,    0xC9D8F0, 0xFFFFFF),
        // Uğur böceği: yuvarlak kabuk, anten, kabuğu ikiye bölen çizgi ve benekler.
        new("ugurbocegi", "Uğur Böceği", BodyShape.Round, EarType.Antennae, TailType.None, MarkingType.LadybugShell, 0xD9342B, 0x241E22),
    ];

    public static SpeciesDefinition ById(string id) =>
        All.FirstOrDefault(s => s.Id == id) ?? All[0];

    public static bool Exists(string id) => All.Any(s => s.Id == id);
}
