using System;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.App.Rendering;

namespace FalyPet.App.Diagnostics;

/// <summary>
/// <c>FalyPet.exe --dump-sprite &lt;klasör&gt;</c> ile çalışır: sprite'ı PNG olarak diske yazar
/// ve alfa maskesi hakkında ölçülebilir bilgi basar.
///
/// Kalıcı bir araç, geçici bir hile değil: Faz 8'de üretilen her sprite'ın gerçekten
/// şeffaf kenarlı olduğunu ve büyütünce bulanıklaşmadığını gözle değil ölçerek
/// doğrulamak için kullanılacak.
/// </summary>
internal static class SpriteDump
{
    public static string Run(string directory)
    {
        Directory.CreateDirectory(directory);

        var sprite = PlaceholderSprite.CreateEgg();
        var mask = AlphaMask.FromBitmap(sprite);

        var originalPath = Path.Combine(directory, "egg-1x.png");
        WritePng(sprite, originalPath);

        // Büyütmeyi WPF'e bırakmıyoruz: pikselleri elle çoğaltmak "nearest neighbor"ın
        // ne üretmesi gerektiğinin bağımsız referansı olur.
        var scaled = ScaleNearestNeighbor(sprite, 8);
        var scaledPath = Path.Combine(directory, "egg-8x.png");
        WritePng(scaled, scaledPath);

        return Describe(mask, originalPath, scaledPath);
    }

    private static string Describe(AlphaMask mask, params string[] paths)
    {
        var opaque = 0;
        var partial = 0;
        for (var y = 0; y < mask.Height; y++)
        for (var x = 0; x < mask.Width; x++)
        {
            var a = mask.Sample(x, y);
            if (a == 255) opaque++;
            else if (a > 0) partial++;
        }

        var total = mask.Width * mask.Height;
        var sb = new StringBuilder();
        sb.AppendLine($"boyut          : {mask.Width}x{mask.Height}");
        sb.AppendLine($"opak piksel    : {opaque}/{total} (%{opaque * 100.0 / total:F1})");
        sb.AppendLine($"yari saydam    : {partial}");
        sb.AppendLine($"saydam         : {total - opaque - partial}");
        sb.AppendLine();
        sb.AppendLine("koseler (hepsi saydam olmali, yoksa tikla-gec calismaz):");
        sb.AppendLine($"  sol-ust   alfa={mask.Sample(0, 0)}   opak={mask.IsOpaqueAt(0, 0)}");
        sb.AppendLine($"  sag-ust   alfa={mask.Sample(mask.Width - 1, 0)}   opak={mask.IsOpaqueAt(mask.Width - 1, 0)}");
        sb.AppendLine($"  sol-alt   alfa={mask.Sample(0, mask.Height - 1)}   opak={mask.IsOpaqueAt(0, mask.Height - 1)}");
        sb.AppendLine($"  sag-alt   alfa={mask.Sample(mask.Width - 1, mask.Height - 1)}   opak={mask.IsOpaqueAt(mask.Width - 1, mask.Height - 1)}");
        sb.AppendLine();
        sb.AppendLine("merkez (opak olmali, yoksa pet'e hic tiklanamaz):");
        sb.AppendLine($"  orta      alfa={mask.Sample(mask.Width / 2, mask.Height / 2)}   opak={mask.IsOpaqueAt(mask.Width / 2, mask.Height / 2)}");
        sb.AppendLine();
        foreach (var p in paths) sb.AppendLine($"yazildi: {p}");
        return sb.ToString();
    }

    private static WriteableBitmap ScaleNearestNeighbor(BitmapSource source, int factor)
    {
        var w = source.PixelWidth;
        var h = source.PixelHeight;
        var src = new byte[w * h * 4];
        source.CopyPixels(src, w * 4, 0);

        var dw = w * factor;
        var dh = h * factor;
        var dst = new byte[dw * dh * 4];

        for (var y = 0; y < dh; y++)
        for (var x = 0; x < dw; x++)
        {
            var si = ((y / factor) * w + (x / factor)) * 4;
            var di = (y * dw + x) * 4;
            dst[di + 0] = src[si + 0];
            dst[di + 1] = src[si + 1];
            dst[di + 2] = src[si + 2];
            dst[di + 3] = src[si + 3];
        }

        var bitmap = new WriteableBitmap(dw, dh, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, dw, dh), dst, dw * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static void WritePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }
}
