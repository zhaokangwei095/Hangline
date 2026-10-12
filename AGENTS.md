# Hangline — agent notes

Windows port of the macOS app Tendedero: screenshots hang on a clothesline at the
top of the screen. Upstream source is cloned at `_ref_tendedero/` (read-only
reference — never edit it, never commit it).

## Layer rules (these make the thing testable)

- `Core/` may only use `double`, `Guid`, `string`, `List<T>` and its own
  `Pt`/`SizeD`/`RectD`. **No WPF, no WinForms, no Win32 types.** Geometry,
  spring math, line rules, file naming and debouncing are all unit-testable
  without an STA thread. If a Core file needs `System.Windows`, it belongs in
  another folder.
- `System.Drawing` is allowed in exactly three files — `Interop/Gdi.cs`,
  `Interop/ShellThumb.cs`, `Shell/IconGen.cs` — because the tray needs a GDI
  `Icon`, a screen grab needs GDI, and the shell hands back an `HBITMAP`.
  Everything else paints with WPF `DrawingContext` and `BitmapSource`.
- `Shell/AppServices.cs` is the only composition point. Phases wire their piece
  there and nowhere else. There is no DI container on purpose.
- A feature that does not exist yet is left out of the tray menu, not shown
  disabled. Nothing in the UI may lie about what it does.

## Porting discipline

Every constant in `Core/Layout.cs` is lifted from the upstream Swift and must
stay there: `Layout` and `PeggedView.swift` are the spec. When behaviour is
ambiguous, read the Swift before deciding.

Where Windows cannot do what macOS does, the degradation is recorded in
`docs/parity.md` — never silently.

The upstream MIT license excludes the name and the shirt icon. Do not reuse
either: the tray icon is drawn in code (`Shell/IconGen.cs`).

## Build

```powershell
scripts/dev.ps1        # run from source
scripts/publish.ps1    # portable folder in publish/
dotnet test
```

The .NET 8 SDK lives at `%LOCALAPPDATA%\Microsoft\dotnet` (installed per-user,
already on PATH).

## Verifying the overlay without a human

Command-line switches, parsed in `Shell/Debug.cs` (dev-only):

- `pin` — force the line down and never retract it
- `hang <path>` — hang a file from the command line
- `take-down <秒>` — fire "take everything down" after N seconds, so the
  reveal-then-fall cascade can be screenshotted without a hand on the tray
- `settings [页]` — open the settings window (page 0 or 1) to lay it out
- `cursor <x>,<y>` — move the real cursor (this hijacks the user's mouse; say so before using it)

`scripts/shot.ps1` captures the screen to a PNG so the result can be inspected.
Window state (is the line down, is the fall overlay up) is cheaper and more
reliable than pixels: `scripts/winrect.ps1` lists every Hangline window with
`vis=` and its rect.
