using System;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _consumableLevelFilter = -1;
    private static int CatalogDisplayGrade(CatalogEntry entry) => entry.Category is "약품" or "음식" ? entry.BaseLevel : entry.RareLv;
    private static int _bookTypeFilter = -1, _bookRarityFilter = -1;

    private static Action BuildBookCatalogFilters(Transform parent, Action refresh, bool editingItem)
    {
        const int filterTop = 498;
        const int rowHeight = 32;
        const int gap = 6;
        const int rowStep = rowHeight + gap;
        var levelButtons = new Button[7];
        for (var i = 0; i < levelButtons.Length; i++)
        {
            var value = i - 1;
            levelButtons[i] = AddButton(parent, i == 0 ? "모든 등급" : LevelName(value),
                new Vector2(i * 98, filterTop), new Vector2(92, rowHeight), () =>
                { if (_consumableLevelFilter == value) return; _consumableLevelFilter = value; _catalogPage = 0; refresh(); });
        }
        var typeButtons = new Button[10];
        var rarityButtons = new Button[7];
        for (var i = 0; i < typeButtons.Length; i++)
        {
            var value = i - 1;
            typeButtons[i] = AddButton(parent, i == 0 ? "모든 무학" : CustomKungfuTypeName(value),
                new Vector2(i % 5 * 138, filterTop + i / 5 * rowStep), new Vector2(132, rowHeight), () =>
                { if (_bookTypeFilter == value) return; _bookTypeFilter = value; _catalogPage = 0; refresh(); });
        }
        for (var i = 0; i < rarityButtons.Length; i++)
        {
            var value = i - 1;
            rarityButtons[i] = AddButton(parent, i == 0 ? "모든 품급" : RarityName(value),
                new Vector2(i * 98, filterTop + 2 * rowStep), new Vector2(92, rowHeight), () =>
                { if (_bookRarityFilter == value) return; _bookRarityFilter = value; _catalogPage = 0; refresh(); });
        }
        var note = AddText(parent, "", Vector2.zero, new Vector2(690, 40), 15, TextAnchor.UpperLeft);
        return () =>
        {
            var categories = CatalogCategories();
            var category = categories[Math.Clamp(_catalogCategory, 0, categories.Count - 1)];
            var visible = category == "비급";
            var consumable = category is "약품" or "음식";
            var rowCount = visible ? 3 : consumable ? 1 : 0;
            var filterHint = visible ? "무학 종류와 비급 품급으로 거릅니다." : consumable ? "카드 테두리 색은 기본 등급이며, 희귀도는 오른쪽에서 설정할 수 있습니다." : "";
            var modeHint = editingItem ? "가방 물품 편집 중입니다. 도감 카드를 누르면 생성 모드로 돌아갑니다." : "";
            note.text = filterHint.Length > 0 && modeHint.Length > 0 ? filterHint + "\n" + modeHint : filterHint + modeHint;
            note.rectTransform.anchoredPosition = new Vector2(0, -(filterTop + rowCount * rowStep));
            note.gameObject.SetActive(note.text.Length > 0);
            for (var i = 0; i < levelButtons.Length; i++) SetBookFilterButton(levelButtons[i], consumable, i - 1 == _consumableLevelFilter);
            for (var i = 0; i < typeButtons.Length; i++) SetBookFilterButton(typeButtons[i], visible, i - 1 == _bookTypeFilter);
            for (var i = 0; i < rarityButtons.Length; i++) SetBookFilterButton(rarityButtons[i], visible, i - 1 == _bookRarityFilter);
        };
    }

    private static void SetBookFilterButton(Button button, bool visible, bool selected)
    {
        button.gameObject.SetActive(visible);
        if (!visible) return;
        StyleNavigation(button, selected);
    }
}
