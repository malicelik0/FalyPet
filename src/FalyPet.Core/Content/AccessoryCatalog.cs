namespace FalyPet.Core.Content;

public enum AccessoryType { None, Hat, Bow, Glasses, Scarf, Crown }

public sealed record AccessoryDefinition(
    string Id,
    string DisplayName,
    AccessoryType Type,
    int Price,
    uint Color);

/// <summary>
/// Dükkandaki eşyalar.
///
/// Bilerek yalnızca KALICI aksesuar; tüketilebilir yiyecek yok. Sebepleri:
/// 1. Yiyecek envanteri, tüketim ve "hangi yemek ne kadar doyurur" dengesi
///    ekonominin karmaşıklığını ikiye katlar ve bakım sistemiyle çakışır.
/// 2. Masaüstü pet'inde insanların coin biriktirip almak istediği şey pet'in
///    görünümüdür. Kalıcı olduğu için de her alışveriş kalıcı bir kazanç olur.
///
/// Coin bakım eylemlerinden geliyor, yani dükkan ilgilenmenin ödülü.
/// </summary>
public static class AccessoryCatalog
{
    public static readonly IReadOnlyList<AccessoryDefinition> All =
    [
        new("fiyonk",  "Fiyonk",  AccessoryType.Bow,      40, 0xE8567A),
        new("sapka",   "Şapka",   AccessoryType.Hat,      70, 0x4C7BE8),
        new("gozluk",  "Gözlük",  AccessoryType.Glasses, 110, 0x3A3A44),
        new("atki",    "Atkı",    AccessoryType.Scarf,   160, 0xD94F3D),
        new("tac",     "Taç",     AccessoryType.Crown,   320, 0xF2C14E),
    ];

    public static AccessoryDefinition? ById(string? id) =>
        id is null ? null : All.FirstOrDefault(a => a.Id == id);

    public static AccessoryType TypeOf(string? id) => ById(id)?.Type ?? AccessoryType.None;
}
