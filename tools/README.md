# BattleLuck helper tools

These files are optional developer helpers. When `BattleLuck.dll` is loaded for
the first time, the plugin copies missing files from its embedded resources to
`BepInEx/config/BattleLuck/tools/` and leaves existing files untouched.

`extract-kindredextract-reference.ps1` refreshes the checked-in ProjectM/Unity
reference snapshot. It is not executed by the server and requires a local
KindredExtract checkout; use it only from a development workstation.
