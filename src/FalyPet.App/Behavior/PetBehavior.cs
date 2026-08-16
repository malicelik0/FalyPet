using System;
using FalyPet.Core.Model;
using FalyPet.Core.Simulation;

namespace FalyPet.App.Behavior;

/// <summary>
/// Pet'in ekranda ne yaptığına karar verir: hangi animasyon, hangi kare, nereye
/// yürüyor, hangi yöne bakıyor.
///
/// Simülasyondan ayrı tutuluyor. Simülasyon "pet aç mı" sorusunu cevaplar;
/// burası "aç pet ekranda nasıl görünür" sorusunu. Karıştırılsalardı ihtiyaç
/// dengesini değiştirmek yürüme hızını bozardı.
/// </summary>
internal sealed class PetBehavior
{
    /// <summary>Pixel art'ın doğru kare hızı. 60 fps hem yanlış görünür hem boşuna CPU yakar.</summary>
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(125);
    private const int FrameCount = 3;

    private readonly Random _random = new();

    private TimeSpan _frameClock;
    private TimeSpan _restRemaining;
    private TimeSpan _actionRemaining;
    private PetAnimation _actionAnimation;
    private double? _targetX;

    public PetAnimation Animation { get; private set; } = PetAnimation.Idle;
    public int Frame { get; private set; }
    public bool FaceLeft { get; private set; }
    public double X { get; private set; }

    /// <summary>Göz o an kapalı mı.</summary>
    public bool IsBlinking { get; private set; }

    /// <summary>
    /// Göz kırpma KENDİ zamanlayıcısında, animasyon karesinde değil.
    ///
    /// Önce "idle'ın 2. karesinde gözler kapalı" diye kurulmuştu. Ama idle 3 kare
    /// ve 8 fps: 2. kare her 375 milisaniyede bir geliyor, yani pet saniyede ~2.7
    /// kez göz kırpıyordu. İnsan 3-5 saniyede bir kırpar; o hızda pet titriyor
    /// gibi görünüyordu.
    /// </summary>
    private static readonly TimeSpan BlinkDuration = TimeSpan.FromMilliseconds(130);

    private TimeSpan _untilBlink;
    private TimeSpan _blinkRemaining;

    public PetBehavior(double startX)
    {
        X = startX;
        _restRemaining = RandomRest();
        _untilBlink = RandomBlinkGap();
    }

    private TimeSpan RandomBlinkGap() => TimeSpan.FromSeconds(2.5 + _random.NextDouble() * 4.0);

    private void AdvanceBlink(TimeSpan delta)
    {
        if (_blinkRemaining > TimeSpan.Zero)
        {
            _blinkRemaining -= delta;
            IsBlinking = _blinkRemaining > TimeSpan.Zero;
            if (!IsBlinking) _untilBlink = RandomBlinkGap();
            return;
        }

        _untilBlink -= delta;
        if (_untilBlink > TimeSpan.Zero) return;

        _blinkRemaining = BlinkDuration;
        IsBlinking = true;
        BlinkCount++;
    }

    /// <summary>Toplam göz kırpma sayısı — teşhis kaydının hızı ölçebilmesi için.</summary>
    public int BlinkCount { get; private set; }

    /// <summary>
    /// Bir bakım eylemini geçici olarak oynatır (yeme, içme, oyun, yıkanma).
    /// Bittiğinde normal davranışa döner.
    /// </summary>
    public void PlayAction(PetAnimation animation, TimeSpan duration)
    {
        _actionAnimation = animation;
        _actionRemaining = duration;
        _targetX = null;
    }

    /// <param name="cursorOver">
    /// Fare pet'in üstünde mi. Üstündeyse pet DURUR.
    ///
    /// Bu davranış olmadan iki ayrı şikayet doğuyordu ve ikisi de aynı sebepten:
    /// pet imlecin altından yürüyüp kaçıyordu. Kullanıcı (a) "üstüne gelince
    /// solmuyor" diyordu — çünkü solma başlıyor ama pet bir saniyede altından
    /// çıkıyordu; (b) "sürekli kayboluyor" diyordu — çünkü bıraktığı yerde durmuyordu.
    /// Ölçüldü: 12 saniyede 312 piksel gidiyordu.
    ///
    /// Durması ayrıca doğru his: hayvan sana bakınca yürümeye devam etmez.
    /// </param>
    public void Update(TimeSpan delta, PetSimulation sim, double minX, double maxX, bool cursorOver)
    {
        AdvanceFrameClock(delta);
        AdvanceBlink(delta);

        if (_actionRemaining > TimeSpan.Zero)
        {
            _actionRemaining -= delta;
            Animation = _actionAnimation;
            return;
        }

        if (cursorOver)
        {
            Animation = PetAnimation.Idle;
            _targetX = null;
            _restRemaining = RandomRest();   // fare çekilince hemen fırlamasın
            return;
        }

        // Uyuyan, küskün ya da hasta pet gezmez. Hareketsizlik burada bir bilgi:
        // kullanıcı bir şeyin yolunda gitmediğini bakışta anlamalı.
        if (sim.IsSleeping) { Animation = PetAnimation.Sleep; _targetX = null; return; }
        if (sim.Mood == PetMood.Sulking) { Animation = PetAnimation.Sulk; _targetX = null; return; }
        if (sim.Mood == PetMood.Sick) { Animation = PetAnimation.Sick; _targetX = null; return; }

        UpdateWandering(delta, sim, minX, maxX);
    }

    private void AdvanceFrameClock(TimeSpan delta)
    {
        _frameClock += delta;
        while (_frameClock >= FrameDuration)
        {
            _frameClock -= FrameDuration;
            Frame = (Frame + 1) % FrameCount;
        }
    }

    private void UpdateWandering(TimeSpan delta, PetSimulation sim, double minX, double maxX)
    {
        if (_targetX is not { } target)
        {
            Animation = PetAnimation.Idle;
            _restRemaining -= delta;
            if (_restRemaining > TimeSpan.Zero) return;

            _restRemaining = RandomRest();

            // Onda bir ihtimalle ekranın kenarına gidip orada oturur; gerisinde
            // YAKIN bir noktaya gider. Eskiden hedef bütün ekran genişliğinden
            // rastgele seçiliyordu ve pet sürekli bir uçtan öbürüne yürüyordu —
            // kullanıcı onu bıraktığı yerde bulamıyordu.
            if (_random.Next(10) == 0)
            {
                _targetX = _random.Next(2) == 0 ? minX : maxX;
            }
            else
            {
                var menzil = (maxX - minX) * 0.18;
                var hedef = X + (_random.NextDouble() * 2 - 1) * menzil;
                _targetX = Math.Clamp(hedef, minX, maxX);
            }

            return;
        }

        Animation = PetAnimation.Walk;

        var speed = SpeedFor(sim);
        var step = speed * delta.TotalSeconds;
        var distance = target - X;

        if (Math.Abs(distance) <= step)
        {
            X = target;
            _targetX = null;
            Animation = PetAnimation.Idle;
            return;
        }

        FaceLeft = distance < 0;
        X = Math.Clamp(X + Math.Sign(distance) * step, minX, maxX);
    }

    /// <summary>DIP/saniye. Küçük pet daha yavaş, mutlu pet daha hızlı.</summary>
    private static double SpeedFor(PetSimulation sim)
    {
        var baseSpeed = sim.Stage switch
        {
            GrowthStage.Baby => 18.0,
            GrowthStage.Child => 26.0,
            GrowthStage.Teen => 32.0,
            _ => 38.0,
        };

        return sim.Mood == PetMood.Happy ? baseSpeed * 1.15 : baseSpeed;
    }

    /// <summary>
    /// İki gezinme arasındaki bekleme. Üç değer denendi:
    ///   3-10 sn  → pet neredeyse sürekli yürüyordu, kullanıcı onu kaybediyordu
    ///   12-32 sn → çok ölü kaldı, pet donmuş gibi görünüyordu
    ///   8-22 sn  → şimdiki. Ortalama ~15 saniyede bir kısa bir gezinti.
    /// </summary>
    private TimeSpan RandomRest() => TimeSpan.FromSeconds(8 + _random.NextDouble() * 14);

    /// <summary>Sürüklenince gezinme hedefi iptal olur — kullanıcı bıraktığı yerde kalsın.</summary>
    public void OnDragged(double newX)
    {
        X = newX;
        _targetX = null;
        _restRemaining = RandomRest();
    }
}
