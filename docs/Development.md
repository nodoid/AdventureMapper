# Adventure Mapper — development

## Stack

* .NET 10 and .NET MAUI (single project).
* SkiaSharp for the map canvas, print sheet and exports.
* Mopups for the centred dialogs.
* CommunityToolkit.Maui for file saving and helpers.
* xunit for the device-independent tests.

`global.json` pins the .NET SDK feature band. The project targets
`net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and, on Windows,
`net10.0-windows10.0.22000.0`.

## Prerequisites

* .NET 10 SDK with the `maui`, `android`, `ios` and `maccatalyst` workloads.
* Xcode 27 or newer for Apple targets.
* Android SDK with an Android 12L+ system image for the emulator.
* Windows 11 with the Windows App SDK for the MSIX target.

```bash
dotnet workload restore
```

## Build and test

```bash
dotnet build AdventureMapper.csproj -f net10.0-maccatalyst -c Debug
dotnet build AdventureMapper.csproj -f net10.0-ios -c Debug
dotnet build AdventureMapper.csproj -f net10.0-android -c Debug
dotnet test Tests/AdventureMapper.Tests/AdventureMapper.Tests.csproj
```

The test project links the app sources and the real `.resx` files, so it needs no
copy of the code. It covers models, map rules, JSON, editor state, autosave,
layout, exports and localisation, including English/German resource parity and a
scan for interface strings missing from the resources.

## Project layout

```
Models/      document, layers, rooms, connections, puzzles, rules, validation
Rendering/   grid layout, printable sheet layout, SkiaSharp renderer
Services/    JSON, autosave, file dialogs, exports, editor commands, printing
Views/       canvas, toolbar, inspector, desktop workspace, tablet tabs, dialogs
Platforms/   per-platform manifests, document types, activation and printing
Resources/   icons, splash, fonts, images and RESX strings
Tests/       xunit suite
```

`MapEditorState` is the shared editing state. `MapRenderer` is used by the canvas
and by `ExportService`, so screen, print and export styling match. Link thickness
and the blue one-way colour live there.

## Platform notes

### Apple signing

Development profiles are kept out of source control (`Signing/` is ignored) and
installed into `~/Library/MobileDevice/Provisioning Profiles`. The project sets:

* `CodesignProvision=devel-advmapper` for iOS
* `CodesignProvision=devel-advmapper-mac` for Mac Catalyst
* `CodesignEntitlements=Platforms/MacCatalyst/Entitlements.plist`

Replace these with your own profiles and bundle id for a fork.

### Mac Catalyst

`Platforms/MacCatalyst/AppDelegate.cs` installs the native Edit-menu Undo/Redo
with `UIMenuBuilder`. Mac Catalyst ignores a custom XAML Edit menu. The in-app
splash page is used because `MauiSplashScreen` only produces launch screens for
Android and iOS.

### Windows

The Windows target is only added when building on Windows. The package identity
is in `Platforms/Windows/Package.appxmanifest`; file associations require the
packaged (MSIX) build. For local unpackaged testing:

```bash
dotnet build -f net10.0-windows10.0.22000.0 -c Debug \
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
```

### iOS document opening

`CFBundleDocumentTypes` and the exported UTI are in `Platforms/iOS/Info.plist`.
`AppDelegate.OpenUrl` places incoming files on `PendingFileRouter`, which the
workspace or tablet page consumes when it appears.

## Localisation

Strings live in `Resources/Strings/AppResources.resx` (English) and
`AppResources.de.resx` (German). XAML uses `{loc:Translate Key}`; code uses
`L.T("Key")` or `L.F("Key", args)`. Set `ADVENTUREMAPPER_CULTURE=de` to force a
locale, or use the `ui.culture` preference. To add a language, copy the neutral
RESX, translate the values and name it `AppResources.<culture>.resx`. The test
suite checks parity and placeholders.

## Release builds

Artifacts are written to `releases/` and are not committed.

### Android

Create/upload key (once, keep it private):

```bash
keytool -genkeypair -v -keystore releases/adventure-mapper.keystore \
  -alias adventure-mapper -keyalg RSA -keysize 2048 -validity 10000 \
  -dname "CN=Paul F. Johnson, O=Adventure Mapper, C=GB"
```

Build a signed App Bundle and APK:

```bash
dotnet publish AdventureMapper.csproj -f net10.0-android -c Release \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=releases/adventure-mapper.keystore \
  -p:AndroidSigningKeyAlias=adventure-mapper \
  -p:AndroidSigningKeyPass=... -p:AndroidSigningStorePass=... \
  -p:AndroidPackageFormats=aab
```

Use `-p:AndroidPackageFormats=apk` for the sideloadable APK.

### macOS

```bash
dotnet publish AdventureMapper.csproj -f net10.0-maccatalyst -c Release \
  -p:RuntimeIdentifier=maccatalyst-arm64
```

Zip or package the `.app` from
`bin/Release/net10.0-maccatalyst/maccatalyst-arm64/publish/`.

### iOS

```bash
dotnet publish AdventureMapper.csproj -f net10.0-ios -c Release \
  -p:RuntimeIdentifier=ios-arm64 -p:BuildIpa=true
```

The IPA is signed with the development profile configured in the project. For
App Store distribution, replace it with a distribution profile and export
through Xcode's organizer or `xcodebuild -exportArchive`.

### Windows MSIX

```powershell
dotnet publish AdventureMapper.csproj -f net10.0-windows10.0.22000.0 -c Release `
  -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=MSIX `
  -p:GenerateAppxPackageOnBuild=true
```

Repeat with `win-arm64` for ARM64. MSIX packaging requires a Windows host and a
code-signing certificate matching the publisher in `Package.appxmanifest`.

## Known limitations

* The Windows print path and MSIX packaging cannot be built or tested on macOS.
* Printing has not been verified against physical printer hardware.
* Phones install the app and use the compact tab layout; the layout is designed
  for tablets and desktops.
