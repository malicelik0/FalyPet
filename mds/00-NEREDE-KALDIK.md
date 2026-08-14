# NEREDE KALDIK

> Her oturuma bu dosyayı okuyarak başla.
> Plan: [01-PLAN.md](01-PLAN.md) · Yayın: [02-YAYIN.md](02-YAYIN.md)
> Son güncelleme: **15 Ağustos 2026, 01:15**

## Durum: Faz 0-5 ve 9'un kodu bitti · Faz 6-8 yapılmadı

Uygulama **oynanabilir**: tür seçiyorsun, yumurta çatlıyor, pet geziniyor,
besleniyor, büyüyor, coin kazanıp dükkandan kostüm alıyor.

## Çalışır durumda olanlar

| Alan | Durum |
|---|---|
| Saydam always-on-top pencere, alfa tıkla-geç, sürükleme | ✅ ölçülerek doğrulandı |
| Tepsi ikonu, tek örnek kilidi, tam ekran oyun algılama | ✅ |
| Çok monitörlü güvenli konumlandırma | ✅ 5 senaryo test edildi |
| İhtiyaç motoru, 8 saat tavanlı offline telafi | ✅ 39 test |
| Bakım puanıyla büyüme (yumurta→bebek→çocuk→genç→yetişkin) | ✅ dikkatli kullanıcı 12 günde yetişkin |
| Hasta / küskün kademeleri, kilitlenme yok | ✅ regresyon testli |
| 10 tür prosedürel pixel-art sprite, 5 aşama, 9 durum | ✅ 110/110 otomatik denetim |
| Gezinme, kenara oturma, 8 fps animasyon | ✅ ölçüldü (~37.5 DIP/s) |
| Onboarding (tür seçimi + isim) | ✅ |
| Sağ tık menüsü, bakım eylemleri, konuşma balonu, durum | ✅ |
| Coin ekonomisi + kostüm dükkanı (5 aksesuar) | ✅ kod bitti, elle denenmedi |
| Velopack otomatik güncelleme + Windows ile başlat | ⚠️ kod bitti, **paketleme doğrulanmadı** |

## HEMEN SIRADAKİ İŞ — paketlemeyi doğrula

`build\paket-yap.ps1` uçtan uca **hiç çalıştırılmadı**. Self-contained yayın
sürerken süreçler durduruldu, `Releases/` çıktısı üretilmedi.

```powershell
cd C:\Users\QuarteX\Documents\FalyPet
.\build\paket-yap.ps1 -Surum 1.0.0
```

**Önemli:** bu çalışırken başka `dotnet build` çalıştırma — `obj/` üzerinde
çakışıyor, ilk denemede takılmasının sebebi buydu.

Doğrulanacaklar:
1. `Releases\FalyPet-win-Setup.exe` üretildi mi, boyutu ne?
2. Setup çalıştırılınca `%LocalAppData%\FalyPet\current\` altına kuruluyor mu?
3. Kurulu sürüm açılınca tepside sürüm numarası görünüyor mu (geliştirme değil)?
4. `%APPDATA%\FalyPet\save.json` kurulumdan sonra da duruyor mu? (kritik)
5. 1.0.1 paketleyip GitHub Release'e atınca kurulu sürüm kendini güncelliyor mu?

## Yapılmayanlar

- **Faz 6 — mini oyunlar**: hiç başlanmadı. Coin şu an sadece bakımdan geliyor.
- **Faz 7 — cila**: ses efekti yok, ayarlar penceresi yok, kayıt yedekleme
  arayüzü yok. "Windows ile başlat" var (tepsi menüsünde).
- **Faz 8 — gerçek sanat**: sprite'lar prosedürel. Yerine gerçek sprite sheet
  koymak için `SpriteCache.Get` içine dosyadan okuma eklenmeli; prosedürel
  üretim yedek olarak kalmalı (bir türün sanatı eksikse oyun yine çalışsın).
- **Per-monitor DPI (PerMonitorV2)**: uygulama sistem DPI'ına duyarlı. Bu
  makinede iki monitör de %100 ölçekte olduğu için fark etmiyor; farklı
  ölçekli monitörde pet kayar. `app.manifest` ile açılacak.

## Bilinen sapma: RAM

Plandaki hedef boşta **< 80 MB**. Ölçülen: **119.6 MB** (Debug derlemesi,
pet + balon penceresi + sprite önbelleği). Release derlemesinde ölçüm
yapılmadı. Faz 7'de ele alınacak; gerekirse sprite önbelleği aşamaya göre
budanır (kullanıcı aynı anda tek aşamada).

## Alınmış tasarım kararları (değiştirmeden önce 01-PLAN.md oku)

1. **Ölüm yok** — hasta → küskün, iyileşme her zaman mümkün
2. **Büyüme bakım EYLEMLERİNDEN** gelir, geçen süreden değil
3. **Türü kullanıcı seçer**, 10 tür
4. **Güncelleme uygulamayı yeniden başlatmaz** — çıkışta kurulur
5. **Dükkan yalnızca kalıcı aksesuar satar**, tüketilebilir yiyecek yok

## Komutlar

```powershell
dotnet run --project src/FalyPet.App          # çalıştır
dotnet test FalyPet.sln                       # 39 test
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\s   # sprite denetimi
.\build\paket-yap.ps1 -Surum 1.0.0            # sürüm paketi
```

## Yol boyunca bulunup düzeltilen hatalar

1. `double.NaN` kayıt sentineli — `System.Text.Json` NaN'ı serileştiremiyor, çıkışta çökme
2. Ekran dışı kurtarma pet'i monitörsüz ölü bölgeye koyuyordu (2560×1440 + 1920×1080'de gerçek)
3. Küskünlük kilitlenmesi — çıkış mutluluk istiyordu ama küskünken mutluluğu yükseltecek eylem yoktu
4. 110 sprite'ın 48'i kareyi taşıyordu (kulak yükseklikleri, zemin kaydırma, blob dokunaçları)
