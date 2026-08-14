using System;
using System.Runtime.InteropServices;

namespace FalyPet.App.Interop;

/// <summary>Faz 0'da gereken Win32 çağrıları: pencere stilleri, imleç konumu, tam ekran algılama.</summary>
internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    /// <summary>Pencere fare olaylarını almaz, tıklamalar altındaki pencereye geçer.</summary>
    public const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>Alt+Tab listesinde görünmez. Pet bir "uygulama penceresi" gibi davranmamalı.</summary>
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // GetWindowLongPtr yalnızca 64-bit Windows'ta dışa aktarılır; 32-bit'te GetWindowLong'a
    // karşılık gelir. Tek bir isimle çağırıp burada ayırıyoruz.
    public static int GetWindowLongEx(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? (int)GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    public static void SetWindowLongEx(IntPtr hWnd, int nIndex, int value)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, nIndex, new IntPtr(value));
        else SetWindowLong32(hWnd, nIndex, value);
    }

    /// <summary>
    /// Kullanıcının o an "rahatsız edilmemesi gereken" bir durumda olup olmadığını söyler.
    /// Tam ekran oyun ve sunum modu bu yolla yakalanır.
    /// </summary>
    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out UserNotificationState state);

    public enum UserNotificationState
    {
        NotPresent = 1,
        Busy = 2,
        /// <summary>Tam ekran bir D3D uygulaması çalışıyor — yani oyun.</summary>
        RunningD3dFullScreen = 3,
        PresentationMode = 4,
        AcceptsNotifications = 5,
        QuietTime = 6,
        RunningWindowsStoreApp = 7,
    }
}
