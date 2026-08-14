# NEREDE KALDIK

> Her oturuma bu dosyayı okuyarak başla. Plan: [01-PLAN.md](01-PLAN.md)
> Son güncelleme: **15 Ağustos 2026**

## Durum: Faz 0 BİTTİ ✅ — sırada Faz 1 (sprite motoru)

## Faz 0'da ne yapıldı

Çözüm iskeleti: `FalyPet.Core` (saf simülasyon) + `FalyPet.App` (WPF) + `FalyPet.Core.Tests` (xunit).

Çalışan özellikler:
- Saydam, her zaman üstte, görev çubuğunda ve Alt+Tab'da görünmeyen 160×160 pencere
- 32×32 pixel-art yumurta placeholder, `NearestNeighbor` ile 5 kat büyütülmüş (bulanık değil)
- **Alfaya dayalı tıkla-geç** — pet'in çevresindeki boşlukta tıklama alttaki pencereye geçer
- Sürükle-bırak; tıklama ile sürükleme ayrı (tıklayınca pet eziliyor-toparlanıyor)
- Konumun kalıcılığı + **çok monitörlü güvenli konumlandırma**
- Tepsi ikonu (çalışma anında çiziliyor), Göster/Gizle + Çıkış menüsü, çift tıkla göster/gizle
- Tek örnek kilidi (iki FalyPet aynı kaydı ezemez)
- Tam ekran oyun algılama — oyun açılınca pet gizleniyor, çıkınca geri geliyor
- Atomik kayıt yazma + otomatik yedek rotasyonu (`save.json` / `save.json.bak`)

## Faz 0'da bulunan ve düzeltilen iki gerçek hata

**1. `double.NaN` kayıt sentineli çökmeye yol açıyordu.**
"Henüz konumlanmadı" işaretçisi olarak `double.NaN` kullanılmıştı; `System.Text.Json`
NaN'ı serileştiremeyip fırlatıyor. Pencere konumlanmadan kapanan bir oturumda uygulama
çıkışta çöküyordu. Sentinel `double?` yapıldı. Regresyon testi:
`Hic_konumlanmamis_pencere_kaydedilebilir`.

**2. Ekran dışı konum kurtarma pet'i ölü bölgeye koyuyordu.**
Kısıtlama sanal masaüstünün sınırlayıcı kutusuna göre yapılıyordu. Bu makinede
2560×1440 + 1920×1080 düzeninde kutu 4480×1440 çıkıyor ama `(4448,1408)` noktası
**hiçbir monitörde yok** — pet oraya konunca tamamen kayboluyordu. Artık
`Screen.FromRectangle` ile en yakın gerçek monitörün çalışma alanına çekiliyor.

## Nasıl çalıştırılır

```
dotnet run --project src/FalyPet.App
```

Sprite'ları denetlemek için (pencere açmadan PNG + alfa raporu üretir):

```
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\sprite
```

Testler:

```
dotnet test FalyPet.sln
```

## Doğrulanan ölçümler (15 Ağu 2026)

- Testler: **11/11 geçiyor**
- Derleme: 0 uyarı, 0 hata
- Pencere: 160×160, `WS_EX_TOPMOST` ✓, `WS_EX_TOOLWINDOW` ✓, `WS_EX_TRANSPARENT` imleç
  pet üstünde değilken açık ✓
- Konumlandırma 5 senaryoda da gerçek ekranda kalıyor (kayıtlı / uzak / ölü bölge /
  2. monitör / kayıt yok)
- Alfa maskesi: 4 köşe saydam, merkez opak, %39.1 dolu

## Faz 0'da BİLEREK yapılmayanlar

- **Per-monitor DPI (PerMonitorV2)**: uygulama şu an sistem DPI'ına duyarlı. Bu makinede
  ölçek %100 olduğu için fark etmiyor, ama farklı ölçekli iki monitörde pet kayar.
  Faz 1'de gezinme gelirken `app.manifest` ile açılacak.
- **Gerçek ikon dosyası**: tepsi ikonu ve pet placeholder kodla çiziliyor. Faz 8'de değişecek.
- **Windows ile başlat**: Faz 7.

## Sıradaki iş — Faz 1

1. Sprite sheet yükleyici (`PlaceholderSprite` yerine) + kare zamanlaması 8-12 fps
2. Animasyon durum makinesi: idle / yürüme / uyuma
3. Serbest gezinme + ekran kenarına oturma, 30 fps pozisyon güncellemesi
4. PerMonitorV2 DPI + çok monitörlü gezinme
5. Faz 1 doğrulaması: pet ekranda yürüyor, kenara oturuyor, 2. monitörde bozulmuyor

## Açık kararlar (senin onayın bekliyor)

`01-PLAN.md` bölüm 3'te ayrıntılı:
1. **Ölüm var mı?** → Önerilen: hayır, yerine hasta/küskün kademeleri
2. **Büyüme neye bağlı?** → Önerilen: duvar saatine değil, iyi bakılan süreye
3. **v1'de kaç tür pet?** → Önerilen: 3 tür, gerisi içerik güncellemesiyle
