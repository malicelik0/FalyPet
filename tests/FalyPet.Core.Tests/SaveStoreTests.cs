using FalyPet.Core.Persistence;

namespace FalyPet.Core.Tests;

/// <summary>
/// Kayıt katmanının testleri. Burası gözle test edilemeyen bir yer: bozuk dosya,
/// yarım yazma ve sürüm uyuşmazlığı ancak zorlanarak üretilebilir — ve hepsinin
/// sonucu kullanıcının pet'ini kaybetmesidir.
/// </summary>
public sealed class SaveStoreTests : IDisposable
{
    private readonly string _dir;

    public SaveStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "FalyPetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private SaveStore NewStore() => new(_dir);
    private string SavePath => Path.Combine(_dir, "save.json");
    private string BackupPath => Path.Combine(_dir, "save.json.bak");

    [Fact]
    public void Kayit_yoksa_varsayilan_doner_ve_firlatmaz()
    {
        var data = NewStore().Load();

        Assert.Equal(SaveData.CurrentVersion, data.Version);
        Assert.Null(data.Window.X);
        Assert.Null(data.Pet);
    }

    [Fact]
    public void Yazilan_kayit_aynen_geri_okunur()
    {
        var store = NewStore();
        store.Save(new SaveData
        {
            Window = new WindowSave { X = 123.5, Y = 456.25, Hidden = true },
        });

        var loaded = NewStore().Load();

        Assert.Equal(123.5, loaded.Window.X);
        Assert.Equal(456.25, loaded.Window.Y);
        Assert.True(loaded.Window.Hidden);
    }

    [Fact]
    public void Hic_konumlanmamis_pencere_kaydedilebilir()
    {
        // REGRESYON: konum sentineli önce double.NaN idi ve System.Text.Json NaN'ı
        // serileştiremeyip fırlatıyordu. Pencere konumlanmadan kapanan bir oturum
        // (açılışta hemen çıkış) kayıt sırasında çöküyordu.
        var exception = Record.Exception(() => NewStore().Save(new SaveData()));

        Assert.Null(exception);
        Assert.Null(NewStore().Load().Window.X);
    }

    [Fact]
    public void Kaydetmek_gecici_dosya_birakmaz()
    {
        NewStore().Save(new SaveData());

        Assert.True(File.Exists(SavePath));
        Assert.False(File.Exists(SavePath + ".tmp"));
    }

    [Fact]
    public void Ikinci_kayit_oncekini_yedege_alir()
    {
        var store = NewStore();
        store.Save(new SaveData { Window = new WindowSave { X = 10, Y = 10 } });
        store.Save(new SaveData { Window = new WindowSave { X = 20, Y = 20 } });

        Assert.True(File.Exists(BackupPath));
        Assert.Equal(20, NewStore().Load().Window.X);
    }

    [Fact]
    public void Bozuk_kayit_yedekten_kurtarilir()
    {
        var store = NewStore();
        store.Save(new SaveData { Window = new WindowSave { X = 10, Y = 11 } });
        store.Save(new SaveData { Window = new WindowSave { X = 20, Y = 21 } });

        // Ana dosyayı boz — yarım yazma veya disk hatası simülasyonu.
        File.WriteAllText(SavePath, "{ bu gecerli json degil");

        var loaded = NewStore().Load();

        // Yedek bir önceki kaydı tutuyordu; ilerleme tamamen kaybolmuyor.
        Assert.Equal(10, loaded.Window.X);
    }

    [Fact]
    public void Bozuk_kayit_ve_yedek_yoksa_sifirdan_baslar_ve_firlatmaz()
    {
        File.WriteAllText(SavePath, "\0\0\0 bozuk");

        var loaded = NewStore().Load();

        Assert.Null(loaded.Window.X);
    }

    [Fact]
    public void Bos_dosya_cokme_yapmaz()
    {
        File.WriteAllText(SavePath, "");

        var loaded = NewStore().Load();

        Assert.Null(loaded.Window.X);
    }

    [Fact]
    public void Gelecekten_gelen_surum_ezilmez_sifirdan_baslanir()
    {
        // Kullanıcı yeni sürümü kurup sonra eskiye döndü. Anlamadığımız alanları
        // ezip kaydetmektense sıfırdan başlamak daha az zarar verir.
        var json = "{\"Version\": " + (SaveData.CurrentVersion + 99) + ", \"Window\": {\"X\": 5, \"Y\": 5}}";
        File.WriteAllText(SavePath, json);

        var loaded = NewStore().Load();

        Assert.Null(loaded.Window.X);
    }

    [Fact]
    public void Kaydetmek_LastSavedUtc_alanini_tazeler()
    {
        var eski = DateTimeOffset.UtcNow.AddDays(-3);
        var store = NewStore();

        store.Save(new SaveData { LastSavedUtc = eski });

        // Offline ihtiyaç telafisi bu alana dayanacak; kaydederken güncellenmezse
        // uygulama her açılışta "3 gündür kapalıydım" sanır.
        var loaded = NewStore().Load();
        Assert.True(loaded.LastSavedUtc > eski.AddDays(2));
    }

    [Fact]
    public void Varsayilan_klasor_APPDATA_altindadir_exe_yaninda_degil()
    {
        // Velopack her güncellemede kurulum klasörünü değiştirir. Kayıt oraya
        // yazılırsa ilk güncellemede kullanıcının pet'i silinir.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.StartsWith(appData, SaveStore.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("FalyPet", SaveStore.DefaultDirectory);
    }
}
