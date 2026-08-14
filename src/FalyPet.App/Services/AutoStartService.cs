using System;
using Microsoft.Win32;

namespace FalyPet.App.Services;

/// <summary>
/// "Windows ile başlat" ayarı.
///
/// HKEY_CURRENT_USER altındaki Run anahtarı kullanılıyor — yönetici hakkı istemez
/// ve kullanıcı bunu Görev Yöneticisi'nin Başlangıç sekmesinden görüp kapatabilir.
/// Zamanlanmış görev ya da HKLM kullanmak daha "güçlü" olurdu ama kullanıcının
/// kontrolünü elinden alırdı; bir pet uygulaması için yanlış takas.
/// </summary>
internal static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FalyPet";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public static bool TrySet(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path)) return false;

            // Velopack uygulamayı %LocalAppData%\FalyPet\current\ altına kurar ve
            // "current" klasörü sürümler arasında sabit kalır — yani bu yol
            // güncellemeden sonra da geçerli olur.
            key.SetValue(ValueName, $"\"{path}\"");
            return true;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
