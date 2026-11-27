using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static void BuildNativeItemCatalog()
    {
        if (ResolveSelectedItem() != null) { BuildLegacyItemCatalog(); return; }
        EnsureCatalog();
        var p = NativePageParent;
        var categories = CatalogCategories();
        var entries = new CatalogEntry?[12];
        var cards = new Button[12];
        var frames = new Image[12];
        var icons = new Image[12];
        var gems = new Image[12];
        var names = new Text[12];
        var missing = new Text[12];
        var categoryButtons = new Button[categories.Count];
        var filtered = new List<CatalogEntry>();
        var pageCount = 1;
        Text pageLabel=null!, title=null!, detail=null!, levelLabel=null!, rareLabel=null!, preview=null!;
        InputField quantity=null!, level=null!, rarity=null!;
        Action updateBookFilters = () => {};

        AddText(p, "물품 도감", Vector2.zero, new Vector2(160,34),22);
        var search = AddInput(p,_catalogSearch,new Vector2(160,0),new Vector2(245,34));
        AddButton(p,"검색",new Vector2(415,0),new Vector2(80,34),()=> { _catalogSearch=search.text.Trim(); _catalogPage=0; UpdateGrid(); });
        AddButton(p,"비우기",new Vector2(505,0),new Vector2(80,34),()=> { search.text=""; _catalogSearch=""; _catalogPage=0; UpdateGrid(); });
        AddButton(p,"데이터 새로고침",new Vector2(595,0),new Vector2(100,34),()=> {
            Catalog.Clear(); _catalogSelected=null; EnsureCatalog(); RefreshRuntimeIconIndex(); UpdateGrid(); UpdateDetails();
        });
        for(var i=0;i<categories.Count;i++) {
            var n=i;
            categoryButtons[i]=AddButton(p,categories[i],new Vector2(0,48+i*38),new Vector2(125,32),()=> {
                if(_catalogCategory==n) return;
                _catalogCategory=n; _catalogPage=0; UpdateGrid();
            });
        }
        for(var i=0;i<12;i++) {
            var n=i;
            cards[i]=AddButton(p,"",new Vector2(140+i%4*142,48+i/4*132),new Vector2(132,122),()=> {
                if(entries[n]==null || ReferenceEquals(_catalogSelected,entries[n])) return;
                SaveInputs(); ClearSelectedItem(); _catalogSelected=entries[n];
                if (_catalogSelected!.Kind == CatalogKind.Book) { _catalogRarity=_catalogSelected.RareLv.ToString(); rarity.text=_catalogRarity; }
                if (_catalogSelected.Kind == CatalogKind.Material || _catalogSelected.Kind == CatalogKind.Treasure)
                { _catalogLevel=_catalogSelected.BaseLevel.ToString(); level.text=_catalogLevel; }
                UpdateSelection(); UpdateDetails();
            });
            frames[i]=cards[i].GetComponent<Image>();
            var paper=UiObject("CardPaper",cards[i].transform,new Vector2(124,114),new Vector2(4,4));
            var bg=paper.AddComponent<Image>(); bg.color=FieldPaper; bg.raycastTarget=false;
            icons[i]=UiObject("RuntimeIcon",paper.transform,new Vector2(82,82),new Vector2(21,3)).AddComponent<Image>();
            icons[i].preserveAspect=true; icons[i].raycastTarget=false;
            gems[i]=UiObject("Rarity",cards[i].transform,new Vector2(18,18),new Vector2(57,-7)).AddComponent<Image>();
            gems[i].preserveAspect=true; gems[i].raycastTarget=false;
            names[i]=AddText(paper.transform,"",new Vector2(3,90),new Vector2(118,21),14,TextAnchor.MiddleCenter);
            missing[i]=AddText(paper.transform,"아이콘 없음",new Vector2(6,26),new Vector2(112,40),14,TextAnchor.MiddleCenter);
        }
        AddButton(p,"이전 페이지",new Vector2(140,452),new Vector2(120,34),()=> { if(_catalogPage>0) { _catalogPage--; UpdateGrid(); } });
        pageLabel=AddText(p,"",new Vector2(270,452),new Vector2(250,34),16,TextAnchor.MiddleCenter);
        AddButton(p,"다음 페이지",new Vector2(530,452),new Vector2(120,34),()=> { if(_catalogPage+1<pageCount) { _catalogPage++; UpdateGrid(); } });
        title=AddText(p,"",new Vector2(730,48),new Vector2(330,38),21,TextAnchor.MiddleCenter);
        detail=AddText(p,"",new Vector2(730,90),new Vector2(330,82),16);
        AddText(p,"수량",new Vector2(730,190),new Vector2(80,34));
        quantity=AddInput(p,_catalogQuantity,new Vector2(815,190),new Vector2(130,34));
        levelLabel=AddText(p,"등급",new Vector2(730,234),new Vector2(80,34));
        level=AddInput(p,_catalogLevel,new Vector2(815,234),new Vector2(130,34));
        rareLabel=AddText(p,"희귀도",new Vector2(730,278),new Vector2(80,34));
        rarity=AddInput(p,_catalogRarity,new Vector2(815,278),new Vector2(130,34));
        preview=AddText(p,"",new Vector2(730,316),new Vector2(330,52),14);
        AddButton(p,"주인공 가방에 추가",new Vector2(730,380),new Vector2(300,44),()=> { SaveInputs(); AddCatalogItem(_catalogSelected,_catalogQuantity,_catalogLevel,_catalogRarity); });
        AddText(p,"희귀도 설명\n"+BuildRarityGuide(),new Vector2(730,438),new Vector2(330,160),15);
        updateBookFilters = BuildBookCatalogFilters(p, UpdateGrid, editingItem: false);
        UpdateGrid(); UpdateDetails();
        LongYinTrainerPlugin.Logger.LogInfo("Catalog UI pool built: 12 reusable cards; runtime icons only.");

        void SaveInputs() { _catalogQuantity=quantity.text; _catalogLevel=level.text; _catalogRarity=rarity.text; }
        void UpdateSelection() {
            for(var i=0;i<12;i++) if(entries[i]!=null) ApplySprite(frames[i],_frameSprite,true,Color.white,
                ReferenceEquals(entries[i],_catalogSelected) ? new Color(.72f,.12f,.08f,1f) : RarityColor(CatalogDisplayGrade(entries[i]!)));
        }
        void UpdateGrid() {
            updateBookFilters();
            filtered=FilterCatalog(categories[Math.Clamp(_catalogCategory,0,categories.Count-1)],_catalogSearch);
            pageCount=Math.Max(1,(filtered.Count+11)/12); _catalogPage=Math.Clamp(_catalogPage,0,pageCount-1);
            for(var i=0;i<categories.Count;i++) {
                StyleNavigation(categoryButtons[i], i == _catalogCategory);
            }
            var shown=0; var found=0;
            for(var i=0;i<12;i++) {
                var index=_catalogPage*12+i;
                entries[i]=index<filtered.Count ? filtered[index] : null;
                cards[i].gameObject.SetActive(entries[i]!=null);
                var e=entries[i]; if(e==null) continue;
                shown++; names[i].text=e.Name;
                icons[i].sprite=GetItemSprite(e.IconName); icons[i].enabled=icons[i].sprite!=null;
                if(icons[i].enabled) found++;
                missing[i].gameObject.SetActive(!icons[i].enabled);
                gems[i].sprite=GetItemSprite("RareLv"+Math.Clamp(CatalogDisplayGrade(e),0,5)); gems[i].enabled=gems[i].sprite!=null;
            }
            pageLabel.text=$"{_catalogPage+1} / {pageCount} 페이지　총 {filtered.Count}개";
            UpdateSelection();
            LongYinTrainerPlugin.Logger.LogInfo($"Catalog runtime icons: {found}/{shown} visible cards resolved; PNG fallback DISABLED.");
        }
        void UpdateDetails() {
            var e=_catalogSelected;
            title.text=e?.Name ?? "왼쪽 도감에서 물품을 선택하세요";
            detail.text=e != null ? e.Detail + (e.Category is "약품" or "음식" ? $"\n기본 등급: {LevelName(e.BaseLevel)} ({e.BaseLevel})" : "") : "목록은 현재 게임 버전의 데이터를 바로 읽습니다.\n가방에 없어도 바로 추가할 수 있습니다.";
            var usesLevel=e!=null && (e.IsEquipment || e.Category=="재료" || e.Category=="보물");
            var usesRarity=e!=null && (usesLevel || e.Category=="비급" || e.Category=="약품" || e.Category=="음식");
            levelLabel.gameObject.SetActive(usesLevel); level.gameObject.SetActive(usesLevel);
            rareLabel.gameObject.SetActive(usesRarity); rarity.gameObject.SetActive(usesRarity);
            _catalogLevelInput=usesLevel ? level : null; _catalogRarityInput=usesRarity ? rarity : null; _catalogPreviewText=preview;
            preview.text=e==null ? "" : $"원래 등급 {e.BaseLevel}｜{RarityName(e.RareLv)}";
            UpdateCatalogPreview();
        }
    }
}
