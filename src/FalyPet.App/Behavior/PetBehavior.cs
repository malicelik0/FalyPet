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

    public PetBehavior(double startX)
    {
        X = startX;
        _restRemaining = RandomRest();
    }

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

    public void Update(TimeSpan delta, PetSimulation sim, double minX, double maxX)
    {
        AdvanceFrameClock(delta);

        if (_actionRemaining > TimeSpan.Zero)
        {
            _actionRemaining -= delta;
            Animation = _actionAnimation;
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

            // Beşte bir ihtimalle ekranın kenarına gidip orada oturur. Sürekli
            // ortalarda dolaşan bir pet dikkat dağıtıcı olur; kenar onun "evi".
            _targetX = _random.Next(5) == 0
                ? (_random.Next(2) == 0 ? minX : maxX)
                : minX + _random.NextDouble() * (maxX - minX);

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

    private TimeSpan RandomRest() => TimeSpan.FromSeconds(3 + _random.NextDouble() * 7);

    /// <summary>Sürüklenince gezinme hedefi iptal olur — kullanıcı bıraktığı yerde kalsın.</summary>
    public void OnDragged(double newX)
    {
        X = newX;
        _targetX = null;
        _restRemaining = RandomRest();
    }
}
