using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using FalyPet.App.Rendering;
using FalyPet.App.Ui;
using FalyPet.Core.Content;
using FalyPet.Core.Model;
using FalyPet.Core.Persistence;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Diagnostics;

/// <summary>
/// <c>FalyPet.exe --self-test &lt;rapor.txt&gt;</c> ile çalışır: bütün pencereleri
/// kurar, gösterir ve kapatır; hata alan olursa raporlar.
///
/// Neden var: birim testleri Core'u kapsıyor ama WPF pencerelerini kapsamıyor,
/// ve pencereler ancak menüden tıklanarak açılıyor — yani otomatik doğrulamanın
/// dışında kalıyorlardı. Bir XAML/bağlama hatası ya da null referans, kullanıcı
/// o menüye tıklayana kadar görünmezdi. Bu test her pencereyi her sürümde açıyor.
///
/// Çıkış kodu: 0 hepsi geçti, 1 en az biri patladı.
/// </summary>
internal static class SelfTest
{
    public static int Run(string reportPath)
    {
        var report = new StringBuilder();
        var failures = 0;

        var tempDir = Path.Combine(Path.GetTempPath(), "FalyPetSelfTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = new SaveStore(tempDir);
            var save = store.Load();
            save.Pet = PetSimulation.CreateNew("tilki", "Test", DateTimeOffset.UtcNow);
            save.Pet.Stage = GrowthStage.Adult;
            save.Pet.Coins = 500;
            save.Pet.OwnedItems.Add("fiyonk");
            save.Pet.EquippedCostumeId = "fiyonk";

            var sim = new PetSimulation(save.Pet);
            var sprites = new SpriteCache();
            var species = SpeciesCatalog.ById(save.Pet.SpeciesId);

            report.AppendLine($"sprite kaynagi : {(sprites.UsingRealArt ? "gercek sanat" : "proseduerel")}");
            report.AppendLine();

            var cases = new (string Name, Func<Window> Create)[]
            {
                ("OnboardingWindow", () => new OnboardingWindow(sprites)),
                // Aynı pencere "pet değiştir" modunda da açılıyor; iki yol da sınanmalı.
                ("OnboardingWindow(değiştir)", () => new OnboardingWindow(sprites, save.Pet!.SpeciesId, save.Pet.Name)),
                // Duman testinde ses kapalı: otomatik koşuda makineyi öttürmesin.
                ("PetWindow",        () => new PetWindow(store, save, sprites, new Services.SoundService { Enabled = false })),
                ("ShopWindow",       () => new ShopWindow(sim, store, save, species, sprites)),
                ("SettingsWindow",   () => new SettingsWindow(store, save, sprites, "self-test")),
                ("CatchGameWindow",  () => new CatchGameWindow(sim, store, save, species, sprites)),
                ("BubbleWindow",     () => new BubbleWindow()),
            };

            foreach (var (name, create) in cases)
            {
                if (!TryWindow(name, create, report)) failures++;
            }

            report.AppendLine();
            report.AppendLine(SpriteCoverage(sprites));
        }
        catch (Exception e)
        {
            failures++;
            report.AppendLine($"KURULUM PATLADI: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
        }

        report.Insert(0, failures == 0
            ? "SONUC: HEPSI GECTI\n\n"
            : $"SONUC: {failures} PENCERE PATLADI\n\n");

        File.WriteAllText(reportPath, report.ToString());
        return failures == 0 ? 0 : 1;
    }

    private static bool TryWindow(string name, Func<Window> create, StringBuilder report)
    {
        try
        {
            var window = create();

            // Show + UpdateLayout şart: kurucu sessizce geçip ölçüm/şablon
            // aşamasında patlayan hatalar var (bağlama, kaynak, ölçü döngüsü).
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;   // ekran dışında aç, kullanıcının gözüne sokma
            window.Top = -10000;
            window.Show();
            window.UpdateLayout();
            window.Close();

            report.AppendLine($"  GECTI  {name}");
            return true;
        }
        catch (Exception e)
        {
            report.AppendLine($"  PATLADI {name}: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    /// <summary>Her tür ve aşama için en az idle sprite'ı üretilebiliyor mu?</summary>
    private static string SpriteCoverage(SpriteCache sprites)
    {
        var problems = new List<string>();
        var count = 0;

        foreach (var species in SpeciesCatalog.All)
        foreach (var stage in new[] { GrowthStage.Egg, GrowthStage.Baby, GrowthStage.Child, GrowthStage.Teen, GrowthStage.Adult })
        foreach (var anim in Enum.GetValues<PetAnimation>())
        {
            try
            {
                var sprite = sprites.Get(species, stage, anim, 0, false);
                count++;
                if (sprite.PixelWidth == 0 || sprite.PixelHeight == 0)
                    problems.Add($"{species.Id}/{stage}/{anim}: sifir olcu");
            }
            catch (Exception e)
            {
                problems.Add($"{species.Id}/{stage}/{anim}: {e.GetType().Name}");
            }
        }

        return problems.Count == 0
            ? $"sprite kapsamasi: {count} bilesim uretildi, sorun yok"
            : $"sprite kapsamasi: {count} bilesim, {problems.Count} SORUN\n  " + string.Join("\n  ", problems);
    }
}
