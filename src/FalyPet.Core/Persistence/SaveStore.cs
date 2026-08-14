using System.Text.Json;
using System.Text.Json.Serialization;

namespace FalyPet.Core.Persistence;

/// <summary>
/// Kayıt dosyasını okur/yazar.
///
/// İki kural pazarlık dışıdır:
///
/// 1. Dosya <c>%APPDATA%\FalyPet\</c> altında durur, ASLA exe'nin yanında değil.
///    Velopack uygulamayı <c>%LocalAppData%\FalyPet\current\</c> içine kurar ve her
///    güncellemede o klasörü değiştirir — exe yanına yazılan kayıt ilk güncellemede silinir.
///
/// 2. Yazma atomiktir: önce .tmp dosyasına yazılır, diske zorlanır (flush), sonra yer
///    değiştirilir. Pet uygulaması sık kayıt yapar; yazmanın ortasında kesilen elektrik
///    kullanıcının pet'ini yok etmemeli.
/// </summary>
public sealed class SaveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _path;
    private readonly string _tempPath;
    private readonly string _backupPath;

    public SaveStore(string? directory = null)
    {
        _directory = directory ?? DefaultDirectory;
        _path = Path.Combine(_directory, "save.json");
        _tempPath = _path + ".tmp";
        _backupPath = _path + ".bak";
    }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FalyPet");

    public string Path_ => _path;

    /// <summary>
    /// Kaydı okur. Dosya yoksa, bozuksa veya okunamıyorsa yedeğe düşer; o da olmazsa
    /// yeni bir kayıt döner. Bu metot hiçbir koşulda fırlatmaz — bozuk bir kayıt
    /// dosyası yüzünden uygulama açılmamazlık etmemeli.
    /// </summary>
    public SaveData Load()
    {
        lock (_gate)
        {
            var loaded = TryRead(_path) ?? TryRead(_backupPath);
            if (loaded is null) return new SaveData();

            // İleride şema atlarsa göç burada yapılacak.
            if (loaded.Version > SaveData.CurrentVersion)
            {
                // Kullanıcı sürümü düşürmüş. Anlamadığımız alanları ezmektense sıfırdan başla.
                return new SaveData();
            }

            return loaded;
        }
    }

    public void Save(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (_gate)
        {
            data.Version = SaveData.CurrentVersion;
            data.LastSavedUtc = DateTimeOffset.UtcNow;

            Directory.CreateDirectory(_directory);

            var json = JsonSerializer.Serialize(data, JsonOptions);

            using (var stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                // İşletim sistemi tamponunu diske zorla. Bu satır olmadan "atomik yazma"
                // sadece bir temenni olur: yer değiştirme başarılı görünürken içerik hâlâ RAM'de olabilir.
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                // Replace eski dosyayı .bak yapar — bedava yedek rotasyonu.
                File.Replace(_tempPath, _path, _backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(_tempPath, _path);
            }
        }
    }

    private static SaveData? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
