using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _cityEditorId = int.MinValue;
    private static IntPtr _cityEditorWorld;
    private static IntPtr _cityObservedWorld;

    private static void TickCityState()
    {
        var world = GameController.Instance?.worldData;
        var pointer = world?.Pointer ?? IntPtr.Zero;
        if (_cityObservedWorld != pointer)
        {
            _cityObservedWorld = pointer;
            _cityEditorWorld = IntPtr.Zero;
            _cityEditorId = int.MinValue;
            InvalidatePages(8);
        }
        CityStateLock.Poll(world, Time.realtimeSinceStartup);
    }

    private static void BuildNativeCity()
    {
        var p = NativePageParent;
        var world = GameController.Instance?.worldData;
        if (world?.Areas == null) { AddText(p, "먼저 세이브를 불러오세요.", Vector2.zero, new Vector2(900, 40)); return; }
        var cities = new List<AreaData>();
        for (var i = 0; i < world.Areas.Count; i++)
            if (world.Areas[i] != null) cities.Add(world.Areas[i]);
        cities.Sort((a, b) => a.areaID.CompareTo(b.areaID));
        if (cities.Count == 0) { AddText(p, "현재 세이브에 편집할 수 있는 도시가 없습니다.", Vector2.zero, new Vector2(900, 40)); return; }
        if (_cityEditorWorld != world.Pointer)
        {
            _cityEditorWorld = world.Pointer;
            _cityEditorId = AreaController.Instance?.areaData?.areaID ?? cities[0].areaID;
        }
        var selected = Math.Max(0, cities.FindIndex(a => a.areaID == _cityEditorId));
        var matches = new List<AreaData>(cities);
        AddText(p, "도시／거점 속성", Vector2.zero, new Vector2(680, 36), 20);
        var search = AddInput(p, "", new Vector2(0, 48), new Vector2(275, 36));
        var title = AddText(p, "", new Vector2(120, 102), new Vector2(670, 42), 20, TextAnchor.MiddleCenter);
        var fields = new InputField[5];
        var labels = new[] { "치안", "민심", "성방", "현재 인구", "인구 상한" };
        for (var i = 0; i < fields.Length; i++)
        {
            AddText(p, labels[i], new Vector2(25, 170 + i * 57), new Vector2(175, 38));
            fields[i] = AddInput(p, "", new Vector2(210, 170 + i * 57), new Vector2(220, 38));
            AddText(p, i < 3 ? "0–100" : i == 3 ? "인구 상한 이하" : "1–10억", new Vector2(450, 170 + i * 57), new Vector2(260, 38), 16);
        }
        var lockStatus = AddText(p, "", new Vector2(735, 165), new Vector2(315, 60), 19);
        void ReadSelection()
        {
            if (!ValidSelection()) return;
            var area = matches[selected];
            _cityEditorId = area.areaID;
            title.text = $"{area.GetAreaName()}（ID {area.areaID}）  {selected + 1}/{matches.Count}";
            var values = CityStateLock.Read(area);
            for (var i = 0; i < fields.Length; i++) fields[i].text = values[i].ToString("R", CultureInfo.InvariantCulture);
            lockStatus.text = CityStateLock.IsLocked(world, area) ? "이 도시: 잠금" : "이 도시: 잠금 해제";
        }
        bool ValidSelection()
        {
            var current = GameController.Instance?.worldData;
            if (current?.Pointer == world.Pointer && current.GetArea(matches[selected].areaID)?.Pointer == matches[selected].Pointer) return true;
            _status = "세이브 또는 도시 데이터가 바뀌었습니다. 도시 페이지를 다시 여세요.";
            RequestPageRebuild(); return false;
        }
        void Apply(bool shouldLock)
        {
            try
            {
                if (!ValidSelection()) return;
                var values = new float[5];
                for (var i = 0; i < fields.Length; i++)
                    if (!float.TryParse(fields[i].text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                        throw new ArgumentException($"{labels[i]}: 올바른 숫자를 입력하세요.");
                var area = matches[selected];
                CityStateLock.Apply(world, area, values, shouldLock);
                ReadSelection();
                _status = $"{area.GetAreaName()}의 속성을 적용" + (CityStateLock.IsLocked(world, area) ? "하고 잠갔습니다. " : "했습니다. ") + "게임에서 저장하세요.";
            }
            catch (Exception ex) { _status = "도시 수정 실패: " + ex.Message; }
        }
        AddButton(p, "이름／ID 검색", new Vector2(285, 48), new Vector2(175, 36), () =>
        {
            var text = search.text.Trim();
            var found = cities.FindAll(a => a.GetAreaName().Contains(text, StringComparison.OrdinalIgnoreCase) || a.areaID.ToString() == text);
            if (found.Count == 0) { _status = "일치하는 도시가 없습니다."; return; }
            matches = found; selected = 0; ReadSelection();
        });
        AddButton(p, "현재 장소 읽기", new Vector2(475, 48), new Vector2(225, 36), () =>
        {
            var area = AreaController.Instance?.areaData;
            var index = area == null ? -1 : cities.FindIndex(a => a.Pointer == area.Pointer);
            if (index < 0) { _status = "먼저 대상 도시／거점에 들어가세요."; return; }
            matches = new List<AreaData>(cities); selected = index; ReadSelection();
        });
        AddButton(p, "모든 도시", new Vector2(715, 48), new Vector2(175, 36), () => { matches = new List<AreaData>(cities); selected = 0; ReadSelection(); });
        AddButton(p, "이전", new Vector2(0, 102), new Vector2(110, 42), () => { selected = (selected + matches.Count - 1) % matches.Count; ReadSelection(); });
        AddButton(p, "다음", new Vector2(810, 102), new Vector2(110, 42), () => { selected = (selected + 1) % matches.Count; ReadSelection(); });
        AddButton(p, "수치 적용", new Vector2(735, 240), new Vector2(300, 42), () => Apply(false));
        AddButton(p, "적용 후 이 도시 잠금", new Vector2(735, 295), new Vector2(300, 42), () => Apply(true));
        AddButton(p, "이 도시 잠금 해제", new Vector2(735, 350), new Vector2(300, 42), () =>
        {
            if (!ValidSelection()) return;
            CityStateLock.Unlock(world, matches[selected]); ReadSelection(); _status = "선택한 도시의 잠금을 해제했습니다.";
        });
        AddButton(p, "현재 수치 다시 읽기", new Vector2(735, 405), new Vector2(300, 42), () => { if (ValidSelection()) ReadSelection(); });
        AddText(p, "도시마다 따로 편집하며 여러 도시를 동시에 잠글 수 있습니다. 도시를 바꾸기 전에 먼저 적용하세요.\n패널을 닫아도 잠금은 유지되며, 불러오기·세이브 변경·게임 재시작 후 해제됩니다.\n잠긴 도시에 수치를 다시 적용하면 잠금 값도 함께 갱신됩니다.", new Vector2(25, 490), new Vector2(1010, 115), 17);
        ReadSelection();
    }
}
