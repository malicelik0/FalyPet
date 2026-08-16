using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace FalyPet.App.Services;

/// <summary>
/// Bildirim alanı (tepsi) ikonu ve sağ tık menüsü.
///
/// WinForms'un NativeMethods'una gidiyoruz çünkü WPF'in kendi tepsi ikonu yok ve
/// tek dış bağımlılık eklemek yerine .NET ile gelen NotifyIcon'u kullanmak
/// hem sıfır NuGet hem de daha az bakım demek.
///
/// İkon çalışma anında çiziliyor — Faz 0'ın hiçbir sanat dosyasına bağımlı olmaması için.
/// Faz 8'de gerçek bir .ico ile değiştirilecek.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _visibilityItem;
    private readonly WinForms.ToolStripMenuItem _autoStartItem;
    private readonly WinForms.ToolStripMenuItem _updateItem;
    private IntPtr _iconHandle;

    public event EventHandler? ToggleVisibilityRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? CheckUpdatesRequested;
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Ayarlar penceresinden değiştirilebildiği için tik dışarıdan tazelenebilmeli.
    /// Bayrak şart: tik'i programatik değiştirmek CheckedChanged'i tetikler ve kayıt
    /// defterine gereksiz bir yazma daha yapılırdı (başarısızsa yanlış uyarı da çıkardı).
    /// </summary>
    public void RefreshAutoStartState()
    {
        _suppressAutoStartEvent = true;
        _autoStartItem.Checked = AutoStartService.IsEnabled;
        _suppressAutoStartEvent = false;
    }

    private bool _suppressAutoStartEvent;

    public TrayIconService()
    {
        var menu = new WinForms.ContextMenuStrip();

        _visibilityItem = new WinForms.ToolStripMenuItem("Gizle");
        _visibilityItem.Click += (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_visibilityItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var settingsItem = new WinForms.ToolStripMenuItem("Ayarlar…");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        _autoStartItem = new WinForms.ToolStripMenuItem("Windows ile başlat")
        {
            CheckOnClick = true,
            Checked = AutoStartService.IsEnabled,
        };
        _autoStartItem.CheckedChanged += (_, _) =>
        {
            if (_suppressAutoStartEvent) return;

            // Yazma başarısız olursa (kısıtlı ortam, grup ilkesi) tik geri alınır —
            // kullanıcı açık sanıp da çalışmadığını fark etmemeli.
            if (!AutoStartService.TrySet(_autoStartItem.Checked))
            {
                _autoStartItem.Checked = AutoStartService.IsEnabled;
                ShowMessage("FalyPet", "Windows ile başlatma ayarlanamadı.");
            }
        };
        menu.Items.Add(_autoStartItem);

        _updateItem = new WinForms.ToolStripMenuItem("Güncellemeleri denetle");
        _updateItem.Click += (_, _) => CheckUpdatesRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_updateItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem("Çıkış");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        _icon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(out _iconHandle),
            Text = "FalyPet",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Menüdeki metni pencerenin gerçek durumuna göre günceller.</summary>
    public void SetPetVisible(bool visible) => _visibilityItem.Text = visible ? "Gizle" : "Göster";

    public void SetVersion(string version) => _icon.Text = $"FalyPet {version}";

    /// <summary>Kurulmamış (geliştirme) çalıştırmalarda güncelleme denetimi anlamsız.</summary>
    public void SetUpdatesSupported(bool supported)
    {
        _updateItem.Enabled = supported;
        if (!supported) _updateItem.Text = "Güncelleme (yalnızca kurulu sürümde)";
    }

    public void ShowMessage(string title, string body) =>
        _icon.ShowBalloonTip(4000, title, body, WinForms.ToolTipIcon.None);

    private static Icon CreateIcon(out IntPtr handle)
    {
        handle = IntPtr.Zero;

        // Önce exe'ye gömülü ikonu kullan: masaüstü kısayolu, görev çubuğu ve
        // tepsi böylece aynı görüntüyü paylaşır. İki ayrı çizim tutmak, birini
        // güncelleyip diğerini unutmanın garantisidir.
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var embedded = Icon.ExtractAssociatedIcon(path);
                if (embedded is not null) return embedded;
            }
        }
        catch (Exception e) when (e is ArgumentException or IOException or System.ComponentModel.Win32Exception)
        {
            // Gömülü ikon okunamadıysa aşağıdaki çizime düş.
        }

        return DrawFallbackIcon(out handle);
    }

    /// <summary>Gömülü ikon okunamazsa kullanılan yedek — uygulama ikonsuz kalmasın.</summary>
    private static Icon DrawFallbackIcon(out IntPtr handle)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var body = new Rectangle(7, 3, 18, 26);
            using var fill = new SolidBrush(Color.FromArgb(245, 238, 222));
            using var pen = new Pen(Color.FromArgb(60, 48, 60), 2f);
            g.FillEllipse(fill, body);
            g.DrawEllipse(pen, body);

            using var spot = new SolidBrush(Color.FromArgb(126, 196, 184));
            g.FillEllipse(spot, 11, 11, 5, 5);
            g.FillEllipse(spot, 17, 18, 6, 6);
        }

        handle = bitmap.GetHicon();
        // FromHandle sahipliği almaz; handle'ı biz tutup Dispose'ta DestroyIcon ile bırakıyoruz.
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }
}
