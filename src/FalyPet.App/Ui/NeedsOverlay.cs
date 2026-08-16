using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using FalyPet.App.Interop;
using FalyPet.Core.Model;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Ui;

/// <summary>
/// Fare pet'in üstündeyken beliren ihtiyaç göstergesi: açlık, susuzluk, mutluluk.
///
/// Konuşma balonundan ayrı bir pencere çünkü davranışı farklı: balon bir süre
/// gösterip kaybolur, bu ise fare üstünde durduğu SÜRECE görünür. İkisini tek
/// pencerede toplamak, birinin öbürünü ezmesi demekti.
///
/// Her zaman fareye geçirgen: pet'e tıklamaya çalışırken araya girmemeli.
/// </summary>
internal sealed class NeedsOverlay : Window
{
    private const double BarWidth = 96;
    private const double BarHeight = 7;

    private readonly List<(NeedKind Kind, Border Fill, TextBlock Value)> _rows = [];

    public NeedsOverlay()
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

        var stack = new StackPanel();
        foreach (var kind in new[] { NeedKind.Hunger, NeedKind.Thirst, NeedKind.Happiness })
            stack.Children.Add(BuildRow(kind));

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFB, 0xF2)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x30, 0x3C)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(9, 7, 9, 7),
            Child = stack,
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongEx(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongEx(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private UIElement BuildRow(NeedKind kind)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BarWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        var label = new TextBlock
        {
            Text = Label(kind),
            FontSize = 11.5,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Dolu kısım, boş yuvanın İÇİNDE sola yaslı duruyor; genişliği değerle
        // birlikte değişiyor. Böylece çubuk her zaman aynı yerde başlıyor.
        var fill = new Border { Height = BarHeight, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3) };
        var track = new Border
        {
            Height = BarHeight,
            Width = BarWidth,
            Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xDC, 0xCE)),
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = fill,
        };
        Grid.SetColumn(track, 1);
        row.Children.Add(track);

        var value = new TextBlock
        {
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Right,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x62, 0x70)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(value, 2);
        row.Children.Add(value);

        _rows.Add((kind, fill, value));
        return row;
    }

    private static string Label(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => "Açlık",
        NeedKind.Thirst => "Susuzluk",
        NeedKind.Happiness => "Oyun",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Renk değere göre: iyi yeşil, orta sarı, kritik kırmızı.
    /// Sayıyı okumadan bakışta anlaşılması için — asıl amaç bu zaten.
    /// </summary>
    private static Color BarColor(double value) => value switch
    {
        >= NeedAlertTracker.ResetThreshold => Color.FromRgb(0x5A, 0xA8, 0x6A),
        >= NeedAlertTracker.LowThreshold => Color.FromRgb(0xE0, 0xB0, 0x4C),
        _ => Color.FromRgb(0xD0, 0x5A, 0x50),
    };

    public void UpdateValues(PetSimulation sim)
    {
        foreach (var (kind, fill, value) in _rows)
        {
            var v = Math.Clamp(sim.Needs.Get(kind), 0, 100);
            fill.Width = Math.Max(2, BarWidth * v / 100.0);
            fill.Background = new SolidColorBrush(BarColor(v));
            value.Text = $"%{v:F0}";
        }
    }

    /// <summary>Pet penceresinin üstüne ortalar.</summary>
    public void ShowAbove(Rect pet)
    {
        if (!IsVisible) Show();
        UpdateLayout();

        Left = pet.Left + pet.Width / 2 - ActualWidth / 2;
        Top = pet.Top - ActualHeight - 6;

        // Pet'in bulunduğu ekrana göre kıstırılıyor, birincil ekrana göre değil —
        // yoksa ikincil monitördeki pet'in paneli öteki ekrana fırlıyor.
        var work = ScreenHelper.NearestWorkArea(this, pet);
        Left = Math.Clamp(Left, work.Left + 4, Math.Max(work.Left + 4, work.Right - ActualWidth - 4));
        if (Top < work.Top + 4) Top = pet.Bottom + 6;   // yukarıda yer yoksa altına
    }
}
