# 오디오 시스템(배경음악 + UI 효과음) 설계

## 배경

`SettingsPanel`에 배경음악/효과음 볼륨 슬라이더가 있지만 아직 실제로 아무것도 재생하지 않는다
(연결할 오디오 시스템 자체가 없음). 이번에 그 시스템을 만든다. 오디오 에셋 파일은 아직 없고
사용자가 직접 나중에 넣을 예정이라, 클립이 비어 있어도 죽지 않는 구조로 만든다.

범위: **UI 효과음(버튼 호버) + 배경음악(하나로 루프)**만. 나레이션 타이핑 효과음은 "카톡 알림
같은 느낌이라 안 어울린다"는 이유로 이번 범위에서 뺀다.

## 방식

### `AudioManager` — 새 싱글턴

`Assets/02_Script/Runtime/Systems/AudioManager.cs`. `DataManager`와 동일한 패턴(씬에 배치된
평범한 MonoBehaviour, `public static AudioManager Instance`, `DontDestroyOnLoad` 없음 — 이
프로젝트는 씬이 하나뿐이고 "나가기"가 씬 리로드라, 다른 싱글턴들과 마찬가지로 리로드마다 새로
초기화되는 게 맞다).

- `AudioSource bgmSource`(loop=true), `AudioSource sfxSource`(`PlayOneShot`으로 여러 개 겹쳐도
  괜찮게)
- `[SerializeField] AudioClip bgmClip;`, `[SerializeField] AudioClip hoverSound;` — 둘 다 처음엔
  비워둔다. 사용자가 나중에 오디오 파일을 가져오면 인스펙터에서 끌어넣기만 하면 된다
- `Awake()`: `RegisterHoverSounds()` 호출
- `Start()`: `bgmClip`이 있으면 `bgmSource.clip = bgmClip; bgmSource.Play();` — 없으면 아무것도
  안 함(에러 없음)
- `public void SetBgmVolume(float volume) => bgmSource.volume = volume;`
- `public void SetSfxVolume(float volume) => sfxSource.volume = volume;`
- `public void PlayHoverSound() { if (hoverSound != null) sfxSource.PlayOneShot(hoverSound); }`
- `public void RegisterHoverSounds()` — `FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)`로
  씬의 모든 Button(비활성 포함)을 찾아서, 아직 `ButtonHoverSound`가 없으면 붙인다. 이미 붙어있는
  버튼은 건너뛴다(중복 방지) — 그래서 여러 번 호출해도 안전하다

### `ButtonHoverSound` — 새 작은 컴포넌트

`Assets/02_Script/Runtime/UI/ButtonHoverSound.cs`. `MonoBehaviour, IPointerEnterHandler` 하나뿐:

```csharp
public void OnPointerEnter(PointerEventData eventData) => AudioManager.Instance?.PlayHoverSound();
```

### `SettingsPanel` 슬라이더 연결

`bgmVolumeSlider`/`sfxVolumeSlider`의 `onValueChanged`가 `AudioManager.Instance`를 직접 호출한다
(타이핑 속도 슬라이더처럼 `ChatBootstrap`을 거치지 않음 — `AudioManager`도 `DataManager`처럼
전역 싱글턴이라 바로 접근 가능하고, 굳이 이벤트로 한 겹 더 감쌀 이유가 없다):

```csharp
bgmVolumeSlider.onValueChanged.AddListener(value => AudioManager.Instance?.SetBgmVolume(value));
sfxVolumeSlider.onValueChanged.AddListener(value => AudioManager.Instance?.SetSfxVolume(value));
```

### 런타임에 새로 생기는 버튼 보완

`AudioManager.Awake()`의 1회 스캔은 부팅 시점에 씬에 이미 있는 버튼(메인 메뉴, 설정, 원형 메뉴,
채팅 입력창 전송 버튼 등)은 다 잡지만, **코드로 나중에 생성되는 버튼**(`KeywordIntroPanel`이
키워드마다 만드는 버튼, `HistoryPanel.Populate()`가 항목마다 만드는 버튼)은 그 시점에 아직
존재하지 않아서 못 잡는다. 두 곳에 각각 생성 직후 한 줄씩 추가한다:

- `KeywordIntroPanel.Awake()`: 키워드 버튼들을 다 만든 뒤 `AudioManager.Instance?.RegisterHoverSounds();`
- `HistoryPanel.Populate()`: 항목 버튼들을 다 만든 뒤 `AudioManager.Instance?.RegisterHoverSounds();`

`RegisterHoverSounds()`가 이미-붙어있는 버튼을 건너뛰므로 재호출 비용은 씬의 Button 목록을 한 번
더 훑는 정도라 부담 없다.

### 씬

`PlayScene.unity`에 `AudioManager` GameObject를 하나 추가한다(`AudioSource` 컴포넌트 2개 포함,
Canvas와는 무관 — 오디오라 UI 계층에 넣을 필요 없음). `bgmClip`/`hoverSound`는 비워둔 채로 커밋
한다 — 사용자가 파일을 넣으면 인스펙터에서 연결한다.

## 테스트

`AudioManager`/`ButtonHoverSound`는 MonoBehaviour + Unity 오디오/이벤트 API 위주라 이 프로젝트의
다른 UI 스크립트들과 같은 이유로 EditMode 자동 테스트 대상이 아니다. 컴파일 확인 + 수동 Play
모드 검증으로 마무리한다(클립이 없어도 에러 안 나는지, 볼륨 슬라이더가 무반응으로 조용히 넘어가는지
확인 — 실제 소리 확인은 사용자가 오디오 파일을 넣은 뒤 별도로).

## 범위 밖 (later)

- 나레이션 타이핑 효과음 — 이번엔 뺌("카톡 알림 같은 느낌")
- 오디오 파일 자체 — 사용자가 직접 준비해서 나중에 인스펙터에 연결
- 메인 메뉴/게임 중 배경음악 트랙 분리 — 지금은 하나로 계속 루프
- 볼륨 설정 저장(persist) — 지금은 껐다 켜면 초기화(다른 설정값들과 동일하게 아직 저장 시스템 없음)
