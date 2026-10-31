# Puts back the settings that were there before Apply-Privacy.ps1 first ran.
# Settings that did not exist before are removed (Windows then uses its default).
. (Join-Path $PSScriptRoot 'settings-list.ps1')

if (-not (Test-Path $BackupFile)) { "No backup found at $BackupFile - nothing to revert."; return }

$backup = Get-Content $BackupFile -Raw | ConvertFrom-Json
foreach ($b in $backup) {
    if ($b.Existed) {
        if (-not (Test-Path $b.Path)) { New-Item -Path $b.Path -Force | Out-Null }
        Set-ItemProperty -Path $b.Path -Name $b.Name -Value $b.Value -Type DWord
        "RESTORED  $($b.Name) = $($b.Value)"
    } else {
        Remove-ItemProperty -Path $b.Path -Name $b.Name -ErrorAction SilentlyContinue
        "DEFAULT   $($b.Name)"
    }
}
Remove-Item $BackupFile
""
"Done. Original settings are back. Sign out and back in (or restart) for everything to take effect."
