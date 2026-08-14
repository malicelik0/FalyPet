# Yayın ve Otomatik Güncelleme

> Bu belge "yeni sürümü nasıl çıkarırım" sorusunun tek cevabı.
> Son güncelleme: 15 Ağustos 2026

## Nasıl çalışıyor

```
sen                          GitHub                      kullanıcı
 |                              |                             |
 | build\paket-yap.ps1 1.0.1    |                             |
 |----> Releases\ klasörü       |                             |
 |                              |                             |
 | gh release create v1.0.1 --->| Release v1.0.1              |
 |                              |    Setup.exe                |
 |                              |    *.nupkg                  |
 |                              |    releases.win.json        |
 |                              |                             |
 |                              |<--- 6 saatte bir denetler --|
 |                              |---- delta paketi ---------->|
 |                              |                             |
 |                              |     kullanıcı çıkınca kurulur
```

**Kilit tasarım kararı:** güncelleme bulununca uygulama kendini yeniden başlatmaz.
FalyPet 7/24 açık duran bir uygulama; çalışırken kapanıp açılması kullanıcının
pet'ini ortadan kaybetmesi demektir. Güncelleme sessizce indirilir ve kullanıcı
uygulamadan **zaten çıktığında** kurulur. Kullanıcı hiçbir kesinti yaşamaz.

## Yeni sürüm çıkarma

### 1. Sürüm numarasını belirle
`MAJOR.MINOR.PATCH`. Kayıt şeması değiştiyse (`SaveData.CurrentVersion` arttıysa)
en az MINOR artır ve göç kodunu yazdığından emin ol.

### 2. Paketi üret
```powershell
.\build\paket-yap.ps1 -Surum 1.0.1
```
Betik önce **testleri çalıştırır**; testler kırmızıysa paket üretmez. Bu kasıtlı:
bozuk bir sürümü otomatik güncellemeyle binlerce makineye göndermek, elle
dağıtmaktan çok daha pahalı bir hatadır.

### 3. GitHub Release'e yükle
```bash
gh release create v1.0.1 Releases/* --title "FalyPet 1.0.1" --notes "Değişiklikler..."
```
**`Releases/` içindeki dosyaların hepsini yükle.** `releases.win.json` eksik
kalırsa güncelleme sessizce çalışmaz — hata da vermez, sadece hiç güncelleme
bulamaz.

### 4. Bitti
Kullanıcıların uygulaması 6 saat içinde (ya da bir sonraki açılışta) yeni sürümü
görür, indirir ve çıkışta kurar.

## İndirme sayfası

Kullanıcılar `FalyPet-win-Setup.exe` dosyasını GitHub Releases sayfasından
indirir. Repo: `https://github.com/malicelik0/FalyPet`

Sayfaya şu notu koy:

> **Windows uyarı verirse:** FalyPet imzalı bir uygulama değil, bu yüzden Windows
> "Bilinmeyen yayıncı" uyarısı gösterebilir. **Daha fazla bilgi → Yine de çalıştır**
> diyerek kurabilirsin.

### Neden imzasız
Microsoft'un ucuz çözümü **Azure Artifact Signing** ($9.99/ay, Nisan 2026'da genel
kullanıma açıldı) bireysel geliştiriciler için yalnızca **ABD ve Kanada'ya**,
kurumlar için ABD/Kanada/AB/İngiltere'ye açık. Türkiye ikisinde de yok.
Klasik OV sertifikası ~$200-400/yıl ve 2023'ten beri donanım token'ı zorunlu.

İmzalasan bile SmartScreen itibarı indirme sayısıyla birikir — sertifika anında
güven vermez. Uygulama tutarsa bu karar tekrar değerlendirilir.

## Repo düzeni

Kaynak kod özel repo olabilir, ama **sürümlerin dağıtıldığı repo public olmalı**.
Sebebi: `GithubSource` özel repoya erişmek için token ister ve o token istemciye
gömülmek zorunda kalır — yani zaten herkese açık olur.

Şu an ikisi de aynı repo (`malicelik0/FalyPet`). Kaynağı kapatmak istersen
`UpdateService.ReleasesRepository` sabitini ayrı bir public repoya çevir.

## Sürüm numarası nereden geliyor

Velopack sürümü paketleme sırasında `--packVersion` ile gömer.
Uygulama içinde `UpdateManager.CurrentVersion` ile okunur ve tepsi ikonunun
ipucu metninde görünür. Kurulu olmayan (geliştirme) çalıştırmalarda
"geliştirme" yazar ve güncelleme denetimi devre dışı kalır.

## Sık karşılaşılacak sorunlar

| Belirti | Sebep |
|---|---|
| Güncelleme hiç bulunamıyor | `releases.win.json` Release'e yüklenmemiş |
| "Yalnızca kurulu sürümde" | Uygulama Setup.exe ile değil, klasörden çalıştırılıyor — beklenen davranış |
| Güncelleme indi ama kurulmadı | Kurulum çıkışta yapılır; uygulamayı tepsiden kapat |
| Kurulumdan sonra pet kayboldu | OLMAMALI. Kayıt `%APPDATA%\FalyPet\` altında, kurulum klasöründe değil. Olursa hata bildir. |
