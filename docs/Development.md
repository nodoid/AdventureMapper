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

Signing profiles are kept out of source control (`Signing/` is ignored) and
installed into `~/Library/MobileDevice/Provisioning Profiles`. The project selects
the profile by configuration:

| Configuration | iOS | Mac Catalyst | Key |
| --- | --- | --- | --- |
| Debug | `devel-advmapper` | `devel-advmapper-mac` | Apple Development |
| Release | `rel-advmapper` | `rel-advmapper-mac` | Apple Distribution |

Mac Catalyst builds also set
`CodesignEntitlements=Platforms/MacCatalyst/Entitlements.plist`. Replace the
profile names, key and bundle id with your own for a fork.

### Mac Catalyst

`Platforms/MacCatalyst/AppDelegate.cs` installs the native Edit-menu Undo/Redo
with `UIMenuBuilder`. Mac Catalyst ignores a custom XAML Edit menu. The in-app
splash page is used because `MauiSplashScreen` only produces launch screens for
Android and iOS.

### Windows

The Windows target is only added when building on Windows. The package identity
is in `Platforms/Windows/Package.appxmanifest`; file associations require the
packaged (MSIX) build. On this multi-target project, pass `UseMonoRuntime=false`
for Windows publishes; otherwise restore asks for the nonexistent
`Microsoft.NETCore.App.Runtime.Mono.win-x64` package.

Build a self-contained MSIX (no separate .NET or Windows App SDK runtime):

```powershell
dotnet publish AdventureMapper.csproj -f net10.0-windows10.0.22000.0 -c Release `
  -p:RuntimeIdentifier=win-x64 -p:Platform=x64 `
  -p:UseMonoRuntime=false -p:SelfContained=true -p:WindowsAppSDKSelfContained=true `
  -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageDir=C:\out\msix\x64\ -p:AppxBundle=Never `
  -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxPackageSigningEnabled=false
```

Use `win-arm64` / `Platform=ARM64` for ARM64. The package is written to
`AppxPackageDir\<name>_Test\*.msix`. Sign it with `signtool.exe` from the
`Microsoft.Windows.SDK.BuildTools` NuGet cache (the MAUI restore already brings it
in), using a certificate whose subject matches the manifest publisher:

```powershell
$signtool = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" `
  -Recurse -Filter signtool.exe | Where-Object FullName -Match '\\arm64\\' | Select-Object -Last 1
& $signtool.FullName sign /fd SHA256 /f msix-signing.pfx /p <password> `
  /tr http://timestamp.digicert.com /td SHA256 .\AdventureMapper_1.0.0.0_x64.msix
```

Package from a clean source tree. An incremental package can contain an incomplete
`resources.pri` (about 38 KB) and crash at launch in `Microsoft.UI.Xaml.dll` with
`0xc000027b`; the packaged `resources.pri` should be about 1.39 MB for the ARM64
build and the installed app must be launched once before publishing.

The build emits a harmless warning that `mspdbcmf.exe` is missing and no symbols
package will be generated. MSIX packaging requires a Windows host; the macOS
host builds the Apple and Android targets. For local unpackaged testing:

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
  -p:RuntimeIdentifier=maccatalyst-arm64 -p:CreatePackage=false

productbuild --component "bin/Release/net10.0-maccatalyst/maccatalyst-arm64/Adventure Mapper.app" \
  /Applications \
  --sign "3rd Party Mac Developer Installer: Paul Johnson (3UH7BE38T3)" \
  releases/AdventureMapper-1.0-macos-arm64.pkg
```

Zip or package the signed `.app` from
`bin/Release/net10.0-maccatalyst/maccatalyst-arm64/`. The `.pkg` is the App Store
installer; the zip and dmg carry the same App Store-signed bundle.

### iOS

```bash
dotnet publish AdventureMapper.csproj -f net10.0-ios -c Release \
  -p:RuntimeIdentifier=ios-arm64 -p:BuildIpa=true
```

The Release configuration selects `rel-advmapper` and the Apple Distribution
identity, so the IPA has `get-task-allow` false and can be uploaded with
transporter or Xcode Organizer.

### Windows MSIX

```powershell
dotnet publish AdventureMapper.csproj -f net10.0-windows10.0.22000.0 -c Release `
  -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=MSIX `
  -p:GenerateAppxPackageOnBuild=true
```

Repeat with `win-arm64` for ARM64. MSIX packaging requires a Windows host and a
code-signing certificate matching the publisher in `Package.appxmanifest`.

## Known limitations

* The Windows print path has not been tested against a physical printer; MSIX packaging requires a Windows host.
* Printing has not been verified against physical printer hardware.
* Phones install the app and use the compact tab layout; the layout is designed
  for tablets and desktops.
