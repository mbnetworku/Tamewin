# Main menu: one place to reach every tool in this folder.
#
#   .\Fastyourpc.ps1
#
# Each option runs the matching script, which asks its own questions.
# Option 4 (protected print) needs Administrator; you'll be offered to reopen it as admin.

function Test-Admin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

while ($true) {
    Write-Host ""
    Write-Host "=== Fastyourpc ==="
    Write-Host "  1) Privacy: turn off data sharing with Microsoft"
    Write-Host "  2) Privacy: restore original settings"
    Write-Host "  3) Classic right-click menu: on / off"
    Write-Host "  4) Windows protected print mode: on / off   (needs Administrator)"
    Write-Host "  Q) Quit"
    switch ("$(Read-Host 'Your choice')".Trim()) {
        '1' { & (Join-Path $PSScriptRoot 'Apply-Privacy.ps1') }
        '2' { & (Join-Path $PSScriptRoot 'Revert-Privacy.ps1') }
        '3' { & (Join-Path $PSScriptRoot 'Set-ClassicMenu.ps1') }
        '4' {
            $script = Join-Path $PSScriptRoot 'Set-ProtectedPrint.ps1'
            if (Test-Admin) {
                & $script
            } elseif ("$(Read-Host 'This needs Administrator. Open it in an admin window? (Y/N)')".Trim() -match '^[Yy]') {
                $shell = (Get-Process -Id $PID).Path
                Start-Process $shell -Verb RunAs -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`""
            }
        }
        { $_ -match '^[Qq]?$' } { return }
        default { Write-Host "Unknown choice." }
    }
}
