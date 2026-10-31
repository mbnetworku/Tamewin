# Builds dist\Fastyourpc.exe from the source in src\ and prints its SHA-256 hash.
# Needs the .NET SDK (https://dotnet.microsoft.com/download). The EXE itself needs nothing extra:
# it uses .NET Framework 4.8, which is built into Windows 10 and 11.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\Fastyourpc\Fastyourpc.csproj'
$dist    = Join-Path $PSScriptRoot 'dist'
$work    = Join-Path $PSScriptRoot 'src\Fastyourpc\bin\out'

dotnet build $project -c Release -o $work --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

New-Item -ItemType Directory -Path $dist -Force | Out-Null
Copy-Item (Join-Path $work 'Fastyourpc.exe') $dist -Force

$exe  = Join-Path $dist 'Fastyourpc.exe'
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
Set-Content -Path (Join-Path $dist 'Fastyourpc.exe.sha256') -Value "$hash  Fastyourpc.exe"
""
"Built:   $exe  ($([math]::Round((Get-Item $exe).Length / 1KB)) KB)"
"SHA-256: $hash"
