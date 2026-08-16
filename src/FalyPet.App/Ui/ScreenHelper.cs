using System;
using System.Windows;
using System.Windows.Media;

namespace FalyPet.App.Ui;

/// <summary>
/// Çoklu monitörde doğru ekranı bulmak için ortak yardımcı.
///
/// <see cref="SystemParameters.WorkArea"/> KULLANILMAMALI: o her zaman BİRİNCİL
/// monitörün alanını verir. İkincil monitördeki bir pencereyi ona göre kıstırmak
/// pencereyi öteki ekrana fırlatır. Ölçüldü: pet (3185,686)'da dururken konuşma
/// balonu (2346,604)'e, yani başka monitöre düşüyordu.
///
/// Sanal masaüstünün sınırlayıcı kutusu da kullanılmamalı — farklı boyutlu iki
/// monitörde kutunun köşeleri hiçbir ekrana denk gelmeyen ölü bölgelerdir.
/// Doğru cevap: <c>Screen.FromRectangle</c> ile EN YAKIN gerçek monitör.
/// </summary>
internal static class ScreenHelper
{
    /// <summary>
    /// <paramref name="anchor"/> dikdörtgenine en yakın gerçek monitörün çalışma
    /// alanı, DIP cinsinden. <paramref name="window"/> yalnızca DPI dönüşümü için
    /// kullanılıyor.
    /// </summary>
    public static Rect NearestWorkArea(Window window, Rect anchor)
    {
        var target = PresentationSource.FromVisual(window)?.CompositionTarget;
        var toDevice = target?.TransformToDevice ?? Matrix.Identity;
        var fromDevice = target?.TransformFromDevice ?? Matrix.Identity;

        var tl = toDevice.Transform(new Point(anchor.Left, anchor.Top));
        var br = toDevice.Transform(new Point(anchor.Right, anchor.Bottom));

        var deviceRect = new System.Drawing.Rectangle(
            (int)Math.Round(tl.X), (int)Math.Round(tl.Y),
            Math.Max(1, (int)Math.Round(br.X - tl.X)),
            Math.Max(1, (int)Math.Round(br.Y - tl.Y)));

        var wa = System.Windows.Forms.Screen.FromRectangle(deviceRect).WorkingArea;

        return new Rect(fromDevice.Transform(new Point(wa.Left, wa.Top)),
                        fromDevice.Transform(new Point(wa.Right, wa.Bottom)));
    }
}
