# FalyPet

Windows masaüstünde yaşayan, bebeklikten yetişkinliğe büyüyen pixel-art bir sanal evcil hayvan.
Beslersin, su verirsin, oynarsın. Tepside çalışır, kendini otomatik günceller.

**Durum:** Geliştirme aşamasında — Faz 0 (temel) tamam.
Yol haritası ve kararlar: [`mds/01-PLAN.md`](mds/01-PLAN.md) · Son durum: [`mds/00-NEREDE-KALDIK.md`](mds/00-NEREDE-KALDIK.md)

## Gereksinimler

- Windows 10 (19045) veya üzeri
- .NET 9 SDK (geliştirme için)

## Çalıştırma

```
dotnet run --project src/FalyPet.App
```

Kapatmak için tepsi ikonuna sağ tıkla → Çıkış.

## Testler

```
dotnet test FalyPet.sln
```

## Proje yapısı

| Klasör | İçerik |
|---|---|
| `src/FalyPet.Core` | Saf simülasyon — ihtiyaçlar, büyüme, kayıt. UI bağımlılığı yok, test edilebilir. |
| `src/FalyPet.App` | WPF katmanı — pencere, sprite çizimi, tepsi, Win32 interop. |
| `tests/FalyPet.Core.Tests` | Simülasyon ve kayıt testleri. |
| `mds/` | Plan ve devam belgeleri. |

## Kayıt dosyası

`%APPDATA%\FalyPet\save.json` (yedek: `save.json.bak`).
Kurulum klasörüne yazılmaz — otomatik güncelleme o klasörü değiştirir ve kayıt silinirdi.

## Teşhis

Sprite'ları PNG olarak dökmek ve alfa maskesini denetlemek için:

```
dotnet run --project src/FalyPet.App -- --dump-sprite C:\temp\sprite
```
