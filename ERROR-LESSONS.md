# Error lessons

Problems hit while building Tamewin, and what fixed them. Kept so the same
mistakes aren't repeated. Newest first.

## Building and the project

- **Missing `app.manifest` broke the build.** The `.csproj` points at
  `app.manifest`; if the file isn't there, the build fails. Keep the manifest
  in `src/Tamewin` next to the project.
- **The version string showed the git commit hash** (e.g. `1.0.0+9f66777`).
  Fix: `<IncludeSourceRevisionInInformationalVersion>false</...>` in the
  `.csproj`, so `Application.ProductVersion` is just `1.0.0`.
- **64-bit registry keys were being redirected to `WOW6432Node`.** Fix: open
  the registry with `RegistryView.Registry64` and build with
  `Prefer32Bit=false`, so HKLM paths aren't silently rewritten.

## The window (WinForms)

- **"&" in a label showed as an underline** ("Tips, suggestions & ads" became
  "Tips, suggestions _ ads"). WinForms treats `&` as a keyboard mnemonic. Fix:
  draw every string with `TextFormatFlags.NoPrefix`.
- **The lists had no scrollbar and couldn't be scrolled.** The default
  WinForms `AutoScroll` never showed a bar. Fix: the list now scrolls itself
  and draws its own scrollbar (wheel, drag, PageUp/PageDown, Ctrl+Home/End).
- **Scrolling stuttered.** Each wheel step moved and repainted every card one
  by one. Fix: shift the whole list in one go with `ScrollWindowEx`, and ease
  between positions with a short timer.
- **`Select` and a class method name clashed with `Control.Select`.** Renamed
  the helper to `SelectWhere` so it didn't shadow the built-in.
- **Saving a screenshot threw `ExternalException`** because the old PNG was
  still open in a viewer and locked. Fix: delete the old PNG first.

## Status and revert logic

- **"Protected print OFF" showed *Partly* even when nothing was set.** Ops that
  only delete a value match whenever the value is absent, so they were counting
  as "partly applied". Fix: only ops that *set* a value count towards *Partly*.
- **Blocking Windows Update can revert itself.** The Windows Update Medic
  Service (`WaaSMedicSvc`) is protected even from Administrators, so it can set
  `wuauserv` back to manual. The policy keys still block updates, but the status
  reads *Partly*. Documented; the fix is to apply the option again.

## Safety and antivirus

- **No local antivirus scan was possible.** Microsoft Defender returned
  `0x80004005` (it was in passive mode) and AVG's command-line scanner crashed
  (`0xE06D7363`). So the EXE could not be scanned on this machine; checking it
  against multiple engines needs a VirusTotal upload, which is the user's call.
- **Obfuscation / anti-reverse-engineering was deliberately NOT added.** Packers
  and obfuscators are exactly what antivirus flags as malware. Keeping the code
  plain and open, with a published SHA-256, is what keeps it clean. The real fix
  for a false positive is code signing and reporting it to the vendor.

## PowerShell scripts (the original tools)

- **`[ordered]` hashtable piped through `foreach` gave a parse error.** Fix:
  write the block to a `.ps1` file and run it, instead of a one-liner.
- **A function named `R` clashed with the `Invoke-History` alias `r`.** Renamed
  it to `GetReg`.
- **Policy keys under `HKLM` gave "access denied" without admin.** Expected:
  wrap the write in try/catch and print `SKIPPED` instead of crashing.
- **`Start-Process` defaulted to the `D:` working directory**, which must be
  avoided. Fix: always pass `-WorkingDirectory` pointing at a `C:` folder.
