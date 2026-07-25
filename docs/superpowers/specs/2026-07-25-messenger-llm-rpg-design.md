# TextingRPG — 메신저 스타일 LLM 연동 RPG 설계

## 개요

플레이어가 메신저(채팅) UI로 NPC와 자유 텍스트로 대화하며 진행하는 RPG. NPC 대사는 실시간으로 LLM이 생성하고, 대화 결과가 호감도/스탯/퀘스트 등 게임 상태에 반영된다. 스탯, 인벤토리, 레벨업, 별도 전투 화면을 포함한 정식 RPG 시스템을 갖춘다. 장르/세계관은 플레이어가 게임 시작 시 몇 가지 프리셋(판타지/현대 연애/생존 등) 중에서 선택한다.

**타겟 플랫폼**: PC 스탠드얼론 (Windows). 클라이언트에서 LLM API를 직접 호출한다.

**LLM 제공자**: 초기에는 Claude API를 사용하고, 추후 로컬 LLM(Ollama 등)으로 교체 가능하도록 설계한다.

## 아키텍처

```
Presentation
 ├─ ChatUI        — 메신저 스타일 대화창 (자유 텍스트 입력)
 ├─ CombatUI      — 별도 턴제 전투 화면
 └─ StatusUI      — 스탯/인벤토리 화면

Core
 ├─ PlayerState        — 스탯, 인벤토리, 퀘스트 플래그, NPC별 호감도, 대화 히스토리 (직렬화 가능, 세이브 대상)
 ├─ GenrePreset (ScriptableObject)   — 세계관 설명(시스템 프롬프트 조각), 시작 스탯, NPC 로스터, 아이템/퀘스트 풀, 전투 대사 풀
 ├─ NPCDefinition (ScriptableObject) — 이름, 페르소나 설명, 초기 호감도, 초상화
 ├─ EnemyDefinition (ScriptableObject) — 전투용 적 스탯, 드롭 테이블, 등장 조건
 └─ EffectApplier      — LLM 응답의 부수효과(effects)를 검증 후 PlayerState에 반영 (화이트리스트/클램프)

LLM Layer
 ├─ ILLMProvider         — SendMessage(context) → LLMResponse (인터페이스)
 ├─ ClaudeProvider       — 현재 구현체. UnityWebRequest + tool-use로 구조화 출력 강제
 ├─ MockLLMProvider      — 테스트/에디터용, 고정 응답 반환
 └─ (추후) LocalLLMProvider — Ollama 등 로컬 추론 서버 연동, 인터페이스만 맞추면 교체 가능

Combat (LLM 비개입)
 └─ CombatManager — 순수 수치/턴제 로직. 대사는 GenrePreset의 전투 대사 풀에서 선택. PlayerState 스탯을 읽고 결과를 기록.
```

핵심 경계선은 `ILLMProvider`다. LLM은 오직 메신저 대화에서만 사용되며, 전투는 완전히 분리된 결정론적 시스템이라 API 비용이 들지 않고 즉각 반응한다. 데이터(장르 프리셋, NPC, 적, 아이템)는 대부분 ScriptableObject로 관리해, 콘텐츠 추가 시 코드 수정 없이 에셋만 추가하면 되게 한다.

## 데이터 모델

- **GenrePreset**: id, name, worldDescription(시스템 프롬프트 조각), startingStats, npcRoster, itemPool, questPool, combatFlavorTexts
- **NPCDefinition**: id, name, personaDescription, initialRelationship, portrait
- **EnemyDefinition**: id, name, stats(hp/atk/def 등), dropTable, spawnCondition
- **PlayerState** (런타임, 직렬화 대상): stats(dict), inventory(list), questFlags(dict), relationships(NPC별 dict), conversationHistories(NPC별)
- **ChatMessage**: sender, text, timestamp
- **LLMResponse** (tool-use로 강제되는 구조화 출력): `{ reply: string, effects: [{ type, target, delta }] }`

## 대화 흐름

1. 플레이어가 ChatUI에 자유 텍스트 입력 후 전송
2. ChatManager가 요청 컨텍스트 조립: `system prompt = GenrePreset.worldDescription + NPCDefinition.persona + 현재 호감도/관련 스탯 스냅샷 + 최근 N개 대화 히스토리(토큰 예산 내 truncate/요약)`
3. `ILLMProvider.SendMessage(context)` 호출. `ClaudeProvider`는 tool-use로 `{reply, effects}` 스키마를 강제
4. 응답 수신: `reply`는 ChatUI에 NPC 메시지로 렌더. `effects`는 EffectApplier가 화이트리스트/클램프 검증 후 PlayerState에 반영 (호감도 변화, 스탯 변화, 아이템 지급, 퀘스트 플래그 갱신)
5. 상태 변화는 이벤트(`OnRelationshipChanged`, `OnQuestUpdated` 등)로 방출되어 StatusUI, 퀘스트 시스템, 전투 진입 조건 등이 구독해 반응
6. 대화 메시지는 NPC별 히스토리에 저장 (다음 대화 컨텍스트 + 세이브 데이터로 사용)

**effects는 최종 수치 결정권을 게임 코드가 갖는다.** LLM은 "무슨 효과가 발생했는지"만 제안하고, EffectApplier가 타입별 허용 범위(예: 호감도 ±1~3)로 클램프한다. 정의되지 않은 target/type은 무시하고 로그만 남긴다.

**토큰/비용 관리**: 대화 히스토리가 쌓이면 최근 N턴은 원문 그대로, 그 이전은 요약본으로 압축해 프롬프트에 포함한다.

## 전투 시스템

- 턴제 루프: 플레이어 액션 선택(공격/스킬/아이템/방어) → 수치 계산 → 적 액션 → 반복
- 대사/연출은 GenrePreset에 딸린 전투 대사 풀에서 랜덤/조건부 선택 (장르별 톤 유지, LLM 미사용)
- 결과(승리/패배, 드롭 아이템, 경험치)는 PlayerState에 기록
- 진입 경로는 두 가지: (1) 필드에서 조우, (2) 대화 중 `effects`에 `type: "start_combat"` 트리거 포함 — 단, 일단 전투에 진입하면 이후 로직은 완전히 수치 기반

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

## 범위 밖 (이번 스펙에서 다루지 않음)

- 모바일 배포, 서버 프록시 경유 (방식 B) — 필요해지면 별도 스펙으로 다룬다
- 로컬 LLM(Ollama 등) 구현체 자체 — 인터페이스만 준비하고, 실제 구현은 이후 스펙
- 멀티플레이어, 클라우드 세이브
