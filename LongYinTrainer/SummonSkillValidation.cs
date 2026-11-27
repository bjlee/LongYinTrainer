using System;

internal static class SummonSkillValidation
{
    internal static void Validate(KungfuSkillData skill)
    {
        if (skill == null || skill.targetType != SkillTargetType.EmptyGridSummon) return;
        var summons = GameDataController.Instance?.SummonDataBase;
        if (summons == null || !summons.ContainsKey(skill.summonID) || summons[skill.summonID] == null)
            throw new InvalidOperationException($"소환 무공 “{skill.name}”의 소환 대상이 올바르지 않습니다(ID {skill.summonID}). 원본 소환 무공을 기반으로 쓰세요. 일반 무공의 대상만 빈 칸 소환으로 바꿔서는 안 됩니다.");
        LongYinTrainerPlugin.Logger.LogInfo($"Summon skill validated: skill={skill.skillID}, summon={skill.summonID}, animation={skill.animationName}");
    }
}
