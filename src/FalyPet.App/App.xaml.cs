using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FalyPet.App.Services;
using FalyPet.App.Ui;
using FalyPet.Core.Persistence;
using Velopack;

namespace FalyPet.App;

public partial class App : System.Windows.Application
{
    /// <summary>
    /// Tek örnek kilidi. İki FalyPet açık olursa ikisi de aynı save.json'a yazar ve
    /// biri diğerinin ilerlemesini ezer — bu, kullanıcının pet'ini kaybetmesi demektir.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\FalyPet.SingleInstance";

    private Mutex? _instanceMutex;
    private SaveStore? _store;
    private SaveData? _save;
    private PetWindow? _petWindow;
    private TrayIconService? _tray;
    private FullscreenDetector? _fullscreen;
    private UpdateService? _updates;
    private DispatcherTimer? _updateTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        // EN BAŞTA olmak zorunda. Velopack kurulum, güncelleme ve kaldırma
        // adımlarında uygulamayı özel argümanlarla çalıştırır ve hızlıca çıkmasını
        // bekler; UI kurulmadan, hatta tek örnek kilidi alınmadan önce ele geçmeli.
        VelopackApp.Build().SetArgs(e.Args).Run();

        base.OnStartup(e);

        // Teşhis modu: pencere açmadan sprite'ları diske döküp çıkar.
        // Tek örnek kilidinden ÖNCE gelmeli — pet çalışırken de denetim yapılabilsin diye.
        if (e.Args.Length >= 2 && e.Args[0] == "--dump-sprite")
        {
            var report = Diagnostics.SpriteDump.Run(e.Args[1]);
            System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "report.txt"), report);
            Shutdown();
            return;
        }

        // Duman testi: bütün pencereleri kurup kapatır, hata varsa raporlar.
        // Birim testleri Core'u kapsıyor ama WPF pencerelerini kapsamıyordu.
        if (e.Args.Length >= 2 && e.Args[0] == "--self-test")
        {
            var code = Diagnostics.SelfTest.Run(e.Args[1]);
            Shutdown(code);
            return;
        }

        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Sessizce çık. Kullanıcı muhtemelen kısayola ikinci kez tıkladı;
            // hata kutusu göstermek gereksiz gürültü olur.
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        _store = new SaveStore();
        _save = _store.Load();

        var sprites = new Rendering.SpriteCache();

        // Pet yoksa bu ilk açılış: tür seçimi olmadan devam edilemez.
        if (_save.Pet is null && !RunOnboarding(sprites))
        {
            Shutdown();
            return;
        }

        _petWindow = new PetWindow(_store, _save, sprites);

        _tray = new TrayIconService();
        // Bu lambdalar _petWindow ALANINI okuyor, yerel bir kopyayı değil.
        // Önemli: "Pet'i sıfırla" pencereyi yok edip yenisini kuruyor; null geçidi
        // o kısa aralıkta çökmeyi engelliyor.
        _tray.ToggleVisibilityRequested += (_, _) =>
        {
            if (_petWindow is null) return;
            _petWindow.ToggleUserVisibility();
            _tray?.SetPetVisible(_petWindow.IsPetVisible);
        };
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.CheckUpdatesRequested += (_, _) => _ = CheckUpdatesAsync(announceWhenUpToDate: true);
        _tray.SettingsRequested += (_, _) => OpenSettings(sprites);

        _fullscreen = new FullscreenDetector();
        _fullscreen.ShouldHideChanged += (_, hide) => _petWindow?.SetHiddenByFullscreen(hide);

        _petWindow.Show();
        _tray.SetPetVisible(_petWindow.IsPetVisible);
        _fullscreen.Start();

        _updates = new UpdateService();
        _tray.SetVersion(_updates.CurrentVersion);
        _tray.SetUpdatesSupported(_updates.IsAvailable);

        // Açılışta bir kez, sonra altı saatte bir. Daha sık denetlemek GitHub'a
        // gereksiz istek, daha seyrek denetlemek güncellemeyi günlerce geciktirir.
        _ = CheckUpdatesAsync(announceWhenUpToDate: false);
        _updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromHours(6) };
        _updateTimer.Tick += (_, _) => _ = CheckUpdatesAsync(announceWhenUpToDate: false);
        _updateTimer.Start();
    }

    private SettingsWindow? _settings;

    private void OpenSettings(Rendering.SpriteCache sprites)
    {
        if (_settings is { IsVisible: true }) { _settings.Activate(); return; }

        _settings = new SettingsWindow(_store!, _save!, sprites, _updates?.CurrentVersion ?? "geliştirme");
        _settings.PetResetRequested += (_, _) => ResetPet(sprites);
        _settings.Closed += (_, _) => { _settings = null; _tray?.RefreshAutoStartState(); };
        _settings.Show();
    }

    /// <summary>
    /// Pet'i siler ve tür seçiminden yeniden başlatır.
    /// Pencere yeniden kuruluyor çünkü <see cref="PetWindow"/> içindeki simülasyon
    /// eski PetSave nesnesine bağlı; sadece kaydı değiştirmek ikisini ayrıştırırdı.
    /// </summary>
    private void ResetPet(Rendering.SpriteCache sprites)
    {
        _petWindow?.SaveOnExit();
        _petWindow?.Close();
        _petWindow = null;

        _save!.Pet = null;
        _save.Window.Hidden = false;
        _store!.Save(_save);

        if (!RunOnboarding(sprites)) { Shutdown(); return; }

        _petWindow = new PetWindow(_store, _save, sprites);
        _petWindow.Show();
        _tray?.SetPetVisible(_petWindow.IsPetVisible);
    }

    private async Task CheckUpdatesAsync(bool announceWhenUpToDate)
    {
        if (_updates is null) return;

        if (!_updates.IsAvailable)
        {
            if (announceWhenUpToDate)
                _tray?.ShowMessage("FalyPet", "Güncelleme yalnızca kurulu sürümde denetlenebilir.");
            return;
        }

        var message = await _updates.CheckAndStageAsync();

        if (message is not null) _tray?.ShowMessage("FalyPet güncellendi", message);
        else if (announceWhenUpToDate) _tray?.ShowMessage("FalyPet", "En güncel sürümü kullanıyorsun.");
    }

    /// <summary>
    /// Tür seçimi ekranını gösterir. Kullanıcı kapatırsa false döner ve uygulama
    /// açılmaz — yarım kurulmuş bir pet ile devam etmek kayıt şemasını bozar.
    /// </summary>
    private bool RunOnboarding(Rendering.SpriteCache sprites)
    {
        var window = new OnboardingWindow(sprites);
        if (window.ShowDialog() != true) return false;

        _save!.Pet = Core.Simulation.PetSimulation.CreateNew(
            window.SelectedSpeciesId, window.PetName, DateTimeOffset.UtcNow);

        _store!.Save(_save);
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateTimer?.Stop();
        _petWindow?.SaveOnExit();
        _fullscreen?.Dispose();
        _tray?.Dispose();

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }
}
