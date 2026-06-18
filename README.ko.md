# RemoteDock

[English](./README.en.md) | [프로젝트 소개 페이지](./docs/index.html) | [패치노트](./RELEASE_NOTES.md)

**RemoteDock**는 라즈베리파이, 리눅스 서버, VPS, 윈도우 PC, 노트북처럼 SSH 접속 가능한 장비를 한곳에서 관리하는 Windows용 개인 장비 도킹 앱입니다.

단순한 SFTP 드라이브 마운트 앱이 아니라, SSH 장비를 **등록 → 감지 → 마운트 → 상태 확인 → 터미널/VS Code/웹/즐겨찾기/명령/백업 실행**까지 묶는 개인 서버 허브를 목표로 합니다.

## 주요 기능

- 장비 등록, 수정, 삭제
- Mount Name, 그룹, 태그, 메모
- LAN 장비 탐색
- 온라인/오프라인 자동 감지
- SSH 상태 팝업
- SSHFS-Win 기반 마운트 / 언마운트 / 마운트 후 열기
- Windows Explorer 열기
- SSH 터미널 열기
- VS Code Remote SSH 열기
- 웹 URL 열기
- 즐겨찾기 경로
- 명령 프리셋
- systemd / Docker 상태 확인 도우미
- 백업 도우미
- 키 저장소 뷰어
- 프로필 내보내기
- 복사된 SSH key 권한 보정
- CPU/RAM/DISK 5칸 상태바
- 고양이/토끼 기반 “MEOWNTING” 로딩 애니메이션

## 요구 사항

- Windows 10 또는 Windows 11
- 빌드용 .NET 8 SDK 또는 실행용 .NET 8 Desktop Runtime
- OpenSSH Client
- WinFsp
- SSHFS-Win
- 선택: Windows Terminal
- 선택: VS Code + Remote SSH 확장

설치 예시:

```bat
winget install -e --id Microsoft.DotNet.SDK.8
winget install -e --id WinFsp.WinFsp
winget install -e --id SSHFS-Win.SSHFS-Win
```

WinFsp / SSHFS-Win 설치 후에는 Windows 재부팅을 권장합니다.

## 빌드

```bat
build.cmd
```

또는:

```bat
dotnet restore RemoteDock.sln
dotnet build RemoteDock.sln -c Release
```

## 실행

```bat
RUN_RELEASE_DEBUG.cmd
```

빌드 후 직접 실행 파일 경로:

```text
RemoteDock\bin\Release\net8.0-windows\RemoteDock.exe
```

개발 실행:

```bat
RUN_DEV.cmd
```

## 사용자 데이터 위치

RemoteDock은 런타임 사용자 데이터를 소스 폴더 밖에 저장합니다.

```text
%APPDATA%\RemoteDock\profiles.json
%APPDATA%\RemoteDock\keys\
%APPDATA%\RemoteDock\crash.log
```

이 파일들은 GitHub에 올리지 않는 것을 권장합니다.

## SSHFS-Win 마운트 규칙

RemoteDock은 SSHFS-Win UNC 경로를 생성하고 `net use`로 드라이브를 마운트합니다.

```text
절대 Linux 경로 + 비밀번호  -> \\sshfs.r\user@host\home\user
절대 Linux 경로 + key       -> \\sshfs.kr\user@host\home\user
홈 기준/빈 경로 + 비밀번호  -> \\sshfs\user@host\path
홈 기준/빈 경로 + key       -> \\sshfs.k\user@host\path
```

UNC 경로 자체에 `user@host`가 들어가므로 SSHFS-Win 마운트에서는 `/user:user`를 붙이지 않습니다.

## 수동 마운트 테스트

RemoteDock 내부에서 마운트가 실패하면 SSHFS-Win을 직접 테스트합니다.

```bat
net use R: "\\sshfs.r\USERNAME@HOST_OR_IP\home\USERNAME" /persistent:no
```

예시:

```bat
net use R: "\\sshfs.r\gomgonegi@192.168.0.55\home\gomgonegi" /persistent:no
```

수동 명령이 실패하면 WinFsp, SSHFS-Win, 계정, 비밀번호, 원격 경로부터 확인해야 합니다.

## SSH key 처리

키 파일을 선택하면 RemoteDock은 키를 아래 위치로 복사합니다.

```text
%APPDATA%\RemoteDock\keys\<profile-id>\
```

또한 Windows OpenSSH가 키 파일을 거부하지 않도록 `icacls`로 권한 보정을 시도합니다.

## GitHub 업로드 전 체크리스트

```bat
git status
git diff --cached --name-only
```

아래 항목이 staged 상태이면 제거하세요.

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

## GitHub Pages 소개 페이지

`docs/index.html`에 한/영 스위칭 소개 페이지가 포함되어 있습니다. GitHub Pages에서 publishing source를 `main` branch의 `/docs` 폴더로 설정하면 됩니다.
