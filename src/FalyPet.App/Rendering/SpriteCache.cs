using System.Collections.Generic;
using System.Windows.Media.Imaging;
using FalyPet.Core.Content;
using FalyPet.Core.Model;

namespace FalyPet.App.Rendering;

/// <summary>
/// Üretilmiş sprite'ları ve alfa maskelerini saklar.
///
/// Önbellek şart: sprite üretimi kare başına yüzlerce piksel hesabı yapıyor ve
/// animasyon saniyede 8 kare istiyor. Her karede yeniden üretmek 7/24 açık kalacak
/// bir uygulamada boşta CPU tüketimini görünür hale getirirdi.
///
/// Toplam yük küçük: bir tür için tüm aşama/durum/kare bileşimleri 32x32'lik
/// birkaç yüz bitmap eder, hepsi birkaç yüz kilobayt.
/// </summary>
internal sealed class SpriteCache
{
    private readonly record struct Key(string Species, GrowthStage Stage, PetAnimation Anim, int Frame, bool FaceLeft, string Accessory);

    private readonly Dictionary<Key, WriteableBitmap> _sprites = [];
    private readonly Dictionary<Key, AlphaMask> _masks = [];

    public WriteableBitmap Get(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame,
        bool faceLeft, AccessoryDefinition? accessory = null)
    {
        var key = new Key(species.Id, stage, anim, frame, faceLeft, accessory?.Id ?? "");
        if (_sprites.TryGetValue(key, out var cached)) return cached;

        var sprite = stage == GrowthStage.Egg
            ? PetSpriteFactory.CreateEgg(species, frame)
            : PetSpriteFactory.Create(species, stage, anim, frame, accessory);

        if (faceLeft) sprite = MirrorHorizontally(sprite);

        _sprites[key] = sprite;
        return sprite;
    }

    public AlphaMask GetMask(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame,
        bool faceLeft, AccessoryDefinition? accessory = null)
    {
        var key = new Key(species.Id, stage, anim, frame, faceLeft, accessory?.Id ?? "");
        if (_masks.TryGetValue(key, out var cached)) return cached;

        var mask = AlphaMask.FromBitmap(Get(species, stage, anim, frame, faceLeft, accessory));
        _masks[key] = mask;
        return mask;
    }

    /// <summary>
    /// Sola bakan sprite'lar ayrıca çizilmez, sağa bakan aynalanır.
    /// Üretilecek varyasyon sayısını yarıya indirir ve iki yönün asla ayrışmamasını garanti eder.
    /// </summary>
    private static WriteableBitmap MirrorHorizontally(BitmapSource source)
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

        var bitmap = new WriteableBitmap(w, h, 96, 96, source.Format, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), dst, w * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }
}
