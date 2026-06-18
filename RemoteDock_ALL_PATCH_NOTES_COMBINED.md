RemoteDock v2 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v2.md
========================================================================

# RemoteDock MVP v2 Patch Notes

## Fixed

1. Right-side input panel no longer clips fields when the window is resized.
2. Action buttons are grouped inside a scrollable Actions area and stay visible.
3. Device order can be changed by dragging rows in the left device list.
4. Double-clicking a device now prints its profile summary, checks connectivity, and then attempts to fetch status/process details over SSH.

## Added

- Stable `SortOrder` field in profiles.json.
- Larger default window and right panel minimum width.
- `build.cmd` and `RUN_DEV.cmd` for Windows users who do not want to change PowerShell execution policy.

## Build without PowerShell policy issues

```bat
S:\RemoteDock\build.cmd
```

Or:

```powershell
cd S:\RemoteDock\RemoteDock
dotnet restore
dotnet build -c Release
```


RemoteDock v4 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v4.md
========================================================================

# RemoteDock MVP v4 LayoutFix

Fixes a WinForms startup crash caused by SplitContainer SplitterDistance validation on some Windows 10 layouts.

Changes:
- Replaced the root SplitContainer with a 2-column TableLayoutPanel.
- Right panel is fixed-width and auto-scrollable.
- Startup crash now returns non-zero exit code after logging.
- Title updated to v4 LayoutFix.


RemoteDock v5 Patch Notes
Source: RemoteDock_v5_AutoDetectPopup/PATCH_NOTES_v5.md
========================================================================

# RemoteDock MVP v5 AutoDetect Popup

Changes:

1. Added a bottom StatusStrip so the bottom UI line is not clipped.
2. Added visible global Auto Detect controls in the lower-left toolbar.
   - Auto detect checkbox
   - Interval seconds control
   - Status strip shows ON/OFF, interval, last check time, and online/offline counts.
3. Double-clicking a device now opens a popup window instead of writing details only to the log.
4. The Status Popup action opens the same popup.
5. The popup shows profile summary, connectivity result, remote system status, memory/disk/uptime, and top processes when SSH command execution succeeds.
6. The popup includes Copy and Close buttons.

Notes:
- Automatic detection checks the configured host/port by TCP connection.
- Remote system/program details require SSH command execution. SSH key/agent authentication is recommended.
- Per-profile Auto Mount still controls whether an online device is mounted automatically after auto detection.


RemoteDock v6 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v6.md
========================================================================

# RemoteDock v6 - Discovery + No-scroll Actions

## Changes
- Right panel layout revised so Selected Device and Actions are visible at once without vertical scrolling in normal 1280x760+ windows.
- Selected Device fixed height increased.
- Actions fixed height increased and internal scrollbar removed.
- Added LAN discovery window.
- Added Discover button in the lower-left toolbar.
- Discovery scans local IPv4 /24 networks and default gateways for common ports:
  - 22 SSH
  - 80 HTTP
  - 443 HTTPS
  - 445 SMB
  - 3389 RDP
- Discovered SSH devices can be added as RemoteDock profiles.
- Discovered web/router devices can be added as Network/Web profiles with Web URL.
- Added Device Type option: Network/Web.

## Notes
SSH/SFTP does not advertise itself like Bluetooth. RemoteDock discovers devices by scanning likely LAN IP ranges and checking open ports. Devices with firewalls or closed ports will not appear automatically.


RemoteDock v7 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v7.md
========================================================================

# RemoteDock v7 Mount/Open Fix

## Changes

- `Open Drive` action renamed to `Mount + Open`.
- The app no longer opens Explorer on an invalid or unmounted drive letter.
- If the selected drive letter is not mounted, RemoteDock now attempts to mount first.
- If mounting fails, a warning dialog shows the SSHFS target and the likely causes.
- Mounting now validates Host, User, and Drive Letter before running `net use`.
- Device status popup now includes whether the configured drive is currently mounted.
- `net use` process output is formatted consistently with exit code.

## Important

A discovered SSH device is only a saved profile until it is mounted. For the remote folder to appear as a Windows drive, WinFsp and SSHFS-Win must be installed and the profile must have a valid SSH user, remote path, and drive letter.


RemoteDock v8 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v8.md
========================================================================

# RemoteDock MVP v8 - SSHFS Mount Fix

Fixes the SSHFS-Win mounting logic reported after manual `net use` worked but RemoteDock did not.

## Changes

- Absolute Linux paths such as `/home/gomgonegi` now use `sshfs.r` mode.
- Key-auth absolute paths use `sshfs.kr` mode.
- RemoteDock now tries to resolve `.local` / hostnames to IPv4 for SSHFS-Win mounting.
- `net use` no longer passes `/user:<user>` because SSHFS-Win expects the SSH user inside the UNC path.
- Before mounting, RemoteDock clears stale network mapping for the chosen drive letter to reduce system error 85.
- Output now shows the exact SSHFS mode, resolved host, and generated UNC target.

## Example generated target

For:

- User: `gomgonegi`
- Host: `raspberrypi.local`
- Remote Path: `/home/gomgonegi`
- Drive: `R`
- Password auth

RemoteDock should generate something like:

```bat
net use R: "\\sshfs.r\gomgonegi@192.168.0.55\home\gomgonegi" "********" /persistent:no
```

This matches the manual command style that successfully mounted on the user's PC.


RemoteDock v10 Patch Notes
Source: RemoteDock_v10_VisualKeyMount/PATCH_NOTES_v10.md
========================================================================

# RemoteDock v10 Visual Key Mount

## Fixes
- Fixes Korean mojibake in `net use` logs by reading child-process output using the current Windows OEM code page.
- Auto Detect no longer stacks mount-failure popup windows. Silent auto-mount failures are logged at most once every 5 minutes per device.
- Check, Mount, Unmount, and Mount+Open show a small loading dialog while the operation is running.

## SSH Key UX
- SSH Key field supports file browsing.
- SSH Key field supports drag-and-drop.
- When a key is selected, dragged, or saved, RemoteDock copies it into `%APPDATA%\RemoteDock\keys` and stores the copied key path in the device profile.
- This means RemoteDock no longer depends on the original key file remaining at its first location for status checks and SSH terminal commands.

## Mount UX
- Added Mount Name. This is a short display name for the mounted storage/device inside RemoteDock.
- Manual mount success shows a notification + success dialog.
- Existing SSHFS-Win mount logic from v8 is preserved: absolute paths use `sshfs.r`, and key-auth absolute paths use `sshfs.kr`.

## Visual status
- Left device list now shows online/offline dots.
- Mounted devices are highlighted with green styling; offline/unmounted problem states are tinted red.
- Status popup now shows visual cards/bars for Online, Mounted, CPU/Load, RAM, and Disk.
- Top process output is moved into a dedicated tab.

## Notes
- SSHFS-Win custom-key mounting is still limited by SSHFS-Win behavior. The copied key path is used by RemoteDock's SSH status/terminal commands. For drive mount, password auth remains the most reliable unless SSHFS-Win/OpenSSH config is set up for key aliases.


RemoteDock v11 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v11.md
========================================================================

# RemoteDock v11 Patch Notes

- Added Explorer label request for mounted SSHFS drives using Mount Name.
- Added best-effort `label <drive>:` command after mount.
- Centered busy dialog against the main RemoteDock window.
- Replaced generic loading faces with cat-themed ASCII frames.
- Converted remaining Korean warning strings to English.
- Fixed imported private key ACL using `icacls`.
- Moved key copies to per-profile folders with hash-prefixed filenames.
- Added safer duplicate-key handling for many devices using identical filenames.
- Reduced DataGridView selection blue intensity.
- Empty area click clears the device selection.
- Added text square progress bars to CPU/RAM/DISK cells.


RemoteDock v12 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v12.md
========================================================================

# RemoteDock v12 All-In-One Hub

## Visual/UI
- CPU/RAM/DISK text bars shortened from 10 blocks to 5 blocks: `■■□□□` style.
- Added Group column in the device list.
- Kept soft selection color and visual online/mount indicators from v11.

## Management Hub
- Added `Hub` button for selected device.
- Hub includes: group, tags, notes, favorite paths, command presets, systemd services, docker compose path, routes, workspace paths, backup paths.

## Operations
- Added `Diagnose` button with mount checklist style report.
- Added `Favorites` button to open mounted favorite paths quickly.
- Added `Run Cmd` button to run saved SSH command presets.
- Added `Svc/Docker` button to check configured systemd services and docker compose/docker status.
- Added `Backup` button that streams a remote tar.gz over SSH to a local backup folder.

## Admin/maintenance
- Added `Keys` button to view copied key store files.
- Added `Export` button to export profiles JSON.
- Kept SSH key copy and ACL hardening behavior.

## Notes
- Large features such as workspace/routes/backup are implemented as first-pass practical controls in the Hub.
- Network drive friendly Explorer label remains best-effort because Windows Explorer caches network mount labels aggressively.


RemoteDock v13 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v13.md
========================================================================

# RemoteDock MVP v13 Fragment Integrated

This version folds the Claude-generated fragment files back into the v12 working tree and applies the most important safety patches directly to the currently working single-file prototype.

## Applied fragments

Structured source folders were added under `RemoteDock/`:

- `Models/DeviceProfile.cs`
- `Models/DiscoveryResult.cs`
- `Models/StatusMetrics.cs`
- `Services/MountService.cs`
- `Services/ProcessRunner.cs`
- `Services/ProfileStore.cs`
- `Services/SshService.cs`
- `UI/Theme.cs`
- `UI/ThemedButton.cs`

The original v12 `Program.cs` remains the active UI/app host so the known-working behavior is preserved. The structured files are now available for the next refactor pass and compile as separate namespaces.

## Direct fixes in Program.cs

- Fixed SSHFS-Win provider construction so key + absolute path now produces `sshfs.kr` instead of the invalid `sshfs.k.r`.
- Added mount-failure guidance using `RemoteDock.Services.MountService.DescribeNetUseError`.
- Updated startup log/title to v13.

## Important note

The structured `Models/Services/UI` files intentionally do not replace all inline v12 classes yet. This keeps the prototype stable while making the fragments available for a controlled class-by-class migration.


RemoteDock v14 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v14.md
========================================================================

# RemoteDock v14 - Themed UI wiring

v13 imported the Claude-style theme fragments but kept most of the original WinForms chrome. v14 wires the theme into the running app.

## Changes

- Main window now applies the warm-neutral RemoteDock theme.
- Main device table uses muted headers, warm surfaces, softer selection, and reduced border intensity.
- Main toolbar buttons now use `ThemedButton`.
- Primary actions such as New, Save Profile, Mount, and Mount + Open use the clay accent.
- Risk/destructive actions such as Delete and Unmount use the danger tone.
- Selected Device / Actions / Output panels now use themed group styling.
- Input boxes, numeric box, device type combo box, SSH key picker, and output console are styled.
- Device row background and border colors now use the shared theme palette.
- Key Store / diagnostic readouts receive initial theme treatment.
- Existing v13 functionality is preserved.

## Notes

This version intentionally keeps the v13 layout and behavior intact. It focuses on actually making the imported `Theme.cs` and `ThemedButton.cs` visible in the UI without doing a risky full rewrite.


RemoteDock v15 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v15.md
========================================================================

# RemoteDock v15 Final Polish

This version keeps the v14 feature set and focuses on the final UX polish requested after visual testing.

## Changes

- Selected Device panel height increased so `Auto mount when online`, `Save Profile`, and the SSH key hint no longer clip at the bottom.
- Device list status chrome moved from cell painting to full-row post painting.
  - The status border now draws as a full rectangle rather than a clipped C-shape.
  - A uniform left status strip and 10px status dot are painted consistently for each row.
- Status popup now recenters against the main RemoteDock window after opening, matching the loading dialog placement behavior.
- Busy/loading dialog upgraded with a mount-pun cat/rabbit text animation based on the requested emoticon concept.
- Busy/loading dialog enlarged so the cat animation is not cramped.
- Explorer label request now also writes a DriveIcons DefaultLabel key in addition to MountPoints2 label metadata and best-effort `label` command.
- Version labels updated to v15.

## Notes

Windows Explorer may cache network drive labels. If the new label does not immediately appear, unmount, remount, and refresh Explorer. In some Windows/SSHFS-Win combinations the UNC provider name can still appear because the label is ultimately controlled by Explorer and the network provider.


RemoteDock v16 Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v16.md
========================================================================

# RemoteDock v16 MeowntingLoader

Focused polish release for the busy/loading dialog.

## Changes

- Rebuilt the loading animation to match the requested composition:
  - `(\ /)` moves left-right.
  - `ヾ(>᎑<)ﾉ` moves left-right.
  - `∧,,,∧`, `(◉_◉)`, `/づ♡` stay fixed.
  - Text appears below the emoticon block.
- Replaced the v15 mixed text/emote frames with a cleaner `BuildFrame()` implementation.
- Kept the loading form centered relative to the main RemoteDock window.

## Build

```bat
cd /d S:\RemoteDock\RemoteDock_v16_MeowntingLoader
build.cmd
RUN_RELEASE_DEBUG.cmd
```


RemoteDock v16b Patch Notes
Source: RemoteDock_GitHub_Distribution_v16b/PATCH_NOTES_v16b.md
========================================================================

# v16b Meownting Loader Build Fix

Fixes CS1009 caused by an unescaped backslash in the loader rabbit line:

```csharp
(\ /)
```

The loader keeps the intended animation:
- top rabbit lines move left/right
- cat lines stay fixed
- status text stays below
