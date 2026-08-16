using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FalyPet.App.Behavior;
using FalyPet.App.Interop;
using FalyPet.App.Rendering;
using FalyPet.App.Services;
using FalyPet.Core.Content;
using FalyPet.Core.Model;
using FalyPet.Core.Persistence;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Ui;

/// <summary>
/// Pet'in yaşadığı saydam, her zaman üstte duran pencere.
/// Simülasyonu tikler, davranışı çizer, kullanıcı girdisini bakım eylemlerine çevirir.
/// </summary>
public partial class PetWindow : Window
{
    private const double MinVisible = 48.0;
    private const double DragThreshold = 3.0;

    /// <summary>Tek zamanlayıcı: ~30 fps. Hareket, animasyon ve tıkla-geç yoklaması aynı tikte.</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);

    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(30);

    private readonly SaveStore _store;
    private readonly SaveData _save;
    private readonly SpriteCache _sprites;
    private readonly SoundService _sound;
    private readonly SpeciesDefinition _species;
    private readonly PetSimulation _sim;
    private readonly PetBehavior _behavior;
    private readonly BubbleWindow _bubble = new();
    private readonly DispatcherTimer _timer;

    private IntPtr _hwnd;
    private bool _clickThrough;
    private DateTime _lastTickWall = DateTime.UtcNow;
    private TimeSpan _sinceSave;

    private AlphaMask _mask;
    private (GrowthStage Stage, PetAnimation Anim, int Frame, bool Left, string Costume, int Gaze) _renderedKey
        = (GrowthStage.Egg, PetAnimation.Idle, -1, false, "", 0);

    /// <summary>
    /// Pet'in imlece bakıp bakmadığı. Fare konumu tıkla-geç için zaten her tikte
    /// okunuyor, yani bu bedava geliyor — ve bir masaüstü pet'ini "canlı"
    /// hissettiren en ucuz detay bu.
    /// </summary>
    private int _gaze;

    /// <summary>İmleç bu uzaklığın ötesindeyse pet ilgilenmez (ör. öteki monitörde).</summary>
    private const double GazeRange = 600.0;

    /// <summary>Bu kadar yakın yatay farkta gözler düz bakar; yoksa bebek titrer durur.</summary>
    private const double GazeDeadZone = 24.0;
    private GrowthStage _lastStage;

    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStartCursor;
    private double _dragStartLeft;
    private double _dragStartTop;

    private bool _hiddenByUser;
    private bool _hiddenByFullscreen;

    // internal: SpriteCache internal olduğu için ctor da olmak zorunda.
    // XAML'den üretilen sınıfın kendisi public kalıyor.
    internal PetWindow(SaveStore store, SaveData save, SpriteCache sprites, SoundService sound)
    {
        _store = store;
        _save = save;
        _sprites = sprites;
        _sound = sound;

        InitializeComponent();

        _save.Pet ??= PetSimulation.CreateNew(SpeciesCatalog.All[0].Id, "Momo", DateTimeOffset.UtcNow);
        _species = SpeciesCatalog.ById(_save.Pet.SpeciesId);
        _sim = new PetSimulation(_save.Pet);
        _lastStage = _sim.Stage;

        Width = PetSpriteFactory.Size * CurrentScale;
        Height = PetSpriteFactory.Size * CurrentScale;

        _hiddenByUser = save.Window.Hidden;
        _behavior = new PetBehavior(save.Window.X ?? 0);
        _mask = _sprites.GetMask(_species, _sim.Stage, PetAnimation.Idle, 0, false);

        ContextMenu = BuildMenu();

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TickInterval };
        _timer.Tick += (_, _) => Tick();

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public bool IsPetVisible => !_hiddenByUser && !_hiddenByFullscreen;

    // ---------------------------------------------------------------- boyut

    private int CurrentScale => Math.Clamp(_save.PetScale, SaveData.MinPetScale, SaveData.MaxPetScale);

    /// <summary>Ölçek kademelerinin kullanıcıya gösterilen adları ve piksel karşılıkları.</summary>
    public static IReadOnlyList<(int Scale, string Label)> ScaleOptions { get; } =
    [
        (3, "Çok küçük"),
        (4, "Küçük"),
        (5, "Normal"),
        (6, "Büyük"),
        (7, "Çok büyük"),
        (8, "Devasa"),
    ];

    /// <summary>
    /// Pet'in boyutunu değiştirir.
    ///
    /// Ayaklar yerinde kalacak şekilde konumlanıyor: pencere büyürken sol-üst
    /// köşe sabit tutulsaydı pet yukarı doğru sıçrar, küçülürken havada asılı
    /// kalırdı. Alt kenar ve yatay merkez sabit tutuluyor.
    /// </summary>
    public void SetScale(int scale)
    {
        scale = Math.Clamp(scale, SaveData.MinPetScale, SaveData.MaxPetScale);
        if (scale == CurrentScale) return;

        var altKenar = Top + Height;
        var merkezX = Left + Width / 2;

        _save.PetScale = scale;
        Width = PetSpriteFactory.Size * scale;
        Height = PetSpriteFactory.Size * scale;

        Left = merkezX - Width / 2;
        Top = altKenar - Height;

        ClampToVisibleArea();
        _behavior.OnDragged(Left);
        SaveNow();
    }

    // ---------------------------------------------------------------- kurulum

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestorePosition();
        _behavior.OnDragged(Left);

        // Kapalı geçen süreyi tek seferde uygula — açılışta pet'in durumu gerçekçi olsun.
        _sim.Advance(DateTimeOffset.UtcNow);
        _lastStage = _sim.Stage;

        ApplyVisibility();
        UpdateSprite();
        _timer.Start();

        ShowWelcomeBubble();
    }

    private void ShowWelcomeBubble()
    {
        if (!IsPetVisible) return;

        var message = _sim.Stage == GrowthStage.Egg
            ? $"{_save.Pet!.Name} yumurtada.\nTıklayarak çıkar ya da 5 dakika bekle."
            : _sim.GrowthStallReason ?? $"{_save.Pet!.Name} seni özlemiş.";

        Say(message, TimeSpan.FromSeconds(5));
    }

    // ---------------------------------------------------------------- ana döngü

    private void Tick()
    {
        var wallNow = DateTime.UtcNow;
        var delta = wallNow - _lastTickWall;
        _lastTickWall = wallNow;

        // Bilgisayar uykudan döndüyse delta çok büyük olabilir; davranış için kırp.
        // Simülasyonun kendi 8 saatlik tavanı var, o ayrı bir mesele.
        if (delta > TimeSpan.FromSeconds(1)) delta = TimeSpan.FromSeconds(1);

        _sim.Advance(DateTimeOffset.UtcNow);
        NotifyIfGrown();

        // Uyarı denetimi görünürlük kontrolünden ÖNCE: pet gizliyken de haber
        // vermeli, hatta asıl o zaman vermeli — kullanıcı onu göremiyor.
        if (_alertGraceRemaining > TimeSpan.Zero)
        {
            _alertGraceRemaining -= delta;
        }
        else
        {
            _sinceAlertCheck += delta;
            if (_sinceAlertCheck >= AlertCheckInterval)
            {
                _sinceAlertCheck = TimeSpan.Zero;
                CheckNeedAlerts();
            }
        }

        if (!IsPetVisible) { SetClickThrough(true); return; }

        // "Fare üstünde mi" tek yerde hesaplanıyor: gezinme, tıkla-geç, solma ve
        // ihtiyaç göstergesi hepsi aynı cevabı kullanmalı. Ayrı ayrı sorulsalardı
        // biri "üstünde" derken öbürü "değil" diyebilirdi.
        var over = !_dragging && IsCursorOverPet();

        var (minX, maxX) = HorizontalBounds();
        if (!_dragging) _behavior.Update(delta, _sim, minX, maxX, over);

        if (!_dragging && Math.Abs(Left - _behavior.X) > 0.5) Left = _behavior.X;

        UpdateGaze();
        UpdateSprite();
        UpdateClickThrough(over);
        UpdateNeedsOverlay(over);
        ApplyFade(delta);

        _sinceSave += delta;
        if (_sinceSave >= AutoSaveInterval)
        {
            _sinceSave = TimeSpan.Zero;
            SaveNow();
        }

        WriteDiagnostics(delta);
    }

    // ---------------------------------------------------------------- teşhis

    /// <summary>
    /// <c>FALYPET_DIAG=&lt;dosya&gt;</c> ortam değişkeni ayarlıysa pencere durumunu
    /// saniyede bir dosyaya yazar.
    ///
    /// "Pet kayboluyor" / "üstüne gelince solmuyor" gibi şikayetler kodu okuyarak
    /// çözülemiyor: sorun görünürlük, en-üstte olma, tıkla-geç ve saydamlık
    /// arasındaki etkileşimde ve hepsi aynı anda oynuyor. Bu kayıt hangisinin
    /// bozulduğunu ölçerek gösteriyor.
    /// </summary>
    private static readonly string? DiagPath = Environment.GetEnvironmentVariable("FALYPET_DIAG");
    private TimeSpan _sinceDiag;

    private void WriteDiagnostics(TimeSpan delta)
    {
        if (DiagPath is null) return;

        _sinceDiag += delta;
        if (_sinceDiag < TimeSpan.FromSeconds(1)) return;
        _sinceDiag = TimeSpan.Zero;

        try
        {
            NativeMethods.GetCursorPos(out var native);
            var c = ScreenToDip(native);
            var topmostFlag = _hwnd != IntPtr.Zero
                && (NativeMethods.GetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE) & 0x8) != 0;

            var satir = string.Join("  ",
                DateTime.Now.ToString("HH:mm:ss"),
                $"pencere=({Left:F0},{Top:F0}) {Width:F0}x{Height:F0}",
                $"imlec=({c.X:F0},{c.Y:F0})",
                $"uzerinde={IsCursorOverPet()}",
                $"hedefOpaklik={_targetOpacity:F2}",
                $"gercekOpaklik={Opacity:F2}",
                $"gorunur={IsPetVisible}",
                $"kullaniciGizledi={_hiddenByUser}",
                $"tamEkranGizledi={_hiddenByFullscreen}",
                $"enUstte={topmostFlag}",
                $"tiklaGec={_clickThrough}",
                $"anim={_behavior.Animation}");

            System.IO.File.AppendAllText(DiagPath, satir + Environment.NewLine);
        }
        catch (System.IO.IOException)
        {
            // Teşhis yazımı asla uygulamayı durdurmamalı.
        }
    }

    /// <summary>İhtiyaç uyarılarına bakma sıklığı. Kararın kendisi zaten kendi
    /// bekleme süresini uyguluyor; burada sadece boşuna sorgulamamak için.</summary>
    private static readonly TimeSpan AlertCheckInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Açılıştan sonra uyarı için beklenen süre. Karşılama balonu zaten durumu
    /// söylüyor ("Pamuk çok aç — ilgilenmen lazım"); hemen ardından ikinci bir
    /// balonun aynı şeyi tekrarlaması gürültü olur.
    /// </summary>
    private static readonly TimeSpan StartupAlertGrace = TimeSpan.FromSeconds(45);

    private readonly NeedAlertTracker _alerts = new();
    private TimeSpan _sinceAlertCheck;
    private TimeSpan _alertGraceRemaining = StartupAlertGrace;

    /// <summary>Pet gizliyken uyarı tepsiden gösterilmeli — bunu App bağlıyor.</summary>
    public event EventHandler<string>? TrayNotificationRequested;

    private void CheckNeedAlerts()
    {
        if (_alerts.Poll(_sim, DateTimeOffset.UtcNow) is not { } alert) return;

        // Görünüyorsa pet'in kendi ağzından söylesin; gizliyse tepsiden.
        // Gizli pet için balon göstermek görünmez bir uyarı olurdu.
        if (IsPetVisible) Say(alert.Message, TimeSpan.FromSeconds(6));
        else TrayNotificationRequested?.Invoke(this, alert.Message);
    }

    private void NotifyIfGrown()
    {
        if (_sim.Stage == _lastStage) return;

        var previous = _lastStage;
        _lastStage = _sim.Stage;

        var message = previous == GrowthStage.Egg
            ? $"{_save.Pet!.Name} yumurtadan çıktı!"
            : $"{_save.Pet!.Name} büyüdü — artık {StageLabel(_sim.Stage)}!";

        Say(message, TimeSpan.FromSeconds(6));
        SaveNow();
    }

    private void UpdateGaze()
    {
        // Uyuyan, küskün ya da hasta pet takip etmez — gözleri zaten kapalı/yok.
        if (_behavior.Animation is PetAnimation.Sleep or PetAnimation.Sulk or PetAnimation.Sick)
        {
            _gaze = 0;
            return;
        }

        if (!NativeMethods.GetCursorPos(out var native)) { _gaze = 0; return; }

        var cursor = ScreenToDip(native);
        var centerX = Left + Width / 2;
        var centerY = Top + Height / 2;

        var dx = cursor.X - centerX;
        var dy = cursor.Y - centerY;

        if (Math.Abs(dx) > GazeRange || Math.Abs(dy) > GazeRange || Math.Abs(dx) < GazeDeadZone)
        {
            _gaze = 0;
            return;
        }

        // Sprite hep sağa bakar çizilip sola aynalanıyor; aynalıyken ekrandaki
        // yön sprite uzayında tersine döner.
        var worldGaze = Math.Sign(dx);
        _gaze = _behavior.FaceLeft ? -worldGaze : worldGaze;
    }

    private void UpdateSprite()
    {
        // Yumurtada "kare" animasyon karesi değil çatlak sayısıdır.
        var frame = _sim.Stage == GrowthStage.Egg ? _save.Pet!.EggCracks : _behavior.Frame;
        var costumeId = _save.Pet!.EquippedCostumeId ?? "";
        var key = (_sim.Stage, _behavior.Animation, frame, _behavior.FaceLeft, costumeId, _gaze);
        if (key == _renderedKey) return;

        _renderedKey = key;
        var accessory = AccessoryCatalog.ById(_save.Pet.EquippedCostumeId);
        SpriteImage.Source = _sprites.Get(_species, key.Item1, key.Item2, key.Item3, key.Item4, accessory, key.Item6);
        _mask = _sprites.GetMask(_species, key.Item1, key.Item2, key.Item3, key.Item4, accessory);
    }

    // ---------------------------------------------------------------- konum

    private (double MinX, double MaxX) HorizontalBounds()
    {
        var work = NearestWorkAreaDip();
        return (work.Left, Math.Max(work.Left, work.Right - Width));
    }

    private void RestorePosition()
    {
        var saved = _save.Window;

        if (saved.X is not { } x || saved.Y is not { } y || !double.IsFinite(x) || !double.IsFinite(y))
        {
            var work = SystemParameters.WorkArea;
            Left = work.Right - Width - 48;
            Top = work.Bottom - Height;
            return;
        }

        Left = x;
        Top = y;
        ClampToVisibleArea();
    }

    private void ClampToVisibleArea()
    {
        var work = NearestWorkAreaDip();
        Left = Math.Clamp(Left, work.Left - Width + MinVisible, work.Right - MinVisible);
        Top = Math.Clamp(Top, work.Top, work.Bottom - MinVisible);
    }

    /// <summary>
    /// Pencereye en yakın gerçek monitörün çalışma alanı (DIP).
    /// Sanal masaüstünün sınırlayıcı kutusu KULLANILMAZ: farklı boyutlu iki monitörde
    /// kutunun köşeleri hiçbir ekrana denk gelmeyen ölü bölgelerdir ve pet oraya
    /// konunca kullanıcı onu bir daha bulamaz.
    /// </summary>
    private Rect NearestWorkAreaDip()
    {
        var target = PresentationSource.FromVisual(this)?.CompositionTarget;
        var toDevice = target?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var fromDevice = target?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

        var tl = toDevice.Transform(new Point(Left, Top));
        var br = toDevice.Transform(new Point(Left + Width, Top + Height));

        var deviceRect = new System.Drawing.Rectangle(
            (int)Math.Round(tl.X), (int)Math.Round(tl.Y),
            Math.Max(1, (int)Math.Round(br.X - tl.X)),
            Math.Max(1, (int)Math.Round(br.Y - tl.Y)));

        var wa = System.Windows.Forms.Screen.FromRectangle(deviceRect).WorkingArea;
        return new Rect(fromDevice.Transform(new Point(wa.Left, wa.Top)),
                        fromDevice.Transform(new Point(wa.Right, wa.Bottom)));
    }

    private void SaveNow()
    {
        if (double.IsFinite(Left) && double.IsFinite(Top))
        {
            _save.Window.X = Left;
            _save.Window.Y = Top;
        }

        _save.Window.Hidden = _hiddenByUser;
        _store.Save(_save);
    }

    // ---------------------------------------------------------------- tıkla-geç

    /// <summary>Fare pet'in üstündeyken pencerenin saydamlığı. Altındakini görebilmek için.</summary>
    private const double HoverOpacity = 0.40;

    /// <summary>Solma/geri gelme süresi (ms). Anî geçiş rahatsız edici oluyor.</summary>
    private const double FadeMs = 130.0;

    private double _targetOpacity = 1.0;

    private void UpdateClickThrough(bool over)
    {
        // Solma yalnızca GÖRÜNÜRLÜĞÜ değiştiriyor, tıklanabilirliği değil:
        // pet soluk haldeyken de beslenebilir, sürüklenebilir. Amaç altındaki
        // pencereyi görebilmek, pet'i devre dışı bırakmak değil.
        if (_dragging)
        {
            SetClickThrough(false);
            _targetOpacity = HoverOpacity;
            return;
        }

        SetClickThrough(!over);
        _targetOpacity = over ? HoverOpacity : 1.0;
    }

    private readonly NeedsOverlay _needsOverlay = new();

    /// <summary>
    /// Fare üstündeyken ihtiyaç çubuklarını gösterir. Yumurtada gösterilmiyor:
    /// yumurtanın ihtiyacı yok, boş çubuklar göstermek yanıltıcı olurdu.
    /// </summary>
    private void UpdateNeedsOverlay(bool over)
    {
        if (!over || _sim.Stage == GrowthStage.Egg || !IsPetVisible)
        {
            if (_needsOverlay.IsVisible) _needsOverlay.Hide();
            return;
        }

        _needsOverlay.UpdateValues(_sim);
        _needsOverlay.ShowAbove(PetRect);
    }

    private void ApplyFade(TimeSpan delta)
    {
        var fark = _targetOpacity - Opacity;
        if (Math.Abs(fark) < 0.01) { Opacity = _targetOpacity; return; }

        var adim = delta.TotalMilliseconds / FadeMs;
        Opacity += Math.Sign(fark) * Math.Min(adim, Math.Abs(fark));
    }

    private bool IsCursorOverPet()
    {
        if (!NativeMethods.GetCursorPos(out var native)) return false;

        var cursor = ScreenToDip(native);
        var relX = cursor.X - Left;
        var relY = cursor.Y - Top;
        if (relX < 0 || relY < 0 || relX >= Width || relY >= Height) return false;

        return _mask.IsOpaqueAt((int)(relX / Width * _mask.Width), (int)(relY / Height * _mask.Height));
    }

    private void SetClickThrough(bool value)
    {
        if (_hwnd == IntPtr.Zero || value == _clickThrough) return;

        var exStyle = NativeMethods.GetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE,
            value ? exStyle | NativeMethods.WS_EX_TRANSPARENT : exStyle & ~NativeMethods.WS_EX_TRANSPARENT);
        _clickThrough = value;
    }

    private Point ScreenToDip(NativeMethods.POINT point)
    {
        var source = PresentationSource.FromVisual(this);
        var raw = new Point(point.X, point.Y);
        return source?.CompositionTarget is null ? raw : source.CompositionTarget.TransformFromDevice.Transform(raw);
    }

    // ---------------------------------------------------------------- sürükleme

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!NativeMethods.GetCursorPos(out var native)) return;

        _dragging = true;
        _dragMoved = false;
        _dragStartCursor = ScreenToDip(native);
        _dragStartLeft = Left;
        _dragStartTop = Top;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        if (!NativeMethods.GetCursorPos(out var native)) return;

        var cursor = ScreenToDip(native);
        var dx = cursor.X - _dragStartCursor.X;
        var dy = cursor.Y - _dragStartCursor.Y;

        if (!_dragMoved && Math.Abs(dx) + Math.Abs(dy) > DragThreshold) _dragMoved = true;
        if (!_dragMoved) return;

        Left = _dragStartLeft + dx;
        Top = _dragStartTop + dy;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;

        _dragging = false;
        ReleaseMouseCapture();

        if (_dragMoved)
        {
            ClampToVisibleArea();
            _behavior.OnDragged(Left);
            SaveNow();
            return;
        }

        PlayPokeAnimation();
        Care(CareAction.Pet);
    }

    private void PlayPokeAnimation()
    {
        var y = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        y.KeyFrames.Add(new EasingDoubleKeyFrame(0.84, KeyTime.FromPercent(0.30)));
        y.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromPercent(0.65)));
        y.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.0)));

        var x = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        x.KeyFrames.Add(new EasingDoubleKeyFrame(1.14, KeyTime.FromPercent(0.30)));
        x.KeyFrames.Add(new EasingDoubleKeyFrame(0.96, KeyTime.FromPercent(0.65)));
        x.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.0)));

        SpriteScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, y);
        SpriteScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, x);
    }

    // ---------------------------------------------------------------- bakım

    private void Care(CareAction action)
    {
        var wasEgg = _sim.Stage == GrowthStage.Egg;
        var result = _sim.Apply(action, DateTimeOffset.UtcNow);

        _sound.Play(SoundFor(action, result.Accepted, wasEgg));

        if (result.Accepted && ActionAnimation(action) is { } anim)
            _behavior.PlayAction(anim, TimeSpan.FromSeconds(1.6));

        var message = !string.IsNullOrEmpty(result.Reason) ? result.Reason
            : result.CarePointsGained > 0 ? $"+{result.CarePointsGained} bakım"
            : null;

        if (message is not null) Say(message, TimeSpan.FromSeconds(2.6));

        UpdateMenuState();
        SaveNow();
    }

    /// <summary>
    /// Reddedilen eylem de ses çıkarır — sessizce hiçbir şey olmaması kullanıcıya
    /// "tıklama işlemedi mi" dedirtir. Kısa ve alçak bir "olmaz" sesi cevabı veriyor.
    /// </summary>
    private static SoundEffect SoundFor(CareAction action, bool accepted, bool wasEgg)
    {
        if (!accepted) return SoundEffect.Refuse;
        if (wasEgg) return SoundEffect.Crack;

        return action switch
        {
            CareAction.Feed => SoundEffect.Eat,
            CareAction.Water => SoundEffect.Drink,
            CareAction.Play => SoundEffect.Play,
            CareAction.Wash => SoundEffect.Wash,
            CareAction.Sleep => SoundEffect.Sleep,
            _ => SoundEffect.Poke,
        };
    }

    private static PetAnimation? ActionAnimation(CareAction action) => action switch
    {
        CareAction.Feed => PetAnimation.Eat,
        CareAction.Water => PetAnimation.Drink,
        CareAction.Play => PetAnimation.Play,
        CareAction.Wash => PetAnimation.Wash,
        _ => null,
    };

    private Rect PetRect => new(Left, Top, Width, Height);

    private void Say(string message, TimeSpan duration) =>
        _bubble.Say(message, PetRect, duration);

    // ---------------------------------------------------------------- menü

    private MenuItem _feedItem = null!, _waterItem = null!, _playItem = null!, _washItem = null!, _sleepItem = null!;

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        _feedItem = AddItem(menu, "Besle", () => Care(CareAction.Feed));
        _waterItem = AddItem(menu, "Su ver", () => Care(CareAction.Water));
        _playItem = AddItem(menu, "Oyna", () => Care(CareAction.Play));
        _washItem = AddItem(menu, "Yıka", () => Care(CareAction.Wash));
        _sleepItem = AddItem(menu, "Uyut", () => Care(CareAction.Sleep));

        menu.Items.Add(new Separator());
        AddItem(menu, "Durum", ShowStatus);
        AddItem(menu, "Yakala oyunu", OpenGame);
        AddItem(menu, "Dükkan", OpenShop);
        menu.Items.Add(BuildScaleMenu());

        menu.Items.Add(new Separator());
        AddItem(menu, "Gizle", ToggleUserVisibility);

        menu.Opened += (_, _) => UpdateMenuState();
        return menu;
    }

    private readonly List<MenuItem> _scaleItems = [];

    /// <summary>
    /// Boyut alt menüsü. Ayarlar penceresinde de var ama asıl yeri burası:
    /// kullanıcı pet'i büyütmek isterken önce pet'e sağ tıklar, ayarları açmayı
    /// düşünmez.
    /// </summary>
    private MenuItem BuildScaleMenu()
    {
        var root = new MenuItem { Header = "Boyut" };

        foreach (var (scale, label) in ScaleOptions)
        {
            var px = PetSpriteFactory.Size * scale;
            var item = new MenuItem { Header = $"{label}  ({px}px)", IsCheckable = true, Tag = scale };
            item.Click += (_, _) => SetScale(scale);
            root.Items.Add(item);
            _scaleItems.Add(item);
        }

        return root;
    }

    private static MenuItem AddItem(ContextMenu menu, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
        return item;
    }

    private void UpdateMenuState()
    {
        // Yumurtayken hiçbir bakım eylemi anlamlı değil; menü bunu göstermeli
        // ki kullanıcı tıklayıp "neden olmuyor" diye düşünmesin.
        var alive = _sim.Stage != GrowthStage.Egg;
        var awake = alive && !_sim.IsSleeping;

        _feedItem.IsEnabled = awake;
        _waterItem.IsEnabled = awake;
        _playItem.IsEnabled = awake && _sim.Mood != PetMood.Sulking;
        _washItem.IsEnabled = awake;
        _sleepItem.IsEnabled = alive;
        _sleepItem.Header = _sim.IsSleeping ? "Uyandır" : "Uyut";

        // Tik'i menü her açıldığında gerçek duruma göre tazele: boyut ayarlar
        // penceresinden de değiştirilebiliyor, iki yerin ayrışmaması gerekiyor.
        foreach (var item in _scaleItems) item.IsChecked = (int)item.Tag! == CurrentScale;
    }

    private CatchGameWindow? _game;

    private void OpenGame()
    {
        if (_game is { IsVisible: true }) { _game.Activate(); return; }

        _game = new CatchGameWindow(_sim, _store, _save, _species, _sprites);
        _game.Closed += (_, _) => _game = null;
        _game.Show();
    }

    private ShopWindow? _shop;

    private void OpenShop()
    {
        // Zaten açıksa öne getir — iki dükkan penceresi aynı kayda yazardı.
        if (_shop is { IsVisible: true }) { _shop.Activate(); return; }

        _shop = new ShopWindow(_sim, _store, _save, _species, _sprites);
        _shop.CostumeChanged += (_, _) =>
        {
            // Anahtarı geçersiz kıl ki bir sonraki tikte sprite yeniden yüklensin.
            _renderedKey = (_renderedKey.Stage, _renderedKey.Anim, -1, _renderedKey.Left, "", _renderedKey.Gaze);
        };
        _shop.Closed += (_, _) => _shop = null;
        _shop.Show();
    }

    private void ShowStatus()
    {
        if (_sim.Stage == GrowthStage.Egg)
        {
            var kalanOksama = SimulationRules.EggCracksRequired - _save.Pet!.EggCracks;
            var kalanSure = _sim.EggTimeRemaining(DateTimeOffset.UtcNow) ?? TimeSpan.Zero;

            // İki koşul da gösteriliyor çünkü VEYA ilişkisi var: kullanıcı ister
            // tıklayıp hızlandırsın ister beklesin, ikisinin de nerede olduğunu görsün.
            Say($"{_save.Pet.Name} yumurtada.\n{kalanOksama} okşama kaldı\nya da {kalanSure.TotalMinutes:F0} dk {kalanSure.Seconds} sn bekle",
                TimeSpan.FromSeconds(6));
            return;
        }

        var n = _sim.Needs;
        var lines =
            $"{_save.Pet!.Name} · {StageLabel(_sim.Stage)}\n" +
            $"Açlık {n.Hunger:F0}  Su {n.Thirst:F0}  Enerji {n.Energy:F0}\n" +
            $"Mutluluk {n.Happiness:F0}  Temizlik {n.Cleanliness:F0}\n" +
            $"Büyüme %{_sim.GrowthProgress * 100:F0}  ·  {_sim.Coins} coin" +
            (_sim.GrowthStallReason is { } stall ? $"\n{stall}" : "");

        Say(lines, TimeSpan.FromSeconds(7));
    }

    private static string StageLabel(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => "yumurta",
        GrowthStage.Baby => "bebek",
        GrowthStage.Child => "çocuk",
        GrowthStage.Teen => "genç",
        _ => "yetişkin",
    };

    // ---------------------------------------------------------------- görünürlük

    public void ToggleUserVisibility()
    {
        _hiddenByUser = !_hiddenByUser;
        ApplyVisibility();
        SaveNow();
    }

    public void SetHiddenByFullscreen(bool hidden)
    {
        if (_hiddenByFullscreen == hidden) return;
        _hiddenByFullscreen = hidden;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (IsPetVisible) { Show(); Topmost = true; }
        else { _bubble.HideNow(); _needsOverlay.Hide(); Hide(); }
    }

    public void SaveOnExit()
    {
        _timer.Stop();
        _bubble.HideNow();
        _needsOverlay.Close();
        _sim.Advance(DateTimeOffset.UtcNow);
        SaveNow();
    }
}
