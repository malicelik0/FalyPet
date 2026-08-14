using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FalyPet.App.Rendering;

/// <summary>
/// 32x32'lik bir pixel-art çizim yüzeyi.
///
/// Anti-aliasing YOK ve olmamalı: pixel art'ta yarı saydam kenar hem stili bozar
/// hem de tıkla-geç maskesini bulanıklaştırır. Her piksel ya tamamen var ya yok.
/// </summary>
internal sealed class SpriteCanvas
{
    public const int Size = 32;

    private readonly uint[] _color = new uint[Size * Size];
    private readonly bool[] _solid = new bool[Size * Size];

    public void Plot(int x, int y, uint rgb)
    {
        if ((uint)x >= Size || (uint)y >= Size) return;
        var i = y * Size + x;
        _color[i] = rgb;
        _solid[i] = true;
    }

    public bool IsSolid(int x, int y) =>
        (uint)x < Size && (uint)y < Size && _solid[y * Size + x];

    public void Ellipse(double cx, double cy, double rx, double ry, uint rgb)
    {
        if (rx <= 0 || ry <= 0) return;

        for (var y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
        for (var x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
        {
            var dx = (x + 0.5 - cx) / rx;
            var dy = (y + 0.5 - cy) / ry;
            if (dx * dx + dy * dy <= 1.0) Plot(x, y, rgb);
        }
    }

    /// <summary>Yalnızca zaten dolu olan piksellere boyar. Desenler siluetin dışına taşmasın diye.</summary>
    public void EllipseInside(double cx, double cy, double rx, double ry, uint rgb)
    {
        if (rx <= 0 || ry <= 0) return;

        for (var y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
        for (var x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
        {
            if (!IsSolid(x, y)) continue;
            var dx = (x + 0.5 - cx) / rx;
            var dy = (y + 0.5 - cy) / ry;
            if (dx * dx + dy * dy <= 1.0) Plot(x, y, rgb);
        }
    }

    public void RectInside(int x0, int y0, int x1, int y1, uint rgb)
    {
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            if (IsSolid(x, y)) Plot(x, y, rgb);
    }

    /// <summary>Tepesi (ax,ay) olan bir üçgen — kulak ve boynuz için.</summary>
    public void Triangle(double ax, double ay, double bx, double by, double cx, double cy, uint rgb)
    {
        var minX = (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)));
        var maxX = (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx)));
        var minY = (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)));
        var maxY = (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy)));

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            double px = x + 0.5, py = y + 0.5;
            var d1 = Edge(px, py, ax, ay, bx, by);
            var d2 = Edge(px, py, bx, by, cx, cy);
            var d3 = Edge(px, py, cx, cy, ax, ay);

            var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            if (!(hasNeg && hasPos)) Plot(x, y, rgb);
        }
    }

    private static double Edge(double px, double py, double ax, double ay, double bx, double by) =>
        (px - bx) * (ay - by) - (ax - bx) * (py - by);

    /// <summary>
    /// Siluetin kenarındaki pikselleri kontur rengine çevirir.
    /// Gövde parçalarının HEPSİ çizildikten sonra, yüz çizilmeden ÖNCE çağrılmalı:
    /// önce çağrılırsa kulaklar konturusuz kalır, sonra çağrılırsa gözler yutulur.
    /// </summary>
    public void Outline(uint rgb)
    {
        var edges = new System.Collections.Generic.List<int>();

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (!IsSolid(x, y)) continue;
            if (IsSolid(x - 1, y) && IsSolid(x + 1, y) && IsSolid(x, y - 1) && IsSolid(x, y + 1)) continue;
            edges.Add(y * Size + x);
        }

        foreach (var i in edges) _color[i] = rgb;
    }

    public WriteableBitmap ToBitmap()
    {
        var pixels = new byte[Size * Size * 4];

        for (var i = 0; i < _color.Length; i++)
        {
            if (!_solid[i]) continue;
            var rgb = _color[i];
            var o = i * 4;
            pixels[o + 0] = (byte)(rgb & 0xFF);         // B
            pixels[o + 1] = (byte)((rgb >> 8) & 0xFF);  // G
            pixels[o + 2] = (byte)((rgb >> 16) & 0xFF); // R
            pixels[o + 3] = 255;
        }

        var bitmap = new WriteableBitmap(Size, Size, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, Size, Size), pixels, Size * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    // ------------------------------------------------------------------ renk yardımcıları

    public static uint Mix(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var r = (byte)(((a >> 16) & 0xFF) * (1 - t) + ((b >> 16) & 0xFF) * t);
        var g = (byte)(((a >> 8) & 0xFF) * (1 - t) + ((b >> 8) & 0xFF) * t);
        var bl = (byte)((a & 0xFF) * (1 - t) + (b & 0xFF) * t);
        return (uint)((r << 16) | (g << 8) | bl);
    }

    public static uint Darken(uint rgb, double amount) => Mix(rgb, 0x000000, amount);
    public static uint Lighten(uint rgb, double amount) => Mix(rgb, 0xFFFFFF, amount);

    /// <summary>Kontur rengi: siyah değil, gövdenin koyulaştırılmış hali. Siyah kontur ucuz görünür.</summary>
    public static uint OutlineFor(uint baseColor) => Mix(Darken(baseColor, 0.62), 0x2A2233, 0.45);
}
