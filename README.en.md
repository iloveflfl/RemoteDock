# RemoteDock

[한국어](./README.ko.md) | [Project Page](./docs/index.html) | [Release Notes](./RELEASE_NOTES.md)

**RemoteDock** is a Windows personal device dock for SSH-capable machines such as Raspberry Pi boards, Linux servers, VPS machines, Windows PCs, and laptops.

It is not just an SFTP drive mapper. RemoteDock aims to be a personal server hub that lets you **register, detect, mount, inspect, open Terminal, open VS Code, open web URLs, run favorites, execute command presets, and trigger backups** from one visual control panel.

## Feature highlights

- Device registration and editing
- Mount Name, groups, tags, notes
- LAN discovery
- Online/offline auto detect
- SSH status popup
- SSHFS-Win mount / unmount / mount + open
- Windows Explorer open
- SSH terminal open
- VS Code Remote SSH open
- Web URL open
- Favorite paths
- Command presets
- systemd / Docker status helpers
- Backup helper
- Key store viewer
- Profile export
- Copied SSH key ACL hardening
- Compact 5-block CPU/RAM/DISK bars
- Cat/rabbit “MEOWNTING” loading animation

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK for building, or .NET 8 Desktop Runtime for running built output
- OpenSSH Client
- WinFsp
- SSHFS-Win
- Optional: Windows Terminal
- Optional: VS Code + Remote SSH extension

Install dependencies with winget where available:

```bat
winget install -e --id Microsoft.DotNet.SDK.8
winget install -e --id WinFsp.WinFsp
winget install -e --id SSHFS-Win.SSHFS-Win
```

After installing WinFsp / SSHFS-Win, rebooting Windows is recommended.

## Build

```bat
build.cmd
```

or:

```bat
dotnet restore RemoteDock.sln
dotnet build RemoteDock.sln -c Release
```

## Run

```bat
RUN_RELEASE_DEBUG.cmd
```

Direct EXE path after build:

```text
RemoteDock\bin\Release\net8.0-windows\RemoteDock.exe
```

Development run:

```bat
RUN_DEV.cmd
```

## User data locations

RemoteDock stores runtime user data outside the source tree:

```text
%APPDATA%\RemoteDock\profiles.json
%APPDATA%\RemoteDock\keys\
%APPDATA%\RemoteDock\crash.log
```

Do not commit these files to GitHub.

## SSHFS-Win mount rules

RemoteDock generates SSHFS-Win UNC paths and mounts them with `net use`.

```text
Absolute Linux path + password  -> \\sshfs.r\user@host\home\user
Absolute Linux path + key       -> \\sshfs.kr\user@host\home\user
Home-relative/empty + password  -> \\sshfs\user@host\path
Home-relative/empty + key       -> \\sshfs.k\user@host\path
```

The UNC already contains `user@host`, so RemoteDock avoids adding `/user:user` to SSHFS-Win mounts.

## Manual mount test

If mounting fails inside RemoteDock, test SSHFS-Win directly:

```bat
net use R: "\\sshfs.r\USERNAME@HOST_OR_IP\home\USERNAME" /persistent:no
```

Example:

```bat
net use R: "\\sshfs.r\gomgonegi@192.168.0.55\home\gomgonegi" /persistent:no
```

If this manual command fails, fix WinFsp, SSHFS-Win, credentials, or the remote path first.

## SSH key handling

When a key file is selected, RemoteDock copies it into:

```text
%APPDATA%\RemoteDock\keys\<profile-id>\
```

It also attempts to harden permissions with `icacls` so Windows OpenSSH does not reject the key.

## GitHub safety checklist

Before pushing to GitHub:

```bat
git status
git diff --cached --name-only
```

Make sure these are not staged:

```text
profiles.json
keys/
*.pem
*.key
id_rsa
id_ed25519
bin/
obj/
*.zip
crash.log
```

## GitHub Pages landing page

A bilingual landing page is included at `docs/index.html`. To publish it, configure GitHub Pages to use the `/docs` folder on the `main` branch.
