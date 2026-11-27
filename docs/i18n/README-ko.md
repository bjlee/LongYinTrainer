# 한국어 번역 메모

- 모드 UI 문자열(약 1,200개)을 소스에서 직접 한국어로 바꿨습니다. 용어는 LongYinKorean(게임 한글화 모드) 사전에 맞췄습니다.
- `ko-KR.tsv`는 원문과 번역을 짝지은 표입니다. 상위 저장소가 업데이트되면 이 표로 다시 적용할 수 있습니다.

## 일부러 중국어로 남긴 문자열

게임 데이터나 게임 UI 글자와 비교하는 값이라 번역하면 기능이 깨집니다. LongYinKorean은 화면에 그릴 때만 번역하므로 게임 내부 값은 계속 중국어입니다.

| 위치 | 문자열 | 이유 |
| --- | --- | --- |
| `BuildingCombat.cs` 239–247 | 抢夺银两 등 선택지 | 게임 원본 선택지 문구와 똑같아야 함(CombatRegression 검사). LongYinKorean이 화면에서 번역 |
| `LongYinTrainer.cs` `IsAdvancedStartTalent` | 高级 | 게임 천부 데이터의 category 값 |
| `LongYinTrainer.cs` `RefreshStartTalentLimitText` | 初始上限 | 게임 시작 화면 라벨 비교 |
| `LongYinTrainer.cs` 건물 판정 | 藏经阁, 书房 | 게임 건물 이름 비교 |
| `LongYinTrainer.cs` `FindNativeSprite` | 通用白色纸块 등 | 게임 스프라이트 리소스 이름 |
| `LongYinTrainer.cs` `LocalizeAffixDescription`, `BreakChoiceName` | 其他： | 게임이 만드는 설명 문자열 비교 |
| `NativeKungfuFields.cs` | StartsWith("基础伤害") | 게임 UI 라벨 비교 |
| `NativeSkillDetailRefresh.cs` | 模组 | 게임 세이브 데이터에 들어가는 제작자 이름 |

## 함께 바꾼 로직

- `ButtonSurface`, `ButtonIcon`: 버튼 색과 아이콘을 버튼 문구로 정하던 로직을 한국어 키워드(삭제, 모두 비우기, 적용, 쓰기, 저장, 에 추가, 이전/다음 페이지, 설정)로 바꿨습니다. 한국어는 동사가 뒤에 오므로 `StartsWith` 대신 `Contains`를 씁니다.
- 회귀 테스트(EventRegression, CategoryRegression, CharacterAudit)의 버튼 이름도 한국어로 맞췄습니다.
