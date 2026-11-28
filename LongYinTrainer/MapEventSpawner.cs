using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _spawnEventRarity = 4;
    private static int? _spawnEventType;
    private static int _spawnEventPage;
    private const int SpawnEventPageSize = 8;
    private static List<int> _spawnEventIds = new();
    private static readonly Button?[] SpawnEventSlots = new Button?[SpawnEventPageSize];
    private static readonly Text?[] SpawnRarityLabels = new Text?[6];
    private static Text? _spawnTypeLabel, _spawnPageLabel;

    // World and ordinary event IDs overlap. Encode world IDs as ~id so a
    // selected world encounter cannot accidentally resolve an ordinary event.
    private static WorldEventDataBase? SpawnWorldDefinition(int key) => key < 0
        ? RandomEventController.Instance?.GetWorldEventDataBase(~key) : null;
    private static EventData? SpawnEventTemplate(int key) => key < 0
        ? SpawnWorldDefinition(key)?.eventData : RandomEventController.Instance?.GetRandomEventDataBase(key);
    private static string SpawnEventLabel(int key) => (SpawnEventTemplate(key)?.Name() ?? "사용할 수 없는 종류") +
        (key < 0 ? "  [강호 소문]" : "  [" + key + "]");

    private static void RefreshSpawnEventPage(int delta = 0)
    {
        var pages = Math.Max(1, (_spawnEventIds.Count + SpawnEventPageSize - 1) / SpawnEventPageSize);
        _spawnEventPage = Math.Clamp(_spawnEventPage + delta, 0, pages - 1);
        if (_spawnEventType.HasValue && !_spawnEventIds.Contains(_spawnEventType.Value)) _spawnEventType = null;
        if (_spawnTypeLabel != null)
            _spawnTypeLabel.text = "현재 종류: " + (_spawnEventType.HasValue
                ? SpawnEventLabel(_spawnEventType.Value) : "무작위 종류");
        if (_spawnPageLabel != null)
            _spawnPageLabel.text = $"{_spawnEventPage + 1} / {pages} 페이지 · 대지도 기연 {_spawnEventIds.Count}종";
        for (var row = 0; row < SpawnEventPageSize; row++)
        {
            var slot = SpawnEventSlots[row];
            if (slot == null) continue;
            var index = _spawnEventPage * SpawnEventPageSize + row;
            var template = index < _spawnEventIds.Count
                ? SpawnEventTemplate(_spawnEventIds[index]) : null;
            slot.gameObject.SetActive(template != null);
            if (template == null) continue;
            var id = _spawnEventIds[index];
            slot.GetComponentInChildren<Text>().text = (_spawnEventType == id ? "✓ " : "") + SpawnEventLabel(id);
        }
    }

    private static void SelectSpawnEventSlot(int row)
    {
        var index = _spawnEventPage * SpawnEventPageSize + row;
        if (index >= _spawnEventIds.Count) return;
        _spawnEventType = _spawnEventIds[index];
        RefreshSpawnEventPage();
    }

    private static void RefreshSpawnRarityLabels()
    {
        for (var i = 0; i < SpawnRarityLabels.Length; i++)
            if (SpawnRarityLabels[i] != null)
                SpawnRarityLabels[i]!.text = (_spawnEventRarity == i ? "✓ " : "") + RarityName(i);
    }

    // Read the native overworld pool afresh; do not retain IL2CPP event wrappers
    // across loading a different save. Include world encounters with a native
    // BigMap placement, without applying the automatic scheduler's date gates.
    // City, village, inn and sect-interior events stay outside this catalog.
    private static List<int> SpawnableMapEventIds()
    {
        var result = new List<int>();
        var database = RandomEventController.Instance;
        var ids = database?.bigMapRandomEventID;
        if (ids != null) foreach (var id in ids)
        {
            var template = database!.GetRandomEventDataBase(id);
            if (template == null || template.isAreaEvent || template.plotData == null ||
                template.plotTargetEvent || template.missionTargetEvent || result.Contains(id)) continue;
            result.Add(id);
        }
        var worldTemplates = database?.worldEventDataBase;
        if (worldTemplates != null) foreach (var definition in worldTemplates)
        {
            if (definition == null || definition.id < 0 || definition.eventRandomArea != WorldEventRandomArea.BigMap) continue;
            var template = definition.eventData;
            var key = ~definition.id;
            if (template == null || template.isAreaEvent || template.plotData == null ||
                template.plotTargetEvent || template.missionTargetEvent || result.Contains(key)) continue;
            result.Add(key);
        }
        return result;
    }

    private static void BuildNativeEventSpawner()
    {
        var p = NativePageParent;
        AddText(p, "대지도 · 기연 직접 생성", Vector2.zero, new Vector2(720, 36), 22);
        AddText(p, "품질 선택", new Vector2(0, 48), new Vector2(160, 30), 18);
        for (var i = 0; i <= 5; i++)
        {
            var rarity = i;
            var button = AddButton(p, "",
                new Vector2(i * 174, 86), new Vector2(165, 38), () =>
                { _spawnEventRarity = rarity; RefreshSpawnRarityLabels(); });
            SpawnRarityLabels[i] = button.GetComponentInChildren<Text>();
        }
        RefreshSpawnRarityLabels();
        _spawnEventIds = SpawnableMapEventIds();
        _spawnTypeLabel = AddText(p, "", new Vector2(0, 140), new Vector2(750, 34), 18);
        AddButton(p, "무작위 종류", new Vector2(820, 140), new Vector2(225, 36), () =>
        { _spawnEventType = null; RefreshSpawnEventPage(); });

        // Fixed slots retain their objects and click handlers across pagination.
        // Resolve the current row on click instead of capturing its original ID.
        for (var row = 0; row < SpawnEventPageSize; row++)
        {
            var slot = row;
            SpawnEventSlots[row] = AddButton(p, "",
                new Vector2((row % 2) * 540, 188 + (row / 2) * 48), new Vector2(505, 40), () =>
                SelectSpawnEventSlot(slot));
        }
        AddButton(p, "이전 페이지", new Vector2(0, 394), new Vector2(160, 36), () =>
            RefreshSpawnEventPage(-1));
        _spawnPageLabel = AddText(p, "",
            new Vector2(180, 394), new Vector2(470, 36), 16);
        AddButton(p, "다음 페이지", new Vector2(690, 394), new Vector2(160, 36), () =>
            RefreshSpawnEventPage(1));
        AddButton(p, "종류 새로고침", new Vector2(880, 394), new Vector2(165, 36), () =>
        { _spawnEventIds = SpawnableMapEventIds(); RefreshSpawnEventPage(); });
        AddButton(p, "주인공 곁에 하나 생성", new Vector2(0, 451), new Vector2(1045, 44), SpawnMapEventNearPlayer);
        AddText(p, "무학 기재 같은 세계 기연과 대지도의 중원귀시를 포함하며, 직접 생성하면 출현 날짜 제한을 받지 않습니다. 도시나 문파 내부 사건은 포함하지 않습니다.\n품질이 난이도도 함께 정하며, 보상은 원래 규칙대로 정산됩니다. 세계 기연은 강호 소문에도 함께 등록되며 원래 유효 기간을 유지합니다.",
            new Vector2(0, 514), new Vector2(1045, 100), 16);
        RefreshSpawnEventPage();
    }

    private static void SpawnMapEventNearPlayer()
    {
        EventData? created = null;
        BigMapController? map = null;
        Il2CppSystem.Collections.Generic.List<EventData>? events = null;
        Il2CppSystem.Collections.Generic.List<EventData>? worldEvents = null;
        var registered = false;
        try
        {
            var game = GameController.Instance;
            var world = game?.worldData;
            var player = world?.Player();
            map = BigMapController.Instance;
            if (world == null || player?.bigMapPos == null)
                throw new InvalidOperationException("먼저 세이브를 불러오세요.");
            if (map == null || !map.gameObject.activeInHierarchy || map.bigmapRoot == null ||
                !map.bigmapRoot.activeInHierarchy || map.playerArmy == null || !map.playerArmy.activeInHierarchy ||
                player.atAreaID >= 0 || BattleController.Instance?.BattleObj?.activeInHierarchy == true)
                throw new InvalidOperationException("도시를 떠나고 전투를 끝낸 뒤 대지도를 걷는 상태로 돌아가세요.");

            var ids = SpawnableMapEventIds();
            if (ids.Count == 0) throw new InvalidOperationException("대지도 기연 종류를 아직 불러오지 않았습니다. 종류를 새로고침하세요.");
            var id = _spawnEventType ?? ids[UnityEngine.Random.Range(0, ids.Count)];
            if (!ids.Contains(id)) throw new InvalidOperationException("선택한 종류를 더 이상 쓸 수 없습니다. 종류를 새로고침하세요.");
            var definition = SpawnWorldDefinition(id);
            var template = SpawnEventTemplate(id)
                ?? throw new InvalidOperationException("선택한 기연 템플릿을 더 이상 쓸 수 없습니다. 종류를 새로고침하세요.");
            var worldController = id < 0 ? WorldEventController.Instance : null;
            if (id < 0)
            {
                if (definition == null || worldController == null)
                    throw new InvalidOperationException("세계 기연 컨트롤러가 아직 준비되지 않았습니다.");
                worldEvents = world.WorldEventDatas ?? throw new InvalidOperationException("강호 소문 목록을 쓸 수 없습니다.");
            }
            created = template.Clone().Cast<EventData>();
            created.difficulty = _spawnEventRarity * 2f;
            if (created.GetEventRareLv() != _spawnEventRarity)
                throw new InvalidOperationException("현재 게임의 품질 계산 규칙이 바뀌어 기연을 생성하지 않았습니다.");

            // Clone preserves plot payload and native expiry. Only initialize the
            // occurrence state, difficulty and position for this new instance.
            created.seen = true;
            created.noticed = false;
            created.hovered = false;
            created.happened = false;
            created.plotTargetEvent = false;
            created.missionTargetEvent = false;
            if (definition != null)
            {
                // Match the native CreateWorldEvent(EventData, ...) initialization.
                // Do not alter the scheduler's dates, completion record or world clock.
                created.leftTime = definition.lastTime;
                created.speTargetID = -1;
            }
            created.randomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            created.isAreaEvent = false;
            created.resourcePointID = -1;
            var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var position = player.bigMapPos.ToVector3() + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .08f;
            position = map.LimitMapPos(position, 1f);
            created.bigMapPos = new BigMapPos(position);
            created.nearAreaID = map.GetNearAreaID(created.bigMapPos);
            // Native GetPosText indexes DirectionName even for a vague location.
            created.nearAreaDirection = 0;
            created.inaccuracyPosText = true;
            events = world.BigMapRandomEventDatas ?? throw new InvalidOperationException("세이브의 기연 목록을 쓸 수 없습니다.");
            events.Add(created);
            registered = true;
            map.CreateBigMapRandomEventIcon(created);
            var found = false;
            foreach (var icon in map.bigmapRandomEventIcons)
            {
                if (icon == null) continue;
                var controller = icon.GetComponent<BigMapRandomEventController>();
                if (controller?.bigMapRandomEventData?.Pointer != created.Pointer) continue;
                controller.Init();
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("게임 기본 기연 아이콘을 만들지 못했습니다.");
            if (definition != null) worldController!.AddNewWorldEvent(created);
            _status = $"주인공 근처에 생성했습니다: {created.Name()} · {RarityName(_spawnEventRarity)}. 패널을 접은 뒤 기연 아이콘을 눌러 이동하세요.";
            LongYinTrainerPlugin.Logger.LogInfo($"Spawned map event id={id}, rarity={created.GetEventRareLv()}, difficulty={created.difficulty}, pos={created.bigMapPos.x},{created.bigMapPos.y}");
        }
        catch (Exception ex)
        {
            // Roll back just this occurrence and its icons, never other events.
            if (registered && created != null)
            {
                events?.Remove(created);
                worldEvents?.Remove(created);
                var icons = map?.bigmapRandomEventIcons;
                if (icons != null)
                    for (var i = icons.Count - 1; i >= 0; i--)
                    {
                        var icon = icons[i];
                        if (icon?.GetComponent<BigMapRandomEventController>()?.bigMapRandomEventData?.Pointer != created.Pointer) continue;
                        icons.RemoveAt(i);
                        UnityEngine.Object.Destroy(icon);
                    }
            }
            _status = "기연 생성 실패: " + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning("Map event spawn: " + ex);
        }
    }
}
