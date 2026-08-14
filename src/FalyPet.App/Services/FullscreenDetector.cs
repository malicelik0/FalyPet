using System;
using System.Windows.Threading;
using FalyPet.App.Interop;

namespace FalyPet.App.Services;

/// <summary>
/// Kullanıcı tam ekran oyun oynarken veya sunum yaparken pet'in üste çıkmasını engeller.
///
/// Pencere numaralandırıp "acaba tam ekran mı" diye tahmin etmek yerine Windows'un kendi
/// cevabı sorulur: SHQueryUserNotificationState. Beş saniyede bir tek çağrı — ölçülebilir
/// bir CPU maliyeti yok.
/// </summary>
internal sealed class FullscreenDetector : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _lastValue;

    /// <summary>Durum değiştiğinde tetiklenir. true = pet gizlenmeli.</summary>
    public event EventHandler<bool>? ShouldHideChanged;

    public bool ShouldHide => _lastValue;

    public FullscreenDetector()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        Poll();
        _timer.Start();
    }

    private void Poll()
    {
        var shouldHide = Query();
        if (shouldHide == _lastValue) return;

        _lastValue = shouldHide;
        ShouldHideChanged?.Invoke(this, shouldHide);
    }

    private static bool Query()
    {
        try
        {
            var hr = NativeMethods.SHQueryUserNotificationState(out var state);
            if (hr != 0) return false; // Sorgulanamadıysa pet'i gizleme — varsayılan görünür olmak.

            return state is NativeMethods.UserNotificationState.RunningD3dFullScreen
                or NativeMethods.UserNotificationState.PresentationMode;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    public void Dispose() => _timer.Stop();
}
