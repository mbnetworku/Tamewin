# Switches the right-click menu between the old classic menu and the Windows 11 menu.
# Current user only (HKCU), no admin needed.
#
#   .\Set-ClassicMenu.ps1                  # menu: shows current state, choose on/off
#   .\Set-ClassicMenu.ps1 -On              # classic menu (no "Show more options" step)
#   .\Set-ClassicMenu.ps1 -Off             # back to the Windows 11 menu
#   .\Set-ClassicMenu.ps1 -Status          # just show which one is active
# Add -RestartExplorer to apply right away (closes open File Explorer windows).
param(
    [switch]$On, [switch]$Off, [switch]$Status, [switch]$RestartExplorer,
    [string]$KeyPath = 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}'
)
$inproc = Join-Path $KeyPath 'InprocServer32'

function Get-ClassicMenuState {
    if (Test-Path $inproc) { 'ON  - classic right-click menu' } else { 'OFF - Windows 11 right-click menu' }
}

if (-not ($On -or $Off -or $Status)) {
    Write-Host ""
    Write-Host "Classic right-click menu is currently: $(Get-ClassicMenuState)"
    Write-Host "  1) Turn ON  (old classic menu)"
    Write-Host "  2) Turn OFF (Windows 11 menu)"
    Write-Host "  Q) Quit"
    switch ("$(Read-Host 'Your choice')".Trim()) {
        '1'     { $On = $true }
        '2'     { $Off = $true }
        default { "No changes made."; return }
    }
    if ("$(Read-Host 'Restart Explorer now to apply? Open File Explorer windows will close. (Y/N)')".Trim() -match '^[Yy]') {
        $RestartExplorer = $true
    }
}

if ($Status) { "Classic right-click menu: $(Get-ClassicMenuState)"; return }

if ($On) {
    # An InprocServer32 key with an empty default value makes Windows fall back to the classic menu.
    New-Item -Path $inproc -Value '' -Force | Out-Null
    "Classic right-click menu turned ON."
} elseif ($Off) {
    if (Test-Path $KeyPath) { Remove-Item -Path $KeyPath -Recurse -Force }
    "Classic right-click menu turned OFF (Windows 11 menu)."
}

if ($RestartExplorer) {
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
    "Explorer restarted."
} else {
    "Restart Explorer, sign out, or restart the PC to see the change."
}
