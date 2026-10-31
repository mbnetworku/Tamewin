# Fastyourpc

PowerShell scripts that turn off Windows 11 and Microsoft Office data sharing, tips and suggestions for the current user, with a one-command undo.

- Changes only current-user settings (`HKEY_CURRENT_USER`). No services, firewall or Defender changes.
- Doesn't touch your Microsoft account or sign-in.
- Backs up your original settings before changing anything.

## Use

Download or clone this repository, open PowerShell in its folder, then:

```powershell
.\Apply-Privacy.ps1     # turn data sharing off (saves a backup first)
.\Revert-Privacy.ps1    # put everything back the way it was
```

If scripts are blocked, run once: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

Sign out and back in (or restart) afterwards. Some Office settings need **Run as Administrator**; without it they're reported as `SKIPPED`.

See [TECHNICAL.md](TECHNICAL.md) for every setting it changes.
