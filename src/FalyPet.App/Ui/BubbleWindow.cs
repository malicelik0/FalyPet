using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FalyPet.App.Interop;

namespace FalyPet.App.Ui;

/// <summary>
/// Pet'in üstünde beliren konuşma balonu.
///
/// Ayrı bir pencere, pet penceresinin içinde değil. Sebebi: pet penceresi bilerek
/// 160x160 tutuluyor (WPF saydamlıkta yazılımsal çiziyor) ve metin oraya sığmaz.
/// Ayrı pencere ayrıca balonun pet ekranın kenarındayken bile okunabilmesini sağlıyor.
///
/// Her zaman fareye geçirgen: balon asla tıklamayı yakalamamalı.
/// </summary>
internal sealed class BubbleWindow : Window
{
    private readonly TextBlock _text;
    private readonly DispatcherTimer _hideTimer;

    public BubbleWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsHitTestVisible = false;
        Focusable = false;
        ShowActivated = false;

        _text = new TextBlock
        {
            FontSize = 12.5,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 190,
        };

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xF2)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x30, 0x3C)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Child = _text,
        };

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); Hide(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongEx(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongEx(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// Balonu gösterir. <paramref name="anchor"/> pet penceresinin sol-üst köşesi,
    /// <paramref name="petWidth"/> genişliği — balon bunun üstüne ortalanır.
    /// </summary>
    public void Say(string message, Point anchor, double petWidth, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _text.Text = message;
        Show();

        // SizeToContent ölçüyü Show'dan sonra kesinleştirir; konumlandırma o yüzden burada.
        UpdateLayout();
        Left = anchor.X + petWidth / 2 - ActualWidth / 2;
        Top = anchor.Y - ActualHeight - 4;

        KeepOnScreen();

        _hideTimer.Stop();
        _hideTimer.Interval = duration;
        _hideTimer.Start();
    }

    private void KeepOnScreen()
    {
        var work = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, work.Left + 4, Math.Max(work.Left + 4, work.Right - ActualWidth - 4));
        if (Top < work.Top + 4) Top = work.Top + 4;
    }

    public void HideNow()
    {
        _hideTimer.Stop();
        Hide();
    }
}
