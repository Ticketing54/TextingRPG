# 로컬 저장 + 히스토리(읽기 전용) 설계

## 배경

메인 메뉴에 "이어하기"/"히스토리" 버튼을 만들어뒀지만 세이브 시스템 자체가 없어 둘 다
비활성 상태였다. 이번엔 "저장 + 히스토리"까지만 만든다 — 실제 게임 재개("이어하기")는
`ChatController`를 저장된 상태로 되살리는 별도 작업이 필요해 다음으로 미룬다.

## 방식

로컬 JSON 파일, `Application.persistentDataPath`에 저장. 이미 프로젝트 의존성인
Newtonsoft.Json 사용 (`GeminiProvider`가 이미 씀 — 새 패키지 없음).

```
persistentDataPath/saves/
├─ current.json        진행 중 게임 1개
└─ history/
   ├─ index.json         완료작 목록 (가벼움: id/title/endingTone/날짜)
   └─ <id>.json           완료작 하나의 전체 GameSaveData
```

## 추가

- `Systems/GameSaveData.cs` — `GameSaveData`(PlayerName, Outline, ImportantFacts, History,
  LastChoices, Tally, EndingTone, SavedAtIso), `HistoryEntry`, `HistoryIndex`
- `Systems/SaveSystem.cs` — 정적 클래스. `SaveCurrent`/`LoadCurrent`/`HasCurrent`/
  `ArchiveAsHistory`/`LoadHistoryIndex`/`LoadHistoryEntry`. 전부 try/catch — 저장 실패로
  게임이 죽지 않는다. `internal static string RootOverride`로 테스트 시 실제 세이브 폴더를
  안 건드리게 경로를 바꿔치기할 수 있다.
- `UI/HistoryPanel.cs` — `SaveSystem.LoadHistoryIndex()`로 목록 버튼 생성, `OnEntrySelected(id)`
  / `OnBackClicked` 이벤트
- 테스트: `Tests/EditMode/SaveSystemTests.cs` — 임시 폴더로 라운드트립/아카이브 검증

## 변경

- `ChatController.ProceedWithTurn.onSuccess`: off-topic이 아닌 매 턴 끝에
  `SaveSystem.SaveCurrent(BuildSaveData(""))` (진행 저장). `ending`이면 대신
  `SaveSystem.ArchiveAsHistory(BuildSaveData(response.EndingTone))` (히스토리로 이동,
  `current.json` 삭제). 새 게임을 다시 시작하면 이전 `current.json`은 그냥 덮어써진다 —
  "이어하기"가 아직 없어서 미완주 세이브를 보존할 이유가 없다 (나중에 재고)
- `ChatBubbleListView.LoadHistory(List<ChatMessage>)` — 신규. 기존 버블 정리 →
  저장된 메시지를 순서대로 타이핑 없이 즉시 표시(`Play` 직후 `Skip`) → `Lock()`으로 입력 잠금
- `ChatInputView.DisableForReadOnly()` — 신규. `Bind()`(컨트롤러 필요) 없이 입력만 잠근다.
  히스토리 화면에서 이걸 씀
- `MainMenuPanel`: `historyButton`을 더 이상 강제 비활성하지 않음, `OnHistoryClicked` 이벤트 추가
- `ChatBootstrap`: 메인 메뉴 → 히스토리 → 항목 선택 → (기존 `chatUIRoot`를 읽기 전용으로 재사용)
  흐름 추가. `historyPanel` 필드 신규

## 씬

`Canvas/ChatUI/HistoryPanel` 추가 (MainMenuPanel과 동일한 다크 패널 스타일):
타이틀 + `EntryContainer`(VerticalLayoutGroup, 스크롤 없음) + `EntryButtonTemplate`(비활성
템플릿, 인스턴스화용) + `EmptyLabel`("아직 완료한 이야기가 없다") + `BackButton`.

## 범위 밖 (later)

- **이어하기(실제 재개)** — `ChatController`를 저장 상태로 되살려 플레이 이어가기. 가장 큰
  다음 작업
- 히스토리 목록에 스크롤 없음 — 항목이 많아지면 화면을 넘어감. 지금은 몇 개 안 될 거라 보류
- 진행 중 게임이 있는데 "새 게임"을 누르면 그냥 덮어씀 — 경고나 보존 없음
- 설정 화면 (여전히 비활성)
