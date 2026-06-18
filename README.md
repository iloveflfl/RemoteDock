# RemoteDock

[한국어](./README.ko.md) | [English](./README.en.md) | [Project Page / 소개 페이지](./docs/index.html)

RemoteDock is a Windows personal device dock for SSH-capable machines such as Raspberry Pi boards, Linux servers, VPS machines, Windows PCs, and laptops.

RemoteDock는 라즈베리파이, 리눅스 서버, VPS, 윈도우 PC, 노트북처럼 SSH 접속 가능한 장비를 한곳에서 관리하는 Windows용 개인 장비 도킹 앱입니다.

> This repository README is a language gateway. Open the Korean or English README above, or enable GitHub Pages from the `/docs` folder for the bilingual landing page.
>
> 이 README는 언어 선택용 입구입니다. 위의 한국어/영문 README를 열거나, GitHub Pages를 `/docs` 폴더 기준으로 켜면 한/영 전환 랜딩 페이지를 사용할 수 있습니다.

## Quick start

```bat
build.cmd
RUN_RELEASE_DEBUG.cmd
```

## Repository layout

```text
RemoteDock.sln
RemoteDock/
README.ko.md
README.en.md
docs/index.html
build.cmd
RUN_RELEASE_DEBUG.cmd
CLAUDE_HANDOFF.md
PATCH_NOTES_*.md
```

## Safety note

Runtime profiles and copied SSH keys are stored outside the source tree under `%APPDATA%\RemoteDock`. Do not commit private keys, `profiles.json`, build output, logs, or release ZIP files.
