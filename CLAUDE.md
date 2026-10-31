# CLAUDE.md

Guidance for Claude (or any AI assistant) working in this repository.

## Rules
- Never change anything on the user's machine without first saying what will change and how to undo it.
- Never touch the Microsoft account or sign-in (login, account settings, OneDrive sign-in, linked accounts).
- Keep every change reversible: back up first, and provide a revert.
- Never commit `privacy-backup.json` or any personal or machine-specific information.

## Layout
| File | Purpose |
|---|---|
| `settings-list.ps1` | The single list of registry settings (Group, Path, Name, Value, Note), the `$PrivacyGroups` menu labels, and `Select-PrivacySettings` (menu / `-All` / `-Group`). Add or remove settings here only. |
| `Apply-Privacy.ps1` | Asks what to change (or takes `-All` / `-Group`), saves the original values of **all** settings to `privacy-backup.json` (first run only), then applies the chosen ones. Prints `SKIPPED` for anything it can't write. |
| `Revert-Privacy.ps1` | Asks what to restore (or takes `-All` / `-Group`) and restores those from `privacy-backup.json`. Deletes the backup only after a full revert. |
| `Fastyourpc.ps1` | Main menu that runs the other scripts. Offers to elevate for protected print. |
| `Set-ClassicMenu.ps1` | Classic / Windows 11 right-click menu (HKCU CLSID key). `-On` / `-Off` / `-Status` / `-RestartExplorer`, or a menu. |
| `Set-ProtectedPrint.ps1` | Windows protected print mode via the HKLM policy key (admin). `-On` / `-Off` / `-Default` / `-Status`, or a menu with an "open Settings" fallback. |
| `TECHNICAL.md` | What every setting does. Keep it in sync with `settings-list.ps1` and the scripts. |

## Conventions
- Write only under `HKCU`. Keys under `HKCU:\Software\Policies\...` need Administrator, and that's expected.
- `Set-ProtectedPrint.ps1` is the one exception that writes `HKLM`, and it must refuse without Administrator. Its registry values aren't officially documented, so always keep the "open Settings" fallback.
- Every `Set-*.ps1` script takes `-KeyPath` so it can be tested on a throwaway `HKCU:\Software\...Test` key. Never test against the real keys.
- Don't disable services or change anything else under `HKLM` from these scripts. Document manual steps in `TECHNICAL.md` instead.
- Every setting needs a `Group` that exists in `$PrivacyGroups` and a unique `Note`, because the Note is what the individual-pick list shows.
- Menu text goes through `Write-Host`, because anything else written inside `Select-PrivacySettings` would end up in its return value.
- After adding a setting, a user who already applied must revert and then re-apply, so the new setting gets backed up.
