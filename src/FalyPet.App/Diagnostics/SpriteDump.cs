using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.App.Rendering;
using FalyPet.Core.Content;
using FalyPet.Core.Model;

namespace FalyPet.App.Diagnostics;

/// <summary>
/// <c>FalyPet.exe --dump-sprite &lt;klasör&gt;</c> ile çalışır: bütün türlerin bütün
/// aşama ve durumlarını tek bir kontakt sayfasına basar, alfa maskesini ölçer.
///
/// Kalıcı bir araç. 10 tür × 5 aşama × 9 durum gözle tek tek denetlenemez; bir sprite
/// bozulduğunda ya da şeffaf kenarını kaybettiğinde bunu yakalayan şey burası.
/// </summary>
internal static class SpriteDump
{
    /// <summary>Kontakt sayfasındaki her karenin piksel boyutu.</summary>
    private const int Cell = 96;
    private const int Pad = 4;

    public static string Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();

        var sheetPath = Path.Combine(directory, "tum-turler.png");
        WritePng(BuildContactSheet(out var checkedCount, out var problems), sheetPath);

        report.AppendLine($"kontakt sayfasi : {sheetPath}");
        report.AppendLine($"denetlenen sprite: {checkedCount}");
        report.AppendLine($"sorunlu sprite   : {problems.Count}");
        report.AppendLine();

        if (problems.Count == 0)
        {
            report.AppendLine("Butun sprite'lar gecti: sifir olcude degil, kenarlari seffaf,");
            report.AppendLine("merkezleri opak (yani tiklanabilir).");
        }
        else
        {
            report.AppendLine("SORUNLAR:");
            foreach (var p in problems) report.AppendLine("  " + p);
        }

        report.AppendLine();
        report.AppendLine("satirlar = turler, sutunlar sirasiyla:");
        report.AppendLine("  " + string.Join(" ", Columns.Select(c => c.Label)));

        File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString());
        return report.ToString();
    }

    private readonly record struct Column(string Label, GrowthStage Stage, PetAnimation Anim, int Frame, int Gaze = 0);

    private static readonly Column[] Columns =
    [
        // Yumurtada "kare" = okşama sayısı. Üç nokta: hiç, yarı, tam —
        // çatlakların gerçekten oransal ilerlediği gözle görülsün.
        new("yum-0",    GrowthStage.Egg,   PetAnimation.Idle,  0),
        new("yum-yari", GrowthStage.Egg,   PetAnimation.Idle,  Core.Simulation.SimulationRules.EggCracksRequired / 2),
        new("yum-tam",  GrowthStage.Egg,   PetAnimation.Idle,  Core.Simulation.SimulationRules.EggCracksRequired),
        new("bebek",    GrowthStage.Baby,  PetAnimation.Idle,  0),
        new("cocuk",    GrowthStage.Child, PetAnimation.Idle,  0),
        new("genc",     GrowthStage.Teen,  PetAnimation.Idle,  0),
        new("yetiskin", GrowthStage.Adult, PetAnimation.Idle,  0),
        // Bakış sütunları: göz bebeği gerçekten kayıyor mu, gözle görülsün.
        new("bakis-sol",  GrowthStage.Adult, PetAnimation.Idle, 0, -1),
        new("bakis-sag",  GrowthStage.Adult, PetAnimation.Idle, 0,  1),
        new("yuruyus",  GrowthStage.Adult, PetAnimation.Walk,  1),
        new("uyku",     GrowthStage.Adult, PetAnimation.Sleep, 1),
        new("oyun",     GrowthStage.Adult, PetAnimation.Play,  1),
        new("hasta",    GrowthStage.Adult, PetAnimation.Sick,  0),
        new("kuskun",   GrowthStage.Adult, PetAnimation.Sulk,  0),
    ];

    private static WriteableBitmap BuildContactSheet(out int checkedCount, out List<string> problems)
    {
        problems = [];
        checkedCount = 0;

        var species = SpeciesCatalog.All;
        var cols = Columns.Length;
        var rows = species.Count;

        var cellPx = Cell + Pad * 2;
        var width = cols * cellPx;
        var height = rows * cellPx;
        var sheet = new byte[width * height * 4];

        // Dama tahtası zemin: açık renkli sprite'ların şeffaf kenarı ancak böyle görülür.
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var dark = ((x / 8) + (y / 8)) % 2 == 0;
            var v = (byte)(dark ? 58 : 74);
            var i = (y * width + x) * 4;
            sheet[i] = v; sheet[i + 1] = v; sheet[i + 2] = v; sheet[i + 3] = 255;
        }

        for (var r = 0; r < rows; r++)
        for (var col = 0; col < cols; col++)
        {
            var def = species[r];
            var spec = Columns[col];

            var sprite = VectorPetRenderer.Render(def, spec.Stage, spec.Anim, spec.Frame,
                null, spec.Gaze, blinking: false, SpriteCache.RenderSize);

            checkedCount++;
            var problem = Inspect(sprite, $"{def.Id}/{spec.Label}");
            if (problem is not null) problems.Add(problem);

            Blit(sheet, width, sprite, col * cellPx + Pad, r * cellPx + Pad, Cell);
        }

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), sheet, width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Bir sprite'ın oynanabilir olması için sağlaması gereken asgari şartlar.</summary>
    private static string? Inspect(BitmapSource sprite, string label)
    {
        var mask = AlphaMask.FromBitmap(sprite);

        var opaque = 0;
        for (var y = 0; y < mask.Height; y++)
        for (var x = 0; x < mask.Width; x++)
            if (mask.IsOpaqueAt(x, y)) opaque++;

        if (opaque == 0) return $"{label}: TAMAMEN BOS";

        var ratio = opaque / (double)(mask.Width * mask.Height);
        if (ratio > 0.85) return $"{label}: neredeyse tum kareyi dolduruyor (%{ratio * 100:F0}) - seffaf kenar yok";

        // Kenarları taşan sprite pencereye sığmıyor demektir.
        for (var x = 0; x < mask.Width; x++)
            if (mask.IsOpaqueAt(x, 0) || mask.IsOpaqueAt(x, mask.Height - 1))
                return $"{label}: ust/alt kenara tasiyor";

        return null;
    }

    /// <summary>
    /// Sprite'ı <paramref name="target"/> boyutuna küçültüp zemine bindirir.
    ///
    /// Vektör sprite'lar 256x256 geliyor, kontakt sayfası hücresi 96 — yani bu bir
    /// KÜÇÜLTME. Eski sürüm yalnızca tam sayı katıyla büyütebiliyordu (pixel-art
    /// içindi). Kutu ortalaması kullanılıyor, yoksa ince çizgiler kayboluyor.
    /// </summary>
    private static void Blit(byte[] dest, int destWidth, BitmapSource src, int dx, int dy, int target)
    {
        var w = src.PixelWidth;
        var h = src.PixelHeight;
        var buffer = new byte[w * h * 4];
        src.CopyPixels(buffer, w * 4, 0);

        var blockX = Math.Max(1, w / target);
        var blockY = Math.Max(1, h / target);

        for (var y = 0; y < target; y++)
        for (var x = 0; x < target; x++)
        {
            double b = 0, g = 0, r = 0, a = 0;
            var sayac = 0;

            for (var by = 0; by < blockY; by++)
            for (var bx = 0; bx < blockX; bx++)
            {
                var sx = Math.Min(w - 1, x * w / target + bx);
                var sy = Math.Min(h - 1, y * h / target + by);
                var si = (sy * w + sx) * 4;

                b += buffer[si + 0]; g += buffer[si + 1]; r += buffer[si + 2]; a += buffer[si + 3];
                sayac++;
            }

            if (sayac == 0) continue;
            var alpha = a / sayac / 255.0;
            if (alpha <= 0.004) continue;

            var di = ((dy + y) * destWidth + (dx + x)) * 4;
            if (di < 0 || di + 3 >= dest.Length) continue;

            dest[di + 0] = (byte)(b / sayac * alpha + dest[di + 0] * (1 - alpha));
            dest[di + 1] = (byte)(g / sayac * alpha + dest[di + 1] * (1 - alpha));
            dest[di + 2] = (byte)(r / sayac * alpha + dest[di + 2] * (1 - alpha));
            dest[di + 3] = 255;
        }
    }

    private static void WritePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }
}
