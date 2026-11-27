using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _learnedSkillPage;
    private static IntPtr _forgetSelection;

    private static string? LearnedOriginalName(int id)
    {
        var world = GameController.Instance?.worldData;
        var db = GameDataController.Instance;
        if (id < 0 || id >= GlobalData.CustomSkillIDStart || world == null || db == null || world.customSkills?.ContainsKey(id) == true) return null;
        return OriginalKungfu(db, id)?.name;
    }

    private static void BuildLearnedSkills(HeroData hero)
    {
        var p = NativePageParent;
        var rows = new List<(KungfuSkillLvData Skill, string Name)>();
        const int pageSize = 18;
        var buttons = new Button[pageSize];
        var labels = new Text[pageSize];
        AddText(p, "익힌 원본 무학 · 잊을 무학을 먼저 해제한 뒤 선택하고 확인하세요.", new Vector2(0, 55), new Vector2(1000, 40));
        Button previous = null!, next = null!;
        Text counter = null!;
        var empty = AddText(p, "이 캐릭터는 익힌 원본 무학이 없습니다.", new Vector2(0, 120), new Vector2(800, 36));
        for (int cell = 0; cell < pageSize; cell++)
        {
            int slot = cell;
            buttons[cell] = AddButton(p, "", new Vector2(cell % 3 * 345, 120 + cell / 3 * 66), new Vector2(330, 50), () => Choose(slot));
            labels[cell] = buttons[cell].GetComponentInChildren<Text>();
        }
        previous = AddButton(p, "이전 페이지", new Vector2(0, 550), new Vector2(210, 38), () => ChangePage(-1));
        counter = AddText(p, "", new Vector2(380, 550), new Vector2(300, 38));
        next = AddButton(p, "다음 페이지", new Vector2(810, 550), new Vector2(210, 38), () => ChangePage(1));
        RefreshList();

        void RefreshList()
        {
            rows.Clear();
            var learned = hero.kungfuSkills;
            for (int i = 0; learned != null && i < learned.Count; i++)
            {
                var skill = learned[i];
                if (skill != null && LearnedOriginalName(skill.skillID) is string name) rows.Add((skill, name));
            }
            int pages = Math.Max(1, (rows.Count + pageSize - 1) / pageSize);
            _learnedSkillPage = Math.Clamp(_learnedSkillPage, 0, pages - 1);
            for (int cell = 0; cell < pageSize; cell++)
            {
                bool visible = _learnedSkillPage * pageSize + cell < rows.Count;
                buttons[cell].gameObject.SetActive(visible);
                if (visible) UpdateLabel(cell);
            }
            empty.gameObject.SetActive(rows.Count == 0);
            counter.text = $"총 {rows.Count}개　{_learnedSkillPage + 1}/{pages} 페이지";
            previous.gameObject.SetActive(_learnedSkillPage > 0);
            next.gameObject.SetActive(_learnedSkillPage + 1 < pages);
        }

        bool InUse(KungfuSkillLvData skill) =>
            HasEquippedKungfu(hero, skill.skillID) || hero.GetNowActiveSkill()?.skillID == skill.skillID;

        void UpdateLabel(int cell)
        {
            var (skill, name) = rows[_learnedSkillPage * pageSize + cell];
            labels[cell].text = InUse(skill) ? $"{name}(사용 중)" : _forgetSelection == skill.Pointer ? $"「{name}」 잊기 확인" : name;
        }

        void ChangePage(int direction)
        {
            _forgetSelection = IntPtr.Zero;
            _learnedSkillPage += direction;
            RefreshList();
        }

        void Choose(int cell)
        {
            var (skill, name) = rows[_learnedSkillPage * pageSize + cell];
            if (TargetHero()?.Pointer != hero.Pointer)
            {
                _forgetSelection = IntPtr.Zero;
                _status = "현재 캐릭터가 바뀌었습니다. 다시 선택하세요.";
                RequestPageRebuild();
                return;
            }
            try
            {
                if (InUse(skill)) throw new InvalidOperationException("먼저 게임의 무학 화면에서 현재 무학을 해제하거나 바꾸세요.");
                if (_forgetSelection == skill.Pointer)
                {
                    ForgetLearnedOriginal(hero, skill);
                    _forgetSelection = IntPtr.Zero;
                    _status = $"{hero.heroName}이(가) 「{name}」을(를) 잊었습니다.";
                    RefreshList();
                    return;
                }
                var old = _forgetSelection;
                _forgetSelection = skill.Pointer;
                if (old != IntPtr.Zero)
                {
                    int oldCell = rows.FindIndex(row => row.Skill.Pointer == old) - _learnedSkillPage * pageSize;
                    if (oldCell >= 0 && oldCell < pageSize) UpdateLabel(oldCell);
                }
                UpdateLabel(cell);
                _status = $"한 번 더 누르면 {hero.heroName}이(가) 「{name}」을(를) 잊습니다.";
            }
            catch (Exception ex)
            {
                _forgetSelection = IntPtr.Zero;
                _status = ex.Message;
                RefreshList();
            }
        }
    }

    private static void ForgetLearnedOriginal(HeroData hero, KungfuSkillLvData skill)
    {
        var battle = BattleController.Instance;
        if (battle != null && battle.battleState != BattleState.None && battle.battleState != BattleState.End)
            throw new InvalidOperationException("전투가 끝난 뒤 무학을 잊으세요.");
        var learned = hero.kungfuSkills;
        int index = learned?.IndexOf(skill) ?? -1;
        if (index < 0 || LearnedOriginalName(skill.skillID) == null)
            throw new InvalidOperationException("무학 목록이 바뀌었습니다. 다시 선택하세요.");
        ForgetUnassignedSkill(hero, skill);
    }

    private static void ForgetUnassignedSkill(HeroData hero, KungfuSkillLvData skill)
    {
        var learned = hero.kungfuSkills;
        int index = learned?.IndexOf(skill) ?? -1;
        if (index < 0) throw new InvalidOperationException("이 캐릭터의 익힌 무학이 바뀌었습니다.");
        var slots = hero.attackSkillSaveRecord;
        var attacks = new int[slots?.Count ?? 0];
        for (int i = 0; i < attacks.Length; i++) attacks[i] = slots![i];
        int inner = hero.internalSkillSaveRecord, dodge = hero.dodgeSkillSaveRecord, unique = hero.uniqueSkillSaveRecord;
        if (HasEquippedKungfu(hero, skill.skillID) || hero.GetNowActiveSkill()?.skillID == skill.skillID ||
            inner == index || dodge == index || unique == index || Array.IndexOf(attacks, index) >= 0)
            throw new InvalidOperationException("먼저 게임의 무학 화면에서 이 무학을 해제하고, 현재 사용 중인 무학을 바꾸세요.");
        int before = learned!.Count;
        hero.LoseSkill(skill);
        if (learned.Count != before - 1 || learned.IndexOf(skill) >= 0)
            throw new InvalidOperationException("게임이 개별 잊기를 끝내지 못했습니다. 캐릭터 목록을 다시 확인하세요.");
        // Derive the saved positions from their pre-removal values even if LoseSkill already adjusted them.
        for (int i = 0; i < attacks.Length; i++) slots![i] = RebaseSavedSkillIndex(attacks[i], index);
        hero.internalSkillSaveRecord = RebaseSavedSkillIndex(inner, index);
        hero.dodgeSkillSaveRecord = RebaseSavedSkillIndex(dodge, index);
        hero.uniqueSkillSaveRecord = RebaseSavedSkillIndex(unique, index);
        RefreshHero(hero);
    }
}
