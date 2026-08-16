using System;
using System.Windows.Media.Imaging;
using FalyPet.Core.Content;
using FalyPet.Core.Model;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Rendering;

/// <summary>
/// Tür tanımından pixel-art sprite üretir.
///
/// NEDEN PROSEDÜREL: 10 tür × 5 aşama × 9 durum × 3 kare = 1350 kare eder ve bu
/// elle çizilecek bir sayı değil. Prosedürel üretim, sanat gelene kadar oyunun
/// tamamının oynanabilir ve test edilebilir olmasını sağlıyor. Gerçek sprite'lar
/// geldiğinde <see cref="SpriteCache"/> onları dosyadan okuyacak; buradaki üretim
/// yedek (fallback) olarak kalacak — yani bir türün sanatı eksik olsa bile oyun çalışır.
/// </summary>
internal static class PetSpriteFactory
{
    public const int Size = SpriteCanvas.Size;

    /// <summary>Ayakların bastığı çizgi. 29'da: altta ayak için 2 piksel pay kalıyor.</summary>
    private const double Ground = 29.0;

    /// <summary>Üstte bırakılan pay. Kulak/boynuz bu çizginin üstüne çıkamaz.</summary>
    private const double Ceiling = 1.0;

    private const double CenterX = 16.0;

    /// <param name="gaze">
    /// Göz bebeğinin kayacağı yön, SPRITE uzayında (-1 sol, 0 düz, 1 sağ).
    /// Sprite hep sağa bakar çizilip sola aynalandığı için, çağıran taraf
    /// aynalamayı hesaba katmak zorunda.
    /// </param>
    /// <param name="blinking">
    /// Göz o an kapalı mı. Animasyon karesinden AYRI bir zamanlayıcıdan geliyor:
    /// kareye bağlıyken pet saniyede ~2.7 kez göz kırpıyordu.
    /// </param>
    public static WriteableBitmap Create(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame,
        AccessoryDefinition? accessory = null, int gaze = 0, bool blinking = false)
    {
        if (stage == GrowthStage.Egg) return CreateEgg(species, frame);

        var canvas = new SpriteCanvas();
        var m = MetricsFor(stage, species.Body);
        var outline = SpriteCanvas.OutlineFor(species.BaseColor);

        var bob = BobOffset(anim, frame);
        var anchor = GetHeadAnchor(species, stage, anim, frame, accessory);

        // Yürürken bakış her zaman ileri: sprite sağa bakar çizilip sola aynalandığı
        // için +1 ileriyi verir ve aynalama iki yönü de doğru gösterir.
        var effectiveGaze = anim == PetAnimation.Walk ? 1 : Math.Clamp(gaze, -1, 1);

        if (species.Body == BodyShape.Blob)
            DrawBlob(canvas, species, m, bob, anim, frame, outline, accessory, anchor, effectiveGaze, blinking);
        else
            DrawCreature(canvas, species, m, bob, anim, frame, outline, accessory, anchor, effectiveGaze, blinking);

        return canvas.ToBitmap();
    }

    /// <summary>Aksesuarın oturacağı yer: kafa merkezi ve yarıçapı.</summary>
    internal readonly record struct HeadAnchor(double X, double Y, double R);

    /// <summary>
    /// Kafa çapasını hesaplar. Hem prosedürel çizim hem de diskten yüklenen gerçek
    /// sprite'lar bunu kullanıyor — böylece bir kullanıcı kostüm takınca aksesuar,
    /// sanatın nereden geldiğine bakmaksızın aynı yere oturuyor.
    ///
    /// Gerçek sprite çizecek biri için şart budur: kafa merkezi bu fonksiyonun
    /// döndürdüğü yerde olsun. Aşama ölçüleri <see cref="MetricsFor"/> içinde.
    /// </summary>
    internal static HeadAnchor GetHeadAnchor(SpeciesDefinition s, GrowthStage stage, PetAnimation anim,
        int frame, AccessoryDefinition? accessory)
    {
        var m = MetricsFor(stage, s.Body);
        var bob = BobOffset(anim, frame);

        if (s.Body == BodyShape.Blob)
        {
            var ry = BlobRadiusY(m);
            var cy = Ground - ry + bob;
            var faceR = m.HeadR * 0.95;
            // Blob'da aksesuar kütlenin tepesine oturur, yüzün hemen üstüne değil.
            return new HeadAnchor(CenterX, Math.Max(cy - ry * 0.28, cy - ry + faceR * 0.55), faceR);
        }

        var bodyCy = Ground - m.BodyRy + bob;
        var headCy = bodyCy - m.BodyRy - m.HeadR * 0.55 + bob * 0.4 + Slump(anim);

        // Kafayı kulaklar VE aksesuar taşmayacak kadar aşağıda tut.
        var topExtent = Math.Max(EarTopExtent(s.Ears), AccessoryTopExtent(accessory));
        headCy = Math.Max(headCy, Ceiling + topExtent * m.HeadR);

        return new HeadAnchor(CenterX + HeadLean(anim, frame), headCy, m.HeadR);
    }

    /// <summary>
    /// Yalnızca aksesuarı çizer, şeffaf zemin üstüne. Diskten yüklenen gerçek
    /// sprite'ların üstüne bindirmek için — gerçek sanat kostümleri içermek
    /// zorunda kalmasın.
    /// </summary>
    internal static WriteableBitmap? RenderAccessoryOnly(AccessoryDefinition? accessory, HeadAnchor anchor)
    {
        if (accessory is null || accessory.Type == AccessoryType.None) return null;

        var canvas = new SpriteCanvas();
        DrawAccessory(canvas, accessory, anchor.X, anchor.Y, anchor.R);
        return canvas.ToBitmap();
    }

    private static double BlobRadiusY(Metrics m) =>
        Math.Min(m.BodyRy + m.HeadR * 0.75, (Ground - Ceiling) / 2.0);

    /// <summary>
    /// Aksesuarın kafa merkezinin ne kadar üstüne çıktığı (kafa yarıçapı katı).
    /// Kulak kısıtlamasıyla aynı mantık: şapka takan bir pet'in kafası biraz
    /// aşağı iner ki şapka kareyi taşmasın.
    /// </summary>
    private static double AccessoryTopExtent(AccessoryDefinition? accessory) => accessory?.Type switch
    {
        AccessoryType.Crown => 1.75,
        AccessoryType.Hat => 1.65,
        AccessoryType.Bow => 1.30,
        _ => 0.0,
    };

    // ---------------------------------------------------------------- ölçüler

    private readonly record struct Metrics(double BodyRx, double BodyRy, double HeadR);

    private static Metrics MetricsFor(GrowthStage stage, BodyShape shape)
    {
        // Bebekte kafa gövdeye göre büyük — sevimliliğin tek en güçlü kaldıracı budur.
        var m = stage switch
        {
            GrowthStage.Baby => new Metrics(5.0, 4.0, 6.0),
            GrowthStage.Child => new Metrics(6.2, 5.2, 6.0),
            GrowthStage.Teen => new Metrics(7.0, 6.2, 5.8),
            _ => new Metrics(7.8, 7.0, 6.2),
        };

        return shape switch
        {
            BodyShape.Tall => m with { BodyRx = m.BodyRx * 0.85, BodyRy = m.BodyRy * 1.25 },
            BodyShape.Wide => m with { BodyRx = m.BodyRx * 1.20, BodyRy = m.BodyRy * 0.92 },
            BodyShape.Blob => m with { BodyRx = m.BodyRx * 1.15, BodyRy = m.BodyRy * 1.10 },
            _ => m,
        };
    }

    /// <summary>Kare başına dikey zıplama. Pixel art'ta 1-2 piksellik oynama bile canlılık verir.</summary>
    private static double BobOffset(PetAnimation anim, int frame) => anim switch
    {
        PetAnimation.Idle => frame == 1 ? -1 : 0,
        PetAnimation.Walk => frame == 1 ? -1 : 0,
        PetAnimation.Play => frame switch { 1 => -3, 2 => -1, _ => 0 },
        PetAnimation.Eat or PetAnimation.Drink => frame == 1 ? 1 : 0,
        PetAnimation.Wash => frame == 1 ? -1 : 0,
        _ => 0,
    };

    /// <summary>
    /// Uyurken/küskünken/hastayken pet çöker. Bunu ZEMİNİ aşağı kaydırarak yapmıyoruz —
    /// öyle yapılınca ayaklar karenin dışına taşıyordu (110 sprite'ın 48'i böyle taşmıştı).
    /// Onun yerine kafa gövdeye doğru iniyor: aynı "çökmüş" hissi, taşma yok.
    /// </summary>
    private static double Slump(PetAnimation anim) => anim switch
    {
        PetAnimation.Sleep => 3.0,
        PetAnimation.Sulk => 2.0,
        PetAnimation.Sick => 1.5,
        _ => 0.0,
    };

    /// <summary>
    /// Kulakların kafa merkezinin ne kadar üstüne çıktığı (kafa yarıçapı katı olarak).
    /// Kafanın konumu bu değere göre kısıtlanıyor; yoksa uzun kulaklı türler
    /// (tavşan, baykuş, ejderha) karenin üstünden taşıyor.
    /// </summary>
    private static double EarTopExtent(EarType ears) => ears switch
    {
        EarType.Pointed => 1.45,
        EarType.Horns => 1.40,
        EarType.Tufts => 1.60,
        EarType.Round => 1.20,
        _ => 0.95,
    };

    // ---------------------------------------------------------------- normal yaratık

    private static void DrawCreature(SpriteCanvas c, SpeciesDefinition s, Metrics m,
        double bob, PetAnimation anim, int frame, uint outline, AccessoryDefinition? accessory, HeadAnchor anchor, int gaze, bool blinking)
    {
        var bodyCy = Ground - m.BodyRy + bob;

        // Kafa konumu GetHeadAnchor'dan geliyor, burada yeniden hesaplanmıyor:
        // iki yerde hesaplansaydı biri değiştiğinde kostümler kaymaya başlardı.
        var headCx = anchor.X;
        var headCy = anchor.Y;

        // Küskünken kuyruk ÖNE geçiyor: pet sırtını dönmüş demektir. Yalnızca yüzü
        // silmek yetmiyordu — o hâlde sprite idle'dan ayırt edilemiyor ve kullanıcı
        // pet'in küstüğünü anlamıyordu.
        var tailSide = anim == PetAnimation.Sulk ? -1 : 1;
        DrawTail(c, s, CenterX + tailSide * m.BodyRx * 0.85, bodyCy, anim, frame);
        DrawFeet(c, s, m, Ground, anim, frame);

        c.Ellipse(CenterX, bodyCy, m.BodyRx, m.BodyRy, s.BaseColor);
        DrawEars(c, s, headCx, headCy, m.HeadR);
        c.Ellipse(headCx, headCy, m.HeadR, m.HeadR * 0.92, s.BaseColor);

        DrawMarkings(c, s, m, bodyCy, headCx, headCy);
        c.Outline(outline);
        DrawFace(c, s, headCx, headCy, m.HeadR, anim, frame, outline, gaze, blinking);
        DrawAccessory(c, accessory, headCx, headCy, m.HeadR);
    }

    private static void DrawBlob(SpriteCanvas c, SpeciesDefinition s, Metrics m,
        double bob, PetAnimation anim, int frame, uint outline, AccessoryDefinition? accessory, HeadAnchor anchor, int gaze, bool blinking)
    {
        // Jöle/hayalet/ahtapotta ayrı kafa yok: tek kütle, yüz üst kısmında.
        // Yükseklik tavana göre kısıtlanıyor, yoksa yetişkin blob kareyi taşıyor.
        var rx = m.BodyRx + 1.0;
        var ry = BlobRadiusY(m);
        var cy = Ground - ry + bob;

        // Nefes alma: yatay genişleyip dikey basılma. Hacim korunuyormuş hissi verir.
        var squash = frame == 1 ? 0.10 : 0.0;

        c.Ellipse(CenterX, cy + ry * squash, rx * (1 + squash), ry * (1 - squash), s.BaseColor);

        if (s.Tail == TailType.Tentacle) DrawTentacles(c, s, rx, cy + ry, frame);
        if (s.Id == "hayalet") DrawGhostFringe(c, s, rx, cy + ry, frame);

        DrawMarkings(c, s, m, cy + ry * 0.35, CenterX, cy - ry * 0.30);
        c.Outline(outline);

        DrawFace(c, s, CenterX, cy - ry * 0.28, m.HeadR * 0.95, anim, frame, outline, gaze, blinking);
        DrawAccessory(c, accessory, anchor.X, anchor.Y, anchor.R);
    }

    /// <summary>Ahtapot dokunaçları — blob'un ALT KENARINA tutturulur, ölçüden türetilmez.</summary>
    private static void DrawTentacles(SpriteCanvas c, SpeciesDefinition s, double rx, double bottom, int frame)
    {
        for (var i = -2; i <= 2; i++)
        {
            var x = CenterX + i * (rx / 2.6);
            var drop = (i + frame) % 2 == 0 ? 0.6 : 0.0;
            c.Ellipse(x, Math.Min(bottom - 0.5 + drop, Ground - 1.0), rx / 4.5, 1.6, s.BaseColor);
        }
    }

    /// <summary>Hayaletin dalgalı alt kenarı — kare kare kayarak akıyormuş gibi görünür.</summary>
    private static void DrawGhostFringe(SpriteCanvas c, SpeciesDefinition s, double rx, double bottom, int frame)
    {
        for (var i = -2; i <= 2; i++)
        {
            var x = CenterX + i * (rx / 2.6);
            var phase = (i + frame) % 2 == 0 ? 1.0 : 0.2;
            c.Ellipse(x, Math.Min(bottom - 1.0 + phase, Ground - 1.0), rx / 4.2, 1.5, s.BaseColor);
        }
    }

    private static double HeadLean(PetAnimation anim, int frame) => anim switch
    {
        PetAnimation.Eat or PetAnimation.Drink => 0.5,
        PetAnimation.Walk => frame == 1 ? 0.5 : 0,
        _ => 0,
    };

    // ---------------------------------------------------------------- parçalar

    private static void DrawEars(SpriteCanvas c, SpeciesDefinition s, double hx, double hy, double r)
    {
        var inner = SpriteCanvas.Mix(s.BaseColor, s.AccentColor, 0.55);

        switch (s.Ears)
        {
            // Uçlar EarTopExtent ile uyumlu olmalı: orada söz verilenden yükseğe çıkan
            // bir kulak, kafa kısıtlamasını atlar ve kareyi taşar.
            case EarType.Pointed:
                c.Triangle(hx - r * 0.85, hy - r * 0.45, hx - r * 0.15, hy - r * 0.75, hx - r * 0.70, hy - r * 1.45, s.BaseColor);
                c.Triangle(hx + r * 0.85, hy - r * 0.45, hx + r * 0.15, hy - r * 0.75, hx + r * 0.70, hy - r * 1.45, s.BaseColor);
                break;

            case EarType.Floppy:
                c.Ellipse(hx - r * 0.95, hy + r * 0.15, r * 0.38, r * 0.80, SpriteCanvas.Darken(s.BaseColor, 0.12));
                c.Ellipse(hx + r * 0.95, hy + r * 0.15, r * 0.38, r * 0.80, SpriteCanvas.Darken(s.BaseColor, 0.12));
                break;

            case EarType.Round:
                c.Ellipse(hx - r * 0.75, hy - r * 0.72, r * 0.42, r * 0.42, s.AccentColor);
                c.Ellipse(hx + r * 0.75, hy - r * 0.72, r * 0.42, r * 0.42, s.AccentColor);
                break;

            case EarType.Horns:
                c.Triangle(hx - r * 0.70, hy - r * 0.60, hx - r * 0.25, hy - r * 0.72, hx - r * 0.55, hy - r * 1.40, s.AccentColor);
                c.Triangle(hx + r * 0.70, hy - r * 0.60, hx + r * 0.25, hy - r * 0.72, hx + r * 0.55, hy - r * 1.40, s.AccentColor);
                break;

            case EarType.Tufts:
                c.Ellipse(hx - r * 0.55, hy - r * 0.95, r * 0.28, r * 0.65, s.BaseColor);
                c.Ellipse(hx + r * 0.55, hy - r * 0.95, r * 0.28, r * 0.65, s.BaseColor);
                c.Ellipse(hx - r * 0.55, hy - r * 1.00, r * 0.13, r * 0.42, inner);
                c.Ellipse(hx + r * 0.55, hy - r * 1.00, r * 0.13, r * 0.42, inner);
                break;
        }
    }

    private static void DrawTail(SpriteCanvas c, SpeciesDefinition s, double baseX, double bodyCy, PetAnimation anim, int frame)
    {
        // Kuyruk sallanması: mutluyken belirgin, hastayken sabit.
        var wag = anim is PetAnimation.Play or PetAnimation.Walk ? (frame - 1) * 1.2 : 0;

        switch (s.Tail)
        {
            case TailType.Thin:
                c.Ellipse(baseX + 1.6, bodyCy - 1.5 + wag, 1.3, 3.2, s.BaseColor);
                break;

            case TailType.Bushy:
                c.Ellipse(baseX + 2.2, bodyCy - 2.0 + wag, 3.0, 3.6, s.BaseColor);
                c.Ellipse(baseX + 2.8, bodyCy - 3.8 + wag, 1.8, 1.8, s.AccentColor);
                break;

            case TailType.Curl:
                c.Ellipse(baseX + 1.5, bodyCy - 2.5 + wag, 1.5, 1.5, s.BaseColor);
                c.Ellipse(baseX + 2.8, bodyCy - 4.0 + wag, 1.3, 1.3, s.BaseColor);
                break;

            // Tentacle burada YOK: dokunaçlar blob'un alt kenarına tutturuluyor
            // ve DrawTentacles içinde çiziliyor.
        }
    }

    private static void DrawFeet(SpriteCanvas c, SpeciesDefinition s, Metrics m, double groundY,
        PetAnimation anim, int frame)
    {
        if (anim is PetAnimation.Sleep) return;

        // Yürürken ayaklar dönüşümlü öne çıkar; duruşta simetrik.
        var swing = anim == PetAnimation.Walk ? (frame - 1) * 1.8 : 0;
        var footColor = SpriteCanvas.Darken(s.BaseColor, 0.18);

        c.Ellipse(CenterX - m.BodyRx * 0.50 - swing, groundY - 0.6, 2.0, 1.5, footColor);
        c.Ellipse(CenterX + m.BodyRx * 0.50 + swing, groundY - 0.6, 2.0, 1.5, footColor);
    }

    private static void DrawMarkings(SpriteCanvas c, SpeciesDefinition s, Metrics m,
        double bodyCy, double headCx, double headCy)
    {
        switch (s.Marking)
        {
            case MarkingType.Belly:
                c.EllipseInside(CenterX, bodyCy + m.BodyRy * 0.35, m.BodyRx * 0.60, m.BodyRy * 0.60, s.AccentColor);
                break;

            case MarkingType.Stripes:
                // Kısa çizgiler: tam genişlikte olunca gövde çizgili bir fıçıya benziyor.
                for (var i = 0; i < 3; i++)
                {
                    var y = (int)(bodyCy - m.BodyRy * 0.45 + i * 2.5);
                    var half = (int)(m.BodyRx * 0.55);
                    c.RectInside((int)CenterX - half, y, (int)CenterX + half, y, SpriteCanvas.Darken(s.BaseColor, 0.25));
                }
                break;

            case MarkingType.Spots:
                c.EllipseInside(CenterX - m.BodyRx * 0.40, bodyCy, 1.8, 1.8, s.AccentColor);
                c.EllipseInside(CenterX + m.BodyRx * 0.45, bodyCy + 1.5, 2.1, 2.1, s.AccentColor);
                c.EllipseInside(CenterX + m.BodyRx * 0.05, bodyCy - 2.0, 1.4, 1.4, s.AccentColor);
                break;

            case MarkingType.Patch:
                c.EllipseInside(CenterX - m.BodyRx * 0.45, bodyCy + 0.5, m.BodyRx * 0.50, m.BodyRy * 0.55, s.AccentColor);
                c.EllipseInside(headCx + 2.2, headCy - 0.5, 2.4, 2.4, s.AccentColor);
                break;
        }
    }

    // ---------------------------------------------------------------- yüz

    private static void DrawFace(SpriteCanvas c, SpeciesDefinition s, double hx, double hy, double r,
        PetAnimation anim, int frame, uint outline, int gaze, bool blinking)
    {
        // Küskün pet sırtını döner: yüz hiç çizilmez. En güçlü ifade, ifadenin yokluğudur.
        if (anim == PetAnimation.Sulk) return;

        var eyeDx = r * 0.42;
        var eyeY = hy - r * 0.10;
        var ink = SpriteCanvas.Darken(outline, 0.35);

        switch (anim)
        {
            case PetAnimation.Sleep:
                DrawClosedEye(c, hx - eyeDx, eyeY, ink);
                DrawClosedEye(c, hx + eyeDx, eyeY, ink);
                DrawSleepBubble(c, hx + r * 1.15, hy - r * 0.95, frame, ink);
                return;

            case PetAnimation.Play:
                DrawHappyEye(c, hx - eyeDx, eyeY, ink);
                DrawHappyEye(c, hx + eyeDx, eyeY, ink);
                DrawMouth(c, hx, hy + r * 0.42, open: true, ink);
                return;

            case PetAnimation.Sick:
                DrawSpiralEye(c, hx - eyeDx, eyeY, ink);
                DrawSpiralEye(c, hx + eyeDx, eyeY, ink);
                DrawMouth(c, hx, hy + r * 0.45, open: false, ink);
                return;

            case PetAnimation.Eat or PetAnimation.Drink:
                DrawOpenEye(c, hx - eyeDx, eyeY, ink, gaze);
                DrawOpenEye(c, hx + eyeDx, eyeY, ink, gaze);
                DrawMouth(c, hx, hy + r * 0.45, open: frame != 1, ink);
                return;

            default:
                // Göz kırpma dışarıdan geliyor (PetBehavior'ın kendi zamanlayıcısı).
                // Animasyon karesine bağlıyken 375 ms'de bir kırpıyordu — titreme gibi.
                if (blinking)
                {
                    DrawClosedEye(c, hx - eyeDx, eyeY, ink);
                    DrawClosedEye(c, hx + eyeDx, eyeY, ink);
                }
                else
                {
                    DrawOpenEye(c, hx - eyeDx, eyeY, ink, gaze);
                    DrawOpenEye(c, hx + eyeDx, eyeY, ink, gaze);
                }
                DrawMouth(c, hx, hy + r * 0.45, open: false, ink);
                return;
        }
    }

    /// <summary><paramref name="gaze"/> göz bebeğinin kayacağı yön (-1 sol, 0 düz, 1 sağ).</summary>
    private static void DrawOpenEye(SpriteCanvas c, double x, double y, uint ink, int gaze)
    {
        // Göz akı, sonra üstüne bebek: bakış yönü ancak ikisi ayrıyken görünür.
        c.Ellipse(x, y, 1.6, 1.9, 0xFFFFFF);
        c.Ellipse(x + gaze * 0.8, y, 1.1, 1.5, ink);
        c.Plot((int)(x - 0.5), (int)(y - 1), 0xFFFFFF); // parlama noktası
    }

    private static void DrawClosedEye(SpriteCanvas c, double x, double y, uint ink)
    {
        for (var d = -1; d <= 1; d++) c.Plot((int)x + d, (int)y, ink);
    }

    private static void DrawHappyEye(SpriteCanvas c, double x, double y, uint ink)
    {
        // ^ şekli
        c.Plot((int)x - 1, (int)y, ink);
        c.Plot((int)x, (int)y - 1, ink);
        c.Plot((int)x + 1, (int)y, ink);
    }

    private static void DrawSpiralEye(SpriteCanvas c, double x, double y, uint ink)
    {
        // x şekli — hastalığın evrensel işareti
        c.Plot((int)x - 1, (int)y - 1, ink);
        c.Plot((int)x + 1, (int)y - 1, ink);
        c.Plot((int)x, (int)y, ink);
        c.Plot((int)x - 1, (int)y + 1, ink);
        c.Plot((int)x + 1, (int)y + 1, ink);
    }

    private static void DrawMouth(SpriteCanvas c, double x, double y, bool open, uint ink)
    {
        if (open) c.Ellipse(x, y, 1.6, 1.4, ink);
        else { c.Plot((int)x - 1, (int)y, ink); c.Plot((int)x, (int)y + 1, ink); c.Plot((int)x + 1, (int)y, ink); }
    }

    private static void DrawSleepBubble(SpriteCanvas c, double x, double y, int frame, uint ink)
    {
        var rise = frame * 1.2;
        var px = (int)(x + frame * 0.4);
        var py = (int)(y - rise);

        // Küçük bir "z"
        c.Plot(px, py, ink); c.Plot(px + 1, py, ink); c.Plot(px + 2, py, ink);
        c.Plot(px + 1, py + 1, ink);
        c.Plot(px, py + 2, ink); c.Plot(px + 1, py + 2, ink); c.Plot(px + 2, py + 2, ink);
    }

    // ---------------------------------------------------------------- aksesuar

    /// <summary>
    /// Dükkandan alınan kostüm. Yüzden SONRA çiziliyor: gözlük gözlerin üstüne
    /// gelmeli, şapka kulakları örtmeli.
    /// </summary>
    private static void DrawAccessory(SpriteCanvas c, AccessoryDefinition? accessory, double hx, double hy, double r)
    {
        if (accessory is null || accessory.Type == AccessoryType.None) return;

        var color = accessory.Color;
        var dark = SpriteCanvas.Darken(color, 0.35);

        switch (accessory.Type)
        {
            case AccessoryType.Hat:
                c.Ellipse(hx, hy - r * 0.82, r * 0.95, 1.1, dark);                 // siperlik
                c.Ellipse(hx, hy - r * 1.22, r * 0.62, r * 0.42, color);           // kubbe
                break;

            case AccessoryType.Bow:
                c.Triangle(hx + r * 0.30, hy - r * 0.95, hx + r * 0.95, hy - r * 1.25, hx + r * 0.95, hy - r * 0.62, color);
                c.Triangle(hx + r * 0.30, hy - r * 0.95, hx - r * 0.35, hy - r * 1.25, hx - r * 0.35, hy - r * 0.62, color);
                c.Ellipse(hx + r * 0.30, hy - r * 0.93, 1.1, 1.1, dark);           // düğüm
                break;

            case AccessoryType.Glasses:
                var eyeDx = r * 0.42;
                var eyeY = hy - r * 0.10;
                DrawRing(c, hx - eyeDx, eyeY, 2.4, dark);
                DrawRing(c, hx + eyeDx, eyeY, 2.4, dark);
                c.Plot((int)hx, (int)eyeY, dark);                                   // köprü
                break;

            case AccessoryType.Scarf:
                c.Ellipse(hx, hy + r * 0.92, r * 0.85, 1.5, color);                 // boyun bandı
                c.Ellipse(hx + r * 0.70, hy + r * 1.45, 1.4, 2.4, dark);            // sarkan uç
                break;

            case AccessoryType.Crown:
                c.Ellipse(hx, hy - r * 0.95, r * 0.72, 1.2, color);                 // taban
                for (var i = -1; i <= 1; i++)
                    c.Triangle(hx + i * r * 0.55 - r * 0.20, hy - r * 1.00,
                               hx + i * r * 0.55 + r * 0.20, hy - r * 1.00,
                               hx + i * r * 0.55, hy - r * 1.60, color);
                break;
        }
    }

    /// <summary>İçi boş çember — gözlük camı için.</summary>
    private static void DrawRing(SpriteCanvas c, double cx, double cy, double radius, uint color)
    {
        for (var y = (int)(cy - radius); y <= (int)(cy + radius); y++)
        for (var x = (int)(cx - radius); x <= (int)(cx + radius); x++)
        {
            var dx = x + 0.5 - cx;
            var dy = y + 0.5 - cy;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= radius && d >= radius - 1.1) c.Plot(x, y, color);
        }
    }

    // ---------------------------------------------------------------- yumurta

    /// <summary>
    /// Yumurta tüm türler için ortak; yalnızca benekleri türün rengini alır.
    /// <paramref name="frame"/> burada animasyon karesi değil ÇATLAK SAYISI (0-3).
    /// </summary>
    public static WriteableBitmap CreateEgg(SpeciesDefinition species, int cracks)
    {
        var c = new SpriteCanvas();
        const double top = 3.0, bottom = 29.0, widest = top + 0.60 * (bottom - top), maxHalf = 10.5;

        const uint shell = 0xF5EEDE; // uint şart: 0xF5EEDE varsayılan olarak int'e çıkarım yapılır
        var shade = SpriteCanvas.Darken(shell, 0.14);

        for (var y = 0; y < SpriteCanvas.Size; y++)
        {
            var yc = y + 0.5;
            if (yc < top || yc > bottom) continue;

            double half;
            if (yc < widest)
            {
                var rel = (widest - yc) / (widest - top);
                half = maxHalf * 0.88 * Math.Sqrt(Math.Max(0, 1 - rel * rel));
            }
            else
            {
                var rel = (yc - widest) / (bottom - widest);
                half = maxHalf * Math.Sqrt(Math.Max(0, 1 - rel * rel));
            }

            for (var x = 0; x < SpriteCanvas.Size; x++)
            {
                var dx = x + 0.5 - CenterX;
                if (Math.Abs(dx) > half) continue;
                c.Plot(x, y, dx > half * 0.35 ? shade : shell);
            }
        }

        c.EllipseInside(12.0, 12.0, 2.2, 2.2, species.BaseColor);
        c.EllipseInside(20.5, 17.5, 2.6, 2.6, species.BaseColor);
        c.EllipseInside(14.0, 22.5, 1.8, 1.8, species.AccentColor);

        var ink = SpriteCanvas.OutlineFor(shell);
        c.Outline(ink);
        DrawCracks(c, cracks, ink);

        return c.ToBitmap();
    }

    /// <summary>
    /// Çatlak yolu: üç ayrı çatlağın pikselleri, oluşma sırasına göre dizilmiş.
    /// Kullanıcının okşama sayısına ORANLA açığa çıkarılıyor.
    ///
    /// Sabit kademeler (1. çatlak, 2. çatlak, 3. çatlak) yerine oran kullanmanın
    /// sebebi: gereken okşama sayısı 20'ye çıkınca üç kademe 3 tıkta biterdi ve
    /// kalan 17 tık hiçbir görsel değişiklik üretmezdi. Kullanıcı boşluğa tıklıyor
    /// hissine kapılırdı. Bu haliyle her okşama gözle görülür bir ilerleme.
    /// </summary>
    private static readonly (int X, int Y)[] CrackPath =
    [
        (13, 9), (14, 10), (13, 11), (15, 11), (16, 12), (15, 13), (17, 13),
        (19, 14), (18, 15), (20, 15), (19, 16), (20, 17), (18, 18), (19, 19),
        (11, 18), (12, 19), (11, 20), (13, 20), (12, 21), (14, 22), (13, 23),
        (12, 24), (14, 24), (15, 25), (13, 25),
    ];

    private static void DrawCracks(SpriteCanvas c, int cracks, uint ink)
    {
        if (cracks <= 0) return;

        var oran = Math.Clamp(cracks / (double)SimulationRules.EggCracksRequired, 0, 1);
        var adet = (int)Math.Ceiling(oran * CrackPath.Length);

        for (var i = 0; i < adet && i < CrackPath.Length; i++)
        {
            var (x, y) = CrackPath[i];
            if (c.IsSolid(x, y)) c.Plot(x, y, ink);
        }
    }
}
