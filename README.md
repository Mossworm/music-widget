# YT Music Controller

[English](README.md) | [한국어](README_kr.md)

A Windows 11 widget that controls the **YouTube Music PWA** (the app installed from Chrome or Edge) from the Widgets board (**Win + W**).

| Light mode | Dark mode |
| :---: | :---: |
| <img src="assets/light.png" alt="YT Music Controller widget in light mode" width="313"> | <img src="assets/dark.png" alt="YT Music Controller widget in dark mode" width="310"> |

- Album art, song title and artist
- Open app / previous / play·pause / next / like·unlike buttons
- Follows the Windows light, dark and high-contrast themes
- Works while the YouTube Music window is minimized

The widget reads the Windows media session that the PWA publishes. It does not read your Google account, browser cookies or passwords.

## Requirements

| Item | Notes |
| --- | --- |
| Windows 11 22H2 or later, x64 | The Widgets board must be available (Windows Web Experience Pack) |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | `winget install Microsoft.DotNet.SDK.10` |
| [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) | Provides `makeappx.exe` and `makepri.exe` |
| Developer Mode | Required to register the widget without a signed package |
| YouTube Music PWA | Installed from Chrome or Edge |
| Internet connection | Needed for the first build to restore NuGet packages |

## Installation

### 1. Install the YouTube Music PWA

Open [music.youtube.com](https://music.youtube.com) in Chrome or Edge, then choose **Install YouTube Music** from the address bar or the browser menu. Sign in to Google inside the PWA.

### 2. Turn on Developer Mode

**Settings → System → For developers → Developer Mode: On**

(On some Windows builds this is under **Settings → System → Advanced**.)

### 3. Get the source

```powershell
git clone https://github.com/Mossworm/music-widget.git
cd music-widget
```

Or download the ZIP from GitHub and extract it. Choose a permanent location, because Windows runs the widget from this folder after installation.

### 4. Build and install

Run this in PowerShell from the project folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-install.ps1
```

The script builds the project, runs the automated checks, packages the app into `artifacts\package\` and registers it for the current Windows user. It prints `Installed successfully` when it is done.

The script does not change system policies or install certificates. `-ExecutionPolicy Bypass` applies only to this one command.

### 5. Add the widget

1. Press **Win + W** to open the Widgets board.
2. Select **+ (Add widgets)**.
3. Find **YT Music Controller** and select **Pin**.

## Usage

1. Open the YouTube Music PWA and play a song once.
2. The widget shows the current song and enables its buttons.

| Button | Action |
| --- | --- |
| ↗ | Opens YouTube Music, or brings it to the front if it is already running |
| ⏮ / ⏯ / ⏭ | Previous, play·pause, next |
| ♥ | Likes the current song, or removes the like |

Before a song is played, or after the PWA is closed, the widget shows a connection message and disables the playback buttons.

A preview app with the same UI is also installed. You can open it from the Start menu (**YT Music Controller**) or run `artifacts\package\Desktop\MusicWidget.Desktop.exe`.

## Updating

Pull the latest source and run the same script again. It replaces the existing installation.

```powershell
git pull
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-install.ps1
```

## Uninstalling

```powershell
Get-AppxPackage Mossworm.YTMusicController | Remove-AppxPackage
```

You can then delete the project folder.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| `Enable Windows Developer Mode...` | Complete step 2, then run the script again |
| `Install the Windows SDK...` | Install the Windows SDK, including the desktop app tools |
| `dotnet` is not recognized | Install the .NET 10 SDK and open a new PowerShell window |
| The widget is not in the list | Close and reopen the Widgets board, or sign out and back in |
| The widget stays on the connection message | Play a song in the PWA. A regular YouTube tab and other music apps are not supported. Check that the browser's media integration with Windows is not turned off |
| Previous / next is disabled | YouTube Music may not allow it (for example, during an ad or on the last song) |
| The like button is disabled | The widget could not read the PWA's player. Restore the PWA window once and try again |
| The widget stopped working after moving the folder | Run the script again from the new location. Do not move or delete `artifacts\package\` after installation |

## Other build options

Build without installing (output goes to `artifacts\publish\`):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-install.ps1 -BuildOnly
```

Create an unsigned MSIX for Microsoft Store submission (output goes to `artifacts\msix\`):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-install.ps1 -Msix
```

Before you submit it, make sure `Identity.Name`, `Identity.Publisher` and `PublisherDisplayName` in `packaging/AppxManifest.xml` match your Partner Center product identity.

## How it works

- Song information and playback commands use the Windows [`GlobalSystemMediaTransportControlsSessionManager`](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession) API.
- Windows does not provide a media-session API for likes, so the widget presses the PWA's own like button through Windows UI Automation. This depends on the YouTube Music page layout and may stop working if it changes.
- When the PWA is minimized, the widget briefly restores it in the background, transparent and without focus, to refresh the like state, and then minimizes it again.
