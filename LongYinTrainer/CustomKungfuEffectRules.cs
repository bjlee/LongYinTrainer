using System;
using System.Collections.Generic;

public static partial class TrainerBehaviour
{
    // Called only while constructing the editor. Never hook combat updates here.
    private static bool MatchesCustomEffectRule(int id, int group)
    {
        var rule = FindSpeAddDefinition(id);
        if (rule == null) return false;
        return group switch
        {
            6 => rule.lastTime != 0 && rule.selfBuff,
            7 => rule.lastTime != 0 && !rule.selfBuff,
            8 => (int)rule.triggerType == 1,
            9 => (int)rule.triggerType == 2,
            _ => false
        };
    }

    private static string CustomEffectRuleDescription(CustomEffectChoice? choice)
    {
        if (choice == null) return "옵션을 선택하면 기본 발동, 대상, 지속 규칙을 봅니다. 이 분류의 원본 구성에 있는 옵션만 표시합니다.";
        var rule = FindSpeAddDefinition(choice.TypeId);
        if (rule == null) return "기본 규칙을 찾지 못했습니다. 기존 수치 형식을 유지하며 발동 방식은 추측하지 않습니다.";
        var trigger = (int)rule.triggerType switch { 1 => "공격 시 발동", 2 => "방어 시 발동", _ => "공수 표지 없음(효과에 따름)" };
        if (rule.lastTime == 0)
            return $"{trigger}; 지속 상태가 아닌 옵션\n수치는 원본 표시 단위로 편집하며, 구체적인 속성이나 효과는 원본이 처리합니다.";
        var duration = rule.lastTime > 0 ? $"지속 {rule.lastTime}턴" : "동적 지속 시간(기본 규칙)";
        var target = rule.selfBuff ? "발동자 자신" : "상호작용 상대(공수 단계로 결정)";
        return $"{trigger}; 대상: {target}\n{duration}; {(rule.stackable ? "중첩 가능" : "중첩 불가")}; 특수 효과는 전용 규칙을 따릅니다\n여기서는 옵션 값을 편집하며 지속 시간이 아닙니다. 쿨다운, 확률 판정, 만료는 원본이 처리합니다.";
    }

    private static string CustomEffectPlacementDescription(string field, CustomEffectChoice? choice)
    {
        var placement = field switch
        {
            "upgrade" => "강화 효과: 강화 속성표이며 지속 상태와는 다릅니다.",
            "equip" => "장비 효과: 장착 후 옵션을 제공하며 기본 조건에 따라 발동합니다.",
            "use" when _customDraft?.Type < 3 => "이 종류의 사용 효과는 공격 무공의 공통 병합 분기를 거치지 않으므로 원본 구성과 대조해야 합니다.",
            _ => "사용 효과: 시전 단계에 옵션을 제공하며 기본 발동 조건을 충족해야 합니다."
        };
        if (choice == null) return placement;
        var database = GameDataController.Instance;
        if (database?.kungfuSkillDataBase == null) return placement + "\n원본 구성을 아직 불러오지 않았습니다.";
        // Read immutable donor snapshots, not edited live definitions. Prefer the
        // current category only; never suggest another category as a fallback.
        var same = new List<string>();

        foreach (var pair in database.kungfuSkillDataBase)
        {
            if (pair.Key < 0 || pair.Key >= CustomKungfuIdMin) continue;
            var skill = OriginalKungfu(database, pair.Key);
            if (skill == null || skill.type != _customDraft?.Type) continue;
            var table = field switch { "upgrade" => skill.upgradeAddData, "equip" => skill.equipAddData, _ => skill.useAddData };
            if (table == null || table.Get(choice.TypeId) == 0f) continue;
            var examples = same;
            if (examples.Count < 2) examples.Add($"{skill.name}（{CustomKungfuTypeName(skill.type)}，ID {pair.Key}）");
        }
        var samples = same;
        return placement + "\n" + (samples.Count == 0
            ? "이 표에서 0이 아닌 원본 구성 예시를 찾지 못했습니다. 발동이 검증되었다는 뜻은 아닙니다."
            : "같은 종류 원본 예시: " + string.Join("、", samples));
    }
}
