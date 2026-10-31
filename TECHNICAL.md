# Technical Notes

# Part 1: Fastyourpc.exe

## Source layout (`src/Fastyourpc`)
| File | What it does |
|---|---|
| `Catalog.cs` | **Every privacy/settings option.** To add one, add a `T(group, id, title, info, ops...)` line. |
| `Apps.cs` | **Every known app** (`AppCatalog`) with its level and effect. To add one, add an `A(level, pattern, name, effect)` line. Also contains the scanner and uninstaller. |
| `Tweak.cs` | `Tweak` / `RegOp` types. `Admin` is worked out automatically (HKLM or `...\Policies\...` keys). |
| `Engine.cs` | Apply, revert and status. Writes the backup before the first change. |
| `MainForm.cs` | The window: sidebar, plus the Privacy, Windows features, Apps and Activity pages (`TweakPage`, `AppsPage`). |
| `Widgets.cs` | Custom-drawn controls: `OptionRow` (card with checkbox, text and pills), `FlatButton`, `NavItem`, `ListPanel`, `PageLayout`. |
| `Theme.cs` | Colours (Windows 11 style, light and dark), fonts, DPI scaling (`S(px)`), and the dark title bar. |
| `Program.cs` | Starts the window, or runs the command-line mode. |

## Adding an option
```csharp
T(Tips, "my_new_id", "Turn off something", "One sentence: what changes for the user.",
    CU(@"Software\...\Key", "ValueName", 0)),          // HKCU, REG_DWORD 0
// LM(...) for HKLM (needs admin). Value: int = REG_DWORD, string = REG_SZ, null = delete the value.
// Optional: .Needs("Restart the PC ...")  .Warn("shown before applying")  .Optional()  .ConflictsWith("other_id")
```
Ids must be unique and never reused, because backups are stored by id. Then run `.\build.ps1` and `tests\Test-Fastyourpc.ps1`.

## How apply / revert work
- **Apply:** before a tweak's first apply, each of its values is saved to `%LOCALAPPDATA%\Fastyourpc\backup.tsv`: whether it existed, its type and value, and the highest key that didn't exist yet. Then the new values are written. Applying again keeps the original backup.
- **Revert:** writes the saved values back, deletes values that didn't exist, and removes keys the app created (only if they're empty). If there's no backup (the setting was changed by something else), it resets to Windows' default.
- **Conflicts:** applying `wpp_off` first reverts `wpp_on`, and the other way round.
- **Status:** *Applied* = every value matches, *Partly* = some do, *Not applied* = none do.
- HKLM is opened in the 64-bit view, so nothing is redirected to `WOW6432Node`.

## Apps tab
- **Scan:** `PackageManager.FindPackagesForUser("")` (current user). Each known app is matched by package name (`*` wildcard) and shown as Installed or Not installed. *Other installed apps* = Store- or developer-signed packages that aren't frameworks, resources or bundles, and aren't in the list.
- **Uninstall:** `PackageManager.RemovePackageAsync(fullName)` for the current user. Removed apps are logged to `%LOCALAPPDATA%\Fastyourpc\removed-apps.tsv`, so *Get from Store* can open the exact Store page.
- It doesn't remove *provisioned* copies (apps preinstalled for new user accounts). That needs DISM and Administrator.
- Levels: **Safe** = nothing depends on it. **Caution** = a Windows feature stops working. **Critical** = Store, Windows Security, sign-in, runtimes, etc.; ticking one shows a warning you must confirm.

## Command line
```
Fastyourpc.exe --list | --apply <ids|recommended> | --revert <ids|all> | --apps | --help
  --test-root <key>   redirect all registry writes to HKCU\<key> (testing)
  --backup <file>     use a different backup file
  --theme light|dark  force a theme
  --screenshot <file.png> <privacy|features|apps|log>   draw a page off-screen to a PNG and exit
```
The README screenshots are made with `--test-root` plus `--screenshot`, so they show a clean state and not a real PC's settings. The Apps page isn't used for screenshots, because it would show the apps installed on the PC that took them.
Exit codes: 0 = OK, 1 = some items failed or were skipped, 2 = bad arguments.

## Testing
`tests\Test-Fastyourpc.ps1` runs the built EXE with `--test-root Software\FastyourpcTest` and a temporary backup, so no real setting changes. It covers apply, revert, restoring a pre-existing value, cleaning up created keys, conflicts, optional items and a read-only app scan. It never uninstalls anything.

## Antivirus and false positives
The app is written to look like what it is:
- Plain compiled C# (.NET Framework 4.8). No packer, obfuscator, embedded scripts or downloader, and no network code.
- No PowerShell or cmd is started. Registry changes use `Microsoft.Win32.Registry`, and apps are removed with `Windows.Management.Deployment.PackageManager`.
- `asInvoker` manifest: it only asks for Administrator when you click *Restart as Administrator*.
- Deterministic build: building the same source gives the same EXE, and the SHA-256 is published.

Some products still flag unsigned tools that change privacy settings or remove apps (heuristic or "hacktool/potentially unwanted" detections). The legitimate fixes:
1. **Code-sign the EXE**, e.g. with Azure Trusted Signing or a free open-source signing service like SignPath Foundation. This is the biggest improvement for SmartScreen and antivirus.
2. **Check every release on VirusTotal** (~70 engines, including Palo Alto/Cortex, ESET, McAfee/Trellix, Kaspersky, Bitdefender and Microsoft).
3. **Report any detection as a false positive** to that vendor: Microsoft (Security Intelligence submission), Palo Alto (WildFire), ESET (samples@eset.com), McAfee/Trellix (false-positive form), Kaspersky (opentip / threat.kaspersky.com).

Never "fix" a detection by obfuscating or packing the EXE. That's exactly what malware does.

# Part 2: PowerShell scripts

Start with `.\Fastyourpc.ps1` (main menu), or run each script directly.

## Privacy — how to run

Turn privacy settings **off** (safe to run again; the original backup is kept):
```powershell
.\Apply-Privacy.ps1                        # menu
.\Apply-Privacy.ps1 -All                   # everything
.\Apply-Privacy.ps1 -Group Tips,Search     # chosen groups
```

**Undo** (back to how it was before the first apply):
```powershell
.\Revert-Privacy.ps1                       # menu
.\Revert-Privacy.ps1 -All                  # everything
.\Revert-Privacy.ps1 -Group Office         # chosen groups
```

Menu input: group numbers (`1,3`), `A` = all, `I` = pick individual settings by number (`1,4,7-9`), `Q` or Enter = quit without changes.

Groups: `Tips`, `Search`, `Typing`, `Office`, `OfficeAdmin`. These match the sections below.

To include the Office policy settings marked **Admin** below, run PowerShell **as Administrator**.

If Windows blocks the scripts ("running scripts is disabled"), run once:
```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

Sign out and back in (or restart) after applying or reverting.

## How it works
1. `settings-list.ps1` defines `$PrivacyGroups`, `$PrivacySettings` (Group, Path, Name, Value, Note), `$BackupFile`, and `Select-PrivacySettings`, which returns the chosen settings from `-All`, `-Group` or the menu.
2. **Apply**: if `privacy-backup.json` doesn't exist yet, it records the current value of **every** setting (not only the chosen ones), or that it didn't exist (`Existed=false`). Then it writes each chosen value as a DWORD. Settings it can't write print `SKIPPED`.
3. **Revert**: for each chosen setting, it writes the old value back, or removes the value if it didn't exist before (Windows then uses its default). The backup is deleted only after a full revert (all settings). After a partial revert it's kept, so the rest can still be restored later.

All writes are under `HKEY_CURRENT_USER`. Nothing under `HKLM`, no services and no account settings are changed.

## Settings

### Windows — tips, suggestions, ads (`Tips`)
| Setting | Registry (HKCU\Software\Microsoft\Windows\CurrentVersion\…) | Value |
|---|---|---|
| Tips and suggestions | `ContentDeliveryManager\SubscribedContent-338389Enabled` | 0 |
| Suggested content in Settings | `ContentDeliveryManager\SubscribedContent-338393/353694/353696Enabled` | 0 |
| Suggestions in Start | `ContentDeliveryManager\SubscribedContent-338388Enabled`, `SystemPaneSuggestionsEnabled` | 0 |
| Tips about Windows | `ContentDeliveryManager\SoftLandingEnabled` | 0 |
| Auto-install suggested apps | `ContentDeliveryManager\SilentInstalledAppsEnabled` | 0 |
| "Finish setting up" prompts | `UserProfileEngagement\ScoobeSystemSettingEnabled` | 0 |
| Advertising ID | `AdvertisingInfo\Enabled` | 0 |
| Tailored experiences | `Privacy\TailoredExperiencesWithDiagnosticDataEnabled` | 0 |
| Track app launches | `Explorer\Advanced\Start_TrackProgs` | 0 |
| Recommendations in Start | `Explorer\Advanced\Start_IrisRecommendations` | 0 |
| Websites read language list | `HKCU\Control Panel\International\User Profile\HttpAcceptLanguageOptOut` | 1 |

### Windows — search (`Search`)
| Setting | Registry | Value |
|---|---|---|
| Search history on device | `…\SearchSettings\IsDeviceSearchHistoryEnabled` | 0 |
| Cloud search (Microsoft account) | `…\SearchSettings\IsMSACloudSearchEnabled` | 0 |
| Cloud search (work account) | `…\SearchSettings\IsAADCloudSearchEnabled` | 0 |
| Bing results in Start search | `…\Search\BingSearchEnabled` | 0 |

### Windows — feedback, speech, typing (`Typing`)
| Setting | Registry (HKCU\Software\Microsoft\…) | Value |
|---|---|---|
| Feedback requests | `Siuf\Rules\NumberOfSIUFInPeriod` | 0 |
| Online speech recognition | `Speech_OneCore\Settings\OnlineSpeechPrivacy\HasAccepted` | 0 |
| Typing data collection | `InputPersonalization\RestrictImplicitTextCollection` | 1 |
| Handwriting data collection | `InputPersonalization\RestrictImplicitInkCollection` | 1 |
| Contacts used for typing | `InputPersonalization\TrainedDataStore\HarvestContacts` | 0 |
| Improve inking & typing | `Input\TIPC\Enabled` | 0 |

### Office (`Office` = rows without Admin; `OfficeAdmin` = rows marked Admin)
| Setting | Registry (HKCU\Software\…) | Value | Needs |
|---|---|---|---|
| Office telemetry | `Microsoft\Office\Common\ClientTelemetry\DisableTelemetry` | 1 | — |
| Office diagnostic data level | `Microsoft\Office\Common\ClientTelemetry\SendTelemetry` (3 = neither) | 3 | — |
| Office diagnostic data (policy) | `Policies\Microsoft\Office\Common\ClientTelemetry\SendTelemetry` | 3 | Admin |
| Experiences that analyze content | `Policies\Microsoft\Office\16.0\Common\Privacy\usercontentdisabled` (2 = off) | 2 | Admin |
| Experiences that download content | `Policies\…\16.0\Common\Privacy\downloadcontentdisabled` | 2 | Admin |
| Optional connected experiences | `Policies\…\16.0\Common\Privacy\controllerconnectedservicesenabled` | 2 | Admin |
| Send feedback / screenshots / surveys | `Policies\…\16.0\Common\Feedback\enabled`, `includescreenshot`, `surveyenabled` | 0 | Admin |

No admin? In Word, go to **File → Account → Account Privacy → Manage Settings** and turn off the connected experiences there. If the policy settings are applied, Office may say "some settings are managed by your organization". That's expected.

## Classic right-click menu (`Set-ClassicMenu.ps1`)

| State | Registry |
|---|---|
| ON (classic) | Key `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32` exists, with its (Default) value set to an empty string |
| OFF (Windows 11) | Key `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}` removed |

- Current user only, no admin needed.
- Takes effect after Explorer restarts. `-RestartExplorer`, or answering `Y` in the menu, does this immediately and closes open File Explorer windows. Signing out or restarting the PC also works.
- `-KeyPath` overrides the key, which is used for testing on a throwaway key.

## Windows protected print mode (`Set-ProtectedPrint.ps1`)

Key: `HKLM\SOFTWARE\Policies\Microsoft\Windows NT\Printers\WPP` (needs Administrator).

| Action | Values (REG_DWORD) |
|---|---|
| `-On` | `EnabledBy`=2, `WindowsProtectedPrintGroupPolicyState`=1, `WindowsProtectedPrintMode`=1, `WindowsProtectedPrintOobeConfigComplete`=1 |
| `-Off` | `WindowsProtectedPrintMode`=0. The other three values are removed. |
| `-Default` | All four values removed. Windows then follows the Settings app toggle. |
| `-Status` | Reads the values only. |

- Restart the PC after any change.
- Turning it **ON** removes printers that aren't Mopria-compatible. After turning it off, add them again under Settings → Printers & scanners.
- These values come from community guides, which don't fully agree, not from official Microsoft documentation. Microsoft's supported ways are Group Policy (Computer Configuration → Administrative Templates → Printers → *Configure Windows protected print*) and the Settings toggle. Menu option 4 opens that Settings page (`ms-settings:printers`).
- The script refuses `-On`, `-Off` and `-Default` without Administrator. `Fastyourpc.ps1` offers to reopen it in an admin window.

## Main menu (`Fastyourpc.ps1`)

A loop that runs `Apply-Privacy.ps1`, `Revert-Privacy.ps1`, `Set-ClassicMenu.ps1` or `Set-ProtectedPrint.ps1`. For protected print, if the current window isn't admin, it starts an elevated PowerShell (UAC prompt) running that script.

## Not covered (manual, by design)
- **Required diagnostic data / DiagTrack service.** It's a system service and can't be fully turned off on Windows 11 Home/Pro. Set it to "Required only" in Settings → Privacy & security → Diagnostics & feedback.
- **Location, camera, microphone.** Settings → Privacy & security. Left alone so calling and video apps keep working.
- **Activity history.** Settings → Privacy & security → Activity history.
- **Edge.** `edge://settings/privacy` → turn off "Help improve Microsoft products" and "Personalize your web experience".
- **Microsoft account / sign-in.** Intentionally untouched.
