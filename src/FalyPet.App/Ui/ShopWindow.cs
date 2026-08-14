using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.App.Rendering;
using FalyPet.Core.Content;
using FalyPet.Core.Model;
using FalyPet.Core.Persistence;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Ui;

/// <summary>
/// Coin harcanan dükkan. Yalnızca kalıcı aksesuar satar.
///
/// Her kart pet'in KENDİ türüyle o aksesuarı takmış hâlini gösteriyor — jenerik
/// bir eşya ikonu değil. Kullanıcı satın almadan önce sonucu görmeli.
/// </summary>
internal sealed class ShopWindow : Window
{
    private readonly PetSimulation _sim;
    private readonly PetSave _pet;
    private readonly SpeciesDefinition _species;
    private readonly SpriteCache _sprites;
    private readonly SaveStore _store;
    private readonly SaveData _save;

    private readonly TextBlock _coinLabel;
    private readonly WrapPanel _grid;

    public event EventHandler? CostumeChanged;

    public ShopWindow(PetSimulation sim, SaveStore store, SaveData save, SpeciesDefinition species, SpriteCache sprites)
    {
        _sim = sim;
        _store = store;
        _save = save;
        _pet = save.Pet!;
        _species = species;
        _sprites = sprites;

        Title = "FalyPet — Dükkan";
        Width = 520;
        Height = 430;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF4, 0xEC));
        FontFamily = new FontFamily("Segoe UI");

        _coinLabel = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
        };

        _grid = new WrapPanel();

        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(_coinLabel, Dock.Top);
        root.Children.Add(_coinLabel);
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _grid,
        });

        Content = root;
        Refresh();
    }

    private void Refresh()
    {
        _coinLabel.Text = $"Coin: {_sim.Coins}   ·   Bakım yaptıkça kazanırsın";

        _grid.Children.Clear();
        foreach (var item in AccessoryCatalog.All)
            _grid.Children.Add(BuildCard(item));
    }

    private Border BuildCard(AccessoryDefinition item)
    {
        var owned = _pet.OwnedItems.Contains(item.Id);
        var equipped = _pet.EquippedCostumeId == item.Id;

        var image = new Image
        {
            // Önizleme pet'in gerçek türü ve aşamasıyla üretiliyor.
            Source = _sprites.Get(_species, PreviewStage(), PetAnimation.Idle, 0, false, item),
            Width = 72,
            Height = 72,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(image);
        stack.Children.Add(new TextBlock
        {
            Text = item.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
        });
        stack.Children.Add(new TextBlock
        {
            Text = owned ? "sahipsin" : $"{item.Price} coin",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = new SolidColorBrush(owned
                ? Color.FromRgb(0x4A, 0x8A, 0x5A)
                : _sim.Coins >= item.Price ? Color.FromRgb(0x2A, 0x22, 0x33) : Color.FromRgb(0xB0, 0x50, 0x50)),
        });

        var button = new Button
        {
            Content = owned ? (equipped ? "Çıkar" : "Tak") : "Satın al",
            Width = 92,
            Height = 26,
            FontSize = 12,
            IsEnabled = owned || _sim.Coins >= item.Price,
        };
        button.Click += (_, _) => OnCardClicked(item, owned, equipped);
        stack.Children.Add(button);

        return new Border
        {
            Width = 112,
            Margin = new Thickness(5),
            Padding = new Thickness(6),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(equipped
                ? Color.FromRgb(0x4A, 0x8A, 0x5A)
                : Color.FromRgb(0xDD, 0xD6, 0xC8)),
            BorderThickness = new Thickness(equipped ? 2.5 : 1.5),
            CornerRadius = new CornerRadius(8),
            Child = stack,
        };
    }

    /// <summary>Yumurtadayken önizleme bebek üstünde gösterilir — yumurtaya şapka takılamaz.</summary>
    private GrowthStage PreviewStage() =>
        _sim.Stage == GrowthStage.Egg ? GrowthStage.Baby : _sim.Stage;

    private void OnCardClicked(AccessoryDefinition item, bool owned, bool equipped)
    {
        if (!owned)
        {
            // Satın alma ile envantere ekleme aynı anda olmalı: TrySpendCoins false
            // dönerse hiçbir şey değişmez, true dönerse para zaten düşmüştür.
            if (!_sim.TrySpendCoins(item.Price)) return;
            _pet.OwnedItems.Add(item.Id);
            _pet.EquippedCostumeId = item.Id;
        }
        else
        {
            _pet.EquippedCostumeId = equipped ? null : item.Id;
        }

        _store.Save(_save);
        Refresh();
        CostumeChanged?.Invoke(this, EventArgs.Empty);
    }
}
