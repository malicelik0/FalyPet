using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FalyPet.App.Rendering;
using FalyPet.Core.Content;
using FalyPet.Core.Model;
using FalyPet.Core.Persistence;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Ui;

/// <summary>
/// "Yakala" mini oyunu: yukarıdan düşen yemekleri pet'le topla.
///
/// CEZA YOK. Kaçırılan yemek puan düşürmüyor, oyun bitmiyor, pet üzülmüyor.
/// Uygulamanın geri kalanı da böyle (ölüm yok, ihmal yalnızca büyümeyi durduruyor);
/// mini oyunun aniden cezalandırıcı olması tonu bozardı.
///
/// Kazanılan coin günlük tavana takılıyor — bkz. SimulationRules.MaxGameCoinsPerDay.
/// </summary>
internal sealed class CatchGameWindow : Window
{
    private const double Width_ = 520;
    private const double Height_ = 380;
    private const double PetSize = 64;
    private const double ItemSize = 22;
    private const double FloorY = Height_ - 96;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    private readonly PetSimulation _sim;
    private readonly SaveStore _store;
    private readonly SaveData _save;
    private readonly Canvas _canvas = new();
    private readonly Image _pet;
    private readonly TextBlock _scoreLabel = Label(16, FontWeights.SemiBold);
    private readonly TextBlock _timeLabel = Label(14, FontWeights.Normal);
    private readonly Border _overlay;
    private readonly TextBlock _overlayText = Label(15, FontWeights.SemiBold);
    private readonly Button _actionButton;

    private readonly List<(Ellipse Shape, double X, double Y, double Speed, int Value)> _items = [];
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();

    private bool _playing;
    private int _score;
    private TimeSpan _remaining;
    private TimeSpan _sinceSpawn;
    private double _petX = Width_ / 2;
    private DateTime _lastFrame;

    public event EventHandler? CoinsChanged;

    public CatchGameWindow(PetSimulation sim, SaveStore store, SaveData save,
        SpeciesDefinition species, SpriteCache sprites)
    {
        _sim = sim;
        _store = store;
        _save = save;

        Title = "FalyPet — Yakala";
        Width = Width_;
        Height = Height_;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF2, 0xF7));

        _pet = new Image
        {
            Source = sprites.Get(species, PlayableStage(), PetAnimation.Play, 1, false,
                AccessoryCatalog.ById(save.Pet?.EquippedCostumeId)),
            Width = PetSize,
            Height = PetSize,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(_pet, BitmapScalingMode.HighQuality);

        BuildScene();
        _overlay = BuildOverlay(out _actionButton);
        _canvas.Children.Add(_overlay);

        Content = _canvas;

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();

        MouseMove += (_, e) => _petX = e.GetPosition(_canvas).X;
        KeyDown += OnKeyDown;
        Closed += (_, _) => _timer.Stop();

        ShowReady();
    }

    /// <summary>Yumurta oynayamaz; önizleme bebek üstünden yapılır.</summary>
    private GrowthStage PlayableStage() => _sim.Stage == GrowthStage.Egg ? GrowthStage.Baby : _sim.Stage;

    private static TextBlock Label(double size, FontWeight weight) => new()
    {
        FontSize = size,
        FontWeight = weight,
        Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
    };

    private void BuildScene()
    {
        var ground = new Rectangle
        {
            Width = Width_,
            Height = Height_ - FloorY - PetSize + 40,
            Fill = new SolidColorBrush(Color.FromRgb(0xBF, 0xE0, 0xC8)),
        };
        Canvas.SetTop(ground, FloorY + PetSize - 40);
        _canvas.Children.Add(ground);

        Canvas.SetLeft(_scoreLabel, 14);
        Canvas.SetTop(_scoreLabel, 10);
        _canvas.Children.Add(_scoreLabel);

        Canvas.SetLeft(_timeLabel, 14);
        Canvas.SetTop(_timeLabel, 32);
        _canvas.Children.Add(_timeLabel);

        Canvas.SetTop(_pet, FloorY);
        _canvas.Children.Add(_pet);
    }

    private Border BuildOverlay(out Button button)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(_overlayText);

        button = new Button { Content = "Başla", Width = 130, Height = 32, Margin = new Thickness(0, 12, 0, 0), FontSize = 13 };
        button.Click += (_, _) => StartRound();
        stack.Children.Add(button);

        var border = new Border
        {
            Width = 300,
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFB, 0xF2)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x30, 0x3C)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18),
            Child = stack,
        };

        Canvas.SetLeft(border, (Width_ - 300) / 2 - 2);
        Canvas.SetTop(border, 96);
        return border;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Fare zaten çalışıyor ama klavye de olmalı: bazı kullanıcılar oyunu
        // tek elle oynamak ister ve fare hassasiyeti herkeste aynı değil.
        var step = 26.0;
        if (e.Key == Key.Left) _petX -= step;
        else if (e.Key == Key.Right) _petX += step;
        else if (e.Key == Key.Space && !_playing) StartRound();
    }

    private void ShowReady()
    {
        var remaining = _sim.RemainingGameCoinsToday(DateTimeOffset.UtcNow);
        _overlayText.Text = remaining > 0
            ? $"Düşen yemekleri yakala!\nFare ya da ok tuşları · 30 saniye\n\nBugün kazanılabilecek: {remaining} coin"
            : "Düşen yemekleri yakala!\nFare ya da ok tuşları · 30 saniye\n\nBugünkü coin sınırın doldu —\nyine de oynayabilirsin.";
        _overlayText.TextAlignment = TextAlignment.Center;
        _actionButton.Content = "Başla";
        _overlay.Visibility = Visibility.Visible;

        _scoreLabel.Text = $"Skor: 0    En iyi: {_save.Pet?.CatchHighScore ?? 0}";
        _timeLabel.Text = "";
    }

    private void StartRound()
    {
        ClearItems();
        _score = 0;
        _remaining = Duration;
        _sinceSpawn = TimeSpan.Zero;
        _playing = true;
        _overlay.Visibility = Visibility.Collapsed;
        _lastFrame = DateTime.UtcNow;
        _timer.Start();
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;
        var delta = now - _lastFrame;
        _lastFrame = now;
        if (delta > TimeSpan.FromMilliseconds(200)) delta = TimeSpan.FromMilliseconds(200);

        MovePet();

        if (!_playing) return;

        _remaining -= delta;
        if (_remaining <= TimeSpan.Zero) { EndRound(); return; }

        SpawnIfDue(delta);
        MoveItems(delta);

        _scoreLabel.Text = $"Skor: {_score}    En iyi: {Math.Max(_score, _save.Pet?.CatchHighScore ?? 0)}";
        _timeLabel.Text = $"Süre: {_remaining.TotalSeconds:F0} sn";
    }

    private void MovePet()
    {
        _petX = Math.Clamp(_petX, PetSize / 2, Width_ - PetSize / 2);
        Canvas.SetLeft(_pet, _petX - PetSize / 2);
    }

    private void SpawnIfDue(TimeSpan delta)
    {
        _sinceSpawn += delta;

        // Oyun ilerledikçe hızlanıyor: sabit tempo 30 saniyeyi uzun hissettirir.
        var elapsed = (Duration - _remaining).TotalSeconds / Duration.TotalSeconds;
        var interval = TimeSpan.FromMilliseconds(750 - 300 * elapsed);
        if (_sinceSpawn < interval) return;

        _sinceSpawn = TimeSpan.Zero;

        // Altıda bir altın yemek: 3 puan. Küçük bir sürpriz, oyunu tekdüzelikten çıkarır.
        var golden = _random.Next(6) == 0;
        var shape = new Ellipse
        {
            Width = ItemSize,
            Height = ItemSize,
            Fill = new SolidColorBrush(golden ? Color.FromRgb(0xF2, 0xC1, 0x4E) : Color.FromRgb(0xE0, 0x70, 0x3C)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x3C, 0x30, 0x3C)),
            StrokeThickness = 1.5,
        };

        var x = ItemSize + _random.NextDouble() * (Width_ - ItemSize * 2);
        Canvas.SetLeft(shape, x - ItemSize / 2);
        Canvas.SetTop(shape, -ItemSize);
        _canvas.Children.Add(shape);

        _items.Add((shape, x, -ItemSize, 120 + _random.NextDouble() * 70 + 60 * elapsed, golden ? 3 : 1));
    }

    private void MoveItems(TimeSpan delta)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            var y = item.Y + item.Speed * delta.TotalSeconds;

            var caught = y + ItemSize >= FloorY + 10
                         && y <= FloorY + PetSize * 0.7
                         && Math.Abs(item.X - _petX) < PetSize * 0.55;

            if (caught)
            {
                _score += item.Value;
                Remove(i);
                continue;
            }

            if (y > Height_) { Remove(i); continue; }   // kaçırıldı — ceza yok

            Canvas.SetTop(item.Shape, y);
            _items[i] = item with { Y = y };
        }
    }

    private void Remove(int index)
    {
        _canvas.Children.Remove(_items[index].Shape);
        _items.RemoveAt(index);
    }

    private void ClearItems()
    {
        foreach (var item in _items) _canvas.Children.Remove(item.Shape);
        _items.Clear();
    }

    private void EndRound()
    {
        _playing = false;
        _timer.Stop();
        ClearItems();

        var pet = _save.Pet;
        var record = pet is not null && _score > pet.CatchHighScore;
        if (record) pet!.CatchHighScore = _score;

        var granted = _sim.AwardGameCoins(_score, DateTimeOffset.UtcNow);
        _store.Save(_save);
        CoinsChanged?.Invoke(this, EventArgs.Empty);

        var lines = $"Süre doldu!\nSkor: {_score}";
        if (record) lines += "\nYeni rekor!";

        lines += granted == _score
            ? $"\n\n+{granted} coin"
            : granted > 0
                ? $"\n\n+{granted} coin (günlük sınıra takıldı)"
                : "\n\nBugünkü coin sınırın dolu — puan yine de sayıldı.";

        _overlayText.Text = lines;
        _overlayText.TextAlignment = TextAlignment.Center;
        _actionButton.Content = "Tekrar oyna";
        _overlay.Visibility = Visibility.Visible;

        _scoreLabel.Text = $"Skor: {_score}    En iyi: {pet?.CatchHighScore ?? _score}";
        _timeLabel.Text = "";
    }
}
