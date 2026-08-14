using System;
using System.Threading;
using System.Windows;
using FalyPet.App.Services;
using FalyPet.App.Ui;
using FalyPet.Core.Persistence;

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

    protected override void OnStartup(StartupEventArgs e)
    {
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
        _tray.ToggleVisibilityRequested += (_, _) =>
        {
            _petWindow.ToggleUserVisibility();
            _tray.SetPetVisible(_petWindow.IsPetVisible);
        };
        _tray.ExitRequested += (_, _) => Shutdown();

        _fullscreen = new FullscreenDetector();
        _fullscreen.ShouldHideChanged += (_, hide) => _petWindow.SetHiddenByFullscreen(hide);

        _petWindow.Show();
        _tray.SetPetVisible(_petWindow.IsPetVisible);
        _fullscreen.Start();
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
        _petWindow?.SaveOnExit();
        _fullscreen?.Dispose();
        _tray?.Dispose();

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }
}
