using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine.UI;

// The official editor owns controls, draft creation and final registration.
// No cloned controls or managed Unity button callbacks are installed.
internal static class NativeKungfuEditor
{
    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<int> _template = null!;
    private static IntPtr _activeDraft;
    internal static bool ChoosingBase;
    internal static bool Enabled => _enabled?.Value == true;

    internal static void Install(Harmony harmony, ConfigFile config)
    {
        _enabled = config.Bind("Experimental", "NativeKungfuEditor", true,
            "Open the official custom skill editor directly without the story/seclusion flow or point budget.");
        _template = config.Bind("Experimental", "NativeKungfuTemplate", -1,
            "Native template skill ID. -1 chooses the lowest available native fist/palm skill ID.");
        void Patch(Type type, string method, string? pre = null, string? post = null)
        {
            var original = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.Name, method);
            harmony.Patch(original, pre == null ? null : new HarmonyMethod(typeof(NativeKungfuEditor), pre),
                post == null ? null : new HarmonyMethod(typeof(NativeKungfuEditor), post));
        }
        Patch(typeof(BuildingUIController), "CustomSkillEntry", nameof(OpenEditor));
        Patch(typeof(PlotController), "StartCustomSkillEntry", nameof(OpenEditor));
        Patch(typeof(CustomSkillCreateController), "ShowCreatePanel", nameof(ResetSession));
        Patch(typeof(CustomSkillCreateController), "RefreshSkillPointText", post: nameof(HideBudget));
        Patch(typeof(CustomSkillCreateController), "GetEffectBudget", nameof(UnlimitedBudget));
        Patch(typeof(CustomSkillCreateController), "ChooseNewSpeAddDataButtonClicked", nameof(OpenUnlimitedEffects));
        Patch(typeof(CustomSkillCreateController), "RefreshSpeAddDataGrid", post: nameof(RefreshEffectLimit));
        Patch(typeof(CustomSkillCreateController), "FinishCreate", nameof(BeginConfirm));
        Patch(typeof(CustomSkillCreateController), "FinishCreateConfirmed", post: nameof(Confirmed));
        NativeKungfuFields.Install(harmony);
        NativeSkillDetailRefresh.Install(harmony);
    }

    private static bool OpenEditor()
    {
        if (!Enabled) return true;
        try
        {
            _activeDraft = IntPtr.Zero;
            var editor = CustomSkillCreateController.Instance;
            var db = GameDataController.Instance;
            if (editor == null || GameController.Instance?.worldData?.Player() == null || db?.kungfuSkillDataBase == null)
                throw new InvalidOperationException("공식 편집기 또는 세이브가 아직 준비되지 않았습니다");
            var id = -1; // Only a temporary native UI bootstrap; selection is mandatory.
            if (id < 0)
            {
                var best = int.MaxValue;
                foreach (var pair in db.kungfuSkillDataBase)
                    if (pair.Key >= 0 && pair.Key < 900000 && pair.Value != null && pair.Value.type == 3 && !pair.Value.summonSkill)
                        best = Math.Min(best, pair.Key);
                if (best == int.MaxValue) throw new InvalidOperationException("권장 템플릿을 찾을 수 없습니다");
                id = best;
            }
            if (!db.kungfuSkillDataBase.ContainsKey(id)) throw new InvalidOperationException("지정한 템플릿이 없습니다");
            LongYinTrainerPlugin.Logger.LogInfo("Native editor: opening official window, template=" + id);
            editor.ShowCreatePanel(id);
            _activeDraft = editor.draftSkillData?.Pointer ?? IntPtr.Zero;
            if (_activeDraft == IntPtr.Zero) throw new InvalidOperationException("공식 초안 생성 실패");
            HideBudget(editor);
            RefreshEffectLimit(editor);
            if (editor.skillPointText != null && editor.skillPointText.gameObject.activeSelf)
                throw new InvalidOperationException("포인트 문구를 숨기지 못했습니다");
            if (CustomSkillCreateController.GetEffectBudget(editor.draftSkillData) < 1000000000f)
                throw new InvalidOperationException("초안 예산 패치가 적용되지 않았습니다");
            ChoosingBase = true;
            NativeKungfuFields.ChooseBaseCategory();
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogError("Native editor opening failed: " + ex);
        }
        return false;
    }

    internal static void SelectBase(int id)
    {
        var editor = CustomSkillCreateController.Instance;
        editor.CloseChooseUI();
        editor.ShowCreatePanel(id);
        _activeDraft = editor.draftSkillData?.Pointer ?? IntPtr.Zero;
        var donor = GameDataController.Instance.kungfuSkillDataBase[id];
        TrainerBehaviour.ApplyNativeVisualSource(editor.draftSkillData ?? throw new InvalidOperationException("공식 초안 생성 실패"), donor, "all");
        if (donor.targetType == SkillTargetType.EmptyGridSummon)
        {
            editor.draftSkillData.targetType = donor.targetType;
            editor.draftSkillData.summonID = donor.summonID;
        }
        editor.RefreshSkillIcon();
        NativeKungfuFields.AutomaticDescription = editor.skillDescribe.text;
        ChoosingBase = false;
        HideBudget(editor); RefreshEffectLimit(editor);
        LongYinTrainerPlugin.Logger.LogInfo("Native editor selected base: " + id + ", category=" + editor.draftSkillData?.type);
    }

    internal static bool Owns(CustomSkillCreateController? editor) => Enabled && _activeDraft != IntPtr.Zero &&
        editor?.draftSkillData?.Pointer == _activeDraft && editor.customSkillCreateUI != null && editor.customSkillCreateUI.activeInHierarchy;

    private static void ResetSession(CustomSkillCreateController __instance)
    {
        _activeDraft = IntPtr.Zero;
        if (__instance.skillPointText != null) __instance.skillPointText.gameObject.SetActive(true);
    }

    private static void HideBudget(CustomSkillCreateController __instance)
    {
        if (Owns(__instance) && __instance.skillPointText != null)
            __instance.skillPointText.gameObject.SetActive(false);
    }

    private static bool UnlimitedBudget(KungfuSkillData __0, ref float __result)
    {
        if (!Owns(CustomSkillCreateController.Instance) || __0 == null || __0.Pointer != _activeDraft) return true;
        __result = 1000000000f;
        return false;
    }

    private static void RefreshEffectLimit(CustomSkillCreateController __instance)
    {
        if (!Owns(__instance) || __instance.chooseNewSpeAddDataButton == null) return;
        // Keep the real effect count. Only remove the native count gate and label;
        // do not falsify GetSpeAddDataCount or mutate the global rarity cap array.
        var button = __instance.chooseNewSpeAddDataButton.GetComponent<Selectable>();
        if (button != null) button.interactable = true;
        var text = __instance.chooseNewSpeAddDataButton.GetComponentInChildren<Text>(true);
        if (text != null) text.text = "특수 효과 추가(현재 " + __instance.GetSpeAddDataCount() + ")";
    }

    private static bool OpenUnlimitedEffects(CustomSkillCreateController __instance)
    {
        if (!Owns(__instance)) return true;
        try
        {
            NativeKungfuFields.AllBonuses();
        }
        catch (Exception ex)
        {
            LongYinTrainerPlugin.Logger.LogError("Native editor effect picker failed: " + ex);
        }
        return false;
    }

    private static bool BeginConfirm(CustomSkillCreateController __instance)
    {
        if (Owns(__instance) && ChoosingBase) { NativeKungfuFields.ChooseBaseCategory(); return false; }
        if (Owns(__instance))
        {
            try { SummonSkillValidation.Validate(__instance.draftSkillData); }
            catch (Exception ex) { __instance.ShowSubmitError(ex.Message); LongYinTrainerPlugin.Logger.LogWarning(ex.Message); return false; }
        }
        return true;
    }

    private static void Confirmed(CustomSkillCreateController __instance)
    {
        if (_activeDraft == IntPtr.Zero) return;
        if (__instance.customSkillCreateUI == null || !__instance.customSkillCreateUI.activeInHierarchy)
        {
            if (__instance.skillPointText != null) __instance.skillPointText.gameObject.SetActive(true);
            _activeDraft = IntPtr.Zero;
            LongYinTrainerPlugin.Logger.LogInfo("Native editor: confirmation closed the official editor.");
        }
    }
}
