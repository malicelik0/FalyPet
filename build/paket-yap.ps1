<#
    FalyPet surum paketi uretir.

    Kullanim:
        .\build\paket-yap.ps1 -Surum 1.0.1

    Uretilenler (Releases/ klasorune):
        FalyPet-win-Setup.exe   -> kullanicinin indirecegi kurulum dosyasi
        FalyPet-<surum>-full.nupkg
        FalyPet-<surum>-delta.nupkg   (ikinci surumden itibaren)
        releases.win.json       -> otomatik guncellemenin okudugu dosya

    Sonra: bu dosyalarin HEPSINI ayni GitHub Release'e yukle.
    Kullanicilarin uygulamasi bir sonraki denetimde kendini gunceller.

    NOT: self-contained yayinlaniyor, yani kullanicinin .NET kurmasi gerekmiyor.
    Bedeli kurulum dosyasinin ~70 MB olmasi. Ucretsiz ve teknik olmayan bir
    kitleye dagitilan bir uygulamada "once .NET 9 kur" demek indirmeleri kaybettirir.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Surum,

    [string]$Kanal = "win"
)

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot
$yayinDizini = Join-Path $kok 'publish'
$cikti = Join-Path $kok 'Releases'

if ($Surum -notmatch '^\d+\.\d+\.\d+$') {
    throw "Surum 1.2.3 biciminde olmali. Verilen: $Surum"
}

$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

Write-Host "==> Testler" -ForegroundColor Cyan
dotnet test (Join-Path $kok 'FalyPet.sln') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Testler basarisiz. Paket uretilmedi." }

Write-Host "==> Temizlik" -ForegroundColor Cyan
if (Test-Path $yayinDizini) { Remove-Item $yayinDizini -Recurse -Force }

Write-Host "==> Yayinlama (self-contained, win-x64)" -ForegroundColor Cyan
dotnet publish (Join-Path $kok 'src\FalyPet.App\FalyPet.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -o $yayinDizini --nologo
if ($LASTEXITCODE -ne 0) { throw "Yayinlama basarisiz." }

Write-Host "==> Duman testi (butun pencereler aciliyor)" -ForegroundColor Cyan
# Yayinlanacak ciktinin UZERINDE calisiyor, gelistirme derlemesinde degil:
# gonderilen seyin ta kendisi sinaniyor.
$rapor = Join-Path $env:TEMP "falypet-selftest-$Surum.txt"
$st = Start-Process (Join-Path $yayinDizini 'FalyPet.exe') -ArgumentList '--self-test', $rapor -PassThru -Wait
if ($st.ExitCode -ne 0) {
    if (Test-Path $rapor) { Get-Content $rapor | Write-Host }
    throw "Duman testi basarisiz. Paket uretilmedi."
}
Get-Content $rapor | Select-Object -First 2 | Write-Host

Write-Host "==> Onceki surum (delta uretimi icin)" -ForegroundColor Cyan
# vpk, delta paketini cikti klasorundeki ONCEKI .nupkg ile karsilastirarak uretir.
# O dosya yoksa sessizce yalnizca tam paket cikar ve kullanicilar her guncellemede
# 78 MB indirir. Bu yuzden yayindaki son surumu indirip klasore koyuyoruz.
$mevcut = @(Get-ChildItem $cikti -Filter '*-full.nupkg' -ErrorAction SilentlyContinue)
if ($mevcut.Count -gt 0) {
    Write-Host "    zaten var: $($mevcut[0].Name)"
} else {
    $gh = 'C:\Program Files\GitHub CLI\gh.exe'
    if (Test-Path $gh) {
        & $gh auth status 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            New-Item -ItemType Directory -Force $cikti | Out-Null

            # YALNIZCA en son surumun tam paketi iniyor, hepsi degil.
            # Velopack manifesti klasorde bulunan her tam paketi referansliyor ve
            # release'e hepsinin yuklenmesi gerekiyor. Hepsini indirseydik her
            # surumde yuk 73 MB daha buyurdu (1.0.3'te 300 MB, 1.0.4'te 370 MB...).
            # Delta uretimi icin bir onceki tam paket yeterli.
            $sonEtiket = (& $gh release view --repo malicelik0/FalyPet --json tagName --jq .tagName 2>$null)
            if ($sonEtiket) {
                $sonSurum = $sonEtiket.TrimStart('v')
                & $gh release download --repo malicelik0/FalyPet --pattern "FalyPet-$sonSurum-full.nupkg" --dir $cikti 2>&1 | Out-Null
            }

            $indi = @(Get-ChildItem $cikti -Filter '*-full.nupkg' -ErrorAction SilentlyContinue)
            if ($indi.Count -gt 0) { Write-Host "    indirildi: $($indi[0].Name)" }
            else { Write-Host "    yayinda surum yok - yalnizca tam paket uretilecek" -ForegroundColor Yellow }
        } else {
            Write-Host "    gh girisi yok - delta uretilemeyecek" -ForegroundColor Yellow
        }
    } else {
        Write-Host "    gh kurulu degil - delta uretilemeyecek" -ForegroundColor Yellow
    }
}

Write-Host "==> Velopack paketi" -ForegroundColor Cyan
vpk pack `
    --packId FalyPet `
    --packVersion $Surum `
    --packDir $yayinDizini `
    --mainExe FalyPet.exe `
    --packTitle "FalyPet" `
    --packAuthors "malicelik0" `
    --channel $Kanal `
    --outputDir $cikti
if ($LASTEXITCODE -ne 0) { throw "vpk pack basarisiz." }

Write-Host ""
Write-Host "TAMAM. Cikti: $cikti" -ForegroundColor Green
Get-ChildItem $cikti | ForEach-Object { "  {0,-42} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
Write-Host ""
Write-Host "Sonraki adim:" -ForegroundColor Yellow
Write-Host "  gh release create v$Surum $cikti\* --title `"FalyPet $Surum`" --notes `"...`""
