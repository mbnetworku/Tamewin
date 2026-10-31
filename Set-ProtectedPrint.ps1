# Turns Windows protected print mode (WPP) on or off, using the Group Policy registry values.
# Needs "Run as Administrator" for -On / -Off / -Default. Restart the PC afterwards.
#
#   .\Set-ProtectedPrint.ps1               # menu: shows current state, choose
#   .\Set-ProtectedPrint.ps1 -Status       # show current policy values
#   .\Set-ProtectedPrint.ps1 -On           # force protected print ON
#   .\Set-ProtectedPrint.ps1 -Off          # force protected print OFF
#   .\Set-ProtectedPrint.ps1 -Default      # remove the policy; the Settings app decides again
#
# WARNING: turning it ON removes printers that aren't Mopria-compatible (they need re-adding after turning it off).
# The registry values come from community guides, not official Microsoft docs. If -Off doesn't take
# effect, use option 4 in the menu (Settings > Bluetooth & devices > Printers & scanners).
param(
    [switch]$On, [switch]$Off, [switch]$Default, [switch]$Status,
    [string]$KeyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\Printers\WPP'
)
$valueNames = 'EnabledBy', 'WindowsProtectedPrintGroupPolicyState', 'WindowsProtectedPrintMode', 'WindowsProtectedPrintOobeConfigComplete'

function Get-ProtectedPrintState {
    $v = Get-ItemProperty -Path $KeyPath -ErrorAction SilentlyContinue
    if (-not $v -or $null -eq $v.WindowsProtectedPrintMode) { return 'NOT SET by policy - the Settings app toggle decides' }
    if ($v.WindowsProtectedPrintMode -eq 1) { 'ON  (forced by policy)' } else { 'OFF (forced by policy)' }
}

function Test-Admin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not ($On -or $Off -or $Default -or $Status)) {
    Write-Host ""
    Write-Host "Windows protected print mode is currently: $(Get-ProtectedPrintState)"
    Write-Host "  1) Turn ON   (removes printers that aren't Mopria-compatible)"
    Write-Host "  2) Turn OFF"
    Write-Host "  3) Default   (remove the policy, let the Settings app decide)"
    Write-Host "  4) Open printer settings (turn it on/off by hand)"
    Write-Host "  Q) Quit"
    switch ("$(Read-Host 'Your choice')".Trim()) {
        '1' {
            if ("$(Read-Host 'Printers that are not Mopria-compatible will be removed. Continue? (Y/N)')".Trim() -notmatch '^[Yy]') {
                "No changes made."; return
            }
            $On = $true
        }
        '2' { $Off = $true }
        '3' { $Default = $true }
        '4' { Start-Process 'ms-settings:printers'; "Opened Settings > Printers & scanners. Look for 'Windows protected print mode'."; return }
        default { "No changes made."; return }
    }
}

if ($Status) {
    "Windows protected print mode: $(Get-ProtectedPrintState)"
    Get-ItemProperty -Path $KeyPath -ErrorAction SilentlyContinue | Select-Object $valueNames | Format-List
    return
}

if ($KeyPath -like 'HKLM:*' -and -not (Test-Admin)) {
    "This needs Administrator. Right-click PowerShell > 'Run as administrator', then run this script again."
    return
}

if ($On) {
    if (-not (Test-Path $KeyPath)) { New-Item -Path $KeyPath -Force | Out-Null }
    Set-ItemProperty -Path $KeyPath -Name EnabledBy -Value 2 -Type DWord
    Set-ItemProperty -Path $KeyPath -Name WindowsProtectedPrintGroupPolicyState -Value 1 -Type DWord
    Set-ItemProperty -Path $KeyPath -Name WindowsProtectedPrintMode -Value 1 -Type DWord
    Set-ItemProperty -Path $KeyPath -Name WindowsProtectedPrintOobeConfigComplete -Value 1 -Type DWord
    "Windows protected print mode turned ON."
} elseif ($Off) {
    if (-not (Test-Path $KeyPath)) { New-Item -Path $KeyPath -Force | Out-Null }
    Set-ItemProperty -Path $KeyPath -Name WindowsProtectedPrintMode -Value 0 -Type DWord
    foreach ($n in 'EnabledBy', 'WindowsProtectedPrintGroupPolicyState', 'WindowsProtectedPrintOobeConfigComplete') {
        Remove-ItemProperty -Path $KeyPath -Name $n -ErrorAction SilentlyContinue
    }
    "Windows protected print mode turned OFF."
} elseif ($Default) {
    foreach ($n in $valueNames) { Remove-ItemProperty -Path $KeyPath -Name $n -ErrorAction SilentlyContinue }
    "Policy removed. Windows protected print mode is now controlled by the Settings app."
}
"Restart the PC for the change to take effect."
