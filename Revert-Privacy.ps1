# Puts back the settings that were there before Apply-Privacy.ps1 first ran.
# Settings that did not exist before are removed (Windows then uses its default).
#
#   .\Revert-Privacy.ps1                       # menu: choose what to restore
#   .\Revert-Privacy.ps1 -All                  # restore everything, no questions
#   .\Revert-Privacy.ps1 -Group Office         # only these groups
param([string[]]$Group, [switch]$All)
. (Join-Path $PSScriptRoot 'settings-list.ps1')

if (-not (Test-Path $BackupFile)) { "No backup found at $BackupFile - nothing to revert."; return }

$selected = @(Select-PrivacySettings -Action 'restore' -Group $Group -All:$All)
if (-not $selected) { "Nothing selected - no changes made."; return }

$wanted = $selected | ForEach-Object { "$($_.Path)|$($_.Name)" }
$backup = Get-Content $BackupFile -Raw | ConvertFrom-Json
foreach ($b in $backup | Where-Object { $wanted -contains "$($_.Path)|$($_.Name)" }) {
    try {
        if ($b.Existed) {
            if (-not (Test-Path $b.Path)) { New-Item -Path $b.Path -Force -ErrorAction Stop | Out-Null }
            Set-ItemProperty -Path $b.Path -Name $b.Name -Value $b.Value -Type DWord -ErrorAction Stop
            "RESTORED  $($b.Name) = $($b.Value)"
        } else {
            Remove-ItemProperty -Path $b.Path -Name $b.Name -ErrorAction SilentlyContinue
            "DEFAULT   $($b.Name)"
        }
    } catch {
        "SKIPPED   $($b.Name)  (needs 'Run as Administrator')"
    }
}

""
if ($selected.Count -eq $PrivacySettings.Count) {
    Remove-Item $BackupFile
    "Done. All original settings are back. Sign out and back in (or restart) for everything to take effect."
} else {
    # Keep the backup so the remaining settings can still be restored later.
    "Done. Backup kept for the settings you didn't restore. Sign out and back in (or restart) to take effect."
}
