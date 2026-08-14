using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FalyPet.App.Rendering;

/// <summary>
/// Faz 0 için geçici sprite: 32x32 pixel-art bir yumurta, gerçek alfa kanalıyla.
///
/// Bilerek dolu bir dikdörtgen değil — Faz 0'da doğrulanması gereken asıl mekanizma
/// "şeffaf piksele tıklayınca alttaki pencereye geçiyor mu" ve bunu ancak gerçekten
/// şeffaf kenarları olan bir görüntü test eder. Faz 1'de bu sınıfın yerini sprite
/// sheet yükleyici alacak; pencerenin geri kalanı hiç değişmeyecek.
/// </summary>
internal static class PlaceholderSprite
{
    public const int Size = 32;

    // Yumurta geometrisi. Üst yarı dar ve uzun bir kubbe, alt yarı yarım daire —
    // gerçek yumurtanın en geniş yeri ortanın altındadır.
    private const double Top = 3.0;
    private const double Bottom = 29.0;
    private const double CenterX = 15.5;
    private const double WidestY = Top + 0.60 * (Bottom - Top);
    private const double MaxHalfWidth = 10.5;

    public static WriteableBitmap CreateEgg()
    {
        var pixels = new byte[Size * Size * 4]; // BGRA, straight alpha

        // Önce hangi pikselin yumurtanın içinde olduğunu çıkar.
        var inside = new bool[Size, Size];
        for (var y = 0; y < Size; y++)
        {
            var halfWidth = HalfWidthAt(y);
            if (halfWidth <= 0) continue;

            for (var x = 0; x < Size; x++)
            {
                if (Math.Abs(x + 0.5 - CenterX) <= halfWidth) inside[x, y] = true;
            }
        }

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (!inside[x, y]) continue;

            var color = IsEdge(inside, x, y) ? Outline
                : IsSpot(x, y) ? Spot
                : (x + 0.5 - CenterX) > HalfWidthAt(y) * 0.35 ? ShellShade
                : Shell;

            var i = (y * Size + x) * 4;
            pixels[i + 0] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }

        var bitmap = new WriteableBitmap(Size, Size, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, Size, Size), pixels, Size * 4, 0);
        bitmap.Freeze(); // Dondurulmuş bitmap arka plan iş parçacığından da okunabilir ve daha hızlı çizilir.
        return bitmap;
    }

    private static double HalfWidthAt(int y)
    {
        var yc = y + 0.5;
        if (yc < Top || yc > Bottom) return 0;

        if (yc < WidestY)
        {
            var rel = (WidestY - yc) / (WidestY - Top);
            return MaxHalfWidth * 0.88 * Math.Sqrt(Math.Max(0, 1 - rel * rel));
        }
        else
        {
            var rel = (yc - WidestY) / (Bottom - WidestY);
            return MaxHalfWidth * Math.Sqrt(Math.Max(0, 1 - rel * rel));
        }
    }

    /// <summary>İçeride ama en az bir komşusu dışarıda olan piksel = kontur.</summary>
    private static bool IsEdge(bool[,] inside, int x, int y)
    {
        return !Get(inside, x - 1, y) || !Get(inside, x + 1, y)
            || !Get(inside, x, y - 1) || !Get(inside, x, y + 1);
    }

    private static bool Get(bool[,] inside, int x, int y) =>
        x >= 0 && y >= 0 && x < Size && y < Size && inside[x, y];

    private static bool IsSpot(int x, int y)
    {
        return InCircle(x, y, 12.0, 12.0, 2.2)
            || InCircle(x, y, 20.5, 17.5, 2.6)
            || InCircle(x, y, 14.0, 22.5, 1.8);
    }

    private static bool InCircle(int x, int y, double cx, double cy, double r)
    {
        var dx = x + 0.5 - cx;
        var dy = y + 0.5 - cy;
        return dx * dx + dy * dy <= r * r;
    }

    private readonly record struct Rgb(byte R, byte G, byte B);

    private static readonly Rgb Shell = new(245, 238, 222);
    private static readonly Rgb ShellShade = new(214, 203, 180);
    private static readonly Rgb Outline = new(60, 48, 60);
    private static readonly Rgb Spot = new(126, 196, 184);
}
