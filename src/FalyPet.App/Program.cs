using System;
using Velopack;

namespace FalyPet.App;

/// <summary>
/// Uygulamanın gerçek giriş noktası.
///
/// WPF normalde App.xaml'den bir Main üretir, ama Velopack'in ilk satırda
/// çalışması gerekiyor: kurulum, güncelleme ve kaldırma adımlarında uygulama
/// özel argümanlarla çalıştırılıp hızlıca çıkması bekleniyor. Bu çağrı
/// App.OnStartup içindeyken WPF önce kaynakları yüklüyordu ve <c>vpk pack</c>
/// bunun için açıkça uyarı veriyordu:
///
///   "VelopackApp.Run() ... does not look like your application's entry point.
///    It is strongly recommended that you move this to the very beginning of
///    your Main() method."
///
/// csproj'daki StartupObject bu sınıfı işaret ediyor.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().SetArgs(args).Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
