using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.App.Rendering;
using FalyPet.Core.Content;
using FalyPet.Core.Model;

namespace FalyPet.App.Ui;

/// <summary>
/// İlk açılışta çıkan tür seçimi ve isim verme ekranı.
///
/// Tür kartlarında BEBEK sprite'ı gösteriliyor, yetişkin değil — kullanıcı ilk
/// göreceği şeyi seçmeli. Yetişkin hali gösterilse seçtiğiyle karşılaştığı
/// arasında uyumsuzluk olurdu.
/// </summary>
internal sealed class OnboardingWindow : Window
{
    private readonly SpriteCache _sprites;
    private readonly TextBox _nameBox;
    private readonly Button _startButton;
    private readonly TextBlock _hint;

    private SpeciesDefinition? _selected;
    private ToggleButton? _selectedButton;

    public string SelectedSpeciesId => _selected?.Id ?? SpeciesCatalog.All[0].Id;
    public string PetName => string.IsNullOrWhiteSpace(_nameBox.Text) ? "Momo" : _nameBox.Text.Trim();

    public OnboardingWindow(SpriteCache sprites)
    {
        _sprites = sprites;

        Title = "FalyPet";
        Width = 560;
        Height = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF4, 0xEC));
        FontFamily = new FontFamily("Segoe UI");

        var root = new DockPanel { Margin = new Thickness(18) };

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Yumurtanı seç",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
        });
        header.Children.Add(new TextBlock
        {
            Text = "Yumurtadan bu tür çıkacak. Sonra değiştiremezsin — acele etme.",
            FontSize = 12.5,
            Margin = new Thickness(0, 4, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x62, 0x70)),
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = BuildFooter(out _nameBox, out _startButton, out _hint);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildSpeciesGrid(),
        });

        Content = root;
    }

    private WrapPanel BuildSpeciesGrid()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var species in SpeciesCatalog.All)
            panel.Children.Add(BuildSpeciesCard(species));

        return panel;
    }

    private ToggleButton BuildSpeciesCard(SpeciesDefinition species)
    {
        var image = new Image
        {
            Source = _sprites.Get(species, GrowthStage.Baby, PetAnimation.Idle, 0, faceLeft: false),
            Width = 64,
            Height = 64,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(image);
        content.Children.Add(new TextBlock
        {
            Text = species.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var button = new ToggleButton
        {
            Content = content,
            Width = 96,
            Height = 100,
            Margin = new Thickness(4),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xD6, 0xC8)),
            BorderThickness = new Thickness(2),
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        button.Checked += (_, _) => Select(species, button);
        button.Click += (_, _) => { if (button.IsChecked != true) button.IsChecked = true; };

        return button;
    }

    private void Select(SpeciesDefinition species, ToggleButton button)
    {
        if (!ReferenceEquals(_selectedButton, button) && _selectedButton is not null)
            _selectedButton.IsChecked = false;

        _selected = species;
        _selectedButton = button;

        _startButton.IsEnabled = true;
        _hint.Text = $"{species.DisplayName} seçildi.";
    }

    private StackPanel BuildFooter(out TextBox nameBox, out Button startButton, out TextBlock hint)
    {
        var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        hint = new TextBlock
        {
            Text = "Bir tür seç.",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x62, 0x70)),
        };
        footer.Children.Add(hint);

        var row = new DockPanel();

        row.Children.Add(new TextBlock
        {
            Text = "Adı:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 13,
        });

        startButton = new Button
        {
            Content = "Başla",
            Width = 110,
            Height = 32,
            IsEnabled = false,
            IsDefault = true,
            Margin = new Thickness(10, 0, 0, 0),
        };
        startButton.Click += (_, _) => { DialogResult = true; Close(); };
        DockPanel.SetDock(startButton, Dock.Right);
        row.Children.Add(startButton);

        nameBox = new TextBox
        {
            Text = "Momo",
            Height = 32,
            FontSize = 13,
            MaxLength = 16,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 0, 6, 0),
        };
        row.Children.Add(nameBox);

        footer.Children.Add(row);
        return footer;
    }
}
