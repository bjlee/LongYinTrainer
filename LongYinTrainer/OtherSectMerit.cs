using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static partial class TrainerBehaviour
{
    // The exchange currency belongs to each ForceData, not to the selected hero.
    private static void BuildOtherSectMerit(Transform p)
    {
        var world = GameController.Instance?.worldData;
        var choices = new List<ForceData>();
        if (world?.Forces != null)
            for (var i = 0; i < world.Forces.Count; i++)
            {
                var force = world.Forces[i];
                if (force != null && force.forceID > 0) choices.Add(force);
            }
        choices.Sort((a, b) => a.forceID.CompareTo(b.forceID));
        AddText(p, "다른 문파 · 교환 공적", new Vector2(540, 415), new Vector2(490, 32), 18);
        if (choices.Count == 0)
        {
            AddText(p, "먼저 세이브를 불러오세요.", new Vector2(540, 455), new Vector2(490, 34));
            return;
        }

        var selected = 0;
        var current = OtherForceContributionExchangeController.Instance;
        if (current?.exchangeUIPanel != null && current.exchangeUIPanel.activeInHierarchy && current.targetForceData != null)
        {
            var index = choices.FindIndex(f => f.forceID == current.targetForceData.forceID);
            if (index >= 0) selected = index;
        }
        var label = AddText(p, "", new Vector2(610, 454), new Vector2(345, 36), 17);
        var value = AddInput(p, "0", new Vector2(610, 499), new Vector2(195, 36));
        AddText(p, "공적", new Vector2(540, 499), new Vector2(65, 36));
        void ReadSelection()
        {
            var target = choices[selected];
            label.text = $"{target.GetForceName(true)}（ID {target.forceID}）";
            value.text = target.playerOutForceContribution.ToString("R", CultureInfo.InvariantCulture);
        }
        AddButton(p, "이전", new Vector2(540, 454), new Vector2(65, 36), () =>
        {
            selected = (selected + choices.Count - 1) % choices.Count;
            ReadSelection();
        });
        AddButton(p, "다음", new Vector2(960, 454), new Vector2(65, 36), () =>
        {
            selected = (selected + 1) % choices.Count;
            ReadSelection();
        });
        AddButton(p, "공적 적용", new Vector2(815, 499), new Vector2(210, 36), () =>
        {
            try
            {
                var target = choices[selected];
                var liveWorld = GameController.Instance?.worldData;
                if (world == null || liveWorld == null || liveWorld.Pointer != world.Pointer ||
                    liveWorld.GetForce(target.forceID)?.Pointer != target.Pointer)
                {
                    _status = "세이브 또는 문파 데이터가 바뀌었습니다. 세력 페이지를 다시 연 뒤 수정하세요.";
                    RequestPageRebuild();
                    return;
                }
                if (!float.TryParse(value.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
                    !float.IsFinite(amount) || amount < 0 || amount > 100000000)
                {
                    _status = "공적은 0–100000000 사이의 올바른 숫자로 입력하세요.";
                    return;
                }
                target.playerOutForceContribution = amount;
                ReadSelection();
                _status = $"{target.GetForceName(true)}의 교환 공적을 {value.text}(으)로 설정했습니다. 다른 문파는 그대로입니다. 게임에서 세이브를 저장하세요.";
                // Refresh only the open exchange panel for this exact faction.
                var exchange = OtherForceContributionExchangeController.Instance;
                if (exchange?.exchangeUIPanel != null && exchange.exchangeUIPanel.activeInHierarchy &&
                    exchange.targetForceData?.Pointer == target.Pointer)
                {
                    try { exchange.RefreshExchangeUI(); }
                    catch (Exception ex) { LongYinTrainerPlugin.Logger.LogWarning("공적은 기록했지만 교환 화면 새로고침에 실패했습니다: " + ex.Message); }
                }
            }
            catch (Exception ex) { _status = "문파 공적 수정 실패: " + ex.Message; }
        });
        AddButton(p, "현재 교환 문파 읽기", new Vector2(540, 544), new Vector2(290, 36), () =>
        {
            var exchange = OtherForceContributionExchangeController.Instance;
            var index = exchange?.exchangeUIPanel != null && exchange.exchangeUIPanel.activeInHierarchy && exchange.targetForceData != null
                ? choices.FindIndex(f => f.Pointer == exchange.targetForceData.Pointer) : -1;
            if (index < 0) { _status = "먼저 대상 문파의 공적 교환 화면을 여세요."; return; }
            selected = index;
            ReadSelection();
        });
        AddButton(p, "수치 다시 읽기", new Vector2(840, 544), new Vector2(185, 36), ReadSelection);
        AddText(p, "문파마다 따로 수정합니다. 바꾸면 수치를 다시 읽으니 먼저 적용하세요.\n여기는 교환 화면의 “공적”에 해당하며, 수정 후 게임을 저장하세요.", new Vector2(540, 589), new Vector2(490, 54), 15);
        ReadSelection();
    }
}
