using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _characterEditor;
    private static int _characterEditorHero = -1;
    private static void OpenCharacterEditor(HeroData hero, int page)
    {
        _characterEditorHero = hero.heroID;
        _characterEditor = page;
        _learnedSkillPage = 0;
        _forgetSelection = IntPtr.Zero;
        RequestPageRebuild();
    }

    private static bool BuildCharacterEditor(HeroData hero)
    {
        if (_characterEditorHero != hero.heroID) _characterEditor = 0;
        if (_characterEditor == 0) return false;
        var p = NativePageParent;
        AddButton(p, "캐릭터 수치로 돌아가기", new Vector2(810, 0), new Vector2(220, 36), () => { _characterEditor = 0; RequestPageRebuild(); });
        if (_characterEditor <= 3) BuildCharacterStats(hero, _characterEditor);
        else if (_characterEditor == 4) BuildCharacterFace(hero);
        else if (_characterEditor == 5) BuildCharacterPreferences(hero);
        else BuildLearnedSkills(hero);
        return true;
    }

    private static void BuildCharacterStats(HeroData hero, int kind)
    {
        var p = NativePageParent;
        var values = kind == 1 ? hero.baseAttri : kind == 2 ? hero.baseFightSkill : hero.baseLivingSkill;
        var caps = kind == 1 ? hero.maxAttri : kind == 2 ? hero.maxFightSkill : hero.maxLivingSkill;
        var names = kind == 1 ? GlobalData.AttriName : kind == 2 ? GlobalData.FightSkillName : GlobalData.LivingSkillName;
        AddText(p, "속성", new Vector2(0, 55), new Vector2(180, 30));
        AddText(p, "기본값(보너스 제외)", new Vector2(200, 55), new Vector2(260, 30));
        AddText(p, "상한", new Vector2(490, 55), new Vector2(240, 30));
        int count = Math.Min(names.Count, Math.Min(values.Count, caps.Count));
        var baseInputs = new List<InputField>();
        var capInputs = new List<InputField>();
        for (int i = 0; i < count; i++)
        {
            float y = 95 + i * 45;
            AddText(p, names[i], new Vector2(0, y), new Vector2(180, 34));
            baseInputs.Add(AddInput(p, F(values[i]), new Vector2(200, y), new Vector2(240, 34)));
            capInputs.Add(AddInput(p, F(caps[i]), new Vector2(490, y), new Vector2(240, 34)));
        }
        var batchBase = AddInput(p, "200", new Vector2(770, 95), new Vector2(260, 34));
        AddButton(p, "기본값 일괄 입력", new Vector2(770, 140), new Vector2(260, 34), () => { foreach (var input in baseInputs) input.text = batchBase.text; });
        var batchCap = AddInput(p, "200", new Vector2(770, 200), new Vector2(260, 34));
        AddButton(p, "상한 일괄 입력", new Vector2(770, 245), new Vector2(260, 34), () => { foreach (var input in capInputs) input.text = batchCap.text; });
        void Apply(bool applyBase, bool applyCap)
        {
            try
            {
                // Validate the entire selected column before writing any value.
                var b = new float[count]; var c = new float[count];
                for (int i = 0; i < count; i++)
                {
                    if (applyBase) b[i] = ParseFloat(baseInputs[i].text);
                    if (applyCap) c[i] = ParseFloat(capInputs[i].text);
                    if (b[i] < 0 || c[i] < 0) throw new FormatException("기본값과 상한은 음수일 수 없습니다.");
                }
                for (int i = 0; i < count; i++) { if (applyBase) values[i] = b[i]; if (applyCap) caps[i] = c[i]; }
                RefreshHero(hero);
                _status = $"{hero.heroName}의 " + (applyBase && applyCap ? "기본값과 상한을 갱신했습니다." : applyBase ? "기본값을 갱신했습니다. 상한은 그대로입니다." : "상한을 갱신했습니다. 기본값은 그대로입니다.");
            }
            catch (Exception ex) { _status = "적용 안 됨: " + ex.Message; }
        }
        AddButton(p, "기본값만 적용", new Vector2(0, 550), new Vector2(240, 38), () => Apply(true, false));
        AddButton(p, "상한만 적용", new Vector2(260, 550), new Vector2(240, 38), () => Apply(false, true));
        AddButton(p, "두 열 모두 적용", new Vector2(520, 550), new Vector2(210, 38), () => Apply(true, true));
        AddText(p, "입력은 입력칸만 바꾸며, 적용을 눌러야 캐릭터에 기록됩니다. 돌아가면 적용하지 않은 입력은 버려집니다.", new Vector2(0, 600), new Vector2(1000, 32), 16);
    }

    private static void BuildCharacterFace(HeroData hero)
    {
        var p = NativePageParent;
        AddText(p, "새 캐릭터 생성 화면의 외형 코드를 사용하며, 주인공과 NPC가 함께 쓸 수 있습니다.", new Vector2(0, 60), new Vector2(1000, 40));
        var code = AddInput(p, hero.GenerateFaceCode(), new Vector2(0, 120), new Vector2(1030, 45));
        AddButton(p, "현재 외형 코드 복사", new Vector2(0, 185), new Vector2(300, 40), () => { code.text = hero.GenerateFaceCode(); GUIUtility.systemCopyBuffer = code.text; _status = "현재 캐릭터의 외형 코드를 복사했습니다."; });
        AddButton(p, "코드 붙여넣기", new Vector2(320, 185), new Vector2(230, 40), () => code.text = GUIUtility.systemCopyBuffer);
        AddButton(p, "현재 캐릭터에 적용", new Vector2(570, 185), new Vector2(300, 40), () =>
        {
            string before = hero.GenerateFaceCode();
            var preferences = GameDataController.playerPrefData?.playerPrefData;
            string portraitKey = hero.heroName + "hideSpeSkeleton";
            int previousPortrait = preferences?.GetInt(portraitKey) ?? 0;
            bool changedPortrait = false;
            try
            {
                var parts = code.text.Trim().Split('/');
                if (parts.Length != hero.faceData.faceID.Count + 2 || (parts[0] != "0" && parts[0] != "1")) throw new FormatException("외형 코드의 필드 수 또는 성별 형식이 잘못되었습니다.");
                for (int i = 1; i < parts.Length - 1; i++) if (!int.TryParse(parts[i], out int part) || part < -1) throw new FormatException("이목구비 번호 형식이 잘못되었습니다.");
                ParseFloat(parts[parts.Length - 1]);
                if (string.IsNullOrWhiteSpace(code.text) || code.text.Length > 4096 || !hero.LoadFaceCode(code.text.Trim())) throw new FormatException("외형 코드가 올바르지 않습니다. 게임의 캐릭터 생성 화면에서 전체 코드를 내보내세요.");
                // Named NPCs may use a fixed portrait. Use the game's own
                // portrait preference to reveal the imported editable face.
                if (hero.UseSpeSkeleton() && preferences != null) { preferences.SetKey(portraitKey, 1); changedPortrait = true; }
                hero.HeroIconDirty = true;
                RefreshHero(hero);
                if (HeroDetailController.Instance?.nowShowHero?.heroID == hero.heroID) HeroDetailController.Instance.RefreshHeroSkeleton();
                _status = $"{hero.heroName}의 외형을 변경했습니다. 유지하려면 게임을 저장하세요.";
            }
            catch (Exception ex) { hero.LoadFaceCode(before); if (changedPortrait) preferences?.SetKey(portraitKey, previousPortrait); _status = "외형 적용 안 됨: " + ex.Message; }
        });
        AddText(p, "코드에는 성별, 이목구비, 피부색이 들어 있으며 옷은 캐릭터 장비를 따릅니다. 적용하면 위에 표시된 이름의 캐릭터가 바로 바뀝니다.", new Vector2(0, 250), new Vector2(1000, 80), 18);
    }

    private static void BuildCharacterPreferences(HeroData hero)
    {
        var p = NativePageParent;
        var fight = new HashSet<int>(); var living = new HashSet<int>(); var hobbies = new HashSet<int>();
        if (hero.kungfuSkillFocus != null) foreach (var x in hero.kungfuSkillFocus) fight.Add(x);
        if (hero.livingSkillFocus != null) foreach (var x in hero.livingSkillFocus) living.Add(x);
        if (hero.hobby != null) foreach (var x in hero.hobby) hobbies.Add(x);
        AddText(p, "전문(복수 선택)", new Vector2(0, 50), new Vector2(1000, 30));
        void Choice(string label, int id, HashSet<int> selected, int col, float y)
        {
            AddNativeToggle(p, label, col * 205, y, () => selected.Contains(id), v => { if (v) selected.Add(id); else selected.Remove(id); }, 195);
        }
        for (int i = 0; i < GlobalData.FightSkillName.Count; i++) Choice(GlobalData.FightSkillName[i], i, fight, i % 5, 90 + i / 5 * 38);
        for (int i = 0; i < GlobalData.LivingSkillName.Count; i++) Choice(GlobalData.LivingSkillName[i], i, living, i % 5, 170 + i / 5 * 38);
        AddText(p, "취향(복수 선택)", new Vector2(0, 255), new Vector2(1000, 30));
        var options = new List<(string Label, int Id)>();
        void Group(int type, Il2CppSystem.Collections.Generic.List<string> labels)
        {
            for (int i = 0; i < labels.Count && i < 10; i++) options.Add((labels[i], type * 10 + i));
        }
        Group(0, GlobalData.EquipmentTypeName);
        options.Add((GlobalData.ItemTypeName[1], 10));
        Group(2, GlobalData.FoodTypeName);
        options.Add((GlobalData.ItemTypeName[3], 30));
        Group(4, GlobalData.TreasureTypeName);
        Group(5, GlobalData.MaterialTypeName);
        Group(6, GlobalData.HorseTypeName);
        var pages = new List<GameObject>();
        for (int page = 0; page * 30 < options.Count; page++)
        {
            var root = UiObject("HobbyPage" + page, p, new Vector2(1030, 225), new Vector2(0, 295));
            pages.Add(root);
            for (int j = page * 30; j < Math.Min(options.Count, (page + 1) * 30); j++)
            {
                var option = options[j]; int slot = j % 30;
                AddNativeToggle(root.transform, option.Label, slot % 5 * 205, slot / 5 * 36,
                    () => hobbies.Contains(option.Id), v => { if (v) hobbies.Add(option.Id); else hobbies.Remove(option.Id); }, 195);
            }
            root.SetActive(page == 0);
        }
        int currentPage = 0;
        var pageText = AddText(p, $"1 / {pages.Count}", new Vector2(220, 535), new Vector2(180, 35));
        void Turn(int delta) { currentPage = Math.Clamp(currentPage + delta, 0, pages.Count - 1); for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == currentPage); pageText.text = $"{currentPage + 1} / {pages.Count}"; }
        AddButton(p, "이전 페이지", new Vector2(0, 535), new Vector2(180, 35), () => Turn(-1));
        AddButton(p, "다음 페이지", new Vector2(420, 535), new Vector2(180, 35), () => Turn(1));
        AddButton(p, "전문과 취향 적용", new Vector2(0, 590), new Vector2(300, 38), () =>
        {
            Il2CppSystem.Collections.Generic.List<int> Make(HashSet<int> set) { var list = new Il2CppSystem.Collections.Generic.List<int>(); foreach (int x in new SortedSet<int>(set)) list.Add(x); return list; }
            hero.kungfuSkillFocus = Make(fight); hero.livingSkillFocus = Make(living); hero.hobby = Make(hobbies);
            RefreshHero(hero); _status = $"{hero.heroName}의 전문과 취향을 갱신했습니다.";
        });
    }
}
