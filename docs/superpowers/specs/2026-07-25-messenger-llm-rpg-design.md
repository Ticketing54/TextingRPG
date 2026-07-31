# TextingRPG — 메신저 스타일 LLM 연동 RPG 설계

## 개요

플레이어가 메신저(채팅) UI로 NPC와 자유 텍스트로 대화하며 진행하는 RPG. NPC 대사는 실시간으로 LLM이 생성하고, 대화 결과가 호감도/스탯/퀘스트 등 게임 상태에 반영된다. 스탯, 인벤토리, 레벨업, 별도 전투 화면을 포함한 정식 RPG 시스템을 갖춘다. 장르/세계관은 플레이어가 게임 시작 시 몇 가지 프리셋(판타지/현대 연애/생존 등) 중에서 선택한다.

**타겟 플랫폼**: PC 스탠드얼론 (Windows). 클라이언트에서 LLM API를 직접 호출한다.

**LLM 제공자**: 초기에는 Gemini API를 사용하고, 추후 로컬 LLM(Ollama 등)으로 교체 가능하도록 설계한다.

**설계 제약**: LLM API 비용이 핵심 제약이다. 턴당 API 호출 횟수를 늘리지 않는 방향을 우선하고, 사용량이 실제로 비용 문제가 될 때만 단계적으로 구조를 확장한다 (자세한 내용은 [비용 관리 전략](#비용-관리-전략) 참고).

## 아키텍처

```
Presentation
 ├─ ChatUI        — 메신저 스타일 대화창 (자유 텍스트 입력)
 ├─ CombatUI      — 별도 턴제 전투 화면
 └─ StatusUI      — 스탯/인벤토리 화면

Core
 ├─ PlayerState        — 스탯, 인벤토리, 퀘스트 플래그, NPC별 호감도, 대화 히스토리, 현재 StoryNode (직렬화 가능, 세이브 대상)
 ├─ GenrePreset (ScriptableObject)   — 세계관 설명(시스템 프롬프트 조각), 시작 스탯, NPC 로스터, 아이템/퀘스트 풀, 전투 대사 풀
 ├─ NPCDefinition (ScriptableObject) — 이름, 페르소나 설명, 초기 호감도, 초상화
 ├─ StoryGraph (ScriptableObject)    — NPC/퀘스트별 분기 노드 그래프. 노드 전이 조건은 IntentTag 조합
 ├─ EnemyDefinition (ScriptableObject) — 전투용 적 스탯, 드롭 테이블, 등장 조건
 └─ EffectApplier      — LLM 응답의 부수효과(effects)를 검증 후 PlayerState에 반영 (화이트리스트/클램프)

LLM Layer
 ├─ ILLMProvider         — SendMessage(context) → LLMResponse { reply, tags, effects } (인터페이스)
 ├─ GeminiProvider       — 현재 구현체. UnityWebRequest + `responseSchema`(구조화 출력)로 reply/tags/effects를 한 번의 호출로 강제 출력
 ├─ MockLLMProvider      — 테스트/에디터용, 고정 응답 반환
 ├─ IIntentClassifier    — (추후 확장 지점) 자유 입력 → IntentTag 분류만 담당하는 인터페이스. 초기에는 별도 구현 없이 GeminiProvider 내부에 통합되어 있음
 └─ (추후) LocalLLMProvider / LocalIntentClassifier — Ollama 등 로컬 추론 서버 연동, 인터페이스만 맞추면 교체 가능

Combat (LLM 비개입)
 └─ CombatManager — 순수 수치/턴제 로직. 대사는 GenrePreset의 전투 대사 풀에서 선택. PlayerState 스탯을 읽고 결과를 기록.
```

핵심 경계선은 `ILLMProvider`다. LLM은 오직 메신저 대화에서만 사용되며, 전투는 완전히 분리된 결정론적 시스템이라 API 비용이 들지 않고 즉각 반응한다. 데이터(장르 프리셋, NPC, 적, 아이템)는 대부분 ScriptableObject로 관리해, 콘텐츠 추가 시 코드 수정 없이 에셋만 추가하면 되게 한다.

대화 의도 분류(`tags` 산출)와 대사 생성(`reply` 산출)은 개념적으로 분리된 책임이지만, 초기 구현에서는 비용 절감을 위해 **한 번의 API 호출**로 묶는다. `IIntentClassifier` 인터페이스는 이 분리 지점을 코드상에 표시해두는 역할만 하며, 지금 당장 별도 구현체를 만들지는 않는다.

## 데이터 모델

- **GenrePreset**: id, name, worldDescription(시스템 프롬프트 조각), startingStats, npcRoster, itemPool, questPool, combatFlavorTexts
- **NPCDefinition**: id, name, personaDescription, initialRelationship, portrait
- **StoryGraph**: id(NPC/퀘스트에 연결), 노드 목록. 각 노드는 **StoryNode**: id, npcId, sceneDescription(해당 노드에서 시스템 프롬프트에 추가되는 상황 설명), transitions(허용 IntentTag → 다음 노드 id 매핑)
- **IntentTag**: 작가가 미리 정의한 유한한 의도/톤 카테고리 (예: 우호적/회유/위협/정보요청/거절). NPC/퀘스트 단위로 사용 가능한 태그 집합을 제한할 수 있음
- **EnemyDefinition**: id, name, stats(hp/atk/def 등), dropTable, spawnCondition
- **PlayerState** (런타임, 직렬화 대상): stats(dict), inventory(list), questFlags(dict), relationships(NPC별 dict), conversationHistories(NPC별), currentStoryNode(NPC별)
- **ChatMessage**: sender, text, timestamp
- **LLMResponse** (Gemini `responseSchema`로 강제되는 구조화 출력, 한 번의 호출로 세 값 모두 생성): `{ reply: string, tags: [IntentTag], effects: [{ type, target, delta }] }`

## 대화 흐름

1. 플레이어가 ChatUI에 자유 텍스트 입력 후 전송
2. ChatManager가 요청 컨텍스트 조립: `system prompt = GenrePreset.worldDescription + NPCDefinition.persona + 현재 StoryNode.sceneDescription + 현재 호감도/관련 스탯 스냅샷 + 최근 N개 대화 히스토리(토큰 예산 내 truncate/요약)`. 프롬프트에는 현재 노드에서 허용되는 IntentTag 목록도 포함해 분류 정확도를 높인다
3. `ILLMProvider.SendMessage(context)` 호출. `GeminiProvider`는 `responseSchema`로 `{reply, tags, effects}` 스키마를 한 번의 호출로 강제
4. 응답 수신:
   - `reply`는 ChatUI에 NPC 메시지로 렌더
   - `effects`는 EffectApplier가 화이트리스트/클램프 검증 후 PlayerState에 반영 (호감도 변화, 스탯 변화, 아이템 지급, 퀘스트 플래그 갱신)
   - `tags`는 현재 StoryNode의 transitions와 매칭되어 다음 노드로 전이 (매칭되는 태그가 없으면 같은 노드 유지)
5. 상태 변화는 이벤트(`OnRelationshipChanged`, `OnQuestUpdated`, `OnStoryNodeChanged` 등)로 방출되어 StatusUI, 퀘스트 시스템, 전투 진입 조건 등이 구독해 반응
6. 대화 메시지는 NPC별 히스토리에 저장 (다음 대화 컨텍스트 + 세이브 데이터로 사용)

**effects는 최종 수치 결정권을 게임 코드가 갖는다.** LLM은 "무슨 효과가 발생했는지"만 제안하고, EffectApplier가 타입별 허용 범위(예: 호감도 ±1~3)로 클램프한다. 정의되지 않은 target/type은 무시하고 로그만 남긴다. `tags`도 동일하게 취급한다 — 현재 노드에서 허용되지 않은 태그는 무시하고 로그만 남긴다.

## 비용 관리 전략

LLM API 비용이 핵심 설계 제약이므로, 다음 원칙을 우선 적용한다.

- **호출 횟수 고정**: 한 턴당 API 호출은 항상 1회. 의도 분류(`tags`)와 대사 생성(`reply`)을 별도 호출로 나누지 않는다 — 나누면 오히려 호출 수가 늘어 비용이 커진다
- **프롬프트 압축**: 시스템 프롬프트에는 그 턴에 필요한 조각만 포함 (전체 GenrePreset이 아닌 worldDescription 요약, 관련 NPC/노드 정보만)
- **응답 길이 캡**: `max_tokens` 제한 + 시스템 프롬프트에 "대사는 N문장 이내" 가이드라인 명시
- **히스토리 압축**: 대화 히스토리가 쌓이면 최근 N턴은 원문 그대로, 그 이전은 요약본으로 압축해 프롬프트에 포함
- **요청 쿨다운**: 세션당 요청 빈도 제한으로 의도치 않은 폭주 방지 (에러 처리 섹션과 동일)

**에스컬레이션 경로 (B안)**: 위 조치로도 실사용 비용이 부담스러워지면, `tags` 분류를 저가/로컬 모델(`IIntentClassifier`의 별도 구현체)로 떼어내는 2단계 파이프라인으로 전환한다. 이 경우 `GeminiProvider`는 분류된 태그를 프롬프트에 포함해 대사 생성에만 집중하므로 호출은 2회로 늘지만, 각 호출의 프롬프트가 짧아지고 대사 생성에는 Gemini의 더 저렴한 모델(예: Flash 계열)을 쓸 수 있는 여지도 생긴다. 지금 단계에서는 인터페이스만 분리해두고 실제 전환은 사용량 데이터가 쌓인 뒤 별도로 판단한다.

## 전투 시스템

- 턴제 루프: 플레이어 액션 선택(공격/스킬/아이템/방어) → 수치 계산 → 적 액션 → 반복
- 대사/연출은 GenrePreset에 딸린 전투 대사 풀에서 랜덤/조건부 선택 (장르별 톤 유지, LLM 미사용)
- 결과(승리/패배, 드롭 아이템, 경험치)는 PlayerState에 기록
- 진입 경로는 두 가지: (1) 필드에서 조우, (2) 대화 중 `effects`에 `type: "start_combat"` 트리거 포함 — 단, 일단 전투에 진입하면 이후 로직은 완전히 수치 기반

## 엔딩 소설화

게임이 엔딩에 도달하면, 플레이 내역을 바탕으로 챕터 단위의 노벨라를 생성하는 기능.

- **트리거**: 엔딩 조건 도달 후 자동 생성이 아니라, 플레이어가 "소설 생성" 버튼을 눌러 명시적으로 요청한다. 매 턴 대화와 달리 비용이 챕터 수만큼 누적되므로, 발생 여부를 플레이어 선택에 맡겨 불필요한 비용을 막는다
- **입력 데이터**: 원문 대화 전체가 아니라, PlayerState에 이미 쌓여 있는 압축 히스토리(요약본)와 주요 이벤트 로그(StoryNode 전이 기록, 호감도 변화, 퀘스트/전투 결과)를 사용한다. 대화 흐름 섹션의 히스토리 압축 방식을 그대로 재사용하므로 별도 데이터 파이프라인이 필요 없다
- **생성 방식**: 챕터별로 순차 호출한다(호출 1회 = 챕터 1개). 챕터 수는 플레이 중 발생한 주요 이벤트 구간 수에 따라 동적으로 정하되, 상한(`maxChapters`)을 둬 비용이 무한정 늘지 않게 한다. 각 챕터 호출 시 직전 챕터의 요약을 컨텍스트로 포함해 문체와 개연성을 유지한다
- **비용 관리**: 챕터당 `max_tokens` 캡 적용. 대화 생성과 달리 응답 속도보다 문체 품질이 중요하므로, 굳이 저가 모델을 강제하지 않고 필요 시 더 나은 모델을 챕터 생성에 한해 허용할 수 있다 — 어차피 플레이당 1회성 이벤트라 전체 비용에서 차지하는 비중이 매 턴 대화보다 작다
- **결과 표시**: 별도 화면(NovelUI)에서 챕터별로 열람. 로컬 텍스트 파일로 내보내는 정도만 지원하고, 그 이상(삽화 생성, 음성 낭독 등)은 범위 밖으로 둔다
- **수익화 지점**: 소설 생성 자체(엔딩 도달 후 미리보기/일부 챕터)는 무료로 열람 가능하되, 전체 텍스트본을 내보내거나 전체 챕터를 받으려면 결제가 필요한 흐름으로 갈 예정이다. 다만 실제 결제 시스템(IAP, 인앱결제 연동 등) 구현은 이번 스펙 범위 밖이며, UI/데이터 흐름에서 "결제 여부 확인" 지점만 인터페이스로 남겨두고 나중에 별도 스펙에서 붙인다

## 에러 처리 & 안전장치

- **네트워크/API 장애**: 1회 자동 재시도 → 실패 시 폴백 문구("NPC가 잠시 답이 없다")로 대체, effects는 발생시키지 않음 (상태 불변 유지)
- **스키마 검증 실패**: 1회 더 엄격한 재프롬프트로 재시도 → 실패 시 `reply`만 안전 문구로 대체, `effects`는 빈 배열
- **Effects 이상치**: EffectApplier의 화이트리스트/클램프가 1차 방어선. 통과 못한 값은 무시 + 로그
- **비용/폭주 방지**: 세션당 요청 쿨다운, 개발 중 요청/응답 로컬 로깅으로 재현 가능하게
- **프롬프트 인젝션 방어**: system prompt에 페르소나 고정 지침 명시, 캐릭터 이탈 유도 입력에 대한 기본 가드레일 포함 (완전 차단은 목표하지 않음)

## 테스트 전략

- `MockLLMProvider`로 실제 API 호출 없이 ChatUI, EffectApplier 클램프 로직, 대화→전투 트리거 흐름을 에디터에서 반복 테스트
- 전투 시스템은 결정론적이므로 일반 유닛 테스트로 데미지 계산/승패 판정/드롭 테이블 검증
- LLM 응답 품질은 자동화가 어려워 수동 플레이테스트로 확인. 프리셋별 "골든 시나리오" 대화 스크립트를 만들어 프롬프트 변경 시 회귀 비교에 활용
- 세이브/로드 후 PlayerState(스탯/인벤토리/대화 히스토리/호감도)가 온전히 복원되는지 검증
- 엔딩 소설화는 `MockLLMProvider`로 챕터 상한(`maxChapters`) 준수, 이전 챕터 컨텍스트 전달 여부를 에디터에서 검증

## 범위 밖 (이번 스펙에서 다루지 않음)

- 모바일 배포, 서버 프록시 경유 (방식 B) — 필요해지면 별도 스펙으로 다룬다
- 로컬 LLM(Ollama 등) 구현체 자체 — 인터페이스만 준비하고, 실제 구현은 이후 스펙
- 멀티플레이어, 클라우드 세이브
- 엔딩 소설의 삽화 생성, 음성 낭독(TTS) 등 텍스트 이외의 부가 연출
- 결제 시스템(IAP 등) 실제 구현 — 소설 전체 텍스트본을 유료화한다는 방향만 명시하고, 결제 연동 자체는 이후 별도 스펙에서 다룬다
