# NEREDE KALDIK

> Her oturuma bu dosyayı okuyarak başla.
> Plan: [01-PLAN.md](01-PLAN.md) · Yayın: [02-YAYIN.md](02-YAYIN.md) · Sanat: [03-SANAT.md](03-SANAT.md)
> Son güncelleme: **15 Ağustos 2026, 09:15**

## Durum: bütün fazlar (0-9) bitti · uygulama kurulabilir ve kendini güncelliyor

Tek eksik: **gerçek sanat** (sprite'lar prosedürel) ve **GitHub'a ilk sürümü
yayınlamak** — ikisi de senin kararını bekliyor, bkz. en alt.

## Doğrulanmış olanlar

| Alan | Nasıl doğrulandı |
|---|---|
| Saydam pencere, alfa tıkla-geç, sürükleme | Win32'den pencere stilleri okunarak |
| Çok monitörlü konumlandırma | 5 senaryo (kayıtlı / uzak / ölü bölge / 2. monitör / kayıt yok) |
| **PerMonitorV2 DPI** | `GetWindowDpiAwarenessContext` ile canlı pencereye sorularak |
| İhtiyaç motoru, offline telafi, büyüme | 51 birim testi |
| **İhtiyaç bildirimleri** | balon ölçüldü: karşılama 0.8-5.7 sn, uyarı 50.2-56.4 sn |
| Bildirim sıklığı makul | test: 24 saat ilgilenilmeyen pet **2-10 bildirim** |
| Denge: dikkatli kullanıcı → yetişkin | ölçüldü: **12 gün** |
| 10 tür × 5 aşama × 9 durum sprite | 110/110 otomatik denetim + kontakt sayfası |
| Gezinme hızı | ölçüldü: ~37.5 DIP/s (spec 38) |
| Bütün pencereler açılıyor | `--self-test`: 6/6 geçti, 450 sprite bileşimi |
| **Kurulum** | Setup.exe çalıştırıldı, `%LocalAppData%\FalyPet\current\` |
| **Kayıt kurulumdan sağ çıkıyor** | MD5 kurulum öncesi/sonrası aynı |
| **Otomatik güncelleme** | 1.0.1 → 1.0.2 uçtan uca: delta indi, çıkışta kuruldu |
| **Kayıt güncellemeden sağ çıkıyor** | pet adı, coin, kostüm korundu |
| Bellek | **44.9 MB** private working set (hedef <80) |
| Boşta CPU | **tek çekirdeğin %0.78'i** (hedef <%1) |

### Paket boyutları
| Dosya | Boyut |
|---|---|
| `FalyPet-win-Setup.exe` | 77.9 MB |
| `FalyPet-1.0.2-full.nupkg` | 73.7 MB |
| **`FalyPet-1.0.2-delta.nupkg`** | **0.15 MB** ← kullanıcı güncellemede bunu indiriyor |

Self-contained yayınlandığı için kullanıcının .NET kurması gerekmiyor; bedeli
ilk indirmenin ~78 MB olması. Güncellemeler delta olduğu için 0.2 MB.

## Doğrulanmamış tek halka

**GitHub üzerinden gerçek güncelleme.** Akışın tamamı yerel klasör kaynağıyla
(`FALYPET_UPDATE_SOURCE`) test edildi ve çalışıyor; sınanmayan tek şey GitHub
transport'u. Onu doğrulamak gerçek bir public release yayınlamayı gerektiriyor —
bu dışa dönük bir işlem olduğu için **senin onayın olmadan yapılmadı.**

## Senin kararını bekleyen iki şey

### 1. İlk sürümü yayınla
```powershell
cd C:\Users\QuarteX\Documents\FalyPet
.\build\paket-yap.ps1 -Surum 1.0.0
gh release create v1.0.0 Releases\* --title "FalyPet 1.0.0" --notes "İlk sürüm"
```
`malicelik0/FalyPet` reposunun **public** olması gerekiyor (istemciye token
gömmemek için). Ayrıntı: [02-YAYIN.md](02-YAYIN.md)

### 2. Gerçek sanat
Sprite'lar şu an prosedürel. `Assets\sprites\` altına dosya koydukça gerçek sanat
devreye giriyor — kod değişikliği yok, tür tür eklenebiliyor.
Şartname: [03-SANAT.md](03-SANAT.md). En yüksek getirili ilk adım: 10 türün
`adult_idle` ve `baby_idle` sprite'ları (~60 kare).

## Komutlar

```powershell
dotnet run --project src/FalyPet.App                              # çalıştır
dotnet test FalyPet.sln                                           # 51 test
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\s   # sprite denetimi
dotnet run --project src/FalyPet.App -- --self-test C:\temp\r.txt # pencere duman testi
.\build\paket-yap.ps1 -Surum 1.0.3                                # sürüm paketi
```

Paketleme betiği önce testleri, sonra duman testini çalıştırıyor; ikisi de
geçmeden paket üretmiyor.

**Paketleme sürerken başka `dotnet` komutu çalıştırma.** Ayrıca gerekirse
`$env:MSBUILDDISABLENODEREUSE = "1"` kullan.

## Alınmış tasarım kararları (değiştirmeden önce 01-PLAN.md oku)

1. **Ölüm yok** — hasta → küskün, iyileşme her zaman mümkün
2. **Büyüme bakım EYLEMLERİNDEN** gelir, geçen süreden değil
3. **Türü kullanıcı seçer**, 10 tür
4. **Güncelleme uygulamayı yeniden başlatmaz** — çıkışta kurulur
5. **Dükkan yalnızca kalıcı aksesuar satar**
6. **Mini oyunda ceza yok**, coin'i günlük tavanlı (bakımın alternatifi değil takviyesi)
7. **Bildirimde üç fren var** — her ihtiyaç/seviye bir kez, hak ancak ihtiyaç
   gerçekten düzelince yenilenir, aralarında 10 dakika bekleme. Hiç uyarmazsa pet
   unutulur ve büyüme durur; fazla uyarırsa 7/24 açık uygulama silinir.

## Yol boyunca bulunup düzeltilen hatalar

1. `double.NaN` kayıt sentineli — `System.Text.Json` NaN'ı serileştiremiyor, çıkışta çökme
2. Ekran dışı kurtarma pet'i monitörsüz ölü bölgeye koyuyordu (2560×1440 + 1920×1080'de gerçek)
3. Küskünlük kilitlenmesi — çıkış mutluluk istiyordu ama küskünken mutluluğu yükseltecek eylem yoktu
4. 110 sprite'ın 48'i kareyi taşıyordu (kulak yükseklikleri, zemin kaydırma, blob dokunaçları)
5. Tepside auto-start tikini programatik tazelemek kayıt defterine gereksiz yazma tetikliyordu
6. Paketlemenin ilk iki denemesi takıldı: bozuk NuGet önbellek girdisi (0 baytlık geçici
   dosya, gece süreç öldürüldüğünde kalmış) + 70 MB runtime paketinin indirme zaman aşımı
