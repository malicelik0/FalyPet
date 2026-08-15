# Sanat Şartnamesi

> Gerçek sprite'ları üretecek olan (sen, bir sanatçı ya da AI) bu belgeye uyar.
> Son güncelleme: 15 Ağustos 2026

## Kural: sanat opsiyoneldir

Motor sprite'ları **prosedürel üretiyor**. Bu klasöre dosya koydukça gerçek sanat
devreye giriyor, koymadıkça prosedürel devam ediyor. Yani:

- 10 türü birden bitirmek zorunda değilsin. Bir tür ekle, o tür gerçek olur.
- Bir türün her durumunu çizmek zorunda değilsin. Eksik durum aynı aşamanın
  `idle`'ına düşer.
- Hiç çizmezsen oyun yine çalışır.

Kod değişikliği gerekmez; dosyayı koy, uygulamayı aç.

## Klasör düzeni

Uygulamanın yanındaki `Assets\sprites\` altında:

```
Assets\sprites\
  kedi\
    egg.png            4 kare  (0,1,2,3 çatlak)
    baby_idle.png      3 kare
    baby_walk.png      3 kare
    adult_idle.png     3 kare
    adult_sleep.png    3 kare
    ...
  ejderha\
    ...
```

**Tür kimlikleri:** `kedi kopek tavsan ejderha jole baykus tilki panda ahtapot hayalet`

**Aşama adları:** `egg baby child teen adult`

**Durum adları:** `idle walk sleep eat drink play wash sick sulk`

**Dosya adı biçimi:** `<aşama>_<durum>.png` — yumurta hariç, o sadece `egg.png`.

## Dosya biçimi

- **PNG**, şeffaf zeminli (alfa kanalı zorunlu)
- **Yatay şerit**: kareler yan yana
- **Kare boyutu = görselin yüksekliği**. Genişlik bunun tam katı olmalı.
  `96x32` = 3 kare, her biri 32x32.
- **32x32 önerilir.** 64x64 de çalışır (otomatik ölçeklenir) ama pet penceresi
  tam sayı katıyla büyüttüğü için 32'nin katları en net sonucu verir.
- **Anti-aliasing yok.** Yarı saydam kenar hem pixel-art stilini bozar hem de
  tıkla-geç maskesini bulanıklaştırır — pet'in etrafındaki boşluk tıklanabilir
  hale gelir ve kullanıcının işini engeller.

## Kare sayısı

Durum başına **3 kare, 8 fps**. Daha fazlası çalışır (motor şeritteki kare
sayısını okur) ama pixel art'ta 3 kare zaten doğru his ve maliyeti en düşük yer.

Yumurta bir istisna: `egg.png` **4 kare** olmalı ve kareler animasyon değil
**çatlak seviyesidir** — 0, 1, 2, 3 çatlak.

## Yerleşim kuralları

Bunlara uymayan sprite oyunda kayık görünür:

| Kural | Değer |
|---|---|
| Zemin çizgisi (ayakların bastığı yer) | y = **29** |
| Üst pay (hiçbir şey buranın üstüne çıkmasın) | y = **1** |
| Yatay merkez | x = **16** |
| Kenarlar | sol/sağ/üst/alt kenar pikselleri **şeffaf** olmalı |

32x32 dışında bir boyut kullanıyorsan bu değerleri orantıla (64x64 için zemin y=58).

### Kafa merkezi — kostümler için

Kullanıcı dükkandan şapka/taç aldığında aksesuar **sanatın üstüne bindirilir**;
yani kostüm varyantlarını çizmen gerekmez. Ama aksesuarın doğru yere oturması
için kafa merkezinin motorun beklediği yerde olması gerekir.

Beklenen kafa merkezi ve yarıçapı `PetSpriteFactory.GetHeadAnchor` tarafından
hesaplanıyor. Pratikte:

| Aşama | Kafa merkezi (y) | Kafa yarıçapı |
|---|---|---|
| baby | ~8-10 | ~6 |
| child | ~8 | ~6 |
| teen | ~7 | ~5.8 |
| adult | ~6-7 | ~6.2 |

Kesin değerleri görmek için mevcut prosedürel sprite'ları referans al:

```powershell
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\referans
```

Bu komut bütün türlerin bütün aşama ve durumlarını bir kontakt sayfasına basar.
**Yeni sanatı bunun üstüne çiz** — oranlar ve zemin çizgisi zaten doğru.

## Denetim

Sanatı koyduktan sonra aynı komutu tekrar çalıştır. Rapor her sprite'ı otomatik
denetler:

- Tamamen boş mu?
- Kareyi neredeyse tamamen dolduruyor mu (şeffaf kenar yok)?
- Üst/alt kenara taşıyor mu?

İlk prosedürel üretimde 110 sprite'ın 48'i bu denetimden kalmıştı. Gözle bakarak
o hataların çoğu kaçar; rapora bak.

## Ne çizilmeli — öncelik sırası

Bütün türlerin bütün durumlarını çizmek ~750 kare. Sıra şu olmalı:

1. **`adult_idle`** — 10 tür. En çok görülen kare. Tek başına uygulamanın
   görünümünü değiştirir.
2. **`baby_idle`** — 10 tür. Onboarding'de tür seçim kartlarında görünüyor.
3. **`adult_walk`, `baby_walk`** — hareket en çok fark edilen ikinci şey.
4. **`egg.png`** — 10 tür (ya da hepsi için tek ortak yumurta).
5. **`child_idle`, `teen_idle`** + yürüyüşleri.
6. Geri kalan durumlar: `sleep sick sulk eat drink play wash`.

İlk üç adım (~60 kare) uygulamanın %90'ının görünümünü gerçek sanata çevirir.
