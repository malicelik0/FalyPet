using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FalyPet.App.Interop;
using FalyPet.App.Rendering;
using FalyPet.Core.Persistence;

namespace FalyPet.App.Ui;

/// <summary>
/// Pet'in yaşadığı saydam, her zaman üstte duran pencere.
///
/// Faz 0'da üç mekanizmayı taşır:
///   1. Saydamlık + always-on-top + görev çubuğunda ve Alt+Tab'da görünmeme
///   2. Alfaya dayalı tıkla-geç: pet'in etrafındaki boşluk çalışmayı engellemez
///   3. Sürükleme ve konumun kalıcı olması
/// </summary>
public partial class PetWindow : Window
{
    /// <summary>Sprite'ın kaç kat büyütüleceği. 32 * 5 = 160 — tam sayı kat şart, yoksa pikseller eşit olmaz.</summary>
    private const int SpriteScaleFactor = 5;

    /// <summary>Fareyi bu sıklıkta yokluyoruz. Pencere tıkla-geç haldeyken fare olayı almaz, tek yol budur.</summary>
    private static readonly TimeSpan HitTestInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>Bu kadar DIP'ten fazla oynadıysa tıklama değil sürükleme sayılır.</summary>
    private const double DragThreshold = 3.0;

    private readonly SaveStore _store;
    private readonly SaveData _save;
    private readonly DispatcherTimer _hitTestTimer;
    private readonly AlphaMask _mask;

    private IntPtr _hwnd;
    private bool _clickThrough;

    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStartCursor;
    private double _dragStartLeft;
    private double _dragStartTop;

    /// <summary>Kullanıcı tepsiden gizledi mi?</summary>
    private bool _hiddenByUser;

    /// <summary>Tam ekran oyun yüzünden mi gizli? Bu ikisi ayrı tutulur — oyundan çıkınca
    /// pet geri gelmeli, ama kullanıcı elle gizlediyse gelmemeli.</summary>
    private bool _hiddenByFullscreen;

    public PetWindow(SaveStore store, SaveData save)
    {
        _store = store;
        _save = save;

        InitializeComponent();

        var sprite = PlaceholderSprite.CreateEgg();
        SpriteImage.Source = sprite;
        _mask = AlphaMask.FromBitmap(sprite);

        Width = PlaceholderSprite.Size * SpriteScaleFactor;
        Height = PlaceholderSprite.Size * SpriteScaleFactor;

        _hiddenByUser = save.Window.Hidden;

        _hitTestTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = HitTestInterval };
        _hitTestTimer.Tick += (_, _) => UpdateClickThrough();

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    /// <summary>Pet'e (boşluğuna değil) tıklandığında tetiklenir. Faz 3'te "okşama" buraya bağlanacak.</summary>
    public event EventHandler? Petted;

    public bool IsPetVisible => !_hiddenByUser && !_hiddenByFullscreen;

    // ---------------------------------------------------------------- kurulum

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;

        // Alt+Tab listesinden çıkar. ShowInTaskbar=False görev çubuğunu halleder ama
        // Alt+Tab için bu stil gerekir — pet bir "uygulama" gibi davranmamalı.
        var exStyle = NativeMethods.GetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestorePosition();
        ApplyVisibility();
        _hitTestTimer.Start();
    }

    // ---------------------------------------------------------------- konum

    private void RestorePosition()
    {
        var saved = _save.Window;

        if (saved.X is not { } savedX || saved.Y is not { } savedY
            || !double.IsFinite(savedX) || !double.IsFinite(savedY))
        {
            // İlk açılış: birincil ekranın sağ alt köşesine, görev çubuğunun üstüne otur.
            var work = SystemParameters.WorkArea;
            Left = work.Right - Width - 48;
            Top = work.Bottom - Height;
            return;
        }

        Left = savedX;
        Top = savedY;
        ClampToVisibleArea();
    }

    /// <summary>Pet'in bu kadarı her zaman tutulabilir halde kalmalı (DIP).</summary>
    private const double MinVisible = 48.0;

    /// <summary>
    /// Kaydedilen konum artık var olmayan bir yere denk gelebilir (monitör sökülmüş,
    /// çözünürlük değişmiş, kayıt bozulmuş). Pet'i en yakın gerçek ekrana geri çeker.
    /// </summary>
    private void ClampToVisibleArea()
    {
        var work = NearestWorkAreaDip();

        Left = Math.Clamp(Left, work.Left - Width + MinVisible, work.Right - MinVisible);
        Top = Math.Clamp(Top, work.Top, work.Bottom - MinVisible);
    }

    /// <summary>
    /// Pencereye en yakın gerçek monitörün çalışma alanı, DIP cinsinden.
    ///
    /// Sanal masaüstünün sınırlayıcı kutusunu (VirtualScreen*) kullanmak yanlıştır ve
    /// bu hata bir kez yapıldı: farklı boyutlardaki iki monitörde kutunun köşeleri
    /// hiçbir ekrana denk gelmeyen ölü bölgelerdir. Ölçüldü — 2560x1440 + 1920x1080
    /// düzeninde kutu 4480x1440 çıkıyor ama (4448,1408) noktası hiçbir monitörde yok.
    /// Pet oraya konunca ekranda hiç görünmez ve kullanıcı onu geri getiremez.
    /// </summary>
    private Rect NearestWorkAreaDip()
    {
        var target = PresentationSource.FromVisual(this)?.CompositionTarget;
        var toDevice = target?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var fromDevice = target?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

        var deviceTopLeft = toDevice.Transform(new Point(Left, Top));
        var deviceBottomRight = toDevice.Transform(new Point(Left + Width, Top + Height));

        var deviceRect = new System.Drawing.Rectangle(
            (int)Math.Round(deviceTopLeft.X),
            (int)Math.Round(deviceTopLeft.Y),
            Math.Max(1, (int)Math.Round(deviceBottomRight.X - deviceTopLeft.X)),
            Math.Max(1, (int)Math.Round(deviceBottomRight.Y - deviceTopLeft.Y)));

        // FromRectangle en çok kesişen ekranı döner; hiç kesişme yoksa EN YAKIN ekranı
        // (MONITOR_DEFAULTTONEAREST). Ölü bölgeye düşen bir konumu kurtaran şey budur.
        var workArea = System.Windows.Forms.Screen.FromRectangle(deviceRect).WorkingArea;

        var dipTopLeft = fromDevice.Transform(new Point(workArea.Left, workArea.Top));
        var dipBottomRight = fromDevice.Transform(new Point(workArea.Right, workArea.Bottom));
        return new Rect(dipTopLeft, dipBottomRight);
    }

    private void SavePosition()
    {
        // Pencere henüz konumlanmadıysa Left/Top NaN olabilir (WindowStartupLocation=Manual).
        // Böyle bir değeri kaydetmek yerine önceki konumu koru.
        if (double.IsFinite(Left) && double.IsFinite(Top))
        {
            _save.Window.X = Left;
            _save.Window.Y = Top;
        }

        _save.Window.Hidden = _hiddenByUser;
        _store.Save(_save);
    }

    // ---------------------------------------------------------------- tıkla-geç

    /// <summary>
    /// İmlecin altındaki sprite pikselinin alfasına bakıp pencereyi fareye "geçirgen"
    /// yapar ya da yapmaz. Pencere geçirgenken hiç fare olayı almadığı için bu iş
    /// olayla değil yoklamayla yapılmak zorunda.
    /// </summary>
    private void UpdateClickThrough()
    {
        if (_dragging)
        {
            SetClickThrough(false);
            return;
        }

        if (!IsPetVisible)
        {
            SetClickThrough(true);
            return;
        }

        SetClickThrough(!IsCursorOverPet());
    }

    private bool IsCursorOverPet()
    {
        if (!NativeMethods.GetCursorPos(out var native)) return false;

        var cursor = ScreenToDip(native);
        var relX = cursor.X - Left;
        var relY = cursor.Y - Top;

        if (relX < 0 || relY < 0 || relX >= Width || relY >= Height) return false;

        var spriteX = (int)(relX / Width * _mask.Width);
        var spriteY = (int)(relY / Height * _mask.Height);
        return _mask.IsOpaqueAt(spriteX, spriteY);
    }

    private void SetClickThrough(bool value)
    {
        if (_hwnd == IntPtr.Zero || value == _clickThrough) return;

        var exStyle = NativeMethods.GetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE);
        var updated = value
            ? exStyle | NativeMethods.WS_EX_TRANSPARENT
            : exStyle & ~NativeMethods.WS_EX_TRANSPARENT;

        NativeMethods.SetWindowLongEx(_hwnd, NativeMethods.GWL_EXSTYLE, updated);
        _clickThrough = value;
    }

    /// <summary>Win32 fiziksel piksel → WPF DIP. Ölçeklenmiş ekranlarda bu dönüşüm atlanırsa pet kayar.</summary>
    private Point ScreenToDip(NativeMethods.POINT point)
    {
        var source = PresentationSource.FromVisual(this);
        var raw = new Point(point.X, point.Y);
        return source?.CompositionTarget is null
            ? raw
            : source.CompositionTarget.TransformFromDevice.Transform(raw);
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

        // DragMove() yerine elle takip: DragMove kendi mesaj döngüsünü çalıştırır ve
        // tıkla-geç zamanlayıcımızı bloklar. Ayrıca tıklamayı sürüklemeden ayırmamıza izin vermez.
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
            SavePosition();
        }
        else
        {
            PlayPokeAnimation();
            Petted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Tıklanınca kısa bir ezilip toparlanma. Faz 0'da işlevden çok kanıt:
    /// pet'e denk gelen tıklamanın gerçekten pet'e ulaştığını gözle görünür kılıyor.
    /// </summary>
    private void PlayPokeAnimation()
    {
        var squashY = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        squashY.KeyFrames.Add(new EasingDoubleKeyFrame(0.84, KeyTime.FromPercent(0.30)));
        squashY.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromPercent(0.65)));
        squashY.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.0)));

        var squashX = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        squashX.KeyFrames.Add(new EasingDoubleKeyFrame(1.14, KeyTime.FromPercent(0.30)));
        squashX.KeyFrames.Add(new EasingDoubleKeyFrame(0.96, KeyTime.FromPercent(0.65)));
        squashX.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.0)));

        SpriteScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, squashY);
        SpriteScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, squashX);
    }

    // ---------------------------------------------------------------- görünürlük

    public void ToggleUserVisibility()
    {
        _hiddenByUser = !_hiddenByUser;
        ApplyVisibility();
        SavePosition();
    }

    public void SetHiddenByFullscreen(bool hidden)
    {
        if (_hiddenByFullscreen == hidden) return;
        _hiddenByFullscreen = hidden;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (IsPetVisible)
        {
            Show();
            Topmost = true; // Başka bir pencere üste geçmişse geri al.
        }
        else
        {
            Hide();
        }
    }

    public void SaveOnExit()
    {
        _hitTestTimer.Stop();
        SavePosition();
    }
}
