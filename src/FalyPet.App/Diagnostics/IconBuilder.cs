using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.App.Rendering;
using FalyPet.Core.Content;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Diagnostics;

/// <summary>
/// <c>FalyPet.exe --make-icon &lt;dosya.ico&gt;</c> ile çalışır.
///
/// Uygulama ikonunu prosedürel yumurtadan üretir. Ayrı bir çizim dosyası
/// tutulmuyor: ikon ile oyundaki yumurta aynı koddan çıkıyor, yani sprite
/// değişirse ikon da tek komutla tazelenebiliyor ve ikisi asla ayrışmıyor.
///
/// Yumurta seçildi çünkü türden bağımsız: kullanıcı hangi türü seçerse seçsin
/// ikon doğru kalıyor, ve zaten uygulamanın ilk gösterdiği görüntü o.
/// </summary>
internal static class IconBuilder
{
    /// <summary>
    /// 32'nin tam bölenleri ve katları. Böylece her boyut piksel hizasında
    /// kalıyor; 48 gibi ara boyutlar bulanık kenar üretirdi, Windows onu
    /// 64'ten kendi ölçekler.
    /// </summary>
    private static readonly int[] Sizes = [16, 32, 64, 128, 256];

    public static string Write(string path)
    {
        var source = PetSpriteFactory.CreateEgg(
            SpeciesCatalog.All[0],
            SimulationRules.EggCracksRequired / 2);   // yarı çatlamış: karakterli ama tanınır

        // 256 dışındaki boyutlar DIB (klasik BMP) olarak gömülüyor, yalnızca 256 PNG.
        // Hepsini PNG yapmak denendi ve System.Drawing.Icon okuyamadı: PNG sıkıştırmalı
        // ICO kareleri Vista+ kabuğunda çalışıyor ama eski API'lerle uyumsuz.
        // Bu düzen her yerde okunuyor ve standart uygulamanın da kendisi.
        var images = new List<byte[]>();
        foreach (var size in Sizes)
        {
            var scaled = Resize(source, size);
            images.Add(size >= 256 ? EncodePng(scaled) : EncodeDib(scaled, size));
        }

        WriteIco(path, Sizes, images);

        var info = new FileInfo(path);
        return $"ikon yazildi: {path}\n  boyutlar: {string.Join(", ", Sizes)}\n  dosya: {info.Length} bayt";
    }

    private static BitmapSource Resize(BitmapSource source, int size)
    {
        var w = source.PixelWidth;
        var src = new byte[w * w * 4];
        source.CopyPixels(src, w * 4, 0);

        var dst = new byte[size * size * 4];

        if (size >= w)
        {
            // Büyütme: en yakın komşu, pixel art keskin kalsın.
            var factor = size / w;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var si = ((y / factor) * w + (x / factor)) * 4;
                var di = (y * size + x) * 4;
                Array.Copy(src, si, dst, di, 4);
            }
            return FromPixels(dst, size);
        }

        // Küçültme: kutu ortalaması. En yakın komşu burada konturu tamamen
        // yiyor — 16x16'da yumurtanın kenar çizgisi kaybolup şekilsiz bir
        // leke kalıyordu. Ortalama silueti koruyor.
        var block = w / size;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            double b = 0, g = 0, r = 0, a = 0;

            for (var by = 0; by < block; by++)
            for (var bx = 0; bx < block; bx++)
            {
                var si = ((y * block + by) * w + (x * block + bx)) * 4;
                var alpha = src[si + 3] / 255.0;
                b += src[si + 0] * alpha;
                g += src[si + 1] * alpha;
                r += src[si + 2] * alpha;
                a += alpha;
            }

            var di = (y * size + x) * 4;
            if (a > 0)
            {
                dst[di + 0] = (byte)Math.Clamp(b / a, 0, 255);
                dst[di + 1] = (byte)Math.Clamp(g / a, 0, 255);
                dst[di + 2] = (byte)Math.Clamp(r / a, 0, 255);
                dst[di + 3] = (byte)Math.Clamp(a / (block * block) * 255, 0, 255);
            }
        }

        return FromPixels(dst, size);
    }

    private static BitmapSource FromPixels(byte[] pixels, int size)
    {
        var bmp = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new System.Windows.Int32Rect(0, 0, size, size), pixels, size * 4, 0);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 32-bit DIB: BITMAPINFOHEADER + alt-üst ters BGRA + AND maskesi.
    ///
    /// İki tuzağı var, ikisi de sessizce bozuk ikon üretir:
    ///   1. Yükseklik iki KATI yazılır (renk verisi + maske birlikte sayılır)
    ///   2. Satırlar alttan üste doğru yazılır, yoksa ikon baş aşağı görünür
    /// </summary>
    private static byte[] EncodeDib(BitmapSource bitmap, int size)
    {
        var pixels = new byte[size * size * 4];
        bitmap.CopyPixels(pixels, size * 4, 0);

        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        w.Write(40);                    // biSize
        w.Write(size);                  // biWidth
        w.Write(size * 2);              // biHeight: renk + maske
        w.Write((short)1);              // biPlanes
        w.Write((short)32);             // biBitCount
        w.Write(0);                     // biCompression = BI_RGB
        w.Write(size * size * 4);       // biSizeImage
        w.Write(0); w.Write(0);         // çözünürlük
        w.Write(0); w.Write(0);         // palet

        for (var y = size - 1; y >= 0; y--)
            w.Write(pixels, y * size * 4, size * 4);

        // AND maskesi: 32-bit ikonda alfa zaten şeffaflığı taşıyor, maske sıfır
        // kalır. Ama satırlar 4 bayta hizalanmak ZORUNDA, yoksa boyut tutmaz.
        var maskRow = ((size + 31) / 32) * 4;
        w.Write(new byte[maskRow * size]);

        w.Flush();
        return stream.ToArray();
    }

    /// <summary>ICO kabı: başlık + dizin girdileri + görüntü verileri.</summary>
    private static void WriteIco(string path, int[] sizes, List<byte[]> images)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(stream);

        w.Write((short)0);              // ayrilmis
        w.Write((short)1);              // tur: 1 = ikon
        w.Write((short)sizes.Length);

        // Veri, dizin girdilerinden sonra basliyor.
        var offset = 6 + sizes.Length * 16;

        for (var i = 0; i < sizes.Length; i++)
        {
            // 256 piksel, bayta sigmadigi icin 0 olarak yazilir.
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0);           // palet rengi yok
            w.Write((byte)0);           // ayrilmis
            w.Write((short)1);          // duzlem
            w.Write((short)32);         // bit derinligi
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }

        foreach (var image in images) w.Write(image);
    }
}
