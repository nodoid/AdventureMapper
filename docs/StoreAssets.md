# Store assets and demo videos

The generated assets live in `stores/` and are not committed (they are release
material, not source). The demo videos live in `~/Movies`.

## iOS screenshots — requirements

Checked against Apple's App Store Connect *Screenshot specifications* on
16 November 2026:

* **iPad 13-inch — required if the app runs on iPad.** 2064 × 2752 portrait,
  2752 × 2064 landscape, or 2048 × 2732 / 2732 × 2048.
* **iPhone 6.3-inch — required when the binary supports iPhone.** 1206 × 2622 or
  1179 × 2556 portrait (rotated for landscape).
* 1–10 images per display size, PNG or JPEG, no transparency.
* App icon: 1024 × 1024 PNG, no alpha, no rounded corners.

The project's `Info.plist` declares `UIDeviceFamily` 1 and 2, so both sets are
provided. The iPad set is the primary listing; the phone shot shows the compact
layout the app uses on iPhone.

## Capturing the iPad set

The build is a Debug simulator build. For capture only, a temporary `#if DEBUG`
hook preloaded `Samples/ten-room-map.ama` from the app's Documents folder; the
hook was removed before committing.

```bash
UDID=...   # iPad Pro 13-inch (M5), iPadOS 27.2
dotnet build AdventureMapper.csproj -f net10.0-ios -c Debug
xcrun simctl boot $UDID
xcrun simctl install $UDID bin/Debug/net10.0-ios/iossimulator-arm64/AdventureMapper.app
DATA=$(xcrun simctl get_app_container $UDID uk.co.allthejohnsons.advmapper data)
cp Samples/ten-room-map.ama "$DATA/Documents/"
xcrun simctl status_bar $UDID override --time 9:41 \
  --batteryState charged --batteryLevel 100 --wifiBars 3
SIMCTL_CHILD_ADVENTUREMAPPER_DEMO_MAP=ten-room-map.ama \
  xcrun simctl launch $UDID uk.co.allthejohnsons.advmapper
xcrun simctl io $UDID screenshot shot.png
```

The simulator was displayed in Xcode's Device Hub and driven with `cliclick`
(mapped from device pixels to the window rectangle). Screenshots came from
`simctl io screenshot`, not the host, so they are exactly 2064 × 2752.

Strip the alpha channel (Apple rejects transparency) and verify:

```bash
ffmpeg -y -i shot.png -pix_fmt rgb24 out.png
sips -g pixelWidth -g pixelHeight -g hasAlpha out.png
```

## Capturing the Mac set

Mac App Store screenshots are 16:10: 2880 × 1800, 2560 × 1600, 1440 × 900 or
1280 × 800. The set in `stores/macos/screenshots` is 1280 × 800.

```bash
# window at 0,30 and 1536×959
screencapture -x -R0,30,1536,959 mac.png
ffmpeg -y -i mac.png -vf scale=1280:800 -pix_fmt rgb24 out.png
```

The Mac build preloaded the sample through the same Debug hook (the hook is gone
from the committed source). The About dialog was opened from **Help → About
Adventure Mapper**.

## Capturing the Android set

A Pixel Tablet AVD (Android 16, arm64) at 2560 × 1600:

```bash
adb install -r bin/Debug/net10.0-android/uk.co.allthejohnsons.advmapper-Signed.apk
adb shell run-as uk.co.allthejohnsons.advmapper \
  sh -c 'cat > files/ten-room-map.ama' < Samples/ten-room-map.ama
adb shell am start -n uk.co.allthejohnsons.advmapper/crc644b7152a3455e5fb7.MainActivity
adb exec-out screencap -p > android.png
```

The status bar is cropped from the Play screenshots with
`ffmpeg -vf "crop=2560:1548:0:52"`; Play accepts 2560 × 1548 (aspect under 2:1).

## Graphics

* `stores/shared/app-icon-1024.png` and `app-icon-512.png` are composed from
  `Resources/AppIcon/appicon.svg` and `appiconfg.svg` with `rsvg-convert` and
  `ffmpeg` overlay.
* `stores/android/feature-graphic-1024x500.png` is rendered from an SVG with the
  same mark, the wordmark and the tagline.

## Demo videos

Videos are in `~/Movies` and are not committed:

| File | Source |
| --- | --- |
| `AdventureMapper-iPad-Demo.mp4` | `xcrun simctl io <udid> recordVideo` while driving the iPad build |
| `AdventureMapper-Mac-Demo.mp4` | `screencapture -v` / ffmpeg screen capture while driving the Mac build |

The iPad recording uses the simulator's native framebuffer, so it is crisp at
2064 × 2752. The Mac recording is cropped to the app window and re-encoded with
H.264 for compatibility.
