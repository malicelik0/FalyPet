using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.Core.Content;
using FalyPet.Core.Model;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Rendering;

/// <summary>
/// Pet'leri VEKTÖR olarak çizer: yumuşak kenarlı, çizgi stilinde.
/// Pixel-art üreten <see cref="PetSpriteFactory"/>'nin yerini aldı.
///
/// NEDEN VEKTÖR:
///   1. Kullanıcı pixel-art istemedi.
///   2. En büyük rakip (Pets Therapy, 100+ pet) pixel-art; çizgi stili bizi
///      ilk bakışta ayırt edilir yapıyor.
///   3. Boyut ayarı artık gerçekten serbest — pixel-art'ta ölçek tam sayı katı
///      olmak zorundaydı, vektörde her boyut aynı netlikte.
///
/// ÇİZİM UZAYI: 100x100 normalize. Hangi piksel boyutunda istenirse orada
/// çiziliyor, sonradan ölçeklenmiyor — bu yüzden hiçbir boyutta bulanıklaşmıyor.
/// </summary>
internal static class VectorPetRenderer
{
    /// <summary>Normalize çizim uzayı. Bütün koordinatlar bunun içinde.</summary>
    private const double W = 100.0;

    /// <summary>Ayakların bastığı çizgi.</summary>
    private const double Ground = 92.0;

    /// <summary>Kulak/boynuz bunun üstüne çıkamaz.</summary>
    private const double Ceiling = 6.0;

    private const double CenterX = 50.0;

    /// <summary>Kontur kalınlığı. Çizgi stilinin karakterini bu belirliyor.</summary>
    private const double Stroke = 3.2;

    public static BitmapSource Render(SpeciesDefinition species, GrowthStage stage, PetAnimation anim,
        int frame, AccessoryDefinition? accessory, int gaze, bool blinking, int pixelSize)
    {
        var visual = new DrawingVisual();

        // Vektörde anti-aliasing açık kalmalı; kapatmak pixel-art'a geri dönmek olur.
        RenderOptions.SetEdgeMode(visual, EdgeMode.Unspecified);

        using (var dc = visual.RenderOpen())
        {
            // Normalize uzaydan istenen piksel boyutuna ölçekleme.
            dc.PushTransform(new ScaleTransform(pixelSize / W, pixelSize / W));

            if (stage == GrowthStage.Egg) DrawEgg(dc, species, frame);
            else DrawPet(dc, species, stage, anim, frame, accessory, gaze, blinking);

            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // ---------------------------------------------------------------- ölçüler

    private readonly record struct Metrics(double BodyRx, double BodyRy, double HeadR);

    private static Metrics MetricsFor(GrowthStage stage, BodyShape shape)
    {
        // Bebekte kafa gövdeye göre büyük — sevimliliğin en güçlü kaldıracı.
        var m = stage switch
        {
            GrowthStage.Baby => new Metrics(16, 13, 19),
            GrowthStage.Child => new Metrics(20, 17, 19),
            GrowthStage.Teen => new Metrics(23, 20, 18),
            _ => new Metrics(26, 23, 20),
        };

        return shape switch
        {
            BodyShape.Tall => m with { BodyRx = m.BodyRx * 0.85, BodyRy = m.BodyRy * 1.25 },
            BodyShape.Wide => m with { BodyRx = m.BodyRx * 1.22, BodyRy = m.BodyRy * 0.92 },
            BodyShape.Blob => m with { BodyRx = m.BodyRx * 1.15, BodyRy = m.BodyRy * 1.10 },
            _ => m,
        };
    }

    private static double Bob(PetAnimation anim, int frame) => anim switch
    {
        PetAnimation.Idle => frame == 1 ? -1.8 : 0,
        PetAnimation.Walk => frame == 1 ? -2.6 : 0,
        PetAnimation.Play => frame switch { 1 => -8, 2 => -3, _ => 0 },
        PetAnimation.Eat or PetAnimation.Drink => frame == 1 ? 2.2 : 0,
        PetAnimation.Wash => frame == 1 ? -2 : 0,
        _ => 0,
    };

    private static double Slump(PetAnimation anim) => anim switch
    {
        PetAnimation.Sleep => 9,
        PetAnimation.Sulk => 6,
        PetAnimation.Sick => 4,
        _ => 0,
    };

    private static double EarTop(EarType ears) => ears switch
    {
        EarType.Pointed => 1.55,
        EarType.Horns => 1.45,
        EarType.Tufts => 1.75,
        EarType.Round => 1.25,
        EarType.Antennae => 1.85,
        _ => 1.0,
    };

    private static double AccessoryTop(AccessoryDefinition? a) => a?.Type switch
    {
        AccessoryType.Crown => 1.85,
        AccessoryType.Hat => 1.75,
        AccessoryType.Bow => 1.35,
        _ => 0,
    };

    // ---------------------------------------------------------------- renkler

    private static Color Rgb(uint v) => Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R * (1 - t) + b.R * t),
            (byte)(a.G * (1 - t) + b.G * t),
            (byte)(a.B * (1 - t) + b.B * t));
    }

    /// <summary>Kontur rengi: siyah değil, gövdenin koyulaştırılmışı. Siyah kontur ucuz görünür.</summary>
    private static Color Ink(uint baseColor) => Mix(Mix(Rgb(baseColor), Colors.Black, 0.62), Rgb(0x2A2233), 0.4);

    private static Pen OutlinePen(uint baseColor, double thickness = Stroke)
    {
        var pen = new Pen(new SolidColorBrush(Ink(baseColor)), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }

    private static Brush Fill(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    // ---------------------------------------------------------------- pet

    private static void DrawPet(DrawingContext dc, SpeciesDefinition s, GrowthStage stage,
        PetAnimation anim, int frame, AccessoryDefinition? accessory, int gaze, bool blinking)
    {
        var m = MetricsFor(stage, s.Body);
        var bob = Bob(anim, frame);
        var pen = OutlinePen(s.BaseColor);
        var body = Fill(Rgb(s.BaseColor));

        if (s.Body == BodyShape.Blob)
        {
            DrawBlob(dc, s, m, bob, anim, frame, pen, body, accessory, gaze, blinking);
            return;
        }

        var bodyCy = Ground - m.BodyRy + bob;
        var headR = m.HeadR;
        var headCy = Math.Max(
            bodyCy - m.BodyRy - headR * 0.42 + Slump(anim),
            Ceiling + EarTop(s.Ears) * headR * Math.Max(1.0, AccessoryTop(accessory) / EarTop(s.Ears)));

        // Aksesuar da kafayı aşağı itebilmeli.
        headCy = Math.Max(headCy, Ceiling + AccessoryTop(accessory) * headR);

        var headCx = CenterX + (anim == PetAnimation.Walk && frame == 1 ? 1.5 : 0);

        DrawTail(dc, s, m, bodyCy, anim, frame, pen, body);
        DrawLegs(dc, s, m, bodyCy, anim, frame, pen, body);

        // Gövde
        dc.DrawEllipse(body, pen, new Point(CenterX, bodyCy), m.BodyRx, m.BodyRy);
        DrawMarking(dc, s, m, bodyCy);

        DrawEars(dc, s, headCx, headCy, headR, pen, body);
        dc.DrawEllipse(body, pen, new Point(headCx, headCy), headR, headR * 0.93);

        DrawFace(dc, s, headCx, headCy, headR, anim, frame, gaze, blinking);
        DrawAccessory(dc, accessory, headCx, headCy, headR);
    }

    private static void DrawBlob(DrawingContext dc, SpeciesDefinition s, Metrics m, double bob,
        PetAnimation anim, int frame, Pen pen, Brush body, AccessoryDefinition? accessory, int gaze, bool blinking)
    {
        var rx = m.BodyRx + 4;
        var ry = Math.Min(m.BodyRy + m.HeadR * 0.8, (Ground - Ceiling) / 2);
        var cy = Ground - ry + bob;

        // Nefes: yatay genişleyip dikey basılma, hacim korunuyormuş hissi.
        var squash = frame == 1 ? 0.06 : 0.0;

        if (s.Tail == TailType.Tentacle) DrawTentacles(dc, s, rx, cy + ry, frame, pen, body);

        dc.DrawEllipse(body, pen, new Point(CenterX, cy + ry * squash),
            rx * (1 + squash), ry * (1 - squash));

        DrawMarking(dc, s, m, cy + ry * 0.35);

        var faceY = cy - ry * 0.26;
        DrawFace(dc, s, CenterX, faceY, m.HeadR * 0.95, anim, frame, gaze, blinking);
        DrawAccessory(dc, accessory, CenterX, Math.Max(faceY, cy - ry + m.HeadR * 0.5), m.HeadR * 0.95);
    }

    // ---------------------------------------------------------------- parçalar

    private static void DrawEars(DrawingContext dc, SpeciesDefinition s, double hx, double hy, double r, Pen pen, Brush body)
    {
        var inner = Fill(Mix(Rgb(s.BaseColor), Rgb(s.AccentColor), 0.6));

        switch (s.Ears)
        {
            case EarType.Pointed:
                foreach (var sign in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(hx + sign * r * 0.78, hy - r * 0.42), true, true);
                        c.LineTo(new Point(hx + sign * r * 0.62, hy - r * 1.5), true, true);
                        c.LineTo(new Point(hx + sign * r * 0.16, hy - r * 0.74), true, true);
                    }
                    g.Freeze();
                    dc.DrawGeometry(body, pen, g);
                }
                break;

            case EarType.Floppy:
                foreach (var sign in new[] { -1, 1 })
                    dc.DrawEllipse(Fill(Mix(Rgb(s.BaseColor), Colors.Black, 0.12)), pen,
                        new Point(hx + sign * r * 0.92, hy + r * 0.2), r * 0.34, r * 0.72);
                break;

            case EarType.Round:
                foreach (var sign in new[] { -1, 1 })
                    dc.DrawEllipse(Fill(Rgb(s.AccentColor)), pen,
                        new Point(hx + sign * r * 0.72, hy - r * 0.7), r * 0.36, r * 0.36);
                break;

            case EarType.Horns:
                foreach (var sign in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(hx + sign * r * 0.62, hy - r * 0.6), true, true);
                        c.LineTo(new Point(hx + sign * r * 0.5, hy - r * 1.38), true, true);
                        c.LineTo(new Point(hx + sign * r * 0.26, hy - r * 0.72), true, true);
                    }
                    g.Freeze();
                    dc.DrawGeometry(Fill(Rgb(s.AccentColor)), pen, g);
                }
                break;

            case EarType.Tufts:
                foreach (var sign in new[] { -1, 1 })
                {
                    dc.DrawEllipse(body, pen, new Point(hx + sign * r * 0.5, hy - r * 1.05), r * 0.24, r * 0.62);
                    dc.DrawEllipse(inner, null, new Point(hx + sign * r * 0.5, hy - r * 1.08), r * 0.1, r * 0.38);
                }
                break;

            case EarType.Antennae:
                // Uğur böceği anteni: ince eğri + ucunda topuz.
                foreach (var sign in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(hx + sign * r * 0.32, hy - r * 0.78), false, false);
                        c.QuadraticBezierTo(
                            new Point(hx + sign * r * 0.62, hy - r * 1.35),
                            new Point(hx + sign * r * 0.5, hy - r * 1.62), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(null, OutlinePen(s.BaseColor, Stroke * 0.7), g);
                    dc.DrawEllipse(Fill(Ink(s.BaseColor)), null,
                        new Point(hx + sign * r * 0.5, hy - r * 1.62), r * 0.13, r * 0.13);
                }
                break;
        }
    }

    private static void DrawTail(DrawingContext dc, SpeciesDefinition s, Metrics m, double bodyCy,
        PetAnimation anim, int frame, Pen pen, Brush body)
    {
        // Küskünken kuyruk öne geçer: pet sırtını dönmüş demektir.
        var side = anim == PetAnimation.Sulk ? -1 : 1;
        var wag = anim is PetAnimation.Play or PetAnimation.Walk ? (frame - 1) * 4.0 : 0;
        var baseX = CenterX + side * m.BodyRx * 0.86;

        switch (s.Tail)
        {
            case TailType.Thin:
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(baseX, bodyCy), false, false);
                    c.QuadraticBezierTo(
                        new Point(baseX + side * 13, bodyCy - 8 + wag),
                        new Point(baseX + side * 8, bodyCy - 19 + wag), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, OutlinePen(s.BaseColor, Stroke * 1.5), g);
                break;

            case TailType.Bushy:
                dc.DrawEllipse(body, pen, new Point(baseX + side * 9, bodyCy - 7 + wag), 10, 13);
                dc.DrawEllipse(Fill(Rgb(s.AccentColor)), null,
                    new Point(baseX + side * 11, bodyCy - 14 + wag), 5, 6);
                break;

            case TailType.Curl:
                dc.DrawEllipse(body, pen, new Point(baseX + side * 6, bodyCy - 8 + wag), 5, 5);
                dc.DrawEllipse(body, pen, new Point(baseX + side * 11, bodyCy - 15 + wag), 4, 4);
                break;
        }
    }

    private static void DrawTentacles(DrawingContext dc, SpeciesDefinition s, double rx, double bottom, int frame, Pen pen, Brush body)
    {
        for (var i = -2; i <= 2; i++)
        {
            var x = CenterX + i * (rx / 2.5);
            var drop = (i + frame) % 2 == 0 ? 2.5 : 0;
            dc.DrawEllipse(body, pen, new Point(x, Math.Min(bottom + drop, Ground - 2)), rx / 5.5, 5.5);
        }
    }

    private static void DrawLegs(DrawingContext dc, SpeciesDefinition s, Metrics m, double bodyCy,
        PetAnimation anim, int frame, Pen pen, Brush body)
    {
        if (anim == PetAnimation.Sleep) return;

        var swing = anim == PetAnimation.Walk ? (frame - 1) * 6.0 : 0;
        var foot = Fill(Mix(Rgb(s.BaseColor), Colors.Black, 0.16));

        foreach (var sign in new[] { -1, 1 })
        {
            var x = CenterX + sign * m.BodyRx * 0.52 + (sign > 0 ? swing : -swing);
            dc.DrawEllipse(foot, pen, new Point(x, Ground - 2), 7, 5);
        }
    }

    private static void DrawMarking(DrawingContext dc, SpeciesDefinition s, Metrics m, double bodyCy)
    {
        var accent = Fill(Rgb(s.AccentColor));

        switch (s.Marking)
        {
            case MarkingType.Belly:
                dc.DrawEllipse(accent, null, new Point(CenterX, bodyCy + m.BodyRy * 0.32),
                    m.BodyRx * 0.58, m.BodyRy * 0.58);
                break;

            case MarkingType.Stripes:
                var dark = Fill(Mix(Rgb(s.BaseColor), Colors.Black, 0.24));
                for (var i = 0; i < 3; i++)
                {
                    var y = bodyCy - m.BodyRy * 0.42 + i * m.BodyRy * 0.42;
                    dc.DrawRoundedRectangle(dark, null,
                        new Rect(CenterX - m.BodyRx * 0.52, y, m.BodyRx * 1.04, 3.4), 1.7, 1.7);
                }
                break;

            case MarkingType.Spots:
                dc.DrawEllipse(accent, null, new Point(CenterX - m.BodyRx * 0.38, bodyCy), 5, 5);
                dc.DrawEllipse(accent, null, new Point(CenterX + m.BodyRx * 0.42, bodyCy + 5), 6, 6);
                dc.DrawEllipse(accent, null, new Point(CenterX + m.BodyRx * 0.04, bodyCy - 7), 4, 4);
                break;

            case MarkingType.Patch:
                dc.DrawEllipse(accent, null, new Point(CenterX - m.BodyRx * 0.42, bodyCy + 2),
                    m.BodyRx * 0.46, m.BodyRy * 0.52);
                break;

            case MarkingType.LadybugShell:
                DrawLadybugShell(dc, s, m, bodyCy);
                break;
        }
    }

    /// <summary>Uğur böceği kabuğu: orta çizgi + simetrik benekler.</summary>
    private static void DrawLadybugShell(DrawingContext dc, SpeciesDefinition s, Metrics m, double bodyCy)
    {
        var dark = Fill(Rgb(s.AccentColor));
        var pen = OutlinePen(s.BaseColor, Stroke * 0.85);

        // Kanatları ayıran orta çizgi.
        dc.DrawLine(pen, new Point(CenterX, bodyCy - m.BodyRy * 0.85), new Point(CenterX, bodyCy + m.BodyRy * 0.9));

        // Simetrik benekler — uğur böceğini uğur böceği yapan şey.
        var noktalar = new (double dx, double dy, double r)[]
        {
            (-0.46, -0.28, 0.20), (0.46, -0.28, 0.20),
            (-0.40,  0.34, 0.17), (0.40,  0.34, 0.17),
            (-0.20,  0.02, 0.13), (0.20,  0.02, 0.13),
        };

        foreach (var (dx, dy, r) in noktalar)
            dc.DrawEllipse(dark, null,
                new Point(CenterX + m.BodyRx * dx, bodyCy + m.BodyRy * dy), m.BodyRx * r, m.BodyRx * r);
    }

    // ---------------------------------------------------------------- yüz

    private static void DrawFace(DrawingContext dc, SpeciesDefinition s, double hx, double hy, double r,
        PetAnimation anim, int frame, int gaze, bool blinking)
    {
        // Küskün pet sırtını döner: yüz hiç çizilmez. En güçlü ifade, ifadenin yokluğudur.
        if (anim == PetAnimation.Sulk) return;

        var eyeDx = r * 0.40;
        var eyeY = hy - r * 0.08;
        var ink = Ink(s.BaseColor);
        var linePen = new Pen(new SolidColorBrush(ink), Stroke * 0.9)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        linePen.Freeze();

        var effectiveGaze = anim == PetAnimation.Walk ? 1 : gaze;

        switch (anim)
        {
            case PetAnimation.Sleep:
                ClosedEye(dc, hx - eyeDx, eyeY, r, linePen);
                ClosedEye(dc, hx + eyeDx, eyeY, r, linePen);
                Zzz(dc, hx + r * 1.15, hy - r * 0.9, frame, ink);
                return;

            case PetAnimation.Play:
                HappyEye(dc, hx - eyeDx, eyeY, r, linePen);
                HappyEye(dc, hx + eyeDx, eyeY, r, linePen);
                Mouth(dc, hx, hy + r * 0.42, r, true, ink, linePen);
                return;

            case PetAnimation.Sick:
                CrossEye(dc, hx - eyeDx, eyeY, r, linePen);
                CrossEye(dc, hx + eyeDx, eyeY, r, linePen);
                Mouth(dc, hx, hy + r * 0.45, r, false, ink, linePen);
                return;

            default:
                if (blinking)
                {
                    ClosedEye(dc, hx - eyeDx, eyeY, r, linePen);
                    ClosedEye(dc, hx + eyeDx, eyeY, r, linePen);
                }
                else
                {
                    OpenEye(dc, hx - eyeDx, eyeY, r, effectiveGaze, ink);
                    OpenEye(dc, hx + eyeDx, eyeY, r, effectiveGaze, ink);
                }

                var acik = anim is PetAnimation.Eat or PetAnimation.Drink && frame != 1;
                Mouth(dc, hx, hy + r * 0.44, r, acik, ink, linePen);
                return;
        }
    }

    private static void OpenEye(DrawingContext dc, double x, double y, double r, int gaze, Color ink)
    {
        dc.DrawEllipse(Fill(Colors.White), new Pen(new SolidColorBrush(ink), Stroke * 0.6), new Point(x, y), r * 0.22, r * 0.27);
        dc.DrawEllipse(Fill(ink), null, new Point(x + gaze * r * 0.09, y), r * 0.13, r * 0.17);
        dc.DrawEllipse(Fill(Colors.White), null, new Point(x + gaze * r * 0.09 - r * 0.05, y - r * 0.07), r * 0.045, r * 0.045);
    }

    private static void ClosedEye(DrawingContext dc, double x, double y, double r, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x - r * 0.2, y), false, false);
            c.QuadraticBezierTo(new Point(x, y + r * 0.12), new Point(x + r * 0.2, y), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static void HappyEye(DrawingContext dc, double x, double y, double r, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x - r * 0.2, y + r * 0.06), false, false);
            c.QuadraticBezierTo(new Point(x, y - r * 0.18), new Point(x + r * 0.2, y + r * 0.06), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static void CrossEye(DrawingContext dc, double x, double y, double r, Pen pen)
    {
        var d = r * 0.16;
        dc.DrawLine(pen, new Point(x - d, y - d), new Point(x + d, y + d));
        dc.DrawLine(pen, new Point(x + d, y - d), new Point(x - d, y + d));
    }

    private static void Mouth(DrawingContext dc, double x, double y, double r, bool open, Color ink, Pen pen)
    {
        if (open)
        {
            dc.DrawEllipse(Fill(ink), null, new Point(x, y), r * 0.17, r * 0.14);
            return;
        }

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x - r * 0.16, y - r * 0.04), false, false);
            c.QuadraticBezierTo(new Point(x, y + r * 0.1), new Point(x + r * 0.16, y - r * 0.04), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static void Zzz(DrawingContext dc, double x, double y, int frame, Color ink)
    {
        var pen = new Pen(new SolidColorBrush(ink), Stroke * 0.75) { LineJoin = PenLineJoin.Round };
        pen.Freeze();

        var rise = frame * 4.0;
        var s = 7.0;
        var px = x + frame * 1.5;
        var py = y - rise;

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(px, py), false, false);
            c.LineTo(new Point(px + s, py), true, false);
            c.LineTo(new Point(px, py + s), true, false);
            c.LineTo(new Point(px + s, py + s), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    // ---------------------------------------------------------------- aksesuar

    private static void DrawAccessory(DrawingContext dc, AccessoryDefinition? a, double hx, double hy, double r)
    {
        if (a is null || a.Type == AccessoryType.None) return;

        var color = Rgb(a.Color);
        var pen = OutlinePen(a.Color, Stroke * 0.9);
        var brush = Fill(color);

        switch (a.Type)
        {
            case AccessoryType.Hat:
                dc.DrawRoundedRectangle(Fill(Mix(color, Colors.Black, 0.3)), pen,
                    new Rect(hx - r * 0.95, hy - r * 0.92, r * 1.9, r * 0.18), 3, 3);
                dc.DrawEllipse(brush, pen, new Point(hx, hy - r * 1.25), r * 0.6, r * 0.42);
                break;

            case AccessoryType.Bow:
                foreach (var sign in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(hx + r * 0.3, hy - r * 0.95), true, true);
                        c.LineTo(new Point(hx + r * 0.3 + sign * r * 0.55, hy - r * 1.28), true, true);
                        c.LineTo(new Point(hx + r * 0.3 + sign * r * 0.55, hy - r * 0.62), true, true);
                    }
                    g.Freeze();
                    dc.DrawGeometry(brush, pen, g);
                }
                dc.DrawEllipse(Fill(Mix(color, Colors.Black, 0.3)), null, new Point(hx + r * 0.3, hy - r * 0.95), r * 0.11, r * 0.11);
                break;

            case AccessoryType.Glasses:
                var gp = OutlinePen(a.Color, Stroke * 0.8);
                foreach (var sign in new[] { -1, 1 })
                    dc.DrawEllipse(null, gp, new Point(hx + sign * r * 0.40, hy - r * 0.08), r * 0.26, r * 0.26);
                dc.DrawLine(gp, new Point(hx - r * 0.14, hy - r * 0.08), new Point(hx + r * 0.14, hy - r * 0.08));
                break;

            case AccessoryType.Scarf:
                dc.DrawRoundedRectangle(brush, pen, new Rect(hx - r * 0.8, hy + r * 0.78, r * 1.6, r * 0.3), 4, 4);
                dc.DrawRoundedRectangle(Fill(Mix(color, Colors.Black, 0.25)), pen,
                    new Rect(hx + r * 0.5, hy + r * 1.0, r * 0.28, r * 0.62), 3, 3);
                break;

            case AccessoryType.Crown:
                var cg = new StreamGeometry();
                using (var c = cg.Open())
                {
                    c.BeginFigure(new Point(hx - r * 0.62, hy - r * 0.88), true, true);
                    c.LineTo(new Point(hx - r * 0.62, hy - r * 1.42), true, true);
                    c.LineTo(new Point(hx - r * 0.31, hy - r * 1.08), true, true);
                    c.LineTo(new Point(hx, hy - r * 1.55), true, true);
                    c.LineTo(new Point(hx + r * 0.31, hy - r * 1.08), true, true);
                    c.LineTo(new Point(hx + r * 0.62, hy - r * 1.42), true, true);
                    c.LineTo(new Point(hx + r * 0.62, hy - r * 0.88), true, true);
                }
                cg.Freeze();
                dc.DrawGeometry(brush, pen, cg);
                break;
        }
    }

    // ---------------------------------------------------------------- yumurta

    /// <summary><paramref name="cracks"/> animasyon karesi değil, okşama sayısı.</summary>
    private static void DrawEgg(DrawingContext dc, SpeciesDefinition species, int cracks)
    {
        var shell = Color.FromRgb(0xF7, 0xF1, 0xE3);
        var pen = OutlinePen(0xC9BFA8, Stroke);

        // Yumurta: alt yarısı daire, üst yarısı daha dar kubbe.
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(CenterX, 12), true, true);
            c.BezierTo(new Point(CenterX + 27, 24), new Point(CenterX + 32, 58), new Point(CenterX + 32, 66), true, true);
            c.ArcTo(new Point(CenterX - 32, 66), new Size(32, 32), 0, true, SweepDirection.Clockwise, true, true);
            c.BezierTo(new Point(CenterX - 32, 58), new Point(CenterX - 27, 24), new Point(CenterX, 12), true, true);
        }
        g.Freeze();

        dc.DrawGeometry(Fill(shell), pen, g);

        // Benekler türün rengini alıyor — hangi yumurta olduğu belli olsun.
        dc.PushClip(g);
        dc.DrawEllipse(Fill(Rgb(species.BaseColor)), null, new Point(CenterX - 12, 38), 7, 7);
        dc.DrawEllipse(Fill(Rgb(species.BaseColor)), null, new Point(CenterX + 14, 58), 8.5, 8.5);
        dc.DrawEllipse(Fill(Rgb(species.AccentColor)), null, new Point(CenterX - 7, 74), 6, 6);

        DrawCracks(dc, cracks);
        dc.Pop();
    }

    private static void DrawCracks(DrawingContext dc, int cracks)
    {
        if (cracks <= 0) return;

        // Çatlaklar okşama sayısına ORANLA açılıyor. Sabit kademe olsaydı 20 tıkın
        // çoğunda hiçbir görsel değişiklik olmaz, kullanıcı boşluğa tıklıyor sanardı.
        var oran = Math.Clamp(cracks / (double)SimulationRules.EggCracksRequired, 0, 1);
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x6A, 0x5C, 0x4C)), Stroke * 0.7)
        { LineJoin = PenLineJoin.Miter };
        pen.Freeze();

        var yol = new[]
        {
            new Point(CenterX - 4, 22), new Point(CenterX + 4, 30), new Point(CenterX - 3, 37),
            new Point(CenterX + 6, 45), new Point(CenterX - 2, 53), new Point(CenterX + 8, 60),
            new Point(CenterX + 1, 68), new Point(CenterX + 10, 76), new Point(CenterX + 3, 83),
        };

        var adet = (int)Math.Ceiling(oran * (yol.Length - 1));
        if (adet < 1) return;

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(yol[0], false, false);
            for (var i = 1; i <= adet && i < yol.Length; i++) c.LineTo(yol[i], true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }
}
