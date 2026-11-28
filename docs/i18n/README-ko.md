# LongYinTrainer 한국어화 가이드

트레이너 UI를 한국어로 바꾼 방법과 자료를 정리한 문서입니다. 게임이나 치트 모드(상위 저장소 goldenSTAME/LongYinTrainer)가 업데이트됐을 때 이 문서대로 다시 한국어화할 수 있습니다.

## 1. 자료 구성

| 파일 | 내용 |
| --- | --- |
| `docs/i18n/ko-KR.tsv` | 번역표. `중국어 원문<TAB>한국어 번역`, 약 1,190개. 같은 원문은 어디서나 같은 번역을 씁니다. |
| `docs/i18n/ko-KR.context.tsv` | 문맥 규칙. 중국어로 남길 곳과, 같은 원문이라도 위치에 따라 번역이 다른 곳을 적습니다. |
| `docs/i18n/button-keywords.patch` | 번역표만으로는 안 되는 코드 수정(버튼 색 판정)입니다. |
| `scripts/i18n/ko_localize.py` | 번역표와 문맥 규칙을 소스에 적용하고, 번역 안 된 문자열을 찾는 도구입니다. |

검증 결과: 원본 0.4.31.43 소스에 이 도구를 적용하고 패치를 더하면, 이 브랜치의 한국어 소스와 한 글자도 다르지 않게 나옵니다(1,556곳 교체).

### 번역표 규칙

- 원문과 번역은 C# 소스에 쓰인 모양 그대로입니다. `\n` 같은 이스케이프와 `{hero.heroName}` 같은 보간 구멍은 원문과 똑같이 둡니다. 구멍이 다르면 도구가 오류를 냅니다.
- 번역문에 큰따옴표(`"`)는 쓰지 않습니다. 따옴표가 필요하면 `“ ”`를 씁니다.
- 조각을 이어 붙이는 문장(예: `heroName + "的背包"` → `"의 가방"`)은 이어진 결과가 자연스럽게 되도록 번역합니다.

### 문맥 규칙 형식

`파일<TAB>줄 정규식<TAB>원문<TAB>번역` 형식입니다. 위에서부터 처음 맞는 규칙을 씁니다.

- 원문이 `*`이면 그 줄의 모든 문자열에 적용합니다.
- 번역이 `=`이면 중국어 원문을 그대로 둡니다.
- 파일이 `*`이면 모든 파일에 적용합니다.

## 2. 중국어로 남겨 둔 문자열과 이유

LongYinKorean(게임 한글화 모드)은 글자를 **화면에 그리는 순간에만** 한국어로 바꿉니다. 게임 데이터와 `Text.text` 값은 계속 중국어입니다. 그래서 트레이너가 게임 값과 비교하는 문자열은 중국어로 남겨야 하고, 번역하면 기능이 깨집니다.

| 위치 | 문자열 | 이유 |
| --- | --- | --- |
| `BuildingCombat.cs` `choices.Add(...)` | 抢夺银两 등 선택지 | 게임 원본 선택지 문구와 똑같아야 합니다(CombatRegression 검사). 화면에서는 LongYinKorean이 번역합니다. |
| `IsAdvancedStartTalent` | 高级 | 게임 천부 데이터의 category 값입니다. |
| `RefreshStartTalentLimitText` | 初始上限 | 게임 시작 화면 라벨과 비교합니다. |
| `data.name` 비교 | 藏经阁, 书房 | 게임 건물 이름과 비교합니다. |
| `FindNativeSprite(...)` | 通用白色纸块 등 18개 | 게임 스프라이트 리소스 이름입니다. |
| `LocalizeAffixDescription`, `BreakChoiceName` | 其他： / 其他: | 게임이 만드는 효과 설명과 비교합니다. |
| `NativeKungfuFields.cs` `text.text.StartsWith` | 基础伤害 | 게임 무공 창설 화면 라벨과 비교합니다. |
| `NativeSkillDetailRefresh.cs` | 模组 | 게임 세이브에 들어가는 제작자 이름입니다. |
| 회귀 테스트 | 武学奇才, 中元鬼市 등 | 가짜 게임 데이터 안의 게임 이름입니다. |

반대로 아이템 분류(武器, 秘籍, 材料, 全部…)처럼 **트레이너가 직접 정하고 직접 비교하는 값**은 번역했습니다. 정의하는 곳과 비교하는 곳이 같은 번역으로 함께 바뀌기 때문에 안전합니다.

### 새 문자열이 게임 값과 비교되는지 판단하는 법

다음과 같으면 중국어로 남기고 `ko-KR.context.tsv`에 `=` 규칙을 추가합니다.

- `==`, `Equals`, `StartsWith`, `Contains`, `IndexOf`, `Replace`, `switch` 등에서 **게임 객체의 값**과 비교하는 경우. 예: `data.name`, `definition.category`, 게임 UI의 `label.text`, `GetDescribe()` 결과.
- 리소스 이름, 세이브에 저장되는 값, 게임 원본 문구를 그대로 재현해야 하는 값.

## 3. 코드 수정 (버튼 색)

`ButtonSurface`는 버튼 문구가 "删除", "应用"으로 **시작하는지**로 색을 정했습니다. 한국어는 동사가 뒤에 오므로("현재 무공 삭제") `StartsWith`를 `Contains`로 바꿨습니다. 이 부분이 `button-keywords.patch`입니다.

버튼 아이콘을 정하는 `ButtonIcon`은 원래 `Contains`라서 번역표만으로 맞춰집니다. 이 판정은 다음 키워드를 씁니다. 버튼 문구를 새로 번역할 때 지켜 주세요.

- 빨간 버튼과 휴지통 아이콘: 삭제, 모두 비우기 / 비우기
- 갈색 버튼: 적용, 쓰기, 저장, 에 추가
- 아이콘: 이전 페이지·다음 페이지(화살표), 추가(더하기), 적용·쓰기·설정(체크)
- 주의: 장비의 "额外词条"는 "추가 옵션"이 아니라 **"부가 옵션"**으로 번역했습니다. "추가"가 들어가면 더하기 아이콘이 붙기 때문입니다.

## 4. 치트 모드가 업데이트됐을 때 다시 한국어화하기

준비물: Git, Python 3(https://www.python.org, 설치할 때 "Add to PATH" 선택), .NET SDK 8.

PowerShell에서 저장소 폴더로 이동한 뒤 진행합니다.

```powershell
# 1) 상위 저장소의 새 버전을 받아 새 브랜치를 만듭니다
git remote add upstream https://github.com/goldenSTAME/LongYinTrainer   # 처음 한 번만
git fetch upstream
git checkout -b ko-새버전 upstream/main

# 2) 이 브랜치의 한국어화 자료를 가져옵니다 (한국어화 브랜치나 main 이름에 맞게)
git checkout origin/main -- docs/i18n scripts/i18n

# 3) 번역표를 적용합니다
python scripts/i18n/ko_localize.py apply

# 4) 버튼 색 패치를 적용합니다
git apply -3 docs/i18n/button-keywords.patch
```

4단계 패치가 실패하면 `LongYinTrainer.cs`의 `ButtonSurface` 함수에서 `label.StartsWith(...)`를 `label.Contains(...)`로 직접 바꾸면 됩니다.

3단계 끝에 **"번역되지 않은 중국어 문자열: N개"**가 나오면 새로 생긴 문구입니다. 목록은 `docs/i18n/untranslated.tsv`에 저장됩니다.

```text
5) untranslated.tsv 각 줄의 두 번째 칸에 번역을 씁니다.
   - 게임 값과 비교하는 문자열이면(2장 참고) 번역하지 말고 ko-KR.context.tsv 에 '=' 규칙을 추가합니다.
6) 번역한 줄을 ko-KR.tsv 끝에 붙여 넣습니다(세 번째 칸의 "# 위치" 주석은 있어도 됩니다).
7) python scripts/i18n/ko_localize.py apply 를 다시 실행합니다. 0개가 될 때까지 반복합니다.
8) untranslated.tsv 는 지웁니다.
```

상위에서 기존 문구를 바꿨다면 그 문구는 새 문자열로 잡힙니다. 예전 번역은 `ko-KR.tsv`에 남아 있으니 참고해서 고치면 됩니다.

`python scripts/i18n/ko_localize.py check`는 파일을 고치지 않고 목록만 보여 줍니다.

## 5. 게임이 업데이트됐을 때

- 게임을 BepInEx와 함께 한 번 실행해 `BepInEx\interop` 파일을 새로 만든 뒤 다시 빌드합니다(6장).
- 빌드 오류가 나면 게임 API가 바뀐 것입니다. 한국어화가 아니라 상위 치트 모드의 업데이트가 필요합니다.
- 게임 원본 문구나 리소스 이름(2장 표)이 바뀌면 상위 모드가 함께 고칩니다. 새 버전을 받을 때 `ko-KR.context.tsv`의 `=` 규칙이 새 문구에도 맞는지 확인합니다.
- 용어는 LongYinKorean 사전(`LongYinKorean\data\dict\*.tsv`)을 기준으로 맞췄습니다. LongYinKorean이 용어를 바꾸면 `ko-KR.tsv`에서 같은 용어를 찾아 바꾼 뒤 3단계를 다시 실행합니다.

## 6. 빌드와 테스트

```powershell
.\scripts\Build.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\LongYinLiZhiZhuan'
```

결과물은 `LongYinTrainer\bin\Release\net6.0\LongYinTrainer.dll`입니다. 게임 폴더의 `BepInEx\plugins`에 있는 기존 DLL을 백업하고 이 파일로 바꿉니다.

가짜 게임 데이터로 도는 회귀 테스트도 실행합니다.

```powershell
foreach ($t in 'IconRegression','ForceLockRegression','EventRegression','KungfuRegression','CategoryRegression','CombatRegression','BookCatalogRegression') {
  dotnet run --project ".\$t\$t.csproj" -c Release
}
```

게임 안에서는 H 키로 패널을 열어 한글 표시, 버튼 글자 잘림, 버튼 색과 아이콘을 확인합니다.

## 7. 용어

LongYinKorean 사전을 우선으로 따랐고, 사전에 없거나 사전 안에서 갈리는 용어는 아래처럼 정했습니다.

| 원문 | 번역 | | 원문 | 번역 |
| --- | --- | --- | --- | --- |
| 功法 | 무공 | | 武学 | 무학 |
| 自创功法 | 자작 무공 | | 自创武学 | 무공 창설 |
| 秘籍 | 비급 | | 藏经阁 | 장경각 |
| 词条 | 옵션 | | 额外词条 | 부가 옵션 |
| 原版 | 원본 | | 母版 / 模板 | 템플릿 |
| 原生 | 게임 기본 / 기본 | | 来源 | 출처 |
| 存档 | 세이브 | | 草稿 | 초안 |
| 修改器 | 트레이너 | | 模组 | 모드 |
| 稀有度 | 희귀도 | | 品级 / 品质 | 품급 / 품질 |
| 等级 | 등급(물품), 레벨(무공) | | 天赋 | 천부 |
| 姿态 / 架势 | 자세 | | 弹道 | 탄도 |
| 场景特效 | 장면 효과 | | 拖尾 | 잔상 |
| 坐骑 | 탈것 | | 鞍具 | 마구 |
| 奇遇 | 기연 | | 江湖传闻 | 강호 소문 |
| 冷却 | 쿨다운 | | 背包 | 가방 |
| 基础 / 进阶 / 上乘 / 秘传 / 顶极 / 绝世 | 기초 / 진급 / 상승 / 비전 / 정극 / 절세 | | 普通 / 优良 / 稀有 / 精良 / 完美 | 보통 / 우량 / 희귀 / 정량 / 완벽 |
