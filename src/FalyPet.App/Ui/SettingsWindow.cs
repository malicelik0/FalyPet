using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FalyPet.App.Rendering;
using FalyPet.App.Services;
using FalyPet.Core.Persistence;

namespace FalyPet.App.Ui;

/// <summary>
/// Ayarlar penceresi. Tepsi menüsündeki dağınık seçenekleri tek yere topluyor
/// ve tepside yeri olmayan işleri (yedekleme, sıfırlama) buraya alıyor.
///
/// Yedekleme özellikle önemli: kullanıcının haftalarca beslediği bir pet tek bir
/// dosyada duruyor. Onu kopyalamanın bir yolu olmalı ve bu yol "AppData'ya git,
/// şu dosyayı bul" olmamalı.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly SaveStore _store;
    private readonly SaveData _save;
    private readonly SpriteCache _sprites;
    private readonly string _version;
    private readonly TextBlock _status;

    public event EventHandler? PetResetRequested;

    /// <summary>Seçilen yeni ölçek. App bunu PetWindow'a iletiyor.</summary>
    public event EventHandler<int>? ScaleChangeRequested;

    public event EventHandler<bool>? SoundToggled;

    public SettingsWindow(SaveStore store, SaveData save, SpriteCache sprites, string version)
    {
        _store = store;
        _save = save;
        _sprites = sprites;
        _version = version;

        Title = "FalyPet — Ayarlar";
        Width = 440;
        Height = 400;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF4, 0xEC));
        FontFamily = new FontFamily("Segoe UI");

        _status = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x8A, 0x5A)),
        };

        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var panel = new StackPanel { Margin = new Thickness(20) };

        panel.Children.Add(Header("Başlangıç"));

        var autoStart = new CheckBox
        {
            Content = "Windows ile birlikte başlat",
            IsChecked = AutoStartService.IsEnabled,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4),
        };
        autoStart.Checked += (_, _) => ApplyAutoStart(autoStart, true);
        autoStart.Unchecked += (_, _) => ApplyAutoStart(autoStart, false);
        panel.Children.Add(autoStart);

        panel.Children.Add(Header("Pet boyutu"));
        panel.Children.Add(BuildScaleRow());

        var sound = new CheckBox
        {
            Content = "Ses efektleri",
            IsChecked = _save.SoundEnabled,
            FontSize = 13,
            Margin = new Thickness(0, 14, 0, 0),
        };
        sound.Checked += (_, _) => ApplySound(true);
        sound.Unchecked += (_, _) => ApplySound(false);
        panel.Children.Add(sound);

        panel.Children.Add(Header("Kayıt"));
        panel.Children.Add(new TextBlock
        {
            Text = _store.Path_,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x62, 0x70)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(MakeButton("Yedekle", Backup));
        buttons.Children.Add(MakeButton("Geri yükle", Restore));
        buttons.Children.Add(MakeButton("Klasörü aç", OpenFolder));
        panel.Children.Add(buttons);

        panel.Children.Add(Header("Bilgi"));
        panel.Children.Add(new TextBlock
        {
            Text = $"Sürüm: {_version}\n" +
                   $"Sprite kaynağı: {(_sprites.UsingRealArt ? "gerçek sanat (Assets\\sprites)" : "prosedürel")}",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
        });

        panel.Children.Add(Header("Tehlikeli"));
        var reset = MakeButton("Pet'i sıfırla", ResetPet);
        reset.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x40, 0x40));
        reset.Margin = new Thickness(0, 0, 8, 0);
        panel.Children.Add(reset);

        panel.Children.Add(_status);
        return panel;
    }

    /// <summary>
    /// Boyut kademeleri düğme olarak. Kaydırıcı KULLANILMIYOR: ölçek tam sayı
    /// katı olmak zorunda (yoksa pixel art'ta pikseller eşitsiz genişlikte çıkar),
    /// ve sürekli görünen bir kaydırıcı kullanıcıya ara değer varmış izlenimi verirdi.
    /// </summary>
    private UIElement BuildScaleRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<(Button Button, int Scale)>();

        foreach (var (scale, label) in PetWindow.ScaleOptions)
        {
            var b = new Button
            {
                Content = label,
                Height = 30,
                MinWidth = 62,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            b.Click += (_, _) =>
            {
                _save.PetScale = scale;
                _store.Save(_save);
                ScaleChangeRequested?.Invoke(this, scale);
                foreach (var (other, s) in buttons) Highlight(other, s == scale);
                Report($"Boyut: {label} ({FalyPet.App.Rendering.PetSpriteFactory.Size * scale} piksel)", ok: true);
            };

            buttons.Add((b, scale));
            row.Children.Add(b);
        }

        foreach (var (b, s) in buttons) Highlight(b, s == _save.PetScale);
        return row;
    }

    private static void Highlight(Button b, bool selected)
    {
        b.FontWeight = selected ? FontWeights.Bold : FontWeights.Normal;
        b.BorderBrush = new SolidColorBrush(selected
            ? Color.FromRgb(0x4A, 0x8A, 0x5A)
            : Color.FromRgb(0xDD, 0xD6, 0xC8));
        b.BorderThickness = new Thickness(selected ? 2 : 1);
    }

    private void ApplySound(bool enabled)
    {
        _save.SoundEnabled = enabled;
        _store.Save(_save);
        SoundToggled?.Invoke(this, enabled);
        Report(enabled ? "Ses efektleri açık." : "Ses efektleri kapalı.", ok: true);
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 16, 0, 6),
        Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x33)),
    };

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Content = text, Height = 28, MinWidth = 92, Margin = new Thickness(0, 0, 8, 0), FontSize = 12 };
        button.Click += (_, _) => action();
        return button;
    }

    private void ApplyAutoStart(CheckBox box, bool enabled)
    {
        if (AutoStartService.TrySet(enabled))
        {
            Report(enabled ? "Windows ile başlatma açıldı." : "Windows ile başlatma kapatıldı.", ok: true);
            return;
        }

        // Yazma başarısızsa kutuyu geri al — kullanıcı açık sanıp da çalışmadığını
        // haftalar sonra fark etmemeli.
        box.IsChecked = AutoStartService.IsEnabled;
        Report("Ayarlanamadı. Kısıtlı bir ortamda olabilirsin.", ok: false);
    }

    private void Backup()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"falypet-yedek-{DateTime.Now:yyyy-MM-dd}.json",
                Filter = "FalyPet kaydı (*.json)|*.json",
            };
            if (dialog.ShowDialog() != true) return;

            // Bellekteki güncel durumu diske yazıp ondan kopyalıyoruz; yoksa
            // yedek, son otomatik kayıttan bu yana geçen 30 saniyeyi kaçırır.
            _store.Save(_save);
            File.Copy(_store.Path_, dialog.FileName, overwrite: true);
            Report($"Yedeklendi: {dialog.FileName}", ok: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Report($"Yedeklenemedi: {e.Message}", ok: false);
        }
    }

    private void Restore()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "FalyPet kaydı (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            "Şu anki pet'in yerine yedektekini koyacağım. Bu geri alınamaz.\n\nDevam edilsin mi?",
            "Geri yükle", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            File.Copy(dialog.FileName, _store.Path_, overwrite: true);
            Report("Geri yüklendi. Değişikliğin geçerli olması için FalyPet'i kapatıp aç.", ok: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Report($"Geri yüklenemedi: {e.Message}", ok: false);
        }
    }

    private void OpenFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(_store.Path_);
            if (folder is null) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            Report($"Klasör açılamadı: {e.Message}", ok: false);
        }
    }

    private void ResetPet()
    {
        var name = _save.Pet?.Name ?? "Pet";
        var confirm = MessageBox.Show(
            $"{name} silinecek ve baştan tür seçeceksin. Bütün ilerleme ve coin'ler gidecek.\n\n" +
            "Önce yedek almak isteyebilirsin. Devam edilsin mi?",
            "Pet'i sıfırla", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        PetResetRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void Report(string message, bool ok)
    {
        _status.Text = message;
        _status.Foreground = new SolidColorBrush(ok
            ? Color.FromRgb(0x4A, 0x8A, 0x5A)
            : Color.FromRgb(0xB0, 0x40, 0x40));
    }
}
