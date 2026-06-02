<#
.SYNOPSIS
    Builds release binaries for Toms and Skysurf and stages them for upload.

.DESCRIPTION
    Publishes each app as a self-contained, single-file executable and copies
    the results into release/<rid>/ with the asset names the install scripts
    expect.

    Trimming is not enabled since Terminal.Gui uses reflection and trimming can
    break the TUI at runtime.

.EXAMPLE
    ./build-release.ps1
    ./build-release.ps1 -Rids win-x64,linux-x64,osx-arm64
#>
param(
    [string[]] $Rids = @('win-x64')
)

$ErrorActionPreference = 'Stop'

$projects = @(
    @{ Proj = 'src/Toms/Toms.csproj';       Win = 'toms.exe';    Nix = 'toms';    Stem = 'toms' }
    @{ Proj = 'src/Skysurf/Skysurf.csproj'; Win = 'Skysurf.exe'; Nix = 'Skysurf'; Stem = 'skysurf' }
)

foreach ($rid in $Rids) {
    $outDir = Join-Path $PSScriptRoot "release/$rid"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    foreach ($p in $projects) {
        Write-Host "Publishing $($p.Stem) for $rid..." -ForegroundColor Cyan
        $pubDir = Join-Path $PSScriptRoot "publish/$rid/$($p.Stem)"
        $projPath = Join-Path $PSScriptRoot $p.Proj

        dotnet publish $projPath `
            -r $rid `
            -c Release `
            --self-contained `
            -p:PublishSingleFile=true `
            -o $pubDir

        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $($p.Proj) ($rid)" }

        # On Windows the apphost has a .exe; on Linux/macOS it has no extension.
        $isWin = $rid -like 'win-*'
        $builtName = if ($isWin) { $p.Win } else { $p.Nix }
        $assetName = if ($isWin) { "$($p.Stem)-$rid.exe" } else { "$($p.Stem)-$rid" }

        Copy-Item (Join-Path $pubDir $builtName) (Join-Path $outDir $assetName) -Force
    }

    Write-Host "`nStaged assets for $rid in $outDir" -ForegroundColor Green
    Get-ChildItem $outDir | Select-Object Name, @{ n = 'SizeMB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
}
