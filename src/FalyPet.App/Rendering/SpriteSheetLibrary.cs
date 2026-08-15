using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FalyPet.Core.Content;
using FalyPet.Core.Model;

namespace FalyPet.App.Rendering;

/// <summary>
/// Diskteki gerçek sprite sheet'leri okur. Yoksa sessizce null döner ve
/// <see cref="SpriteCache"/> prosedürel üretime düşer.
///
/// TASARIM: arama zinciri YEDEKLİ. Bir türün bir durumu çizilmemişse oyun
/// çökmez, o durum için idle'a düşer; hiç sanat yoksa tamamen prosedürel devam
/// eder. Böylece sanat 10 türü tek seferde bitirmek zorunda değil — tür tür,
/// durum durum eklenebilir ve her ekleme anında devreye girer.
///
/// DOSYA DÜZENİ (uygulamanın yanındaki Assets\sprites klasörü):
///
///   Assets\sprites\kedi\adult_walk.png   yatay şerit, kare başına 32 piksel
///   Assets\sprites\kedi\baby_idle.png
///   Assets\sprites\kedi\egg.png          4 kare = 0,1,2,3 çatlak
///
/// Aşama adları : egg baby child teen adult
/// Durum adları : idle walk sleep eat drink play wash sick sulk
///
/// Şerit yüksekliği kare boyutunu belirler; genişlik onun tam katı olmalı.
/// 32x32 dışında bir boyut da çalışır (64x64 sheet'ler otomatik ölçeklenir),
/// ama pet penceresi tam sayı katı büyüttüğü için 32'nin katları önerilir.
/// </summary>
internal sealed class SpriteSheetLibrary
{
    private readonly string _root;
    private readonly Dictionary<string, BitmapSource[]?> _strips = [];

    public SpriteSheetLibrary(string? rootDirectory = null)
    {
        _root = rootDirectory ?? DefaultRoot();
    }

    private static string DefaultRoot()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? ".";
        return Path.Combine(exeDir, "Assets", "sprites");
    }

    public bool RootExists => Directory.Exists(_root);

    /// <summary>
    /// İstenen kareyi döner; sanat yoksa null.
    /// <paramref name="frame"/> yumurtada çatlak sayısıdır.
    /// </summary>
    public BitmapSource? TryGetFrame(SpeciesDefinition species, GrowthStage stage, PetAnimation anim, int frame)
    {
        if (!RootExists) return null;

        var strip = LoadStrip(species.Id, FileNameFor(stage, anim));

        // Bu durum çizilmemişse aynı aşamanın idle'ına düş. Yürüme sprite'ı olmayan
        // bir tür duruyor gibi görünür — çirkin ama çalışır; hiç görünmemekten iyi.
        if (strip is null && stage != GrowthStage.Egg && anim != PetAnimation.Idle)
            strip = LoadStrip(species.Id, FileNameFor(stage, PetAnimation.Idle));

        if (strip is null || strip.Length == 0) return null;

        return strip[((frame % strip.Length) + strip.Length) % strip.Length];
    }

    private static string FileNameFor(GrowthStage stage, PetAnimation anim) =>
        stage == GrowthStage.Egg ? "egg" : $"{StageName(stage)}_{AnimName(anim)}";

    private static string StageName(GrowthStage stage) => stage switch
    {
        GrowthStage.Egg => "egg",
        GrowthStage.Baby => "baby",
        GrowthStage.Child => "child",
        GrowthStage.Teen => "teen",
        _ => "adult",
    };

    private static string AnimName(PetAnimation anim) => anim switch
    {
        PetAnimation.Walk => "walk",
        PetAnimation.Sleep => "sleep",
        PetAnimation.Eat => "eat",
        PetAnimation.Drink => "drink",
        PetAnimation.Play => "play",
        PetAnimation.Wash => "wash",
        PetAnimation.Sick => "sick",
        PetAnimation.Sulk => "sulk",
        _ => "idle",
    };

    private BitmapSource[]? LoadStrip(string speciesId, string name)
    {
        var cacheKey = $"{speciesId}/{name}";
        if (_strips.TryGetValue(cacheKey, out var cached)) return cached;

        var path = Path.Combine(_root, speciesId, name + ".png");
        var frames = ReadAndSlice(path);

        // Bulunamayan dosya da önbelleğe yazılıyor (null olarak): her karede
        // diske gidip "yok mu" diye sormak 8 fps'te gereksiz dosya sistemi trafiği.
        _strips[cacheKey] = frames;
        return frames;
    }

    private static BitmapSource[]? ReadAndSlice(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(path, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad; // dosya kilidi bırakılmasın
            sheet.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            sheet.EndInit();
            sheet.Freeze();

            var size = sheet.PixelHeight;
            if (size <= 0 || sheet.PixelWidth % size != 0) return null;

            var count = sheet.PixelWidth / size;
            var frames = new BitmapSource[count];

            for (var i = 0; i < count; i++)
            {
                var cropped = new CroppedBitmap(sheet, new Int32Rect(i * size, 0, size, size));
                frames[i] = Normalize(cropped);
            }

            return frames;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UriFormatException or ArgumentException)
        {
            // Bozuk ya da okunamayan PNG oyunu durdurmamalı — prosedüre düşülür.
            System.Diagnostics.Debug.WriteLine($"Sprite sheet okunamadı ({path}): {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Kareyi Bgra32'ye çevirip dondurur. Alfa maskesi düz alfa bekliyor;
    /// dosyadan gelen format ne olursa olsun burada tekilleşiyor.
    /// </summary>
    private static BitmapSource Normalize(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        converted.Freeze();
        return converted;
    }
}
