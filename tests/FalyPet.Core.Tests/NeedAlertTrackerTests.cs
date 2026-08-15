using FalyPet.Core.Model;
using FalyPet.Core.Simulation;

namespace FalyPet.Core.Tests;

/// <summary>
/// Uyarı mantığının testleri. İki yönlü de kırılabilir bir denge:
/// hiç uyarmazsa pet unutulur ve büyüme durur; fazla uyarırsa uygulama silinir.
/// </summary>
public sealed class NeedAlertTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    private static PetSimulation Yetiskin()
    {
        var save = PetSimulation.CreateNew("kedi", "Momo", T0);
        save.Stage = GrowthStage.Adult;
        return new PetSimulation(save);
    }

    [Fact]
    public void Ihtiyaclar_iyiyken_uyari_yok()
    {
        var sim = Yetiskin();
        var tracker = new NeedAlertTracker();

        Assert.Null(tracker.Poll(sim, T0));
    }

    [Fact]
    public void Dusuk_ihtiyac_uyari_uretir_ve_ismi_gecer()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = NeedAlertTracker.LowThreshold - 1;

        var alert = new NeedAlertTracker().Poll(sim, T0);

        Assert.NotNull(alert);
        Assert.Equal(NeedKind.Hunger, alert.Kind);
        Assert.Equal(AlertLevel.Low, alert.Level);
        Assert.Contains("Momo", alert.Message);
    }

    [Fact]
    public void Ayni_uyari_tekrar_edilmez()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = 30;
        var tracker = new NeedAlertTracker();

        Assert.NotNull(tracker.Poll(sim, T0));

        // Bekleme süresi geçse bile aynı seviye tekrar duyurulmaz.
        Assert.Null(tracker.Poll(sim, T0 + NeedAlertTracker.Cooldown * 5));
    }

    [Fact]
    public void Durum_kotulesirse_ikinci_kez_uyarilir()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = 30;
        var tracker = new NeedAlertTracker();

        var ilk = tracker.Poll(sim, T0);
        Assert.Equal(AlertLevel.Low, ilk!.Level);

        // Kritik seviyeye düşmek yeni bir uyarıyı hak eder.
        sim.Needs.Hunger = NeedAlertTracker.CriticalThreshold - 1;
        var ikinci = tracker.Poll(sim, T0 + NeedAlertTracker.Cooldown);

        Assert.NotNull(ikinci);
        Assert.Equal(AlertLevel.Critical, ikinci.Level);
    }

    [Fact]
    public void Bekleme_suresi_dolmadan_ikinci_uyari_cikmaz()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = 30;
        sim.Needs.Thirst = 30;
        var tracker = new NeedAlertTracker();

        Assert.NotNull(tracker.Poll(sim, T0));

        // İkinci ihtiyaç da düşük ama hemen arkasından uyarmak rahatsız edici olur.
        Assert.Null(tracker.Poll(sim, T0 + NeedAlertTracker.Cooldown - TimeSpan.FromMinutes(1)));
        Assert.NotNull(tracker.Poll(sim, T0 + NeedAlertTracker.Cooldown));
    }

    [Fact]
    public void Ihtiyac_duzelince_uyari_hakki_yenilenir()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = 30;
        var tracker = new NeedAlertTracker();

        Assert.NotNull(tracker.Poll(sim, T0));

        // Kullanıcı besledi, sonra pet tekrar acıktı: bu yeni bir olaydır.
        sim.Needs.Hunger = NeedAlertTracker.ResetThreshold + 5;
        Assert.Null(tracker.Poll(sim, T0 + NeedAlertTracker.Cooldown));

        sim.Needs.Hunger = 30;
        Assert.NotNull(tracker.Poll(sim, T0 + NeedAlertTracker.Cooldown * 2));
    }

    [Fact]
    public void En_kotu_ihtiyac_once_soylenir()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = 30;
        sim.Needs.Thirst = 5;   // daha kötü

        var alert = new NeedAlertTracker().Poll(sim, T0);

        Assert.Equal(NeedKind.Thirst, alert!.Kind);
    }

    [Fact]
    public void Uyuyan_pet_rahatsiz_edilmez()
    {
        var sim = Yetiskin();
        sim.Needs.Hunger = 5;
        sim.Apply(CareAction.Sleep, T0);

        Assert.True(sim.IsSleeping);
        Assert.Null(new NeedAlertTracker().Poll(sim, T0));
    }

    [Fact]
    public void Yumurta_uyari_uretmez()
    {
        var save = PetSimulation.CreateNew("kedi", "Momo", T0);
        var sim = new PetSimulation(save);
        sim.Needs.Hunger = 0;

        Assert.Equal(GrowthStage.Egg, sim.Stage);
        Assert.Null(new NeedAlertTracker().Poll(sim, T0));
    }

    [Fact]
    public void Uzun_bir_gunde_uyari_sayisi_makul_kaliyor()
    {
        // Gerçek senaryo: kullanıcı hiç ilgilenmiyor, uygulama 24 saat açık.
        // Bütün ihtiyaçlar dibe vuracak. Kaç bildirim alır?
        var sim = Yetiskin();
        var tracker = new NeedAlertTracker();
        var now = T0;
        var count = 0;

        for (var i = 0; i < 24 * 60; i++)   // dakika dakika, 24 saat
        {
            now += TimeSpan.FromMinutes(1);
            sim.Advance(now);
            if (tracker.Poll(sim, now) is not null) count++;
        }

        // 5 ihtiyaç × 2 seviye = en fazla 10. Gün boyu ilgilenilmeyen bir pet
        // için bu makul; kullanıcıyı bildirime boğmuyor.
        Assert.InRange(count, 2, 10);
    }
}
