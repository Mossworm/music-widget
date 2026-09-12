# Music Controller

AI Usage Widget의 C# / Windows App SDK 구조를 바탕으로 만든 Windows 11 음악 위젯입니다. YouTube Music PWA의 Windows 미디어 세션을 이용합니다.

- **중간 크기 고정**, Customize widget 메뉴 없음
- 정사각형 앨범 썸네일, 노래 제목, 가수 이름
- 이전 곡 / 재생·일시정지 / 다음 곡
- Windows 밝은·어두운 테마 및 고대비 대응
- 별도 데스크톱 미리보기 앱 제공, 창 크기 고정
- 한국어 Windows에서는 한국어 안내, 그 외에는 영어

## 사용

1. Chrome 또는 Edge에 설치한 **YouTube Music PWA**를 열고 곡을 한 번 재생합니다.
2. 개발 등록 후 **Win + W → 위젯 추가 → Music Controller**를 고정합니다.
3. 위젯 버튼으로 이전 곡, 재생·일시정지, 다음 곡을 제어합니다.

PWA는 실행 중이어야 합니다. 최소화해도 Windows에 미디어 세션을 제공하는 동안 사용할 수 있습니다. 곡을 재생하기 전이나 앱을 종료한 뒤에는 연결 안내와 비활성 버튼이 표시됩니다. 썸네일이 없으면 기본 이미지가 표시됩니다. 긴 제목·가수명은 한 줄로 잘라 표시하며 데스크톱에서는 마우스를 올려 전체 내용을 볼 수 있습니다.

설정·로그인 화면, 진행 바, 음량, 반복, 셔플은 없습니다. Google 로그인은 기존 PWA에서 진행합니다. 이 앱은 Google 계정 정보, 브라우저 쿠키, 비밀번호를 읽지 않습니다.

## 빌드 및 등록

Windows 11 22H2 이상 / x64 / .NET 10 SDK / Windows SDK(makeappx, makepri) / Windows Web Experience Pack이 필요합니다. 최초 빌드에는 NuGet 연결이 필요합니다.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell -ExecutionPolicy Bypass -File scripts/Install-Dev.ps1
```

개발 등록에는 Windows 개발자 모드가 필요합니다. 스크립트는 시스템 정책이나 인증서를 변경하지 않습니다. 결과물은 `artifacts/MusicWidget.msix`와 `artifacts/package/`입니다. MSIX는 서명 전 상태이며 개발 PC에서는 `Install-Dev.ps1`로 압축 해제된 패키지를 등록합니다. 등록 후 `artifacts/package`를 이동하거나 삭제하지 마세요. 다른 PC 배포에는 신뢰할 수 있는 서명 또는 Store 배포가 필요합니다.

```powershell
# 실제 미디어 연결 화면
powershell -ExecutionPolicy Bypass -File scripts/Preview.ps1
# 샘플 화면: 실제 음악은 제어하지 않음
powershell -ExecutionPolicy Bypass -File scripts/Preview.ps1 -Sample
# 개발 등록 제거
powershell -ExecutionPolicy Bypass -File scripts/Uninstall-Dev.ps1
```

직접 실행: `artifacts/package/Desktop/MusicWidget.Desktop.exe`.

## 연결 방식 및 제한

Windows `GlobalSystemMediaTransportControlsSessionManager`에서 앨범 이미지와 메타데이터를 읽고 해당 세션의 전송 제어 API를 호출합니다. 보이는 위젯과 데스크톱은 2초 간격으로 갱신합니다. 위젯이 비활성 상태이면 폴링을 멈추고, 내용이 바뀐 경우에만 카드를 게시합니다.

설치 앱 목록의 YouTube Music 앱 ID 또는 Windows 앱 표시 이름으로 PWA를 식별합니다. Chromium이 일반 브라우저 ID를 제공하는 경우에는 같은 브라우저의 창 제목에 YouTube Music과 해당 곡명이 함께 있는 세션만 허용합니다. Windows의 기본 재생 세션으로 무조건 대체하지 않습니다. PWA가 식별 가능한 정보를 제공하지 않으면 연결 안내 상태를 유지합니다. 일반 YouTube 탭과 다른 음악 앱은 지원 대상이 아닙니다. 여러 YouTube Music 세션 중에는 현재 재생 중인 세션을 우선합니다.

버튼은 PWA가 노출한 지원 여부를 따릅니다. 마지막 곡, 광고 등에서 다음/이전 버튼을 제공하지 않거나 요청을 거부할 수 있습니다. 브라우저의 Windows 미디어 연동이 꺼져 있으면 연결되지 않습니다. 표시한 곡이 바뀐 뒤 늦게 도착한 버튼 요청은 무시합니다. 설치 경로나 Chrome 프로필 번호를 하드코딩하지 않습니다.

위젯 본문은 Windows 호스트가 Adaptive Card로 렌더링하므로 버튼 모양과 간격은 WPF 데스크톱 미리보기와 일부 다를 수 있습니다. 크기와 Customize 메뉴의 지원 여부는 패키지 매니페스트에 선언되어 있습니다. Windows가 제공하는 기본 `…` 메뉴는 유지됩니다.

## 검증

```powershell
dotnet run --project tests/MusicWidget.Checks
# Windows 미디어 세션을 읽기만 하며 재생은 변경하지 않습니다.
dotnet run --project tests/MusicWidget.Checks -- --live
```

세션 선택, 미연결/미지원 버튼, 재생 상태별 명령, 메타데이터 이스케이프, 고정 크기 및 사용자 지정 메뉴 비활성 선언 등을 검사합니다. 빌드 시 라이트·다크 샘플 이미지를 `artifacts/package/Assets/preview-*.png`로 렌더링합니다. 샘플 곡은 가상 데이터입니다.

현재 검증: 16개 검사 통과, 라이트·다크 미리보기 확인, MSIX 빌드 및 이 PC의 v1.0.0.0 개발 등록 완료(Status: Ok). 설치된 Chrome YouTube Music PWA의 앱 ID 인식을 확인했습니다. 확인 시 재생 중인 미디어 세션이 없어 실제 곡 정보·썸네일 수신과 재생/곡 전환, Windows 위젯 패널 내 클릭은 아직 실기 검증하지 못했습니다.

참고 문서: [Windows 미디어 세션 API](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession), [Windows 위젯 매니페스트](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-provider-manifest).
