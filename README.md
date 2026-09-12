# Music Controller

AI Usage Widget의 C# / Windows App SDK 구조를 바탕으로 만든 Windows 11 음악 위젯입니다. YouTube Music PWA의 Windows 미디어 세션을 이용합니다.

- **중간 크기 고정**, Customize widget 메뉴 없음
- 정사각형 앨범 썸네일, 노래 제목, 가수 이름
- YouTube Music 앱 열기 / 이전 곡 / 재생·일시정지 / 다음 곡 / 좋아요·취소
- Windows 밝은·어두운 테마 및 고대비 대응
- 별도 데스크톱 미리보기 앱 제공, 창 크기 고정
- Windows 표시 언어에 관계없이 위젯·데스크톱 UI는 영어만 사용

## 사용

1. Chrome 또는 Edge에 설치한 **YouTube Music PWA**를 열고 곡을 한 번 재생합니다.
2. 개발 등록 후 **Win + W → 위젯 추가 → Music Controller**를 고정합니다.
3. 위젯 버튼으로 앱 열기, 이전 곡, 재생·일시정지, 다음 곡, 좋아요·취소를 제어합니다.

왼쪽 앱 열기 버튼은 연결 전에도 사용할 수 있습니다. 실행 중인 YouTube Music PWA 창이 있으면 최소화를 복원하고 포커스를 이동하며, 없으면 설치된 앱 목록에서 찾아 실행합니다. PWA가 설치되어 있지 않으면 YouTube Music 웹사이트를 엽니다.

재생 제어에는 실행 중인 PWA의 Windows 미디어 세션이 필요합니다. 최소화해도 세션을 제공하는 동안 사용할 수 있습니다. 곡을 재생하기 전이나 앱을 종료한 뒤에는 연결 안내와 비활성 재생·좋아요 버튼이 표시됩니다. 썸네일이 없으면 기본 이미지가 표시됩니다. 긴 제목·가수명은 한 줄로 잘라 표시하며 데스크톱에서는 마우스를 올려 전체 내용을 볼 수 있습니다.

오른쪽 좋아요 버튼은 PWA의 현재 곡에 좋아요를 설정하고, 선택된 상태에서 다시 누르면 취소합니다. 실제 앱의 선택 상태를 읽어 아이콘에 반영합니다. 앱의 플레이어 바를 읽지 못하면 좋아요 버튼만 비활성화되며, 앱 열기 버튼으로 창을 연 뒤 다시 확인할 수 있습니다.

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

좋아요는 Windows 미디어 세션 API에서 제공하지 않아 Windows UI Automation의 `TogglePattern`으로 PWA 플레이어 바의 실제 버튼을 제어합니다. 현재 곡 제목과 가수가 일치하는 플레이어만 허용하며, 여러 창이 일치하면 동작하지 않습니다. 한국어·영어 좋아요 버튼을 지원하며, 브라우저 접근성이나 YouTube Music 화면 구조가 바뀌면 사용하지 못할 수 있습니다. UI Automation은 별도 스레드에서 실행하고 시간 초과 후 늦은 버튼 실행을 막습니다. 앱 열기는 Win32 창 활성화를 사용하므로 Windows가 포커스 이동을 거부하면 안내가 표시됩니다.

위젯 본문은 Windows 호스트가 Adaptive Card로 렌더링하므로 버튼 모양과 간격은 WPF 데스크톱 미리보기와 일부 다를 수 있습니다. 크기와 Customize 메뉴의 지원 여부는 패키지 매니페스트에 선언되어 있습니다. Windows가 제공하는 기본 `…` 메뉴는 유지됩니다.

## 검증

```powershell
dotnet run --project tests/MusicWidget.Checks
# Windows 미디어 세션을 읽기만 하며 재생은 변경하지 않습니다.
dotnet run --project tests/MusicWidget.Checks -- --live
```

세션 선택, 미연결/미지원 버튼, 재생 상태별 명령, 메타데이터 이스케이프, 고정 크기 및 사용자 지정 메뉴 비활성 선언 등을 검사합니다. 빌드 시 라이트·다크 샘플 이미지를 `artifacts/package/Assets/preview-*.png`로 렌더링합니다. 샘플 곡은 가상 데이터입니다.

버튼 추가 검증: 24개 검사 통과, 라이트·다크 미리보기 확인 및 MSIX 빌드 완료. 설치된 Chrome YouTube Music PWA에서 현재 곡의 좋아요 버튼과 선택 상태를 읽는 동작을 확인했으며 최소화 상태에서도 읽을 수 있었습니다. 실제 계정의 좋아요 변경은 별도 실기 검증이 필요합니다.

버튼 잘림 수정: Adaptive Card의 양옆 빈 열과 자동 너비 ActionSet을 제거하고, 다섯 버튼이 각각 같은 너비의 열을 사용하도록 변경했습니다. 최소화 복원은 비동기 복원 요청 직후 포커스를 시도하는 대신 복원 명령 처리가 끝난 뒤 활성화합니다. 포커스를 담당하는 작업 스레드의 메시지 큐를 준비하고, 실제 전경 창과 최소화 상태로 성공 여부를 확인합니다. 앱 열기는 좋아요 접근성 조회의 잠금과 분리했습니다.

실제 Windows 위젯 패널의 300px 카드에서 다섯 버튼이 잘리지 않고 보이는 것을 캡처로 확인했습니다. 해당 카드의 앱 열기 버튼을 직접 호출해 최소화된 기존 YouTube Music 창이 복원되고 전경 창으로 활성화되는 것도 확인했습니다. 캡처: `artifacts/live-widget.png`.

참고 문서: [Windows 미디어 세션 API](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession), [Windows 위젯 매니페스트](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-provider-manifest), [창 활성화](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow), [UI Automation 스레드](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-threading-issues).
