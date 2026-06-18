# GitHub Pages Setup / GitHub Pages 설정

[한국어](#한국어) | [English](#english)

## 한국어

이 저장소에는 한/영 전환 소개 페이지가 `docs/index.html`에 들어 있습니다.

GitHub에서 설정하는 방법:

1. GitHub 저장소로 이동
2. **Settings** 클릭
3. **Pages** 클릭
4. **Build and deployment**에서 Source를 **Deploy from a branch**로 선택
5. Branch를 `main`으로 선택
6. Folder를 `/docs`로 선택
7. Save

잠시 뒤 아래 형태의 주소가 생깁니다.

```text
https://<GitHub-ID>.github.io/<Repository-Name>/
```

소개 페이지는 브라우저 언어가 한국어이면 한국어를 먼저 보여주고, 버튼으로 한국어/English를 전환할 수 있습니다.

README 쪽은 GitHub Markdown에서 JavaScript 스위칭을 사용할 수 없으므로 `README.md`를 언어 선택 입구로 두고 `README.ko.md`, `README.en.md`로 이동하게 구성했습니다.

## English

This repository includes a bilingual landing page at `docs/index.html`.

How to enable it:

1. Open the GitHub repository
2. Click **Settings**
3. Click **Pages**
4. Under **Build and deployment**, set Source to **Deploy from a branch**
5. Select the `main` branch
6. Select the `/docs` folder
7. Click Save

After a short delay, GitHub Pages will publish the site at:

```text
https://<GitHub-ID>.github.io/<Repository-Name>/
```

The landing page auto-selects Korean for Korean browsers, and includes a language switch button.

GitHub Markdown does not run custom JavaScript inside repository README files, so `README.md` is used as a language gateway linking to `README.ko.md` and `README.en.md`.
