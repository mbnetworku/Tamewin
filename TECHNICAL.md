# Technical Notes

## How to run

Turn privacy settings **off** (safe to run again; the original backup is kept):
```powershell
.\Apply-Privacy.ps1
```

**Undo** everything (back to how it was before the first apply):
```powershell
.\Revert-Privacy.ps1
```

To include the Office policy settings marked **Admin** below, run PowerShell **as Administrator**.

If Windows blocks the scripts ("running scripts is disabled"), run once:
```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

Sign out and back in (or restart) after applying or reverting.

## How it works
1. `settings-list.ps1` defines `$PrivacySettings` (Path, Name, Value, Note) and `$BackupFile`.
2. **Apply**: if `privacy-backup.json` doesn't exist yet, it records each setting's current value, or that it didn't exist (`Existed=false`). Then it writes each value as a DWORD. Settings it can't write print `SKIPPED`.
3. **Revert**: writes each old value back, or removes the value if it didn't exist before (Windows then uses its default). Then it deletes the backup, so the next Apply takes a fresh one.

All writes are under `HKEY_CURRENT_USER`. Nothing under `HKLM`, no services and no account settings are changed.

## Settings

### Windows — tips, suggestions, ads
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

### Windows — search
| Setting | Registry | Value |
|---|---|---|
| Search history on device | `…\SearchSettings\IsDeviceSearchHistoryEnabled` | 0 |
| Cloud search (Microsoft account) | `…\SearchSettings\IsMSACloudSearchEnabled` | 0 |
| Cloud search (work account) | `…\SearchSettings\IsAADCloudSearchEnabled` | 0 |
| Bing results in Start search | `…\Search\BingSearchEnabled` | 0 |

### Windows — feedback, speech, typing
| Setting | Registry (HKCU\Software\Microsoft\…) | Value |
|---|---|---|
| Feedback requests | `Siuf\Rules\NumberOfSIUFInPeriod` | 0 |
| Online speech recognition | `Speech_OneCore\Settings\OnlineSpeechPrivacy\HasAccepted` | 0 |
| Typing data collection | `InputPersonalization\RestrictImplicitTextCollection` | 1 |
| Handwriting data collection | `InputPersonalization\RestrictImplicitInkCollection` | 1 |
| Contacts used for typing | `InputPersonalization\TrainedDataStore\HarvestContacts` | 0 |
| Improve inking & typing | `Input\TIPC\Enabled` | 0 |

### Office
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

## Not covered (manual, by design)
- **Required diagnostic data / DiagTrack service.** It's a system service and can't be fully turned off on Windows 11 Home/Pro. Set it to "Required only" in Settings → Privacy & security → Diagnostics & feedback.
- **Location, camera, microphone.** Settings → Privacy & security. Left alone so calling and video apps keep working.
- **Activity history.** Settings → Privacy & security → Activity history.
- **Edge.** `edge://settings/privacy` → turn off "Help improve Microsoft products" and "Personalize your web experience".
- **Microsoft account / sign-in.** Intentionally untouched.
