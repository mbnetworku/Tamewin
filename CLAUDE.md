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
| `settings-list.ps1` | The single list of registry settings (Path, Name, Value, Note). Add or remove settings here only. |
| `Apply-Privacy.ps1` | Saves the original values to `privacy-backup.json` (first run only), then applies the list. Prints `SKIPPED` for anything it can't write. |
| `Revert-Privacy.ps1` | Restores from `privacy-backup.json` (removes values that didn't exist before), then deletes the backup. |
| `TECHNICAL.md` | What every setting does. Keep it in sync with `settings-list.ps1`. |

## Conventions
- Write only under `HKCU`. Keys under `HKCU:\Software\Policies\...` need Administrator, and that's expected.
- Don't disable services or change anything under `HKLM` from these scripts. Document manual steps in `TECHNICAL.md` instead.
- After adding a setting, a user who already applied must revert and then re-apply, so the new setting gets backed up.
