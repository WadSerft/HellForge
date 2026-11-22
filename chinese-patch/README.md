# HellForge Chinese localization patch

The launcher's UI in Chinese (the `l10n/zh-cn` branch of this fork), as a small reversible patch for an
**installed** HellForge 0.4.0-beta or newer. It replaces only the launcher's own files in `app\launcher`:

- `HellForge.Launcher.dll` - the localized launcher (every window text, the Settings > LANGUAGE dropdown)
- `HellForge.Core.dll`, `HellForge.SteamDownload.dll` - unchanged code, from the same build
- `zh-CN\` - the Chinese resources
- `language.json` is written to the installation root to turn Chinese on (delete it, or switch the
  dropdown, for English)

## Apply

    powershell -ExecutionPolicy Bypass -File apply.ps1
    powershell -ExecutionPolicy Bypass -File apply.ps1 -Root <your HellForge folder>

The first run backs the launcher's current files up to `backup-pre-chinese\` inside the installation.

## Revert

    powershell -ExecutionPolicy Bypass -File apply.ps1 -Revert

The game copy (`game\`), profiles and mods are never touched. The patch refuses to install when the
installation's version differs from the one it was built from.

The same branch also builds complete installers (the fork's Actions: "Build the Chinese package"),
which carry this patch already applied.
