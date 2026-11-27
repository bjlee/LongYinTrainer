using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NativeLists = Il2CppSystem.Collections.Generic;

public static partial class TrainerBehaviour
{
    private static List<HeroData> RemovalHeroes(WorldData world)
    {
        var heroes = KungfuHeroes().ToList();
        if (world.TempHeros != null)
            foreach (var hero in world.TempHeros)
                if (hero != null) heroes.Add(hero);
        return heroes.GroupBy(hero => hero.Pointer).Select(group => group.First()).ToList();
    }

    private static bool ReferencesSkill(HeroData hero, int id) =>
        hero.FindSkill(id) != null || HasEquippedKungfu(hero, id) || hero.GetNowActiveSkill()?.skillID == id;

    private static void OpenKungfuRemoval(CustomKungfuRecord record)
    {
        try
        {
            var world = GameController.Instance?.worldData ?? throw new InvalidOperationException("먼저 세이브를 불러오세요.");
            var users = RemovalHeroes(world).Where(hero => ReferencesSkill(hero, record.Id)).ToList();
            void Show(int page)
            {
                InvalidateCustomKungfuPages();
                ActivateCustomKungfuPage("remove", parent =>
                {
                    var action = record.EditOriginal ? "원본 정의 복원" : "영구 삭제";
                    AddText(parent, $"{action}「{record.Name}」  ID {record.Id}", new Vector2(15, 12), new Vector2(1210, 52), 22);
                    AddText(parent, record.EditOriginal ? "원본 무공의 사용자 구성을 제거합니다. 캐릭터는 원본 무공을 계속 익힌 상태로 남습니다." :
                        "확인하면 현재 세이브의 아래 캐릭터에게서 이 무학을 해제하고 잊게 하며, 모드 공용 구성에서 정의를 삭제합니다. 다른 세이브에서 이 무공을 쓰고 있어도 영향을 받습니다.", new Vector2(15, 72), new Vector2(1210, 70), 17);
                    AddText(parent, $"현재 세이브에서 이 무공을 익히거나 장착한 캐릭터: {users.Count}명", new Vector2(15, 155), new Vector2(1200, 42), 19);
                    const int pageSize = 20;
                    for (int i = 0; i < pageSize && page * pageSize + i < users.Count; i++)
                    {
                        var h = users[page * pageSize + i];
                        AddText(parent, $"{h.heroName}（ID {h.heroID}）", new Vector2(15 + i % 4 * 305, 212 + i / 4 * 52), new Vector2(300, 44), 16);
                    }
                    if (page > 0) AddCustomKungfuButton(parent, "이전 페이지", new Vector2(15, 486), new Vector2(180, 40), () => Show(page - 1));
                    if ((page + 1) * pageSize < users.Count) AddCustomKungfuButton(parent, "다음 페이지", new Vector2(220, 486), new Vector2(180, 40), () => Show(page + 1));
                    AddCustomKungfuButton(parent, "취소", new Vector2(15, 575), new Vector2(380, 52), ShowCustomKungfuTab);
                    AddCustomKungfuButton(parent, "확인" + action, new Vector2(650, 575), new Vector2(550, 52), () =>
                    {
                        try
                        {
                            if (GameController.Instance?.worldData?.Pointer != world.Pointer || FindSavedCustomKungfu(record.Id) != record)
                                throw new InvalidOperationException("세이브 또는 무공이 바뀌었습니다. 다시 선택하세요.");
                            var current = RemovalHeroes(world);
                            var currentUsers = current.Where(h => ReferencesSkill(h, record.Id)).ToList();
                            if (!users.Select(h => h.Pointer).ToHashSet().SetEquals(currentUsers.Select(h => h.Pointer)))
                            { OpenKungfuRemoval(record); return; }
                            DeleteKungfu(record, world, current, currentUsers);
                        }
                        catch (Exception ex) { _customStatus = ex.Message; LongYinTrainerPlugin.Logger.LogWarning(ex); }
                    });
                });
            }
            Show(0);
        }
        catch (Exception ex) { _customStatus = ex.Message; }
    }

    private static void CheckKungfuBooks(WorldData world, List<HeroData> heroes, int id)
    {
        void Book(ItemData? item, string location)
        {
            if (item?.type == ItemType.Book && item.bookData?.skillID == id)
                throw new InvalidOperationException($"「{location}」에 이 무공의 비급이 남아 있습니다. 비급을 먼저 처리한 뒤 삭제하세요.");
        }
        void Storage(ItemListData? store, string location)
        {
            if (store?.allItem != null) foreach (var item in store.allItem) Book(item, location);
        }
        void Display(NativeLists.List<NativeLists.List<ItemData>>? shared, string location)
        {
            if (shared != null) foreach (var row in shared)
                if (row != null) foreach (var item in row) Book(item, location);
        }
        void Writers(NativeLists.List<BookWriterData>? writers)
        {
            if (writers == null) return;
            foreach (var writer in writers)
            {
                if (writer == null) continue;
                Book(writer.targetBookData, "저술"); Book(writer.combineBookData, "합본");
                if (writer.targetSkillData?.skillID == id) throw new InvalidOperationException("이 무공이 저술 목록에 남아 있습니다. 저술 작업을 먼저 처리하세요.");
            }
        }
        foreach (var h in heroes)
        {
            Storage(h.itemListData, h.heroName + "의 가방"); Storage(h.selfStorage, h.heroName + "의 창고");
        }
        if (world.Forces != null) foreach (var f in world.Forces)
        {
            if (f == null) continue;
            Storage(f.forceStorage, "세력 창고"); Storage(f.bookStorage, "장경각");
            Display(f.showRoomItems, "세력 진열실"); Writers(f.bookWriterList);
        }
        Storage(world.governStorage, "관아 창고"); Storage(world.speBookStorage, "특수 장서");
        Storage(GameDataController.ExternalStorage, "외부 창고");
        Display(world.showRoomItems, "진열실"); Book(world.playerAuctionItem, "경매"); Writers(world.playerBookWriter);
    }

    private static Action CaptureList<T>(NativeLists.List<T>? list)
    {
        if (list == null) return () => { };
        var entries = new T[list.Count];
        for (var i = 0; i < entries.Length; i++) entries[i] = list[i];
        return () =>
        {
            list.Clear();
            foreach (var entry in entries) list.Add(entry);
        };
    }

    private static Action CaptureLoadout(HeroData hero)
    {
        var learned = CaptureList(hero.kungfuSkills);
        var attacks = CaptureList(hero.attackSkills);
        var slots = CaptureList(hero.attackSkillSaveRecord);
        var inner = hero.internalSkill;
        var dodge = hero.dodgeSkill;
        var unique = hero.uniqueSkill;
        var active = hero.nowActiveSkill;
        var innerSlot = hero.internalSkillSaveRecord;
        var dodgeSlot = hero.dodgeSkillSaveRecord;
        var uniqueSlot = hero.uniqueSkillSaveRecord;
        var flags = new List<(KungfuSkillLvData Skill, bool Equipped)>();
        if (hero.kungfuSkills != null) foreach (var entry in hero.kungfuSkills)
            if (entry != null) flags.Add((entry, entry.equiped));
        return () =>
        {
            learned(); attacks(); slots();
            hero.internalSkill = inner; hero.dodgeSkill = dodge; hero.uniqueSkill = unique;
            hero.internalSkillSaveRecord = innerSlot; hero.dodgeSkillSaveRecord = dodgeSlot;
            hero.uniqueSkillSaveRecord = uniqueSlot; hero.nowActiveSkill = active;
            foreach (var flag in flags) flag.Skill.equiped = flag.Equipped;
            hero.RecountSkillState();
            RefreshHero(hero);
        };
    }

    private static void DeleteKungfu(CustomKungfuRecord record, WorldData world, List<HeroData> heroes, List<HeroData> users)
    {
        var battle = BattleController.Instance;
        if (_customKungfuStoreLoadFailed || _customBattlePreviewActive ||
            (battle != null && battle.battleState != BattleState.None && battle.battleState != BattleState.End))
            throw new InvalidOperationException("구성이 정상적으로 불러와지고 전투가 끝난 뒤 삭제하세요.");
        var db = GameDataController.Instance ?? throw new InvalidOperationException("데이터베이스를 불러오지 않았습니다.");
        var id = record.Id;
        var original = record.EditOriginal ? OriginalKungfu(db, id) : null;
        if (record.EditOriginal && original == null) throw new InvalidOperationException("원본 정의를 불러오지 않았습니다.");
        if (!record.EditOriginal) CheckKungfuBooks(world, heroes, id);
        var store = new CustomKungfuStore { Schema = _customKungfuStore.Schema, NextId = _customKungfuStore.NextId,
            Skills = _customKungfuStore.Skills.Where(s => s.Id != id).ToList() };
        var undo = new Stack<Action>();
        try
        {
            if (!record.EditOriginal) foreach (var hero in users)
            {
                undo.Push(CaptureLoadout(hero));
                var skill = hero.FindSkill(id);
                while (skill != null)
                {
                    if (HasEquippedKungfu(hero, id) || skill.equiped) hero.UnequipSkill(skill, false);
                    ForgetUnassignedSkill(hero, skill);
                    skill = hero.FindSkill(id);
                }
                if (ReferencesSkill(hero, id)) throw new InvalidOperationException($"먼저 {hero.heroName}에게서 남은 초식을 해제한 뒤 다시 시도하세요.");
            }
            WriteCustomKungfuStore(store);
        }
        catch
        {
            while (undo.Count > 0) undo.Pop()();
            throw;
        }
        _customKungfuStore = store;
        if (original != null)
        {
            CopyKungfuDefinition(original, db.kungfuSkillDataBase[id]);
            RefreshLearnedCustomKungfuInstances(id);
        }
        else db.kungfuSkillDataBase.Remove(id);
        CustomKungfuIconDonors.Remove(id); CustomKungfuIconFiles.Remove(id); CustomKungfuBaseDonors.Remove(id);
        _customDraft = NewCustomKungfuDraft();
        _customBasicChoiceField = _customEffectEditorField = "";
        Catalog.Clear();
        _customStatus = original == null ? $"「{record.Name}」을(를) 영구 삭제했습니다. 현재 게임을 저장하세요." : $"「{original.name}」의 원본 정의를 복원했습니다.";
        InvalidateCustomKungfuPages();
        ShowCustomKungfuTab();
    }
}
