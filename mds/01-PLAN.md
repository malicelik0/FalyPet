# FalyPet — A'dan Z'ye Plan

> Son güncelleme: 14 Ağustos 2026
> Bu belge projenin anayasasıdır. Karar değişirse önce burası güncellenir, sonra kod.

---

## 1. Ürün tek cümlede

Windows masaüstünde yaşayan, bebeklikten yetişkinliğe büyüyen, beslenip su verilen ve
oynanan bir pixel-art sanal evcil hayvan. Tepside çalışır, Windows ile açılır, kendini
otomatik günceller.

## 2. Alınmış kararlar (14 Ağu 2026)

| Konu | Karar | Gerekçe |
|---|---|---|
| Teknoloji | .NET 9 + WPF (`net9.0-windows`) | SDK zaten kurulu. 7/24 açık kalacak bir uygulama için en düşük RAM/CPU. Electron ~200MB RAM ile bu türde en çok şikayet edilen konu. |
| Sanat stili | Pixel art | AI üretiminde kareler arası tutarlılığı en kolay korunan stil; elle düzeltmesi kolay, dosyası küçük. |
| Sanat kaynağı | AI ile üretim | Özgün karakter + lisans sorunu yok. |
| Para modeli | Tamamen ücretsiz | Lisans anahtarı, doğrulama sunucusu, vergi süreci yok. İlk sürümü en hızlı çıkaran yol. |
| Dağıtım | Kendi kanalı, Store yok | GitHub Releases + Velopack ile otomatik güncelleme. Sunucu maliyeti sıfır. |
| Davranış | Serbest gezer, kenara oturur | Canlılık ile rahatsız etmeme arasındaki denge. |
| Kapsam (v1) | İhtiyaçlar + büyüme + ekonomi + dükkan + 1-2 mini oyun | Uygulamayı "her gün açılan" seviyeye çıkaran eşik. |

## 3. Oyun tasarımı kararları (15 Ağu 2026'da kilitlendi)

### 3.1 Ölüm YOK — üç kademeli ceza
```
İhtiyaçlar dibe vurur → HASTA (yeşil surat, yavaş hareket, oyun oynamaz)
      ↓ (ilgilenilmezse)
KÜSKÜN (köşeye çekilir, sırtını döner, tepki vermez)
      ↓ (ilgilenilirse, her aşamadan)
İYİLEŞME her zaman mümkün
```
Bakımsızlığın gerçek bedeli **büyümenin durması**.

### 3.2 Büyüme = bakım EYLEMLERİ (geçen süre değil)

Pet, uygulama açık durduğu için büyümez. **Yalnızca gerçek bir ihtiyacı karşıladığında**
büyür. Bu, "orada olmayı" değil "ilgilenmeyi" ödüllendirir.

Her bakım eylemi **Bakım Puanı (BP)** verir — ama yalnızca o ihtiyaç gerçekten
düşükken. Tok pete yemek vermek 0 BP'dir.

| Eylem | Koşul | BP |
|---|---|---|
| Besle | açlık < %60 | +3 |
| Su ver | susuzluk < %60 | +3 |
| Oyna | mutluluk < %70 | +5 |
| Yıka | temizlik < %50 | +4 |
| Uyut | enerji < %30, uyku tamamlanınca | +6 |
| Okşa | — (günde en fazla 5 kez) | +1 |

| Aşama geçişi | Gereken BP | Dikkatli kullanımda |
|---|---|---|
| 🥚 Yumurta → Bebek | 3 çatlak (~10 dk + okşama) | ilk oturum |
| Bebek → Yavru | 60 BP | ~1.5 gün |
| Yavru → Genç | 150 BP | ~4 gün |
| Genç → Yetişkin | 300 BP | ~7 gün |

Toplam ~510 BP ≈ **2 hafta**.

**Spam koruması kendiliğinden var:** BP yalnızca ihtiyaç düşükken verilir ve
ihtiyaçlar sadece zamanla düşer. Yani hızlandırmak mümkün değil — dakikada 20 kez
beslemek 3 BP'den fazla getirmez.

**Kapalıyken:** büyüme işlemez (işleseydi yine duvar saati olurdu), ihtiyaçlar
azalır (en fazla 8 saatlik tavanla). Asimetri kasıtlı: *ilgilenmediğinde kaybedersin,
sadece yanındayken kazanırsın.*

**Okunabilirlik şartı:** büyüme durduğunda kullanıcı bunu görmeli. Sessizce duran
ilerleme "uygulama bozuk" demektir. Pet'in üstünde uyarı çıkacak:
*"Büyüme durdu — Momo çok aç."*

### 3.3 Türü kullanıcı seçer, en az 10 tür

Onboarding'de türler gösterilir, kullanıcı seçer ve isim verir.

**Bu karar sanat maliyetini projenin en büyük riski yapıyor** ve mimari buna göre
kurulacak. Düz yaklaşımla 10 tür × 4 aşama × 8 durum × 4 kare = **1280 kare** — bu
sayı projeyi öldürür. Çözüm, sprite'ları katmanlı üretmek:

| Katman | Paylaşım | Maliyet |
|---|---|---|
| 🥚 Yumurta | **Tek set**, tür rengine göre boyanır | ~8 kare, hepsi için |
| Bebek | **Tek siluet**, tür rengi + tek aksan (kulak/boynuz) | ~24 kare + 10 küçük overlay |
| Yavru / Genç / Yetişkin | Türe özel | 3 aşama × ~24 kare = 72/tür |

Toplam: ~8 + 24 + (10 × 72) ≈ **750 kare**, ve bunun 720'si tek bir üretim hattından
çıkıyor. Kare sayısı da düşürüldü: pixel art'ta durum başına 4 değil **3 kare, 8 fps**
zaten doğru his.

**Gerçek darboğaz çizmek değil, tutarlılık.** AI ile 750 kare üretmek ucuz; kareler
arasında aynı karakteri tutturmak zor. Bu yüzden hat şöyle olacak:
1. Tür başına **tek karakter sayfası** (character sheet) üretilir ve kilitlenir
2. Her durum sheet'i o sayfadan türetilir
3. Her sprite `--dump-sprite` ile otomatik denetlenir (boyut, şeffaf kenar, palet)

**Sıralama:** motor önce 2 tür ile tam çalışır hale gelir; kalan 8 tür içerik olarak
eklenir. Otomatik güncelteme altyapısı zaten bunun için seçildi.

---

## 4. Mimari

```
FalyPet.sln
├── src/FalyPet.Core/          ← Saf simülasyon. UI bağımlılığı YOK. Test edilebilir.
│   ├── Model/                    Pet, İhtiyaçlar, Büyüme aşaması, Envanter
│   ├── Simulation/               İhtiyaç azalması, offline hesap, büyüme, durum makinesi
│   ├── Content/                  Tür/yemek/eşya tanımları (JSON'dan yüklenir)
│   └── Persistence/              Kayıt dosyası (atomik yazma)
│
├── src/FalyPet.App/           ← WPF. Sadece görüntüleme ve giriş.
│   ├── Interop/                  Win32 P/Invoke (saydamlık, tıkla-geç, tam ekran algılama)
│   ├── Rendering/                Sprite sheet motoru, animasyon zamanlayıcı
│   ├── Windows/                  PetWindow, MenuWindow, ShopWindow, OnboardingWindow
│   └── Services/                 Tepsi ikonu, otomatik başlangıç, güncelleme, ses
│
└── tests/FalyPet.Core.Tests/  ← Simülasyon mantığının testleri
```

**Neden Core ayrı:** İhtiyaç azalması, offline telafi ve büyüme mantığı hataların
saklandığı yerdir ve gözle test edilemez ("3 gün bekleyip bakalım" diye test olmaz).
UI'dan ayrılınca saniyeler içinde 30 günlük simülasyon koşturulabilir.

### 4.1 Pencere tekniği
`WindowStyle=None` + `AllowsTransparency=True` + `Background=Transparent`.

- **Bilinen bedel:** WPF bu modda pencereyi donanım hızlandırmasız (yazılımsal) çizer.
  256×256 bir sprite için maliyeti önemsiz — ama pencereyi büyütmemek bir kural.
- **Pixel art şartı:** `RenderOptions.BitmapScalingMode="NearestNeighbor"`. Bu olmazsa
  WPF pixel art'ı bulanıklaştırır ve tüm sanat çöpe gider.
- **Kare hızı:** Sprite karesi **8-12 fps** (pixel art'ın doğru hissi zaten budur, ayrıca
  ucuz), pozisyon hareketi 30 fps.
- **Tıkla-geç:** Fare hareketinde imlecin altındaki pikselin alfa değeri okunur; şeffafsa
  `WS_EX_TRANSPARENT` açılır, pete denk geliyorsa kapatılır. Böylece pet'in etrafındaki
  boşluk çalışmanı engellemez.

### 4.2 Zaman ve offline telafi — projenin en riskli mantığı
```
elapsed = now - lastSaved
elapsed < 0        → 0 kabul et   (kullanıcı saati geri aldı; ne cezalandır ne ödüllendir)
elapsed > 8 saat   → 8 saate kırp (tatilden dönünce pet ölmüş olmasın)
```
Bu üç satır uygulamanın kaderini belirler ve **birim testi olmadan yazılmayacak.**

### 4.3 Kayıt yeri — Velopack tuzağı
Kayıt dosyası **`%APPDATA%\FalyPet\save.json`** olacak, **asla exe'nin yanına değil.**
Velopack uygulamayı `%LocalAppData%\FalyPet\current\` altına kurar ve her güncellemede
bu klasörü değiştirir — exe yanına yazılan kayıt ilk güncellemede silinir.

Yazma **atomik** olacak (önce `.tmp`, sonra yer değiştirme). Pet uygulaması sık kayıt
yapar; yazma ortasında elektrik kesintisi kayıt dosyasını bozmamalı.

### 4.4 Oyun modu (senin için zorunlu)
`shell32.dll → SHQueryUserNotificationState()` 5 saniyede bir sorgulanır.
`QUNS_RUNNING_D3D_FULL_SCREEN` veya `QUNS_PRESENTATION_MODE` dönerse pet gizlenir.
Ucuz, güvenilir ve tam ekran oyunda pet'in üste çıkmasını engeller.

---

## 5. Fazlar

Her fazın sonunda **gözle görülür, çalışan bir şey** olur. "Bitti" demek, o fazın
doğrulama kutusunun geçtiğini görmek demektir.

| Faz | İçerik | Doğrulama |
|---|---|---|
| **0. Temel** | Saydam always-on-top pencere, sürükleme, tepsi ikonu, alfa tıkla-geç, atomik kayıt, oyun modu | Masaüstünde bir şekil görünüyor, sürükleniyor, boşluğuna tıklayınca alttaki pencere tepki veriyor, kapatıp açınca aynı yerde |
| **1. Simülasyon** | İhtiyaç azalması, offline telafi, Bakım Puanı, büyüme aşamaları, hasta/küskün | 30 günlük hızlandırılmış simülasyon testi geçiyor; offline testleri geçiyor |
| **2. Sprite motoru** | Sprite sheet yükleme, animasyon durum makinesi, serbest gezinme, kenara oturma, çoklu monitör + DPI | Pet ekranda yürüyor, duruyor, kenara oturuyor; ikinci monitörde bozulmuyor |
| **3. Etkileşim** | Sağ tık menüsü, besle/su/oyna/uyut/yıka/okşa, konuşma balonu, durum paneli | Her eylem pet'in durumunu ve animasyonunu değiştiriyor |
| **4. Onboarding** | Yumurta → çıkma, tür seçimi, isim verme | Temiz kurulumda akış baştan sona çalışıyor |
| **5. Ekonomi** | Coin, dükkan, yemek çeşitleri, kostüm, envanter | Satın alınan kostüm pet'in üstünde görünüyor ve kayıtta kalıyor |
| **6. Mini oyunlar** | 2 oyun (top yakalama + hafıza), coin ödülü | Oyunlar oynanıyor, coin hesaba geçiyor |
| **7. Cila** | Ses efektleri, tepsi bildirimleri, ayarlar penceresi, Windows ile başlat, kayıt yedekleme | Ayarlardaki her seçenek gerçekten bir şey yapıyor |
| **8. Sanat** | 3 tür × 4 aşama × ~8 durum sprite üretimi | Her sprite oyunda görüntüleniyor, hiçbiri bulanık değil |
| **9. Yayın** | Velopack paketleme, GitHub Releases, indirme sayfası, otomatik güncelleme testi | Eski sürüm kurulu bir makine, yeni release'i kendi kendine indirip güncelliyor |

**Faz 8 (sanat) diğerlerine paraleldir** — motor placeholder ile ilerlerken sanat
arka planda birikir. Faz 8'in bitmesini beklemek projeyi kilitler.

---

## 6. Bilinen riskler

| Risk | Etki | Önlem |
|---|---|---|
| **Sanat üretimi tıkanır** | Proje ölür — bu türde en sık ölüm sebebi | Placeholder ile tam çalışan motor. Sanat hiç gelmese bile uygulama çalışır durumda kalır. |
| **SmartScreen "Bilinmeyen yayıncı"** | İndirmelerin bir kısmı kaybedilir | İmzasız başlanıyor (Azure Artifact Signing Türkiye'ye kapalı, bkz. 7.2). İndirme sayfasına açıklama notu. |
| **Boşta CPU tüketimi** | Kullanıcı "bilgisayarımı yavaşlatıyor" der ve siler | Sprite 8-12 fps, pencere küçük, pet uyurken zamanlayıcı yavaşlar. Faz 7'de Görev Yöneticisi ile ölçülecek, hedef: boşta < %1 CPU, < 80MB RAM. |
| **WPF saydamlık + yazılımsal çizim** | Büyük pencerede takılma | Pencereyi 256×256 üstüne çıkarmama kuralı. |
| **Çoklu monitör + farklı DPI** | Pet yanlış yere konumlanır veya kaybolur | Faz 1'de ele alınacak. WinForms `Screen` fiziksel piksel, WPF DIP kullanır — dönüşüm şart. |

## 7. Yayın altyapısı

### 7.1 Otomatik güncelleme — Velopack
[velopack/velopack](https://github.com/velopack/velopack) (repo 9 Ağu 2026'da güncel).
`GithubSource` ile GitHub Releases'i doğrudan güncelleme kaynağı olarak okur, delta
güncelleme yapar, kurulum dosyasını tek komutla üretir.

**Akış:** `vpk pack` → GitHub Release → kullanıcının uygulaması sonraki açılışta günceller.

**Repo düzeni:** Kaynak kod özel repo kalabilir; sürümler **ayrı bir public repo**dan
dağıtılır (istemciye token gömmemek için).

### 7.2 Kod imzalama — şimdilik yok
Microsoft'un ucuz çözümü Azure Artifact Signing ($9.99/ay, Nisan 2026'da GA) bireysel
geliştiriciler için **yalnızca ABD ve Kanada'ya**, kurumlar için ABD/Kanada/AB/İngiltere'ye
açık — Türkiye ikisinde de yok. Klasik OV sertifikası ~$200-400/yıl + donanım token'ı.

**Karar:** İmzasız başla. Not: imzalasan bile SmartScreen itibarı indirme sayısıyla birikir,
sertifika anında güven vermez. Paketleme fazına gelince bu bilgi tekrar doğrulanacak.
