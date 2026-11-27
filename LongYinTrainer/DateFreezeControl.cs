using System;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

internal static class DateFreezeControl
{
    private const string Owner = "codex.longyin.trainer.freeze-date";
    private static readonly Harmony Patcher = new(Owner);
    private static readonly MethodInfo Gate = AccessTools.Method(typeof(DateFreezeControl), nameof(AllowDay));
    private static readonly MethodInfo[] Targets = {
        AccessTools.Method(typeof(GameController), nameof(GameController.ChangeDay), Type.EmptyTypes),
        AccessTools.Method(typeof(GameController), nameof(GameController.ChangeDay), new[] { typeof(int) })
    };
    private static ConfigEntry<bool>? _setting;
    private static ManualLogSource? _log;
    private static bool _enabled;

    internal static bool Enabled => _enabled;
    internal static void Initialize(ConfigEntry<bool> setting, ManualLogSource log)
    {
        _setting = setting;
        _log = log;
        Apply(setting.Value);
        setting.SettingChanged += (_, _) => Apply(setting.Value);
    }

    internal static void SetEnabled(bool enabled)
    {
        if (_setting == null) throw new InvalidOperationException("날짜 스위치가 아직 초기화되지 않았습니다");
        if (_setting.Value != enabled) _setting.Value = enabled;
        else Apply(enabled);
        // Also reconcile an already-false setting: turning off must remove any
        // remaining patches, not merely update a checkbox or a config value.
        if (_enabled != enabled) throw new InvalidOperationException("날짜 스위치를 요청한 상태로 바꾸지 못했습니다");
    }

    private static bool AllowDay() => !_enabled;

    private static void Apply(bool enabled)
    {
        // Fail open before patch changes; a failed unpatch cannot keep freezing.
        _enabled = false;
        try
        {
            foreach (var target in Targets)
            {
                if (target == null) throw new InvalidOperationException("게임의 날짜 넘김 진입점을 찾을 수 없습니다");
                Patcher.Unpatch(target, HarmonyPatchType.Prefix, Owner);
                if (enabled) Patcher.Patch(target, prefix: new HarmonyMethod(Gate));
            }
            _enabled = enabled;
            _log?.LogInfo(enabled
                ? "Date freeze ON: both native ChangeDay entry points blocked."
                : "Date freeze OFF: both freeze patches removed; native day progression restored.");
        }
        catch (Exception ex)
        {
            _enabled = false;
            _log?.LogError("Date freeze patch update failed; gate fails open: " + ex);
            throw;
        }
    }
}
