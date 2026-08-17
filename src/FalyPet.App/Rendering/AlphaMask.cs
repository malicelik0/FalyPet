using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FalyPet.App.Rendering;

/// <summary>
/// Bir sprite'ın alfa kanalının kopyası. "Fare pet'in üstünde mi, yoksa etrafındaki
/// boşlukta mı" sorusunu ucuza cevaplar — tıkla-geç davranışının dayanağı budur.
/// Bitmap'i her fare hareketinde yeniden okumak yerine alfa bir kez çıkarılıp saklanır.
/// </summary>
internal sealed class AlphaMask
{
    private readonly byte[] _alpha;

    public int Width { get; }
    public int Height { get; }

    private AlphaMask(int width, int height, byte[] alpha)
    {
        Width = width;
        Height = height;
        _alpha = alpha;
    }

    public static AlphaMask FromBitmap(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Kaynağın piksel formatı ne olursa olsun Bgra32'ye çevir; böylece alfa
        // her zaman 4. baytta ve düz (premultiplied olmayan) halde olur.
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var buffer = new byte[stride * height];
        converted.CopyPixels(buffer, stride, 0);

        var alpha = new byte[width * height];
        for (var i = 0; i < alpha.Length; i++) alpha[i] = buffer[i * 4 + 3];

        return new AlphaMask(width, height, alpha);
    }

    /// <summary>Sprite piksel koordinatındaki alfa. Sınırların dışı 0 sayılır.</summary>
    public byte Sample(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        return _alpha[y * Width + x];
    }

    /// <summary>
    /// Bu noktanın pet'e mi yoksa boşluğa mı denk geldiği.
    ///
    /// Eşik yüksek tutuluyor: vektör çizimde kenarlar yumuşatılmış (anti-aliased),
    /// yani siluetin dışında birkaç piksellik yarı saydam bir halka var. Eşik düşük
    /// olsaydı o halka da "pet" sayılır, kullanıcı pet'in yanındaki boşluğa
    /// tıkladığında tıklama alttaki pencereye geçmezdi.
    /// Pixel-art'ta kenar keskin olduğu için 24 yetiyordu.
    /// </summary>
    public bool IsOpaqueAt(int x, int y) => Sample(x, y) >= OpacityThreshold;

    public const byte OpacityThreshold = 128;
}
