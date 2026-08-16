# Sıfırdan Kurulum — Hiçbir Şey Bilmeyene

> Bilgisayarında hiçbir şey yokmuş gibi anlatılmıştır.
> Yazıldığı tarih: 16 Ağustos 2026. Sürüm numaraları o günkü hâlleridir.

---

# BÖLÜM A — Sadece FalyPet'i kullanmak istiyorsan

Geliştirici değilsen sana sadece bu bölüm lazım. 2 dakika.

### A1. İndir
https://github.com/malicelik0/FalyPet/releases/latest adresine git,
**`FalyPet-win-Setup.exe`** dosyasını indir. (~78 MB)

### A2. Çalıştır
Dosyaya çift tıkla.

**Windows mavi bir uyarı ekranı çıkarırsa** — çıkaracak — panik yapma:
1. **"Daha fazla bilgi"** yazısına tıkla
2. Altta beliren **"Yine de çalıştır"** düğmesine bas

> Bu uyarının sebebi: FalyPet dijital sertifikayla imzalanmadı. Sertifika
> yılda birkaç yüz dolar ve Türkiye'den bireysel geliştiriciye Microsoft'un ucuz
> seçeneği kapalı. Uygulamada zararlı bir şey olduğu anlamına gelmiyor;
> Windows "bu yayıncıyı tanımıyorum" diyor.

### A3. Bitti
- Kurulum kendiliğinden yapılır, ekstra soru sormaz
- **Masaüstüne** ve **Başlat menüsüne** kısayol koyar
- İlk açılışta tür seçim ekranı gelir: yumurtanı seç, isim ver

### A4. Kullanırken
| İstediğin | Yapman gereken |
|---|---|
| Pet'i sevmek | Üstüne **tıkla** |
| Yer değiştirmek | **Sürükle** |
| Beslemek, oynamak, dükkan | Üstüne **sağ tıkla** |
| Gizlemek / ayarlar / çıkmak | Saat yanındaki **tepsi ikonuna sağ tıkla** |

Güncellemeler kendiliğinden iner ve sen uygulamadan çıkınca kurulur.
Seni hiç kesmez.

### A5. Kaldırmak
Windows Ayarlar → Uygulamalar → FalyPet → Kaldır.

---

# BÖLÜM B — Sıfır bilgisayarda geliştirmeye başlamak

Yeni bir Windows makinesine oturdun, elinde hiçbir şey yok. Adım adım.

## B0. Önce şunu anla: neyi neden kuruyoruz

| Araç | Ne işe yarar |
|---|---|
| **winget** | Windows'un paket yöneticisi. Program indirip kurmayı tek komuta indirir. Windows 10/11 ile zaten gelir. |
| **Git** | Kodun sürüm geçmişini tutan sistem. "Şu değişikliği geri al", "dün ne yapmıştım" bunun işi. |
| **.NET 9 SDK** | FalyPet'in yazıldığı platform. Kodu çalıştıran ve derleyen şey. **SDK** derleyebilen sürüm, **Runtime** sadece çalıştırabilen. Bize SDK lazım. |
| **GitHub CLI (`gh`)** | GitHub'la terminalden konuşmayı sağlar. Sürüm yayınlamak için. |
| **VS Code** | Kod yazma programı. Zorunlu değil ama olmadan zor. |

**Terminal** = komut yazdığın siyah pencere. Windows'ta **PowerShell**.
Açmak için: Başlat'a `powershell` yaz, Enter.

## B1. Araçları kur

PowerShell'i aç ve şunları sırayla çalıştır. Her biri birkaç dakika sürer.

```powershell
winget install Git.Git
```

```powershell
winget install Microsoft.DotNet.SDK.9
```

```powershell
winget install GitHub.cli
```

```powershell
winget install Microsoft.VisualStudioCode
```

**Sonra PowerShell'i KAPAT ve YENİDEN AÇ.** Bu şart — yeni kurulan programlar
ancak yeni bir terminalde tanınır. (Buna "PATH'in tazelenmesi" denir.)

### Kurulumu doğrula
```powershell
git --version; dotnet --version; gh --version
```
Üçü de sürüm numarası yazdırmalı. Biri "tanınmıyor" derse o kurulmamış demektir.

## B2. Kodu indir

```powershell
cd $env:USERPROFILE\Documents
```

```powershell
git clone https://github.com/malicelik0/FalyPet.git
```

```powershell
cd FalyPet
```

> `clone` = GitHub'daki kodun bir kopyasını bilgisayarına indirir.
> Artık `Documents\FalyPet` klasöründe her şey var.

## B3. Çalıştır

```powershell
dotnet run --project src/FalyPet.App
```

İlk sefer 1-2 dakika sürer (bağımlılıkları indirir), sonrakiler saniyeler.
Pet ekranın sağ altında belirir.

**Kapatmak için:** tepsi ikonuna sağ tıkla → Çıkış.
(Terminaldeki `Ctrl+C` de çalışır ama düzgün kapanış değil.)

## B4. Bir değişiklik yap ve gör

VS Code'da klasörü aç:
```powershell
code .
```

Deneme olarak `src/FalyPet.Core/Simulation/SimulationRules.cs` dosyasını aç,
şu satırı bul:

```csharp
public const int EggCracksRequired = 3;
```

`3` yerine `1` yaz, kaydet, sonra:
```powershell
dotnet run --project src/FalyPet.App
```

Artık yumurta tek okşamada çatlıyor. Geri almak için:
```powershell
git checkout src/FalyPet.Core/Simulation/SimulationRules.cs
```

> Oyunun bütün ayarlanabilir sayıları o tek dosyadadır. Denge değiştirmek
> için başka yere dokunmana gerek yok.

## B5. Testleri çalıştır

```powershell
dotnet test FalyPet.sln
```

51 testin hepsi geçmeli. **Bir şeyi bozup bozmadığını anlamanın en hızlı yolu bu.**
Kırmızı gördüğün an geri al.

## B6. Sprite'lara bak

```powershell
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\sprite
```

`C:\temp\sprite\tum-turler.png` dosyasını aç: 10 türün bütün hâlleri tek
sayfada. Sanata dokunduğunda buraya bak.

## B7. Pencerelerin hepsini sına

```powershell
dotnet run --project src/FalyPet.App -- --self-test C:\temp\rapor.txt
```

Bütün pencereleri açıp kapatır. "HEPSI GECTI" yazmalı.

## B8. Yeni sürüm yayınla

### Bir kez: GitHub'a giriş
```powershell
gh auth login
```
Sorulara: **GitHub.com** → **HTTPS** → **Yes** → **Login with a web browser**.
Ekranda 8 haneli kod çıkar, Enter'a bas, tarayıcıda kodu yapıştır, onayla.

### Her sürümde: paketi üret
```powershell
.\build\paket-yap.ps1 -Surum 1.0.1
```

Bu betik sırayla: testleri koşar → yayınlar → bütün pencereleri sınar →
paketi üretir. **Herhangi biri kırmızıysa paket üretmez.** Bozuk bir sürümü
otomatik güncellemeyle herkese göndermek, elle dağıtmaktan çok daha pahalıdır.

Çıktı `Releases\` klasörüne düşer.

### Sonra: GitHub'a yükle
```powershell
gh release create v1.0.1 Releases\* --title "FalyPet 1.0.1" --notes "Ne degisti"
```

⚠️ `Releases\*` yıldızı önemli — **6 dosyanın hepsi** gitmeli.
`releases.win.json` eksik kalırsa otomatik güncelleme sessizce hiç çalışmaz,
hata bile vermez.

### Sürüm numarası nasıl seçilir
`1.0.1` gibi üç parçalı: **BÜYÜK.ORTA.KÜÇÜK**
- Küçük düzeltme → son rakamı artır (1.0.0 → 1.0.1)
- Yeni özellik → ortadakini artır (1.0.1 → 1.1.0)
- Her şeyi değiştiren büyük sürüm → ilkini (1.1.0 → 2.0.0)

Numara **hep artmalı**. Geri gitmek güncellemeyi bozar.

---

# BÖLÜM C — Nerede ne var

## Proje klasörleri
| Klasör | İçinde ne var |
|---|---|
| `src/FalyPet.Core/` | Oyunun **beyni**: ihtiyaçlar, büyüme, kayıt. Ekranla ilgisi yok, o yüzden test edilebiliyor. |
| `src/FalyPet.App/` | Oyunun **yüzü**: pencere, çizim, tepsi ikonu, sesler. |
| `tests/` | Testler. |
| `mds/` | Bu belgeler. |
| `build/` | Sürüm paketleme betiği. |
| `Releases/` | Üretilen kurulum dosyaları. Git'e girmez. |

## Bilgisayarındaki yerler
| Ne | Nerede |
|---|---|
| **Pet'in kaydı** | `%APPDATA%\FalyPet\save.json` |
| Kaydın yedeği | `%APPDATA%\FalyPet\save.json.bak` |
| Kurulu uygulama | `%LOCALAPPDATA%\FalyPet\current\` |

Adres çubuğuna `%APPDATA%\FalyPet` yazıp Enter'a basarsan klasör açılır.

> **Kayıt neden orada:** kurulum klasörü her güncellemede değişiyor.
> Kayıt oraya yazılsaydı ilk güncellemede pet'in silinirdi.

---

# BÖLÜM D — Takılırsan

| Belirti | Sebebi ve çözümü |
|---|---|
| `dotnet` tanınmıyor | Terminali kapatıp yeniden aç. Olmazsa .NET SDK kurulmamış. |
| `dotnet run` takılıp kalıyor | Aynı anda başka bir derleme çalışıyordur. Hepsini kapat, `$env:MSBUILDDISABLENODEREUSE = "1"` yazıp tekrar dene. |
| Pet açılmıyor, hata da yok | Zaten açıktır. Aynı anda tek FalyPet çalışabilir. Tepsi ikonuna bak. |
| Pet kayboldu | Tepsi ikonu → Göster. Yine yoksa pencere ekran dışına düşmüştür; ayarlardan pet'i sıfırla. |
| Testler kırmızı | Son değişikliğini geri al: `git checkout .` — bu **kaydedilmemiş her şeyi** siler, dikkat. |
| Güncelleme çıkmıyor | Sadece **kurulu** sürümde çalışır. `dotnet run` ile açılanda çalışmaz, normaldir. |
| Yayınladım ama güncelleme gelmiyor | `releases.win.json` release'e yüklenmemiştir. GitHub'da release'e bak, 6 dosya olmalı. |

## Yanlışlıkla bir şey bozarsan

Son kaydedilmiş hâle dön (**kaydedilmemiş değişiklikler gider**):
```powershell
git checkout .
```

Ne değiştirdiğini gör:
```powershell
git status
```

Geçmişi gör:
```powershell
git log --oneline
```
