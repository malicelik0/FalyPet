using System;
using System.IO;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace FalyPet.App.Services;

/// <summary>
/// GitHub Releases üzerinden otomatik güncelleme.
///
/// TASARIM KARARI: güncelleme bulununca uygulama yeniden başlatılmaz.
/// FalyPet 7/24 açık duran bir uygulama; çalışırken kendini kapatıp açması
/// kullanıcının pet'ini ortadan kaybetmek demektir. Onun yerine güncelleme
/// sessizce indirilir ve kullanıcı uygulamadan ZATEN çıktığında uygulanır
/// (<see cref="UpdateManager.WaitExitThenApplyUpdates"/>). Kullanıcı hiçbir
/// kesinti yaşamaz; bir sonraki açılışta yeni sürümdedir.
/// </summary>
internal sealed class UpdateService
{
    /// <summary>
    /// Sürümlerin dağıtıldığı public repo. Kaynak kod özel kalabilir — istemciye
    /// erişim anahtarı gömmemek için sürüm reposu public olmalı.
    /// </summary>
    public const string ReleasesRepository = "https://github.com/malicelik0/FalyPet";

    private readonly UpdateManager? _manager;

    /// <summary>
    /// Test için güncelleme kaynağını yerel bir klasöre çevirir.
    ///
    /// Bu olmadan güncelleme akışını doğrulamanın tek yolu GitHub'a gerçek bir
    /// sürüm yayınlamak olurdu — yani mekanizmayı test etmek için kullanıcılara
    /// yayın yapmak gerekirdi. Bu değişkenle tüm akış (denetle → indir → çıkışta
    /// kur) yayın yapmadan, yerelde uçtan uca sınanabiliyor.
    /// </summary>
    public const string LocalSourceVariable = "FALYPET_UPDATE_SOURCE";

    public UpdateService()
    {
        try
        {
            var localSource = Environment.GetEnvironmentVariable(LocalSourceVariable);

            _manager = !string.IsNullOrWhiteSpace(localSource) && Directory.Exists(localSource)
                ? new UpdateManager(localSource)
                : new UpdateManager(new GithubSource(ReleasesRepository, null, false));
        }
        catch (Exception)
        {
            // Güncelleme altyapısı kurulamadıysa uygulama yine de çalışmalı.
            _manager = null;
        }
    }

    /// <summary>Geliştirme sırasında (kurulmamış exe) güncelleme denetimi anlamsızdır.</summary>
    public bool IsAvailable => _manager?.IsInstalled == true;

    public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? "geliştirme";

    /// <summary>Bir güncelleme indirildi ve çıkışta kurulmayı bekliyor.</summary>
    public bool UpdatePending { get; private set; }

    /// <summary>
    /// Güncelleme var mı diye bakar, varsa indirir ve çıkışta kurulmak üzere kuyruğa alır.
    /// Sonuç kullanıcıya gösterilecek mesaj; null ise sessiz kal (yeni sürüm yok).
    /// </summary>
    public async Task<string?> CheckAndStageAsync()
    {
        if (_manager is null || !_manager.IsInstalled) return null;

        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return null;

            await _manager.DownloadUpdatesAsync(update);

            // Uygulama kapanınca kurulur; yeniden başlatma YOK.
            _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
            UpdatePending = true;

            return $"FalyPet {update.TargetFullRelease.Version} indirildi. " +
                   "Uygulamayı bir dahaki kapatışında kurulacak.";
        }
        catch (Exception e)
        {
            // Ağ yoksa, GitHub erişilemezse ya da sürüm okunamazsa sessizce geç.
            // Güncelleme denetimi bir pet uygulamasını asla engellememeli.
            System.Diagnostics.Debug.WriteLine($"Güncelleme denetimi başarısız: {e.Message}");
            return null;
        }
    }
}
