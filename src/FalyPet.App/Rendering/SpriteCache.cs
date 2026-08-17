using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.Core.Content;
using FalyPet.Core.Model;

namespace FalyPet.App.Rendering;

/// <summary>
/// Sprite'ları ve alfa maskelerini saklar; kaynağı seçen yer de burası.
///
/// Öncelik: <see cref="SpriteSheetLibrary"/> (diskteki gerçek sanat) → prosedürel üretim.
/// Yani gerçek sanat eklendiği anda devreye girer, kod değişikliği gerekmez; ve bir
/// türün sanatı eksikse o tür prosedürel çalışmaya devam eder. Sanatın hepsini
/// birden bitirme zorunluluğu böylece ortadan kalkıyor.
///
/// Önbellek şart: prosedürel üretim kare başına yüzlerce piksel hesabı yapıyor ve
/// animasyon saniyede 8 kare istiyor. 7/24 açık kalan bir uygulamada her karede
/// yeniden üretmek boşta CPU tüketimini görünür hale getirirdi.
/// </summary>
internal sealed class SpriteCache
{
    private readonly record struct Key(string Species, GrowthStage Stage, PetAnimation Anim, int Frame, bool FaceLeft, string Accessory, int Gaze, bool Blinking);

    private readonly Dictionary<Key, BitmapSource> _sprites = [];
    private readonly Dictionary<Key, AlphaMask> _masks = [];
    private readonly SpriteSheetLibrary _sheets;

    /// <summary>
    /// Vektör çizimin üretildiği piksel boyutu.
    ///
    /// En büyük pet boyutuyla (256) aynı: o boyutta 1:1, daha küçük boyutlarda
    /// yüksek kaliteli küçültme. Vektör olduğu için her boyutta net kalıyor —
    /// pixel-art'ta ölçek tam sayı katı olmak zorundaydı, artık değil.
    /// </summary>
    public const int RenderSize = 256;

    public SpriteCache(SpriteSheetLibrary? sheets = null) => _sheets = sheets ?? new SpriteSheetLibrary();

    /// <summary>Teşhis için: gerçek sanat klasörü bulundu mu?</summary>
    public bool UsingRealArt => _sheets.RootExists;

    public BitmapSource Get(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame,
        bool faceLeft, AccessoryDefinition? accessory = null, int gaze = 0, bool blinking = false)
    {
        var key = new Key(species.Id, stage, anim, frame, faceLeft, accessory?.Id ?? "", gaze, blinking);
        if (_sprites.TryGetValue(key, out var cached)) return cached;

        var sprite = Build(species, stage, anim, frame, accessory, gaze, blinking);
        if (faceLeft) sprite = MirrorHorizontally(sprite);

        _sprites[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Alfa maskesi bakış yönünden ETKİLENMEZ — göz bebeği siluetin içinde kalır.
    /// Bu yüzden maske anahtarında gaze her zaman 0: aynı silueti üç kez saklamak
    /// tıkla-geç için hiçbir şey kazandırmaz, sadece bellek harcar.
    /// </summary>
    public AlphaMask GetMask(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame,
        bool faceLeft, AccessoryDefinition? accessory = null)
    {
        var key = new Key(species.Id, stage, anim, frame, faceLeft, accessory?.Id ?? "", 0, false);
        if (_masks.TryGetValue(key, out var cached)) return cached;

        var mask = AlphaMask.FromBitmap(Get(species, stage, anim, frame, faceLeft, accessory));
        _masks[key] = mask;
        return mask;
    }

    private BitmapSource Build(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame,
        AccessoryDefinition? accessory, int gaze, bool blinking)
    {
        var sheetFrame = _sheets.TryGetFrame(species, stage, anim, frame);

        if (sheetFrame is null)
        {
            // Vektör olarak çiziliyor: yumuşak kenar, çizgi stili, her boyutta net.
            return VectorPetRenderer.Render(species, stage, anim, frame, accessory, gaze, blinking, RenderSize);
        }

        // Kostüm takılıysa vektör çizime dönülüyor.
        //
        // Gerekçe: kostümü gerçek sanatın üstüne bindirmek, aksesuarın kafanın tam
        // olarak nerede olduğunu bilmesini gerektiriyor. Pixel-art'ta bu bilgi tek
        // bir formülden geliyordu; gerçek sanatta her sanatçının çizimi farklı olur
        // ve şapka yanlış yere oturur. Sessizce kayık bir şapka göstermektense
        // tutarlı vektör hâli gösteriliyor.
        //
        // Sanatçı kostüm varyantlarını da çizerse, dosya adına ekleyerek verebilir
        // (bkz. mds/03-SANAT.md) ve bu yol devreye hiç girmez.
        return accessory is null || stage == GrowthStage.Egg ? sheetFrame : VectorPetRenderer.Render(
            species, stage, anim, frame, accessory, gaze, blinking, RenderSize);
    }

    /// <summary>
    /// Aksesuarı taban sprite'ın üstüne bindirir. Taban 32x32'den büyükse
    /// (ör. 64x64 sanat) bindirme tam sayı katıyla, en yakın komşu ile ölçeklenir —
    /// pixel art'ta yumuşatma yapılamaz.
    /// </summary>
    private static BitmapSource Composite(BitmapSource baseFrame, BitmapSource overlay)
    {
        var w = baseFrame.PixelWidth;
        var h = baseFrame.PixelHeight;

        var basePixels = new byte[w * h * 4];
        baseFrame.CopyPixels(basePixels, w * 4, 0);

        var ow = overlay.PixelWidth;
        var oh = overlay.PixelHeight;
        var overlayPixels = new byte[ow * oh * 4];
        overlay.CopyPixels(overlayPixels, ow * 4, 0);

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var sx = ow == w ? x : x * ow / w;
            var sy = oh == h ? y : y * oh / h;
            var si = (sy * ow + sx) * 4;
            if (overlayPixels[si + 3] == 0) continue;

            var di = (y * w + x) * 4;
            basePixels[di + 0] = overlayPixels[si + 0];
            basePixels[di + 1] = overlayPixels[si + 1];
            basePixels[di + 2] = overlayPixels[si + 2];
            basePixels[di + 3] = overlayPixels[si + 3];
        }

        var result = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        result.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), basePixels, w * 4, 0);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Sola bakan sprite'lar ayrıca çizilmez, sağa bakan aynalanır.
    /// Üretilecek varyasyon sayısını yarıya indirir ve iki yönün asla ayrışmamasını garanti eder.
    /// </summary>
    private static BitmapSource MirrorHorizontally(BitmapSource source)
    {
        var w = source.PixelWidth;
        var h = source.PixelHeight;
        var src = new byte[w * h * 4];
        source.CopyPixels(src, w * 4, 0);

        var dst = new byte[src.Length];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var si = (y * w + x) * 4;
            var di = (y * w + (w - 1 - x)) * 4;
            dst[di + 0] = src[si + 0];
            dst[di + 1] = src[si + 1];
            dst[di + 2] = src[si + 2];
            dst[di + 3] = src[si + 3];
        }

        var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), dst, w * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }
}
