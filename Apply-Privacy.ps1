# Turns OFF data sharing with Microsoft for the current user (Windows + Office).
# Does NOT touch your Microsoft account / sign-in. No admin needed (except the OfficeAdmin group).
# The first run saves your original settings to privacy-backup.json; later runs keep that original backup.
# Undo with Revert-Privacy.ps1
#
#   .\Apply-Privacy.ps1                        # menu: choose what to turn off
#   .\Apply-Privacy.ps1 -All                   # everything, no questions
#   .\Apply-Privacy.ps1 -Group Tips,Search     # only these groups
param([string[]]$Group, [switch]$All)
. (Join-Path $PSScriptRoot 'settings-list.ps1')

$selected = @(Select-PrivacySettings -Action 'turn OFF' -Group $Group -All:$All)
if (-not $selected) { "Nothing selected - no changes made."; return }

# Back up ALL settings (not only the selected ones) the first time, before anything changes.
if (-not (Test-Path $BackupFile)) {
    $backup = foreach ($s in $PrivacySettings) {
        $cur = Get-ItemProperty -Path $s.Path -Name $s.Name -ErrorAction SilentlyContinue
        [pscustomobject]@{
            Path    = $s.Path
            Name    = $s.Name
            Existed = [bool]$cur
            Value   = if ($cur) { $cur.($s.Name) } else { $null }
        }
    }
    $backup | ConvertTo-Json | Set-Content -Path $BackupFile -Encoding UTF8
    "Original settings saved to $BackupFile"
} else {
    "Backup already exists, keeping original: $BackupFile"
}

foreach ($s in $selected) {
    try {
        if (-not (Test-Path $s.Path)) { New-Item -Path $s.Path -Force -ErrorAction Stop | Out-Null }
        Set-ItemProperty -Path $s.Path -Name $s.Name -Value $s.Value -Type DWord -ErrorAction Stop
        "OFF      $($s.Note)"
    } catch {
        # Keys under ...\Policies\... can only be created by an Administrator.
        "SKIPPED  $($s.Note)  (needs 'Run as Administrator')"
    }
}
""
"Done. Sign out and back in (or restart) for everything to take effect."
