using JunkToNeon.Contracts;
using JunkToNeon.Core;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        JtnYardGraphic[] yardFamilies;
        RectTransform yardGround;
        JtnProductIcon yardHousePlaceholder;
        GameSession yardSession;
        readonly int[] yardPaidStages=new int[6];
        bool yardPresented;
        string yardSelectedName;
        readonly UnityEngine.UI.Button[] yardButtons=new UnityEngine.UI.Button[6];
        UnityEngine.UI.Button yardActivityButton;
        TMPro.TMP_Text yardInspectionHint;
        GameSession yardInputSession,yardRouteSession;
        long yardInputSequence=-1;
        bool yardInputUsable;
        string yardReturnFamily;
        int yardReturnBoundary;
        static readonly float[] YardHouseWidths={652f/809,596f/809,694f/809},YardHouseOrigins={.5368098f,.5f,.4135447f};

        // This projection contains appearance only. The HUD always receives its actual snapshot.
        // Ownership is validated by the owning chapter rules; free absolute3 is paid stage2.
        void RenderYard(ModelState state)
        {
            if(yardFamilies==null)
            {
                yardGround=Area("Persistent yard",world,Vector2.zero,Vector2.one);
                yardGround.SetSiblingIndex(2);
                var ground=yardGround.gameObject.AddComponent<JtnYardGraphic>();ground.Configure(-1,0);
                if(Resources.Load<Sprite>("Art/StyleC/courtyard-background")!=null)ground.enabled=false;
                yardFamilies=new JtnYardGraphic[6];
                for(int i=0;i<6;i++)
                {
                    var root=Area("Yard "+FamilyIds.All[i],world,Vector2.zero,Vector2.zero);
                    root.SetSiblingIndex(3+i);yardFamilies[i]=root.gameObject.AddComponent<JtnYardGraphic>();
                }
                yardFamilies[0].rectTransform.SetSiblingIndex(house.transform.GetSiblingIndex()+1);
            }
            if(!ReferenceEquals(yardSession,Session))
            {yardSession=Session;yardPresented=false;}
            if(!Busy||!yardPresented)
            {
                var chapter=ChapterEconomy.Project(state);
                var stages=ChapterCatalog.Rules.Stages(chapter.Progress);
                yardSelectedName=chapter.SelectedProjectId==null?null:ProjectName(chapter.SelectedProjectId);
                for(int i=0;i<6;i++)
                {
                    int absolute=stages.AbsoluteLevel(FamilyIds.All[i]);
                    yardPaidStages[i]=absolute<=2?absolute:absolute-1;
                    int local=stages.LocalLevel(FamilyIds.All[i]);
                    bool selected=chapter.SelectedProjectId!=null&&local<2&&
                        chapter.SelectedProjectId==ChapterIds.Project(chapter.Progress.EraId,FamilyIds.All[i],local+1);
                    yardFamilies[i].Configure(i,yardPaidStages[i],selected);
                }
                yardPresented=true;
                int houseStage=Mathf.Min(houseStages!=null&&houseStages.Length==5?4:2,yardPaidStages[0]);
                var sprite=houseStages!=null&&houseStages.Length>houseStage?houseStages[houseStage]:null;
                if(sprite!=null&&sprite.texture==null)sprite=null;
                house.sprite=sprite;house.color=sprite!=null?Color.white:Color.clear;
                if(sprite==null&&yardHousePlaceholder==null)
                {
                    var placeholder=Area("House placeholder",house.transform,Vector2.zero,Vector2.one);
                    yardHousePlaceholder=placeholder.gameObject.AddComponent<JtnProductIcon>();
                    yardHousePlaceholder.raycastTarget=false;
                }
                if(yardHousePlaceholder!=null)
                {
                    yardHousePlaceholder.gameObject.SetActive(sprite==null);
                    var shape=houseStage==0?JtnProductIcon.Symbol.YardRuin:houseStage==1?JtnProductIcon.Symbol.YardHouse:JtnProductIcon.Symbol.YardNeon;
                    if(yardHousePlaceholder.Shape!=shape)yardHousePlaceholder.Shape=shape;
                    yardHousePlaceholder.color=ColorOf(houseStage==0?"BE854F":houseStage==1?"EDDEB4":"86E2E5");
                }
                bool illustrated=houseStages!=null&&houseStages.Length==5;
                yardFamilies[0].UseHouseSprite=illustrated;
                house.rectTransform.pivot=illustrated?new Vector2(.5f,.10f):new Vector2(YardHouseOrigins[houseStage],.1409147f);
            }
            // Geometry reflows even while a saved acquisition is waiting for its normal reveal.
            float h=world.rect.height*.47f;int visibleHouse=Mathf.Min(2,yardPaidStages[0]);
            house.rectTransform.anchorMin=house.rectTransform.anchorMax=new Vector2(.48f,.34f);
            house.rectTransform.anchoredPosition=Vector2.zero;
            house.rectTransform.sizeDelta=new Vector2(h*(houseStages!=null&&houseStages.Length==5?1:YardHouseWidths[visibleHouse]),h);
            // Keep a clear lane between the porch and cart, with side silhouettes apart.
            YardRect(0,.48f,.31f,.25f,.17f);
            YardRect(1,.34f,.485f,.16f,.20f);
            YardRect(2,.69f,.485f,.14f,.18f);
            YardRect(3,.28f,.25f,.15f,.19f);
            YardRect(4,.51f,.16f,.18f,.15f);
            YardRect(5,.73f,.25f,.15f,.20f);
            RenderYardNavigation();
        }
        void RenderYardNavigation()
        {
            if(!Application.isPlaying)return;
            var state=Session?.State;
            if(state==null)return;
            bool usable=ProductCanNavigate()&&!panel.activeSelf;
            if(yardInspectionHint==null)yardInspectionHint=Label("Tap a building to inspect",world,new Vector2(.06f,.10f),new Vector2(.94f,.16f),10,ColorOf("183A4B"),TMPro.TextAlignmentOptions.Center);
            yardInspectionHint.text=yardSelectedName==null?"Tap a building to inspect":"Selected: "+yardSelectedName+" · tap to inspect";
            // With no goal the plaque below already asks to inspect; keep only the selected-goal line.
            yardInspectionHint.gameObject.SetActive(!panel.activeSelf&&(yardSelectedName!=null||productGoal==null||!productGoal.gameObject.activeSelf));
            for(int i=0;i<6;i++)
            {
                if(yardButtons[i]==null)
                {
                    var button=Button("Inspect yard "+FamilyIds.All[i],"",world,Vector2.zero,Vector2.one,Color.clear,()=>{});
                    button.transition=UnityEngine.UI.Selectable.Transition.None;AddSectionConsent(button);yardButtons[i]=button;
                }
                var rect=(RectTransform)yardButtons[i].transform;var source=yardFamilies[i].rectTransform;
                rect.anchorMin=source.anchorMin;rect.anchorMax=source.anchorMax;rect.offsetMin=rect.offsetMax=Vector2.zero;
                if(i==0){rect.anchorMin=new Vector2(.40f,.33f);rect.anchorMax=new Vector2(.60f,.48f);}
                yardButtons[i].interactable=usable;yardButtons[i].gameObject.SetActive(!panel.activeSelf);
            }
            // One call-local snapshot for binding; callbacks still read current authority.
            if(ReferenceEquals(yardInputSession,Session)&&yardInputSequence==state.sequence&&yardInputUsable==usable)return;
            yardInputSession=Session;yardInputSequence=state.sequence;yardInputUsable=usable;
            for(int i=0;i<6;i++)
            {
                var button=yardButtons[i];string family=FamilyIds.All[i];var session=Session;long sequence=state.sequence;
                var visible=new UnityEngine.UI.Button.ButtonClickedEvent();button.onClick=visible;InvalidateExpansionConsent(button);
                visible.AddListener(()=>
                {
                    if(!ReferenceEquals(button.onClick,visible)||!ReferenceEquals(session,Session)||Session.State.sequence!=sequence||
                        !ProductCanNavigate()||panel.activeSelf||!button.isActiveAndEnabled||!button.IsInteractable()||!PointerConsentAllows(button))return;
                    ClearProductRoute();ClearSectionReturn();yardReturnFamily=null;ShowChapterFamily(family);
                });
            }
        }
        void InvalidateYardActions()
        {
            foreach(var button in yardButtons)if(button!=null)InvalidateExpansionConsent(button);
            if(yardActivityButton!=null){InvalidateExpansionConsent(yardActivityButton);yardActivityButton.gameObject.SetActive(false);}
        }
        void ShowYardFamilyActivity(string family)
        {
            if(family!=FamilyIds.PigeonLoft&&family!=FamilyIds.Recycler)return;
            if(yardActivityButton==null)
            {
                yardActivityButton=Button("Yard activity","",panel.transform,new Vector2(.52f,.19f),new Vector2(.93f,.34f),ColorOf("B9CCBB"),()=>{});
                yardActivityButton.targetGraphic=Surface(yardActivityButton.gameObject,ColorOf("263C50"),18,true);
                yardActivityButton.GetComponentInChildren<TMPro.TMP_Text>().color=ColorOf("F2EDDC");
                AddSectionConsent(yardActivityButton);FitChapterLabel(yardActivityButton);
            }
            // ResetJourneyActionLayout has already captured the authored full-width action.
            var select=(RectTransform)selectButton.transform;
            var activity=(RectTransform)yardActivityButton.transform;
            activity.anchorMin=new Vector2(.52f,select.anchorMin.y);activity.anchorMax=new Vector2(.93f,select.anchorMax.y);
            if(selectButton.gameObject.activeSelf){select.anchorMax=new Vector2(.48f,select.anchorMax.y);FitChapterLabel(selectButton);}
            yardActivityButton.GetComponentInChildren<TMPro.TMP_Text>().text=family==FamilyIds.PigeonLoft?
                "Pigeon's Parcels\n"+(Session.Parcels.ActiveRun!=null?(Session.Parcels.ActiveRun.Ready?"Saved result":"Resume free"):
                    Session.Arcade.ActiveRun!=null?"Other round saved":!Session.Parcels.Unlocked?"Requires Loft I":Session.Arcade.Tickets==0?"No Ticket":"Start costs 1 Ticket"):
                "Resource exchange\nInspect batches";
            var session=Session;long sequence=Session.State.sequence;var visible=new UnityEngine.UI.Button.ButtonClickedEvent();yardActivityButton.onClick=visible;
            visible.AddListener(()=>
            {
                if(!ReferenceEquals(yardActivityButton.onClick,visible)||!ReferenceEquals(session,Session)||Session.State.sequence!=sequence||
                    chapterFamily!=family||!SectionAction(yardActivityButton,ChapterFamily))return;
                if(family==FamilyIds.PigeonLoft)ShowParcelsGame();
                else{expansionFilteredCategory=-1;expansionOwnedCategory=-1;ShowExpansionCategory(4);}
                if(wavePanel==ChapterFamily)return;
                yardReturnFamily=family;yardRouteSession=session;yardReturnBoundary=wavePanel;
                closeButton.GetComponentInChildren<TMPro.TMP_Text>().text=BackCaption;
            });
            yardActivityButton.gameObject.SetActive(true);InvalidateExpansionConsent(yardActivityButton);
        }
        bool TryReturnYardActivity()
        {
            if(wavePanel==ArcadePreview&&(parcelsFinishPreview||arcadeAbandonPreview)&&Session?.Parcels.ActiveRun!=null)return false;
            bool boundary=wavePanel==yardReturnBoundary||(yardReturnFamily==FamilyIds.PigeonLoft&&
                (wavePanel==ArcadePreview||wavePanel==ArcadeRound||wavePanel==ArcadeResult));
            if(yardReturnFamily==null||!ReferenceEquals(yardRouteSession,Session)||!boundary||!ProductCanNavigate())return false;
            string family=yardReturnFamily;yardReturnFamily=null;ShowChapterFamily(family,true);return true;
        }
        void ObserveYardRoute()
        {
            if(!panel.activeSelf||wavePanel>=ProductDistrict&&wavePanel<=ProductSettings||!ReferenceEquals(yardRouteSession,Session))
            {yardReturnFamily=null;return;}
            if(wavePanel==ArcadePreview&&(parcelsFinishPreview||arcadeAbandonPreview)&&Session?.Parcels.ActiveRun!=null)return;
            string caption=yardReturnFamily==FamilyIds.PigeonLoft&&(wavePanel==ArcadePreview||wavePanel==ArcadeRound||wavePanel==ArcadeResult)?BackCaption:
                yardReturnFamily==FamilyIds.Recycler&&wavePanel==yardReturnBoundary?BackCaption:null;
            if(caption!=null){var label=closeButton.GetComponentInChildren<TMPro.TMP_Text>();if(label.text!=caption)label.text=caption;}
        }
        void YardRect(int index,float x,float y,float width,float height)
        {
            var rect=yardFamilies[index].rectTransform;
            rect.anchorMin=new Vector2(x-width/2,y);rect.anchorMax=new Vector2(x+width/2,y+height);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
