$ErrorActionPreference = 'Stop'

$BaseUrl = 'https://www.userbus.xyz/downloads/skysurf'

$Apps = @(
    @{ Name = 'Toms';    Asset = 'toms-win-x64.exe';    Exe = 'toms.exe' }
    @{ Name = 'Skysurf'; Asset = 'skysurf-win-x64.exe'; Exe = 'skysurf.exe' }
)

$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\skysurf'
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

foreach ($app in $Apps) {
    $dest = Join-Path $InstallDir $app.Exe
    Write-Host "Downloading $($app.Name)..."
    Invoke-WebRequest -Uri "$BaseUrl/$($app.Asset)" -OutFile $dest
    Write-Host "Installed: $dest"
}

# Add to user PATH if not already present
$userPath = [Environment]::GetEnvironmentVariable('PATH', 'User') ?? ''
if ($userPath -notlike "*$InstallDir*") {
    $newPath = ($userPath.TrimEnd(';') + ";$InstallDir").TrimStart(';')
    [Environment]::SetEnvironmentVariable('PATH', $newPath, 'User')
    Write-Host ""
    Write-Host "Added $InstallDir to your user PATH."
    Write-Host "Restart your terminal, then run:  toms  or  skysurf"
} else {
    Write-Host ""
    Write-Host "Ready. Run:  toms  or  skysurf"
}
