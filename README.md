# Fastyourpc

PowerShell scripts that turn off Windows 11 and Microsoft Office data sharing, tips and suggestions for the current user, with a one-command undo.

- Changes only current-user settings (`HKEY_CURRENT_USER`). No services, firewall or Defender changes.
- Doesn't touch your Microsoft account or sign-in.
- Backs up your original settings before changing anything.

## Use

Download or clone this repository, open PowerShell in its folder, then:

```powershell
.\Apply-Privacy.ps1     # shows a menu: choose what to turn off (saves a backup first)
.\Revert-Privacy.ps1    # shows a menu: choose what to put back
```

The menu:
```
Choose what to turn OFF:
  1) Tips, suggestions & ads  [14 settings]
  2) Search history & web search  [4 settings]
  3) Feedback, speech & typing  [6 settings]
  4) Office telemetry  [2 settings]
  5) Office connected experiences & feedback (needs Administrator)  [7 settings]
  A) All
  I) Pick individual settings
  Q) Quit
Your choice (e.g. 1,3):
```

Type one or more numbers (`1,3`), `A` for everything, or `I` to pick single settings from a numbered list (ranges work too, like `1,4,7-9`).

No menu, for scripts:
```powershell
.\Apply-Privacy.ps1 -All                 # everything
.\Apply-Privacy.ps1 -Group Tips,Search   # groups: Tips, Search, Typing, Office, OfficeAdmin
.\Revert-Privacy.ps1 -Group Office       # undo just one group
.\Revert-Privacy.ps1 -All                # undo everything
```

If scripts are blocked, run once: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

Sign out and back in (or restart) afterwards. Some Office settings need **Run as Administrator**; without it they're reported as `SKIPPED`.

See [TECHNICAL.md](TECHNICAL.md) for every setting it changes.
