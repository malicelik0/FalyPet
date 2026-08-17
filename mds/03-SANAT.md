# Sanat Şartnamesi

> Son güncelleme: 17 Ağustos 2026
> **Pixel art bırakıldı, vektör çizgi stiline geçildi.**

---

## Neden vektör

1. **Pixel art beğenilmedi.**
2. **Rekabet:** en büyük rakip [Pets Therapy](https://pets-therapy.com/) 100+ pixel
   pet sunuyor. Aynı stilde yarışmak bizi kalabalığın içinde kaybeder; çizgi stili
   ilk bakışta ayırt edilir yapıyor.
3. **Boyut ayarı serbest kaldı.** Pixel art'ta ölçek tam sayı katı olmak zorundaydı
   (yoksa pikseller eşitsiz genişlikte çıkıyordu). Vektörde her boyut aynı netlikte.

## Şu anki durum: her şey kodda çiziliyor

`VectorPetRenderer` pet'leri WPF vektör çizimiyle üretiyor. Hiçbir görsel dosyası yok.

- **Çizim uzayı:** 100×100 normalize, istenen piksel boyutunda çiziliyor
- **Üretim boyutu:** 256×256 (`SpriteCache.RenderSize`), aşağı ölçekleniyor
- **Zemin çizgisi:** y = 92 · **Tavan:** y = 6 · **Merkez:** x = 50
- **Kontur:** 3.2 birim, yuvarlak uçlu, siyah değil — gövde renginin koyulaştırılmışı

Bir türün görünümü `SpeciesCatalog`'daki **tek satırdan** çıkıyor:

```csharp
new("ugurbocegi", "Uğur Böceği", BodyShape.Round, EarType.Antennae,
    TailType.None, MarkingType.LadybugShell, 0xD9342B, 0x241E22),
```

| Alan | Seçenekler |
|---|---|
| Gövde | `Round` `Tall` `Wide` `Blob` |
| Kulak | `None` `Pointed` `Floppy` `Round` `Horns` `Tufts` `Antennae` |
| Kuyruk | `None` `Thin` `Bushy` `Curl` `Tentacle` |
| Desen | `None` `Belly` `Stripes` `Spots` `Patch` `LadybugShell` |

**Yeni tür eklemek = bir satır veri.** Yeni bir görünüm parçası gerekiyorsa
(ör. kanat, boynuz çeşidi) enum'a bir değer ve `VectorPetRenderer`'a bir `case`.

## Denetim

```powershell
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\sprite
```

`tum-turler.png`: bütün türlerin bütün aşama ve durumları tek sayfada.
Rapor her sprite'ı otomatik denetliyor — boş mu, kareyi dolduruyor mu, kenara
taşıyor mu. Sanata dokunduğunda buraya bak.

---

## Elle çizilmiş sanat koymak istersen

Motor hâlâ diskten sprite okuyabiliyor; koyduğun dosya vektör çizimin yerine geçer.

### Klasör düzeni
`Assets\sprites\<tür>\<aşama>_<durum>.png` — yatay şerit, kare boyutu = yükseklik.

**Tür kimlikleri:** `kedi kopek tavsan ejderha jole baykus tilki panda ahtapot hayalet ugurbocegi`
**Aşamalar:** `egg baby child teen adult` · **Durumlar:** `idle walk sleep eat drink play wash sick sulk`

Yumurta istisnası: `egg.png`, kareler animasyon değil **okşama seviyesi**.

### Kurallar
- PNG, şeffaf zeminli
- **256×256 önerilir** (motorun ürettiği boyut). Daha küçük de olur, ölçeklenir.
- Durum başına **3 kare**
- Eksik durum aynı aşamanın `idle`'ına düşer; hiç dosya yoksa vektör devreye girer
- Zemin çizgisi görselin **%92** yüksekliğinde, merkez yatayda ortada

### Kostümler
Kostüm takılıyken motor **vektör çizime döner.** Sebebi: şapkanın kafanın tam olarak
nerede olduğunu bilmesi gerekiyor ve her sanatçının çizimi farklı oturur — sessizce
kayık bir şapka göstermektense tutarlı vektör hâli gösteriliyor.

Kostümlü hâlleri de çizmek istersen dosya adına ekle:
`adult_idle_sapka.png` (bu yol henüz kodlanmadı, gerekirse eklenir).

## Öncelik sırası

Elle sanat yapılacaksa en yüksek getirili sıra:

1. `adult_idle` — 11 tür. En çok görülen kare.
2. `baby_idle` — 11 tür. Tür seçim ekranında görünüyor.
3. `adult_walk`, `baby_walk`
4. `egg.png`
5. `child_idle`, `teen_idle` + yürüyüşleri
6. Geri kalan durumlar

İlk üç adım (~66 kare) uygulamanın görünümünün %90'ını değiştirir.
