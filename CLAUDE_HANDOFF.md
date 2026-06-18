# v13 update note

The uploaded fragment files from the interrupted Claude run have been placed into structured folders and the known SSHFS provider bug has been patched directly in Program.cs. Continue future refactors gradually: keep Program.cs behavior stable, then migrate one class at a time to Models/Services/UI.

# Claude handoff: RemoteDock repair/refactor brief

You are receiving a zipped .NET 8 WinForms project named RemoteDock. The user wants you to inspect, repair, and improve it without changing the core concept.

## Project concept

RemoteDock is a Windows personal server dock for SSH-capable devices. It should manage Raspberry Pis, Linux servers, VPS machines, Windows PCs, laptops, and other SSH/SFTP targets.

The app should feel like a visual device hub:

- registered devices appear in a left-side list/card area
- online devices show a green dot
- offline devices show a red dot
- mounted devices show a green/soft highlight
- unavailable or disconnected devices show a red/soft warning highlight
- CPU/RAM/DISK show both numbers and compact 5-block bars, e.g. `42% ■■□□□`
- double-clicking a device opens a visual status popup, not just logs
- long-running operations show a cat-themed loading dialog centered over the RemoteDock main window
- user-facing messages should be English-first to reduce encoding issues

## Environment

Target OS: Windows 10/11
Framework: .NET 8 WinForms
Build command: `build.cmd`
Run command: `RUN_RELEASE_DEBUG.cmd`
Main source file: `RemoteDock/Program.cs`
Profile storage: `%APPDATA%\RemoteDock\profiles.json`
SSH key store: `%APPDATA%\RemoteDock\keys`
Crash log: `%APPDATA%\RemoteDock\crash.log`

External tools expected:

- OpenSSH Client
- WinFsp
- SSHFS-Win
- Optional Windows Terminal
- Optional VS Code Remote SSH

## Essential behavior to preserve

1. Device CRUD
2. Group/tag/note fields
3. Mount Name field
4. SSH status check
5. Device status popup
6. LAN discovery
7. Registered-device auto detect
8. SSHFS-Win drive mounting through `net use`
9. Mount + Open behavior
10. Unmount behavior
11. Explorer open
12. SSH terminal open
13. VS Code Remote SSH open
14. Web URL open
15. Favorite paths
16. Command presets
17. systemd service and Docker status helper
18. Backup helper
19. Profile export
20. Key store viewer
21. Copied SSH key ACL hardening

## SSHFS-Win mount rules

RemoteDock should not invent its own filesystem driver. It should generate correct SSHFS-Win UNC paths and call `net use`.

Rules:

- Absolute Linux path + password: `\\sshfs.r\user@host\home\user`
- Absolute Linux path + key: `\\sshfs.kr\user@host\home\user`
- Home-relative/empty path + password: `\\sshfs\user@host\path`
- Home-relative/empty path + key: `\\sshfs.k\user@host\path`

Important: do not append `/user:user` to `net use` for SSHFS-Win when the UNC already contains `user@host`; it can cause failures or confusion.

If the profile host is `.local`, resolve it to IPv4 for mount attempts when possible. Plain `ssh` often handles `.local`, while SSHFS-Win can be less reliable.

## Current user-reported fixes already requested

- 5-block bars instead of 10-block bars to avoid text clipping
- Mount Name should be used as the visible app label and should be attempted as Explorer drive label
- Loading window must appear centered on the active RemoteDock main window
- Loading animation should be cat-themed
- User-facing messages should be English-first
- Multiple devices may import keys with the same original filename; store them per profile and ideally by hash/fingerprint
- Selected row color should be very light, not strong blue
- Clicking empty area in the device list should clear selection
- Auto detect should not pile up modal error dialogs if auto mount fails repeatedly
- Check/status/mount/unmount operations should show a non-blocking loading indicator
- SSH key input should support browse and drag/drop
- Imported SSH keys should be copied into RemoteDock key storage and ACL-tightened
- Key ACL issue to handle:
  `WARNING: UNPROTECTED PRIVATE KEY FILE! Permissions are too open.`

## Repair/refactor priorities

### Priority 1: build and runtime verification

- Run `build.cmd` on Windows.
- Run `RUN_RELEASE_DEBUG.cmd`.
- Confirm no startup crash.
- Confirm crash log is written for unhandled exceptions.
- Confirm all buttons either work or show a clear English error.

### Priority 2: split the monolithic code

The current MVP has a very large `Program.cs`. Refactor into maintainable files/classes without changing behavior.

Suggested structure:

```text
RemoteDock/
  Program.cs
  MainForm.cs
  Models/
    DeviceProfile.cs
    DeviceStatus.cs
    HubSettings.cs
  Services/
    ProfileStore.cs
    KeyStoreService.cs
    SshService.cs
    MountService.cs
    DiscoveryService.cs
    MetricsParser.cs
    BackupService.cs
    DiagnosticService.cs
  UI/
    LoadingDialog.cs
    DeviceStatusDialog.cs
    DeviceHubDialog.cs
    DiscoveryDialog.cs
    KeyStoreDialog.cs
    VisualBars.cs
```

### Priority 3: mount reliability

- Verify `net use` commands against working manual commands.
- Clear existing drive mapping before remount when requested.
- Detect error 85 and propose unmount/remap.
- Detect error 67 and tell user to check WinFsp/SSHFS-Win installation.
- Show generated UNC in Diagnose.
- Avoid broken-encoding logs by using the correct Windows codepage.

### Priority 4: key security

- When importing a key, copy it into `%APPDATA%\RemoteDock\keys\<profile-id>\...`.
- Use file hash/fingerprint to avoid collisions.
- Run ACL hardening after copy and before SSH use.
- Verify OpenSSH accepts the copied key.
- Provide a repair-permissions button or automatic repair.

### Priority 5: visual UX

- Device list should remain readable with many devices.
- Group support should become useful: collapsible groups, group counts, group-level actions if feasible.
- CPU/RAM/DISK bars should be compact and not clip text.
- Device status popup should present cards/bars, not a wall of text.
- Auto detect state should update the UI without intrusive modal dialogs.

### Priority 6: scale to many Raspberry Pis

Assume 30+ Raspberry Pis may be registered.

Implement or improve:

- groups
- search/filter
- duplicate host/user warning
- duplicate key fingerprint detection
- per-device key folder
- key fingerprint display
- drive-letter conflict prevention
- bulk check
- bulk mount/unmount by group
- notification throttling

## Acceptance tests

1. Add a Raspberry Pi profile:
   - Host: `raspberrypi.local` or IP
   - User: `gomgonegi`
   - Remote path: `/home/gomgonegi`
   - Drive: `R:`
   - Mount name: `PiHome`
2. Check status: should show online and metrics.
3. Mount with password: generated UNC should use `sshfs.r` for `/home/gomgonegi`.
4. Mount + Open: Explorer should open the mounted drive/folder only after mount succeeds.
5. Unmount: `R:` should disappear.
6. Import SSH key by browse and drag/drop: key should be copied to app key store and permissions tightened.
7. Auto detect: no repeated modal error spam.
8. Double-click device: visual status popup appears.
9. Left list: green online dot, soft selection, compact bars.
10. Empty list area click: selection clears.

## Style preference

The user prefers practical, visible, tactile UX over plain log output. Prefer clear visual states, compact metrics, and direct buttons.

Keep app text English-first. Logs can include raw command outputs, but the app's own UI strings should be English to avoid mojibake.
