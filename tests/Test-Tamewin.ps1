# Tests dist\Tamewin.exe without touching real settings: every registry change is redirected
# to HKCU\Software\TamewinTest (removed at the end) and a temporary backup file is used.
# The app scan (--apps) is read-only. Nothing is uninstalled.
$ErrorActionPreference = 'Stop'
$exe    = Join-Path $PSScriptRoot '..\dist\Tamewin.exe' | Resolve-Path
$tmp    = Join-Path ([IO.Path]::GetTempPath()) 'TamewinTest'
$root   = 'Software\TamewinTest'
$backup = Join-Path $tmp 'backup.tsv'
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
Remove-Item "HKCU:\$root" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $backup -ErrorAction SilentlyContinue

$script:failed = 0
function Check($name, $ok) { if ($ok) { "PASS  $name" } else { "FAIL  $name"; $script:failed++ } }

function Run([string[]]$argList) {
    $out = Join-Path $tmp 'out.txt'
    $p = Start-Process $exe -ArgumentList $argList -RedirectStandardOutput $out -WorkingDirectory $tmp -Wait -PassThru -NoNewWindow
    [pscustomobject]@{ Code = $p.ExitCode; Text = (Get-Content $out -Raw) }
}
function T([string[]]$a) { Run (@('--test-root', $root, '--backup', $backup) + $a) }
function Status($id) { ((T '--list').Text -split "`r?`n" | Where-Object { $_ -match "^$id\s" }) -replace '\s+', ' ' }

# A value that exists before applying, to check revert restores it (not just deletes it).
$adKey = "HKCU:\$root\CU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo"
New-Item $adKey -Force | Out-Null
Set-ItemProperty $adKey -Name Enabled -Value 1 -Type DWord

$r = Run '--help';                         Check 'help exits 0' ($r.Code -eq 0)
$r = T '--apply', 'nope';                  Check 'unknown id rejected' ($r.Code -eq 2)
$r = T '--apply', 'recommended';           Check 'apply recommended' ($r.Code -eq 0 -and $r.Text -notmatch 'FAILED')
Check 'ad_id applied'        ((Status 'ad_id') -match 'Applied')
Check 'diag_services applied (HKLM redirected)' ((Status 'diag_services') -match 'Applied')
Check 'optional location NOT applied' ((Status 'location') -match 'NotApplied')

$r = T '--apply', 'classic_menu'
$inproc = "HKCU:\$root\CU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"
Check 'classic menu key with empty default' ((Test-Path $inproc) -and (Get-Item $inproc).GetValue('') -eq '')

$r = T '--apply', 'wpp_on';  Check 'wpp_on applied' ((Status 'wpp_on') -match 'Applied')
$r = T '--apply', 'wpp_off'; Check 'wpp_off reverts wpp_on first' ($r.Text -match 'RESTORED.*force ON' -and (Status 'wpp_off') -match 'Applied' -and (Status 'wpp_on') -notmatch ' Applied')

$wuKey = "HKCU:\$root\LM\SYSTEM\CurrentControlSet\Services\wuauserv"
New-Item $wuKey -Force | Out-Null
Set-ItemProperty $wuKey -Name Start -Value 3 -Type DWord
Check 'block_updates not in recommended' ((Status 'block_updates') -match 'NotApplied')
$r = T '--apply', 'block_updates'
Check 'block_updates applied' ((Status 'block_updates') -match 'Applied' -and (Get-ItemProperty $wuKey).Start -eq 4 -and
    (Get-ItemProperty "HKCU:\$root\LM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate").WUServer -eq 'http://127.0.0.1')

$r = T '--apply', 'ad_id'
Check 'second apply keeps original backup' ((Get-Content $backup | Select-String 'ad_id' | Select-String "`t1`tDWord`t1`t").Count -eq 1)

$r = T '--revert', 'all';                  Check 'revert all' ($r.Code -eq 0 -and $r.Text -notmatch 'FAILED')
Check 'ad_id restored to original 1' ((Get-ItemProperty $adKey).Enabled -eq 1)
Check 'classic menu key removed' (-not (Test-Path "HKCU:\$root\CU\Software\Classes\CLSID"))
Check 'update service restored to 3, policy key removed' ((Get-ItemProperty $wuKey).Start -eq 3 -and
    -not (Test-Path "HKCU:\$root\LM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"))
Check 'backup emptied' (@(Get-Content $backup | Where-Object { $_ -notmatch '^#' }).Count -eq 0)
$left = Get-ChildItem "HKCU:\$root" -Recurse | Where-Object { $_.ValueCount -gt 0 -and $_.PSChildName -notin 'AdvertisingInfo', 'wuauserv' }
Check 'no leftover values' (@($left).Count -eq 0)

$r = Run '--apps'
Check 'app scan runs' ($r.Code -eq 0 -and $r.Text -match 'Installed')
""
"App scan summary:"
$r.Text -split "`r?`n" | Where-Object { $_ } | Group-Object { ($_ -split '\s+')[0] + ' ' + ($(if ($_ -match 'Not installed') { 'Not installed' } else { 'Installed' })) } |
    ForEach-Object { "  {0,-22} {1}" -f $_.Name, $_.Count }

Remove-Item "HKCU:\$root" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
""
if ($script:failed) { "$script:failed test(s) FAILED"; exit 1 } else { "All tests passed." }
