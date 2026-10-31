# Fastyourpc

PowerShell tools for Windows 11:

- **Privacy**: turn off Windows and Microsoft Office data sharing, tips and suggestions, with an undo.
- **Classic right-click menu**: switch between the old full menu and the Windows 11 menu.
- **Windows protected print mode**: turn it on or off.

None of them touch your Microsoft account or sign-in.

## Start here

Download or clone this repository, open PowerShell in its folder, then:

```powershell
.\Fastyourpc.ps1
```

```
=== Fastyourpc ===
  1) Privacy: turn off data sharing with Microsoft
  2) Privacy: restore original settings
  3) Classic right-click menu: on / off
  4) Windows protected print mode: on / off   (needs Administrator)
  Q) Quit
```

If scripts are blocked, run once: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

Each option can also be run on its own, as shown below.

## Privacy

```powershell
.\Apply-Privacy.ps1     # shows a menu: choose what to turn off (saves a backup first)
.\Revert-Privacy.ps1    # shows a menu: choose what to put back
```

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

No menu:
```powershell
.\Apply-Privacy.ps1 -All                 # everything
.\Apply-Privacy.ps1 -Group Tips,Search   # groups: Tips, Search, Typing, Office, OfficeAdmin
.\Revert-Privacy.ps1 -Group Office       # undo just one group
.\Revert-Privacy.ps1 -All                # undo everything
```

These change only current-user settings (`HKEY_CURRENT_USER`). Sign out and back in (or restart) afterwards. The `OfficeAdmin` group needs **Run as Administrator**; without it those settings are reported as `SKIPPED`.

## Classic right-click menu

Brings back the full Windows 10-style right-click menu, so you don't have to click "Show more options".

```powershell
.\Set-ClassicMenu.ps1                    # menu: shows current state, choose on/off
.\Set-ClassicMenu.ps1 -On                # classic menu
.\Set-ClassicMenu.ps1 -Off               # Windows 11 menu
.\Set-ClassicMenu.ps1 -On -RestartExplorer   # apply right away (closes File Explorer windows)
```

Current user only, no admin needed.

## Windows protected print mode

```powershell
.\Set-ProtectedPrint.ps1                 # menu: status, on, off, default, open printer settings
.\Set-ProtectedPrint.ps1 -Status
.\Set-ProtectedPrint.ps1 -On             # ⚠ removes printers that aren't Mopria-compatible
.\Set-ProtectedPrint.ps1 -Off
.\Set-ProtectedPrint.ps1 -Default        # remove the policy, let the Settings app decide
```

Needs **Run as Administrator** for on/off/default. Restart the PC afterwards. The registry values aren't officially documented by Microsoft. If a change doesn't take effect, choose **4) Open printer settings** in the menu and use the toggle under **Settings → Bluetooth & devices → Printers & scanners**.

See [TECHNICAL.md](TECHNICAL.md) for exactly what each tool changes.
