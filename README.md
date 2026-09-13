# YT Music Controller

AI Usage Widget의 C# / Windows App SDK 구조를 바탕으로 만든 Windows 11 음악 위젯입니다. YouTube Music PWA의 Windows 미디어 세션을 이용합니다.

- **중간 크기 고정**, Customize widget 메뉴 없음
- 정사각형 앨범 썸네일, 노래 제목, 가수 이름
- YouTube Music 앱 열기 / 이전 곡 / 재생·일시정지 / 다음 곡 / 좋아요·취소
- 배경 없는 단색 아이콘 버튼: 다크 테마는 흰색, 라이트 테마는 검은색
- Windows 밝은·어두운 테마 및 고대비 대응
- 별도 데스크톱 미리보기 앱 제공, 창 크기 고정
- Windows 표시 언어에 관계없이 위젯·데스크톱 UI는 영어만 사용

## 사용

1. Chrome 또는 Edge에 설치한 **YouTube Music PWA**를 열고 곡을 한 번 재생합니다.
2. 개발 등록 후 **Win + W → 위젯 추가 → YT Music Controller**를 고정합니다.
3. 위젯 버튼으로 앱 열기, 이전 곡, 재생·일시정지, 다음 곡, 좋아요·취소를 제어합니다.

왼쪽 앱 열기 버튼은 연결 전에도 사용할 수 있습니다. 실행 중인 YouTube Music PWA 창이 있으면 최소화를 복원하고 포커스를 이동하며, 없으면 설치된 앱 목록에서 찾아 실행합니다. PWA가 설치되어 있지 않으면 YouTube Music 웹사이트를 엽니다.

재생 제어에는 실행 중인 PWA의 Windows 미디어 세션이 필요합니다. 최소화해도 세션을 제공하는 동안 사용할 수 있습니다. 곡을 재생하기 전이나 앱을 종료한 뒤에는 연결 안내와 비활성 재생·좋아요 버튼이 표시됩니다. 썸네일이 없으면 기본 이미지가 표시됩니다. 긴 제목·가수명은 한 줄로 잘라 표시하며 데스크톱에서는 마우스를 올려 전체 내용을 볼 수 있습니다.

오른쪽 좋아요 버튼은 PWA의 현재 곡에 좋아요를 설정하고, 선택된 상태에서 다시 누르면 취소합니다. 최소화 상태에서도 사용할 수 있으며 실제 앱의 선택 상태를 읽어 아이콘에 반영합니다. 최소화 중 오래된 플레이어 정보가 남지 않도록 화면 노출과 포커스 이동 없이 백그라운드에서 갱신합니다. 앱 연결이 끊기거나 플레이어 정보를 확인할 수 없을 때만 좋아요 버튼이 비활성화됩니다.

설정·로그인 화면, 진행 바, 음량, 반복, 셔플은 없습니다. Google 로그인은 기존 PWA에서 진행합니다. 이 앱은 Google 계정 정보, 브라우저 쿠키, 비밀번호를 읽지 않습니다.

## 빌드 및 등록

Windows 11 22H2 이상 / x64 / .NET 10 SDK / Windows SDK(makeappx, makepri) / Windows Web Experience Pack이 필요합니다. 최초 빌드에는 NuGet 연결이 필요합니다.

프로젝트 폴더에서 다음 명령으로 빌드·검사·재설치를 한 번에 실행합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-install.ps1
```

다른 폴더에서도 스크립트의 전체 경로를 지정하면 실행할 수 있습니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\jwide\Mossworm\Workspace\music-widget\build-install.ps1"
```

스크립트는 Release 빌드, 31개 검사, 자체 포함 x64 게시, 미리보기·리소스 생성을 완료한 뒤 `artifacts/publish/`를 최신 빌드 파일로 교체하고 현재 사용자의 개발 등록을 갱신합니다. 기본 실행에서는 MSIX를 생성하지 않습니다. 설치 교체 중 기존 실행 파일은 임시 폴더에 보관하고, 등록에 실패하면 복원한 뒤 임시 파일을 삭제합니다. 실행 중인 이 프로젝트의 위젯 제공자와 데스크톱 미리보기는 교체 직전에 종료합니다. 완료 후 **Win + W**로 열고, 카드가 없다면 **위젯 추가 → YT Music Controller → 고정**을 선택하세요.

설치 없이 빌드만 하려면 `-BuildOnly`를 붙입니다. 이 경우 결과물은 `artifacts/publish/`에 생성되며 설치된 파일은 교체하지 않습니다. 다음 성공한 빌드가 이전 내용을 덮어쓰므로 가장 최근 빌드만 남습니다.

**Microsoft Store 제출용 MSIX**는 다음 명령으로 생성합니다. 로컬 개발 등록은 변경하지 않습니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-install.ps1 -Msix
```

결과는 `artifacts/msix/<Identity.Name>_<Version>_x64.msix`에 저장됩니다. 현재 매니페스트 기준 파일명은 `Mossworm.YTMusicController_1.0.0.0_x64.msix`입니다. 최신 소스를 빌드·검사하고 `resources.pri`를 생성한 뒤 MakeAppx의 기본 패키지 검증을 거쳐 압축합니다. 같은 이름의 파일은 성공한 패키지로 교체합니다.

제출 전에 `packaging/AppxManifest.xml`의 `Identity.Name`, `Identity.Publisher`, `PublisherDisplayName`이 Partner Center의 제품 ID 정보와 일치하는지 확인하세요. MSIX는 서명 없이 생성하며 Store가 인증 후 서명합니다. MakeAppx 검증은 Windows App Certification Kit 검사나 Store 심사를 대신하지 않습니다. [Microsoft Store 패키지 요구 사항](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).

패키지 루트에도 `Microsoft.Windows.Widgets.winmd`를 배치하여 위젯 제공자의 WinRT 메타데이터 검색을 지원합니다. 등록 후 실제 COM 제공자를 활성화해 확인하며, 활성화에 실패해도 기존 파일과 등록으로 복원합니다.

개발 등록에는 Windows 개발자 모드가 필요합니다. 스크립트는 시스템 정책이나 인증서를 변경하지 않습니다. 결과물은 `artifacts/publish/`와 `artifacts/package/`입니다. 등록 후 `artifacts/package`를 이동하거나 삭제하지 마세요. 다른 PC 배포에는 신뢰할 수 있는 서명 또는 Store 배포가 필요합니다.


직접 실행: `artifacts/package/Desktop/MusicWidget.Desktop.exe`.

## 연결 방식 및 제한

Windows `GlobalSystemMediaTransportControlsSessionManager`에서 앨범 이미지와 메타데이터를 읽고 해당 세션의 전송 제어 API를 호출합니다. 보이는 위젯과 데스크톱은 2초 간격으로 갱신합니다. 위젯이 비활성 상태이면 폴링을 멈추고, 내용이 바뀐 경우에만 카드를 게시합니다.

설치 앱 목록의 YouTube Music 앱 ID 또는 Windows 앱 표시 이름으로 PWA를 식별합니다. Chromium이 일반 브라우저 ID를 제공하는 경우에는 같은 브라우저의 창 제목에 YouTube Music과 해당 곡명이 함께 있는 세션만 허용합니다. Windows의 기본 재생 세션으로 무조건 대체하지 않습니다. PWA가 식별 가능한 정보를 제공하지 않으면 연결 안내 상태를 유지합니다. 일반 YouTube 탭과 다른 음악 앱은 지원 대상이 아닙니다. 여러 YouTube Music 세션 중에는 현재 재생 중인 세션을 우선합니다.

버튼은 PWA가 노출한 지원 여부를 따릅니다. 마지막 곡, 광고 등에서 다음/이전 버튼을 제공하지 않거나 요청을 거부할 수 있습니다. 브라우저의 Windows 미디어 연동이 꺼져 있으면 연결되지 않습니다. 표시한 곡이 바뀐 뒤 늦게 도착한 버튼 요청은 무시합니다. 설치 경로나 Chrome 프로필 번호를 하드코딩하지 않습니다.

좋아요는 Windows 미디어 세션 API에서 제공하지 않아 Windows UI Automation의 `TogglePattern`으로 PWA 플레이어 바의 실제 버튼을 제어합니다. 현재 곡 제목과 가수가 일치하는 플레이어만 허용하며, 여러 창이 일치하면 동작하지 않습니다. 한국어·영어 좋아요 버튼을 지원하며, 브라우저 접근성이나 YouTube Music 화면 구조가 바뀌면 사용하지 못할 수 있습니다. UI Automation은 별도 스레드에서 실행하고 시간 초과 후 늦은 버튼 실행을 막습니다. 앱 열기는 Win32 창 활성화를 사용하므로 Windows가 포커스 이동을 거부하면 안내가 표시됩니다.

Chromium은 최소화 중 접근성 트리에 이전 곡 정보를 남길 수 있습니다. 좋아요 조회·변경 전에 최소화된 창에 일시적으로 투명·입력 통과 속성을 적용하고, 포커스 없이 잠시 복원해 정보를 갱신한 다음 최소화와 기존 창 속성을 복구합니다. 포커스 이동은 `SW_SHOWNOACTIVATE` / `SW_SHOWMINNOACTIVE` 명령으로 방지하며, 작업 표시줄의 창 처리까지 바꾸는 `WS_EX_NOACTIVATE` 속성은 적용하지 않습니다. UI Automation 조회는 창 복구가 끝난 뒤 실행하므로 접근성 응답이 멈춰도 창이 투명한 상태로 남지 않습니다. 위젯과 데스크톱 미리보기의 창 갱신 및 앱 열기는 창별 프로세스 간 잠금으로 조율합니다. 기존에 다른 프로그램이 투명도를 적용한 창은 속성을 덮어쓰지 않으며, 갱신에 실패하면 좋아요를 비활성화합니다.

위젯 본문은 Windows 호스트가 Adaptive Card로 렌더링합니다. 제어 버튼은 기본 ActionSet 대신 투명한 아이콘 이미지의 `selectAction`을 사용하며, 40×36px 클릭 영역과 기능별 접근성 이름을 제공합니다. 위젯의 [`$host.hostTheme`](https://github.com/microsoft/WindowsAppSDK/discussions/3300) 값에 따라 다크 테마에서는 흰색, 라이트 테마에서는 검은색 아이콘을 표시합니다. 사용할 수 없는 버튼은 흐리게 표시하고 입력을 비활성화합니다. 데스크톱 미리보기에도 같은 벡터 아이콘을 사용합니다. 크기와 Customize 메뉴의 지원 여부는 패키지 매니페스트에 선언되어 있습니다. Windows가 제공하는 기본 `…` 메뉴는 유지됩니다.

## 검증

세션 선택, 미연결/미지원 버튼, 재생 상태별 명령, 메타데이터 이스케이프, 고정 크기 및 사용자 지정 메뉴 비활성 선언 등을 검사합니다. 빌드 시 라이트·다크 샘플 이미지를 `artifacts/package/Assets/preview-*.png`로 렌더링합니다. 샘플 곡은 가상 데이터입니다.

버튼 추가 검증: 24개 검사 통과, 라이트·다크 미리보기 확인 및 MSIX 빌드 완료. 설치된 Chrome YouTube Music PWA에서 현재 곡의 좋아요 버튼과 선택 상태를 읽는 동작을 확인했으며 최소화 상태에서도 읽을 수 있었습니다. 실제 계정의 좋아요 변경은 별도 실기 검증이 필요합니다.

버튼 잘림 수정: Adaptive Card의 양옆 빈 열과 자동 너비 ActionSet을 제거하고, 다섯 버튼이 각각 같은 너비의 열을 사용하도록 변경했습니다. 최소화 복원은 비동기 복원 요청 직후 포커스를 시도하는 대신 복원 명령 처리가 끝난 뒤 활성화합니다. 포커스를 담당하는 작업 스레드의 메시지 큐를 준비하고, 실제 전경 창과 최소화 상태로 성공 여부를 확인합니다. 앱 열기는 좋아요 접근성 조회의 잠금과 분리했습니다.

실제 Windows 위젯 패널의 300px 카드에서 다섯 버튼이 잘리지 않고 보이는 것을 캡처로 확인했습니다. 해당 카드의 앱 열기 버튼을 직접 호출해 최소화된 기존 YouTube Music 창이 복원되고 전경 창으로 활성화되는 것도 확인했습니다. 캡처: `artifacts/live-widget.png`.

2026-09-13 포커스 보강: 위젯 작업 스레드를 현재 전경 창 및 YouTube Music 창의 입력 큐에 잠시 연결하고, 창 순서와 활성 창을 함께 갱신한 뒤 연결을 해제합니다. 위젯 패널이 닫히면서 전경 창이 바뀌는 경우 한 번 더 시도하며, 항상 위에 고정하는 설정은 사용하지 않습니다. 실제 Chrome PWA에 공유 앱 열기 코드를 실행하여 다른 Chrome 창 뒤에 가려진 상태와 최소화 상태에서 모두 `GetForegroundWindow`가 YouTube Music이고 최소화가 해제됐음을 확인했습니다. 재설치 후 실제 Windows 위젯 카드의 앱 열기 버튼을 마우스로 클릭한 검사에서도 가림·최소화 두 경우 모두 복원과 전경 활성화를 확인했습니다. 27개 검사 및 `build-install.ps1`을 통한 재등록·COM 제공자 활성화도 통과했습니다.

참고 문서: [Windows 미디어 세션 API](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession), [Windows 위젯 매니페스트](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-provider-manifest), [창 활성화](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow), [UI Automation 스레드](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-threading-issues).

2026-09-13 최소화 좋아요 수정: 실제 Chrome PWA에서 Windows 미디어 세션은 현재 곡을 제공하지만 최소화된 접근성 트리는 이전 곡을 제공하는 문제를 재현했습니다. 백그라운드 갱신 후 현재 곡 조회, 실제 좋아요 변경·원상 복구, 동일 명령 중복 실행, 다른 곡에 대한 변경 거부, 아이콘 상태 조회, 취소 시 창 속성 복구를 검증했습니다. PWA 최소화 상태가 유지되고 검사 중 PWA가 전경으로 활성화되지 않았습니다. Edge 실기 검증은 별도로 필요합니다.

명시적 실기 검사: PWA 하나를 최소화한 뒤 `dotnet run --project tests/MusicWidget.Checks -c Release -- --verify-minimized-like`를 실행합니다. 이 검사는 현재 곡의 실제 좋아요를 잠시 변경하고 `finally`에서 원래 값으로 복구하므로 자동 빌드 검사에는 포함하지 않습니다.

2026-09-13 최소화 창 잔상 수정: 최소화된 PWA의 좋아요 조회 후 좌측 하단에 160×28px 창이 남는 현상을 확인했습니다. 백그라운드 갱신에서 `WS_EX_NOACTIVATE` 속성을 제거해 작업 표시줄의 정상 최소화 처리를 유지합니다. 실제 Chrome PWA에서 반복 조회 5회와 갱신 중 취소 후 창이 모든 모니터 밖에 있고, 최소화·원래 창 속성·좋아요 조회가 유지되며 PWA가 전경으로 활성화되지 않는 것을 확인했습니다. 재생이나 좋아요를 변경하지 않는 회귀 검사는 PWA 하나를 최소화한 뒤 `dotnet run --project tests/MusicWidget.Checks -c Release -- --verify-minimized-refresh`로 실행합니다.
