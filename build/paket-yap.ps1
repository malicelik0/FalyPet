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
