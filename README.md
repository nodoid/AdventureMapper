# Adventure Mapper

A .NET MAUI editor for adventure-game maps. Rooms live on numbered layers (floors),
connect to one another, and can contain puzzles. Maps are saved as `.ama` (JSON) and
exported to JPEG and PDF.

Built by **Paul F. Johnson**. Released under the **DILLIGAF** licence — see
[LICENSE](LICENSE). Documentation: [user guide](docs/UserGuide.md),
[development](docs/Development.md), [privacy](docs/Privacy.md),
[store assets](docs/StoreAssets.md).

## Platforms

| Platform | Target | Notes |
| --- | --- | --- |
| Android | API 32 (Android 12L) minimum, API 36 compile | Tablets only (`supports-screens` excludes small/normal) |
| iPadOS | 18.0+ | iPad-first; iPhones install and use the compact tab layout |
| macOS | 26.0+ (Mac Catalyst) | Desktop window chrome and menu bar |
| Windows | 11 (10.0.22000+) | MSIX packaged; reserved identity `49556nodoid.AdventureMapper` |

Tablets get a Shell tab bar (**Map** and **Details**); iPhones use the same tabs in a
compact layout. macOS and Windows get the desktop workspace: menu bar, canvas, docked
inspector and status bar.

## Map model

* **Layer** — a floor. Layer `0` is the ground, positive numbers are above it, negative
  numbers below it. Each layer has a name and any number of rooms.
* **Room** — has a name, a short description (127 characters maximum), a grid position on
  its layer, connections, and puzzles.
* **Connection** — one-way link stored with its destination in `layer-room` notation,
  e.g. `0-3` (ground, room 3) or `-1-2` (first basement, room 2). `Up` moves to layer + 1,
  `Down` to layer - 1.
* **Bridge** — a room whose walkway sits one layer above the floor it spans. Stepping onto
  the bridge raises the player a layer; `Down` returns them to the spanned layer (the
  bridge's own layer, normally 0) unless the room beneath is a **pit**, in which case the
  drop is a fall.
* **Puzzle** — belongs to a room: name, type (lock, riddle, mechanism, trap, item, other),
  the prompt the players see, the solution and an optional reward.

The inspector's **Map checks** section reports broken links, duplicate directions,
layer/room mismatches and bridge-over-pit hazards as you edit.

Adding rooms: pick a room, then use any of the eight compass directions (**N, NE, E, SE, S,
SW, W, NW**) or **up a floor / down a floor**. Both directions of each connection are created,
the canvas grows in every direction and scrolls to the room that was just added.

Descriptions and puzzles are edited in centred [Mopups](https://github.com/LuckyDucko/Mopups)
dialogs.

## Files

* `.ama` — the native map file. It is plain JSON (`format: "adventure-mapper"`), so it can
  be diffed, scripted or generated. The extension is registered with the OS:
  * macOS: `CFBundleDocumentTypes` + exported UTI `uk.co.allthejohnsons.advmapper.ama`
  * iPadOS: the same document type; maps open in place
  * Windows: `windows.fileTypeAssociation` in `Package.appxmanifest`
  * Android: `VIEW` intent filters for content/file URIs ending in `.ama`

  Double-clicking a map launches the app and loads it (handled through
  `Services/PendingFileRouter.cs` and the platform entry points).
* **Save** writes the current `.ama`; **Save as…** asks for a location. Files are written
  through a temporary file and moved into place so an interrupted write cannot destroy an
  existing map.
* **Export** produces JPEG (whole map sheet, one panel per layer), PDF (map sheet plus a
  room/puzzle index) or JSON. The generator is `Services/ExportService.cs`, rendering with
  the same SkiaSharp `MapRenderer` used on screen.
* **Print** sends the same multi-page PDF to the platform print system: the macOS/iPadOS
  print panel, Android's print service, or the Windows print action. It is available from
  **File → Print map…** and the toolbar.

Auto save runs on a timer (default 60 seconds; 30 s / 1 / 2 / 5 / 10 minutes or off in the
inspector) and also when the window is deactivated or stopped. Maps that have never been
saved are auto-saved into `Documents/Adventure Mapper`.

## Localisation

The UI is localised through RESX resources in `Resources/Strings`:

* `AppResources.resx` holds the neutral (English) strings.
* `AppResources.de.resx` is a German translation and proves the satellite-assembly path.

XAML uses the `Translate` markup extension (`Text="{loc:Translate Menu_File}"`), code uses
`L.T("key")` / `L.F("key", args)` from `Resources/Strings/Localization.cs`. The culture follows
the device language; `ADVENTUREMAPPER_CULTURE=de` (environment variable) or the `ui.culture`
preference forces a locale, which is how the localisation is verified. Adding a language means
copying `AppResources.resx`, translating the values and keeping the file name
`AppResources.<culture>.resx`.

## Documentation

* [`docs/UserGuide.md`](docs/UserGuide.md) — using the editor, files, export and print.
* [`docs/Development.md`](docs/Development.md) — building, architecture, signing, releases.
* [`docs/Privacy.md`](docs/Privacy.md) — the no-data-collection policy.
* [`docs/StoreAssets.md`](docs/StoreAssets.md) — App Store / Play requirements and capture steps.

## Building

```bash
dotnet build -f net10.0-maccatalyst        # macOS
dotnet build -f net10.0-ios                # iPad
dotnet build -f net10.0-android            # Android tablet
dotnet build -f net10.0-windows10.0.22000.0  # Windows 11 (on Windows)
```

Requirements: .NET 10 SDK with the `maui`, `android`, `ios` and `maccatalyst` workloads,
Xcode 27+, and the Android SDK for Android builds. The Windows target is only added by the
project file when building on Windows.

### Signing (iOS and macOS)

The development profiles from `~/Downloads` are copied into `Signing/`:

* `devel-advmapper.mobileprovision` — iOS
* `devel-advmapper-mac.provisionprofile` — macOS

They are installed for Xcode/MSBuild under:

```
~/Library/MobileDevice/Provisioning Profiles/
  cba07b13-b2b0-416f-b02d-6ae403a4cc87.mobileprovision
  41ed4742-1b25-49c5-93a5-0f7661e55ec9.provisionprofile
```

Both use bundle id `uk.co.allthejohnsons.advmapper` and team `3UH7BE38T3`; the project sets
`CodesignProvision` (`devel-advmapper`, `devel-advmapper-mac`) and an Apple Development key.
Mac Catalyst builds use `Platforms/MacCatalyst/Entitlements.plist`, which enables the app
sandbox with read/write access to user-selected files.

### Windows identity

`Platforms/Windows/Package.appxmanifest` carries the reserved package identity:

* Name: `49556nodoid.AdventureMapper`
* Publisher: `CN=A6EAEB04-6634-41C1-BDFE-695819ECE444`
* Publisher display name: `all-the-johnsons`

File association and file activation require the packaged (MSIX) build, which is why
`WindowsPackageType` is MSIX rather than unpackaged.

For local testing without the Windows SDK (and therefore without MSIX packaging), build the
app unpackaged with the Windows App SDK bundled. Without the self-contained flag the bootstrapper
fails with `Class not registered`:

```bash
dotnet build -f net10.0-windows10.0.22000.0 -c Debug \
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
```

The resulting `AdventureMapper.exe` runs on Windows 11 ARM64 and x64 with the menu bar, canvas,
inspector, autosave and JPEG/PDF/JSON export working; the `.ama` double-click association only
applies once the MSIX package is built with `makeappx`/`signtool` from the Windows SDK.

## Tests

The device-independent logic — models, map rules, JSON, the editor state, autosave, layout,
exports and localisation — is covered by an xunit suite in `Tests/AdventureMapper.Tests`. The
project links the app sources directly (plus the real `.resx` files), so it needs no separate
copy of the code:

```bash
dotnet test Tests/AdventureMapper.Tests/AdventureMapper.Tests.csproj
```

The suite also checks the resources key by key (English/German parity, placeholders) and scans
the XAML and C# for interface strings that are missing from the resources.

## Layout

```
Models/      document, layers, rooms, connections, puzzles, rules and validation
Rendering/   grid layout, printable sheet layout and the SkiaSharp renderer
Services/    JSON, autosave, file dialogs, exports, editor commands, pending-file router
Views/       canvas, toolbar, inspector, desktop workspace, tablet tabs, Mopups dialogs
Platforms/   per-platform manifests, document types and activation hooks
```

## Author and licence

Adventure Mapper is © 2026 Paul F. Johnson. Released under the DILLIGAF licence
(see [LICENSE](LICENSE)).
