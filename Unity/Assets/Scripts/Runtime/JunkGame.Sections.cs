using System;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using TMPro;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int SectionHub=20,SectionMap=21,SectionOrdersLocked=22,SectionProgress=23,SectionClearGoal=24,SectionRules=25;
        [SerializeField] UnityEngine.UI.Button sectionsButton;
        [SerializeField] RectTransform sectionsHubRoot,sectionMapRoot;
        [SerializeField] UnityEngine.UI.Button[] sectionButtons=new UnityEngine.UI.Button[7];
        [SerializeField] UnityEngine.UI.Button[] sectionMapNodes=new UnityEngine.UI.Button[24];
        [SerializeField] UnityEngine.UI.Button sectionMapClose,sectionMapEra;
        [SerializeField] TMP_Text sectionMapCount,sectionMapGoal,sectionMapGate;
        readonly bool[] sectionNodeInspectable=new bool[24];
        bool sectionsWired;
        enum SectionReturn { None, Map, Hub, Progress, Construction, Skills, Orders }
        SectionReturn sectionReturnOrigin;
        int sectionReturnPanel;
        string progressNextCaption;
        bool progressNextBorrowed,progressNextInteractable;
        UnityEngine.UI.Button.ButtonClickedEvent progressOrderEvent,progressOrderOriginalEvent;
        string progressPreviousCaption;
        bool progressPreviousBorrowed,progressPreviousInteractable;
        ClearGoalRequest goalClearRequest;
        int rulesPage;
        const int SectionRulePages=5;

        // Add a small entry beside the existing menu; keep the authored Canvas and
        // all existing controls/actions. No data projection or per-frame map query.
        public void CreateSectionsUi()
        {
            if(sectionsWired&&Application.isPlaying)return;
            var menu=(RectTransform)moreProjectsButton.transform;
            var min=menu.anchorMin;var max=menu.anchorMax;float edge=max.x-(max.x-min.x)*.27f;
            if(sectionsButton==null)
            {
                sectionsButton=CreateSectionControl("SectionsButton","All Sections",menu.parent,new Vector2(edge+.015f,min.y),max,ColorOf("B9CCBB"),ShowSections);
                menu.anchorMax=new Vector2(edge,max.y);
            }
            else if(Application.isPlaying)sectionsButton.onClick.AddListener(ShowSections);
            FitChapterLabel(sectionsButton);AddSectionConsent(sectionsButton);
            if(sectionsHubRoot==null)sectionsHubRoot=Area("SectionsHub",panel.transform,new Vector2(.07f,.18f),new Vector2(.93f,.73f));
            if(sectionButtons==null)sectionButtons=new UnityEngine.UI.Button[7];
            else if(sectionButtons.Length!=7)Array.Resize(ref sectionButtons,7);
            string[] labels={"Game","Progress map","Buildings","Skills","Orders","Progress","Rules"};
            for(int i=0;i<7;i++)
            {
                int index=i;float left=i==6?.03f:.03f+i%2*.50f,top=i==6?.24f:.99f-i/2*.25f;
                if(sectionButtons[i]==null)sectionButtons[i]=CreateSectionControl("Section"+i,labels[i],sectionsHubRoot,Vector2.zero,Vector2.one,ColorOf("F4B85C"),()=>OpenSection(index));
                else if(Application.isPlaying)sectionButtons[i].onClick.AddListener(()=>OpenSection(index));
                var rect=(RectTransform)sectionButtons[i].transform;rect.anchorMin=new Vector2(left,top-.23f);rect.anchorMax=new Vector2(i==6?.47f:left+.44f,top);rect.offsetMin=rect.offsetMax=Vector2.zero;
                FitChapterLabel(sectionButtons[i]);AddSectionConsent(sectionButtons[i]);
            }
            if(sectionMapRoot==null)
            {
                sectionMapRoot=Box("ProgressMap",safe,new Vector2(.04f,.10f),new Vector2(.96f,.90f),ColorOf("F2EAD2"));
                sectionMapRoot.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                Label("MAP · TWO ERAS",sectionMapRoot,new Vector2(.04f,.93f),new Vector2(.96f,.985f),18,ColorOf("203A30"));
                sectionMapCount=Label("",sectionMapRoot,new Vector2(.04f,.88f),new Vector2(.96f,.93f),12,ColorOf("203A30"));
                sectionMapGoal=Label("",sectionMapRoot,new Vector2(.04f,.765f),new Vector2(.96f,.88f),11,ColorOf("203A30"));
                Label("Junkyard",sectionMapRoot,new Vector2(.29f,.705f),new Vector2(.61f,.755f),12,ColorOf("203A30"),TextAlignmentOptions.Center);
                Label("Workshop Yard",sectionMapRoot,new Vector2(.64f,.705f),new Vector2(.98f,.755f),11,ColorOf("203A30"),TextAlignmentOptions.Center);
                for(int family=0;family<6;family++)
                {
                    float top=.695f-family*.085f;
                    Label(FamilyName(FamilyIds.All[family]),sectionMapRoot,new Vector2(.025f,top-.08f),new Vector2(.275f,top),11,ColorOf("203A30"));
                    for(int era=0;era<2;era++)for(int level=1;level<=2;level++)
                    {
                        int index=era*12+family*2+level-1;int captured=index;
                        float left=(era==0?.29f:.64f)+(level-1)*.165f;
                        sectionMapNodes[index]=CreateSectionControl("MapNode"+index,"",sectionMapRoot,new Vector2(left,top-.08f),new Vector2(left+.155f,top),ColorOf("B9CCBB"),()=>InspectMapNode(captured));
                        var text=sectionMapNodes[index].GetComponentInChildren<TMP_Text>();text.fontSize=9;text.enableAutoSizing=true;text.fontSizeMin=8;text.fontSizeMax=10;
                    }
                }
                sectionMapGate=Label("",sectionMapRoot,new Vector2(.04f,.115f),new Vector2(.96f,.185f),10,ColorOf("203A30"));
                sectionMapClose=CreateSectionControl("MapClose",BackCaption,sectionMapRoot,new Vector2(.04f,.02f),new Vector2(.47f,.11f),ColorOf("B9CCBB"),CloseMap);
                sectionMapEra=CreateSectionControl("MapEra","About this era",sectionMapRoot,new Vector2(.53f,.02f),new Vector2(.96f,.11f),ColorOf("F4B85C"),InspectMapEra);
            }
            else if(Application.isPlaying)
            {
                for(int i=0;i<24;i++){int index=i;sectionMapNodes[i].onClick.AddListener(()=>InspectMapNode(index));}
                sectionMapClose.onClick.AddListener(CloseMap);sectionMapEra.onClick.AddListener(InspectMapEra);
            }
            foreach(var node in sectionMapNodes)AddSectionConsent(node);
            AddSectionConsent(sectionMapClose);AddSectionConsent(sectionMapEra);
            FitChapterLabel(sectionMapClose);FitChapterLabel(sectionMapEra);StyleProgressMap();
            if(Application.isPlaying){AddSectionConsent(chapterNext);AddSectionConsent(chapterPrevious);}
            CreateDirectNavigationUi();
            sectionsHubRoot.gameObject.SetActive(false);sectionMapRoot.gameObject.SetActive(false);sectionsWired=Application.isPlaying;
        }
        void StyleProgressMap()
        {
            // Match the modal top below the persistent wallets. Reclaim the old
            // header gap so the shorter map retains its node target heights.
            Place(sectionMapRoot,safe,new Vector2(.04f,.10f),new Vector2(.96f,.842f));
            Surface(sectionMapRoot.gameObject,ColorOf("F2EDDC"),20,true);
            // The title row is taller to match the other screens' headings; the count and goal give way.
            Place(sectionMapCount.rectTransform,sectionMapRoot,new Vector2(.04f,.893f),new Vector2(.96f,.933f));
            Place(sectionMapGoal.rectTransform,sectionMapRoot,new Vector2(.04f,.80f),new Vector2(.96f,.888f));
            sectionMapGoal.enableAutoSizing=true;sectionMapGoal.fontSizeMin=10;sectionMapGoal.fontSizeMax=12;sectionMapGoal.fontSize=12;
            InnerNavContext(sectionMapGoal);
            foreach(var label in sectionMapRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.transform.parent!=sectionMapRoot||label==sectionMapGoal)continue;
                label.color=ColorOf("142638");label.raycastTarget=false;
                if(string.Equals(label.text,"MAP · TWO ERAS",StringComparison.OrdinalIgnoreCase))
                {
                    // Same heading as the other screens: sentence case, size and left edges.
                    label.text="Map · two eras";
                    Place(label.rectTransform,sectionMapRoot,new Vector2(.236f,.935f),new Vector2(.96f,.99f));
                    SharedHeadingText(label);
                    StyleCHeading(label,"map",.08f,.20f,.236f);
                }
                else if(label.text=="Junkyard"||label.text=="Workshop Yard")
                {
                    float left=label.text=="Junkyard"?.25f:.625f;
                    Place(label.rectTransform,sectionMapRoot,new Vector2(left,.755f),new Vector2(left+.34f,.795f));
                    label.fontStyle=FontStyles.Bold;label.enableAutoSizing=true;label.fontSizeMin=10;label.fontSizeMax=12;label.fontSize=12;
                }
                else for(int family=0;family<6;family++)
                {
                    if(label.text!=FamilyName(FamilyIds.All[family])&&!(family==5&&label.text=="Power"))continue;
                    float top=.745f-family*.092f;
                    Place(label.rectTransform,sectionMapRoot,new Vector2(.025f,top-.087f),new Vector2(.225f,top));
                    label.fontStyle=FontStyles.Bold;label.enableAutoSizing=true;label.fontSizeMin=10;label.fontSizeMax=11;label.fontSize=11;
                    break;
                }
            }
            for(int i=0;i<sectionMapNodes.Length;i++)
            {
                int family=(i%12)/2,level=i%2;float left=(i<12?.25f:.625f)+level*.175f,top=.745f-family*.092f;
                var node=sectionMapNodes[i];
                Place((RectTransform)node.transform,sectionMapRoot,new Vector2(left,top-.087f),new Vector2(left+.165f,top));
                node.targetGraphic=Surface(node.gameObject,ColorOf("D6D8D4"),6,true);
                var label=node.GetComponentInChildren<TMP_Text>(true);
                label.color=ColorOf("142638");label.enableAutoSizing=true;label.fontSizeMin=8;label.fontSizeMax=11;label.fontSize=11;
                label.textWrappingMode=TextWrappingModes.Normal;label.raycastTarget=false;
            }
            InnerButton(sectionMapClose,false,false);InnerButton(sectionMapEra,true,false);
            sectionMapClose.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        UnityEngine.UI.Button CreateSectionControl(string name,string label,Transform parent,Vector2 min,Vector2 max,Color color,UnityEngine.Events.UnityAction action)
        {
            var button=Button(name,label,parent,min,max,color,action);
            // Stopped-Editor authoring serializes references/layout, not transient
            // delegates. Awake binds each existing control once when Play starts.
            if(!Application.isPlaying)button.onClick.RemoveListener(action);
            return button;
        }
        static void AddSectionConsent(UnityEngine.UI.Button button)
        { if(!button.TryGetComponent<JtnPanelPointerConsent>(out _))button.gameObject.AddComponent<JtnPanelPointerConsent>(); }
        bool SectionAction(UnityEngine.UI.Button button,int mode)
        { return !Busy&&!failed&&!InBackground&&Session?.State!=null&&!Session.RecoveryRequired&&wavePanel==mode&&PanelActionAvailable(button)&&PointerConsentAllows(button); }
        void ShowSections()
        {
            if(!CanOpenChapterPanel()||panel.activeSelf||!sectionsButton.isActiveAndEnabled||!sectionsButton.IsInteractable()||!PointerConsentAllows(sectionsButton))return;
            if(ShowChapterTransition())return;
            ShowSectionsHub();
        }
        void ShowSectionsHub()
        {BeginChapterPanel(SectionHub,"All Sections");ShowNavigationHubContext();sectionsHubRoot.gameObject.SetActive(true);sectionsButton.interactable=false;closeButton.GetComponentInChildren<TMP_Text>().text=BackCaption;RenderExpansionVisibility(!Busy&&!failed&&!InBackground);RenderArcadeVisibility(!Busy&&!failed&&!InBackground);StyleCSectionsHub();}
        void OpenSection(int index)
        {
            if(index<0||index>=7||!SectionAction(sectionButtons[index],SectionHub))return;
            switch(index)
            {
                case 0:ClearSectionReturn();ClosePanel();break;
                case 1:ShowProgressMap();break;
                case 2:ShowDirectConstruction();break;
                case 3:ShowDirectSkills();break;
                case 5:ShowSectionProgress();break;
                case 6:ShowSectionRules();break;
                default:
                    ShowDirectOrders();
                    break;
            }
        }
        void ShowSectionRules(int page=0)
        {
            BeginChapterPanel(SectionRules,$"Rules · {page+1}/{SectionRulePages}");shownGuide=null;rulesPage=page;
            switch(page)
            {
                case 0:panelBody.text=$"<b>Free spin: 3 symbols.</b>\n\nBase Coins per outcome:\nall different: {EconomyRules.DifferentReward}; pair: {EconomyRules.PairReward}; triple: {EconomyRules.TripleReward}.\n\nBase rewards do not stack.\nBuilding and skill bonuses are added.";break;
                case 1:panelBody.text="Inspect for free; choose separately.\nChoose one building.\nEnough Coins: buy immediately.\nOtherwise, pays on a spin when funded.\nSelected goals keep their price.\nClear goal: free, no refund.\nNew selections use current prices.";break;
                case 2:panelBody.text=$"Repair House to unlock AUTO.\nAUTO pauses when your goal is built.\nIt stops in menus and background.\nNo spins in background.\nWith Workshop: 1 Coin per full minute.\nUp to {EconomyRules.OfflineCapSeconds/EconomyRules.OfflineSecondsPerCoin} per absence ({EconomyRules.OfflineCapSeconds/3600}h).\nIncome is credited automatically\non return.";break;
                case 3:panelBody.text="Two eras: 24 paid building stages.\nSecond era entry is free: 12/24.\nSkills and orders are optional.\nProgress stays on this device.\nRestoring a backup may\nroll back newer actions.";break;
                default:panelBody.text=ExpansionCopy.FirstUse+"\n\nInstall modules in sockets. Equipment upgrades its own building. Unlock the bonus reel in the shop.";break;
            }
            RememberSectionReturn(SectionReturn.Hub,SectionRules);
            BorrowProgressControl(chapterPrevious,page>0,"Previous",ref progressPreviousBorrowed,ref progressPreviousCaption,ref progressPreviousInteractable);
            BorrowProgressControl(chapterNext,page<SectionRulePages-1,"Next",ref progressNextBorrowed,ref progressNextCaption,ref progressNextInteractable);
        }
        bool PageSectionRules(int delta)
        {
            if(wavePanel!=SectionRules)return false;
            var button=delta<0?chapterPrevious:chapterNext;
            if(SectionAction(button,SectionRules)&&rulesPage+delta>=0&&rulesPage+delta<SectionRulePages)ShowSectionRules(rulesPage+delta);
            return true; // Consume this mode even for a hidden, stale or boundary callback.
        }
        void ShowSectionProgress()
        {
            var session=Session;if(session==null||session.RecoveryRequired)return;
            var state=session.State;if(state==null||state.pending!=null||session.RecoveryRequired||!ReferenceEquals(Session,session))return;
            BeginChapterPanel(SectionProgress,"Progress");shownGuide=null;
            var chapter=ChapterEconomy.Project(state);var journey=JourneyEconomy.Project(state);
            var snapshot=ChapterMilestones.Project(chapter);var optional=Core.OptionalAchievements.Project(chapter,journey);var order=journey.Orders;
            panelBody.text=MilestoneFacts(snapshot,chapter,optional)+"\n\n<b>"+MilestoneAdvice(snapshot,state,chapter)+"</b>";
            RememberSectionReturn(SectionReturn.Hub,SectionProgress);
            SetProgressGoalAction(snapshot.SelectedProjectId!=null,snapshot.NextAction);
            if(snapshot.SelectedProjectId==null&&ChapterCatalog.Rules.IsChapterComplete(chapter.Progress)&&order.ActiveOrderId!=null)
            {
                var definition=JunkToNeon.Orders.OrderCatalog.Get(order.ActiveOrderId);
                panelBody.text+="\nAccepted optional order: "+NavigationOrderTitle(order.ActiveOrderId)+$" · {order.Progress}/{definition.Target}."+
                    (order.Ready?$" Ready · {definition.RewardCoins} Coins unclaimed.":" Keep spinning manually.");
                BindProgressOrder(state.sequence,order.InstanceSequence,order.ActiveOrderId);
            }
        }
        void SetProgressGoalAction(bool visible,MilestoneNextAction action=MilestoneNextAction.FinishOrContinueOptionally)
        {
            ClearProgressOrderAction();
            string caption=visible?"Inspect goal":action==MilestoneNextAction.InspectHouse?"Inspect house":action==MilestoneNextAction.EnterWorkshopYard?"Inspect era entry":"Choose goal";
            bool preview=action==MilestoneNextAction.InspectHouse||action==MilestoneNextAction.EnterWorkshopYard||action==MilestoneNextAction.ChooseConstruction;
            BorrowProgressControl(chapterNext,visible||preview,caption,ref progressNextBorrowed,ref progressNextCaption,ref progressNextInteractable);
            BorrowProgressControl(chapterPrevious,visible,"Clear goal",ref progressPreviousBorrowed,ref progressPreviousCaption,ref progressPreviousInteractable);
        }
        void ClearProgressOrderAction()
        {
            if(progressOrderEvent!=null&&chapterNext!=null&&ReferenceEquals(chapterNext.onClick,progressOrderEvent))chapterNext.onClick=progressOrderOriginalEvent;
            progressOrderEvent=null;progressOrderOriginalEvent=null;
        }
        void BindProgressOrder(long sequence,long instance,string id)
        {
            BorrowProgressControl(chapterNext,true,"Open orders",ref progressNextBorrowed,ref progressNextCaption,ref progressNextInteractable);
            var session=Session;var original=chapterNext.onClick;var visible=new UnityEngine.UI.Button.ButtonClickedEvent();
            progressOrderOriginalEvent=original;progressOrderEvent=visible;
            visible.AddListener(()=>
            {
                if(!ReferenceEquals(chapterNext.onClick,visible)||!ReferenceEquals(Session,session)||!SectionAction(chapterNext,SectionProgress))return;
                var current=Session.State;if(current.sequence!=sequence)return;
                var active=JourneyEconomy.Project(current).Orders;
                if(active.InstanceSequence!=instance||active.ActiveOrderId!=id)return;
                ProductRoute(ShowDirectOrders,ProductEvents);
            });
            chapterNext.onClick=visible;
        }
        static void BorrowProgressControl(UnityEngine.UI.Button button,bool visible,string caption,ref bool borrowed,ref string originalCaption,ref bool originalInteractable)
        {
            if(button==null){borrowed=false;originalCaption=null;return;}
            var label=button.GetComponentInChildren<TMP_Text>(true);
            if(visible)
            {
                if(!borrowed){originalCaption=label==null?null:label.text;originalInteractable=button.interactable;borrowed=true;}
                if(label!=null)label.text=caption;button.interactable=true;button.gameObject.SetActive(true);
            }
            else if(borrowed)
            {
                if(label!=null)label.text=originalCaption;button.interactable=originalInteractable;button.gameObject.SetActive(false);borrowed=false;originalCaption=null;
            }
        }
        bool OpenProgressGoal()
        {
            if(wavePanel!=SectionProgress)return false;
            if(!SectionAction(chapterNext,SectionProgress))return true;
            var snapshot=Session.Milestones;string selected=snapshot.SelectedProjectId;
            if(selected==null)
            {
                switch(snapshot.NextAction)
                {
                    case MilestoneNextAction.ChooseConstruction:ShowDirectConstruction();break;
                    case MilestoneNextAction.InspectHouse:
                        ShowChapterFamily(FamilyIds.House,true);
                        if(wavePanel==ChapterFamily)RememberSectionReturn(SectionReturn.Progress,ChapterFamily);
                        break;
                    case MilestoneNextAction.EnterWorkshopYard:
                        ShowChapterEra();RememberSectionReturn(SectionReturn.Progress,ChapterEra);break;
                }
                return true;
            }
            if(!ChapterCatalog.Rules.TryGetUpgrade(selected,out var upgrade))return true;
            var stage=ChapterCatalog.Rules.Definition.Eras.SelectMany(e=>e.Catalog.Stages).First(s=>s.Id==upgrade.ToStageId);
            ShowChapterFamily(stage.FamilyId,true);
            if(chapterProject==selected)RememberSectionReturn(SectionReturn.Progress,ChapterFamily);
            return true;
        }
        bool OpenProgressClearGoal()
        {
            if(wavePanel!=SectionProgress)return false;
            if(!SectionAction(chapterPrevious,SectionProgress))return true;
            try
            {
                // Capture before Begin/Hide invalidates every transient confirmation.
                var request=Session.QuoteClearGoal();long wallet=Session.State.coins;
                BeginChapterPanel(SectionClearGoal,"Clear goal?");goalClearRequest=request;
                panelBody.text=$"Clear goal: {ProjectName(request.ProjectId)}?\nCost: 0. Coins remain {wallet}; no refund.\nSaved price {request.SavedPrice} expires. Selecting again uses the current price ({ProjectPrice(request.ProjectId)}). Buildings, skills and carryover remain.";
                RememberSectionReturn(SectionReturn.Progress,SectionClearGoal);
                selectButton.GetComponentInChildren<TMP_Text>(true).text="Clear goal";selectButton.gameObject.SetActive(true);
            }
            catch(Exception error){FinishGoalClearFailure(error);}
            return true;
        }
        bool SelectSectionGoalClear()
        {
            if(wavePanel!=SectionClearGoal)return false;
            if(!CanOpenChapterPanel()||!PanelActionAvailable(selectButton))return true;
            var request=goalClearRequest; // Hide invalidates the field; only this immutable local can commit.
            HideWaveChoices();
            if(request==null){panel.SetActive(false);Render("Goal changed. Reopen Progress.");return true;}
            try
            {
                var before=Session.State;var after=Session.ClearGoal(request,UtcSeconds());ObserveCommitted(before,after);
                panel.SetActive(false);Render("Goal cleared. Coins unchanged. Choose the next goal yourself.");
            }
            catch(Exception error){FinishGoalClearFailure(error);}
            return true;
        }
        void FinishGoalClearFailure(Exception error)
        {
            if(Session?.RecoveryRequired==true){ObserveOperationFailure();Error(error.Message);}
            else{HideWaveChoices();panel.SetActive(false);Render("Goal changed. Reopen Progress.");}
        }
        void ShowProgressMap()
        {
            BeginChapterPanel(SectionMap,"Progress map");
            var chapter=Session.Chapter;var state=Session.State;var snapshot=Session.Milestones;
            var owned=chapter.Progress.CompletedUpgradeIds;var available=ChapterCatalog.Rules.AvailableUpgrades(chapter.Progress);
            sectionMapCount.text=$"Paid buildings: {snapshot.ConstructionCount}/24 · skills optional";
            sectionMapGoal.text=chapter.SelectedProjectId==null?"No goal selected. Inspection selects and buys nothing.":
                $"Goal: {ProjectName(chapter.SelectedProjectId)} · {state.coins}/{ChapterEconomy.SelectedProjectPrice(chapter)} Coins at its saved price.\nNew selection price: {ProjectPrice(chapter.SelectedProjectId)} Coins. Reselect explicitly.";
            for(int i=0;i<24;i++)
            {
                string era=i<12?EraIds.Junkyard:EraIds.WorkshopYard;int family=(i%12)/2,level=i%2+1;
                string id=ChapterIds.Project(era,FamilyIds.All[family],level);bool done=owned.Contains(id),open=available.Contains(id),selected=chapter.SelectedProjectId==id;
                long price=ProjectPrice(id);
                string status=done?"Done":open?"Available":"Locked\n"+MapLockReason(chapter,era,FamilyIds.All[family],id);
                sectionMapNodes[i].GetComponentInChildren<TMP_Text>().text=(level==1?"I":"II")+"\n"+status+
                    (open?"\n"+(selected?"New "+price:price+(state.coins>=price?" · funded":" · saving")):"")+(selected?"\nGoal "+ChapterEconomy.SelectedProjectPrice(chapter):"");
                sectionNodeInspectable[i]=open;sectionMapNodes[i].interactable=open;
                StyleMapNode(sectionMapNodes[i],done,open,selected);
            }
            foreach(var label in sectionMapRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.text=="Junkyard"){label.color=ColorOf("456B54");label.fontStyle=FontStyles.Bold;}
                else if(label.text=="Workshop Yard"){label.color=ColorOf("6D567C");label.fontStyle=FontStyles.Bold;}
            }
            bool complete=snapshot.ConstructionCount==24;
            sectionMapGate.text=complete?"24/24: you may finish. Orders and skills are optional. No further era here.":
                chapter.Progress.EraId==EraIds.Junkyard?"I → II for each building. Available ≠ funded.\nAll 12 Junkyard stages → free era entry; progress stays 12/24.":
                "Junkyard complete; free entry added no purchases.\nWorkshop Yard: I → II per building. Available ≠ funded.";
            sectionMapRoot.SetAsLastSibling();sectionMapRoot.gameObject.SetActive(true);
            RememberSectionReturn(SectionReturn.Hub,SectionMap);
        }
        static void StyleMapNode(UnityEngine.UI.Button node,bool owned,bool available,bool selected)
        {
            // Disabled owned and locked nodes retain different fills. Labels and
            // availability remain authoritative; styling never changes an action.
            node.targetGraphic.color=ColorOf(selected?"F4D39C":owned?"B9D6C2":available?"F2EAD2":"D6D8D4");
            var colors=node.colors;
            colors.normalColor=colors.disabledColor=Color.white;
            colors.highlightedColor=new Color(.86f,.93f,1,1);colors.selectedColor=new Color(.80f,.88f,1,1);
            colors.pressedColor=new Color(.72f,.80f,.90f,1);colors.colorMultiplier=1;
            node.colors=colors;
            var outline=node.targetGraphic.GetComponent<UnityEngine.UI.Outline>();
            if((selected||available)&&outline==null)outline=node.targetGraphic.gameObject.AddComponent<UnityEngine.UI.Outline>();
            if(outline!=null)
            {
                outline.effectColor=ColorOf(selected?"795019":"84928A");
                outline.effectDistance=selected?new Vector2(2,-2):new Vector2(1,-1);outline.enabled=selected||available;
            }
        }
        static string MapLockReason(ChapterState chapter,string era,string family,string project)
        {
            // Availability first selects the current era, then requires every owned
            // prerequisite. Show that same gate; wallet is not a locking condition.
            if(era!=chapter.Progress.EraId)return "Era entry";
            ChapterCatalog.Rules.TryGetUpgrade(project,out var upgrade);
            return string.Join("\n",upgrade.Prerequisites.Where(p=>!chapter.Progress.CompletedUpgradeIds.Contains(p))
                .Select(p=>p==ChapterIds.Project(era,family,1)?"Requires I":p==GameIds.HouseRepair?"House repair":p));
        }
        void InspectMapNode(int index)
        {
            if(index<0||index>=24||!SectionAction(sectionMapNodes[index],SectionMap))return;
            string era=index<12?EraIds.Junkyard:EraIds.WorkshopYard;string family=FamilyIds.All[(index%12)/2];
            string id=ChapterIds.Project(era,family,index%2+1);
            if(!ChapterCatalog.Rules.AvailableUpgrades(Session.Chapter.Progress).Contains(id))return;
            ShowChapterFamily(family,true); // Existing preview/explicit purchase/reprice path only.
            RememberSectionReturn(SectionReturn.Map,ChapterFamily);
        }
        void CloseMap(){if(SectionAction(sectionMapClose,SectionMap))ShowSectionsHub();}
        void InspectMapEra()
        {
            if(!SectionAction(sectionMapEra,SectionMap))return;
            if(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress))
            {BeginChapterPanel(JourneyGuide,"Chapter complete");shownGuide=null;panelBody.text=ChapterEndMilestones();RememberSectionReturn(SectionReturn.Map,JourneyGuide);}
            else {ShowChapterEra();RememberSectionReturn(SectionReturn.Map,ChapterEra);} // Offer only, no entry/ack.
        }
        void RememberSectionReturn(SectionReturn origin,int expectedPanel)
        {
            if(!CanOpenChapterPanel()||wavePanel!=expectedPanel||Session.Chapter.TransitionPending)return;
            sectionReturnOrigin=origin;sectionReturnPanel=expectedPanel;
            closeButton.GetComponentInChildren<TMP_Text>().text=BackCaption;
        }
        void ClearSectionReturn()
        {
            if(sectionReturnOrigin!=SectionReturn.None&&closeButton!=null)closeButton.GetComponentInChildren<TMP_Text>().text="Close";
            sectionReturnOrigin=SectionReturn.None;sectionReturnPanel=0;
        }
        bool TryReturnSection()
        {
            var origin=sectionReturnOrigin;
            bool valid=origin!=SectionReturn.None&&CanOpenChapterPanel()&&wavePanel==sectionReturnPanel&&
                !Session.Chapter.TransitionPending&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel;
            ClearSectionReturn();
            if(!valid)return false;
            if(origin==SectionReturn.Map)ShowProgressMap();else if(origin==SectionReturn.Progress)ShowSectionProgress();
            else if(origin==SectionReturn.Construction)ShowDirectConstruction();else if(origin==SectionReturn.Skills)ShowDirectSkills();
            else if(origin==SectionReturn.Orders)ShowDirectOrders();else ShowSectionsHub();
            return true;
        }
        void InvalidateSections()
        {
            InvalidateYardActions();
            InvalidateProductUi();
            InvalidateArcadeUi();
            InvalidateExpansionUi();
            InvalidateDirectNavigation();
            ClearSectionReturn();SetProgressGoalAction(false);rulesPage=0;
            if(goalClearRequest!=null&&wavePanel==SectionClearGoal){selectButton.gameObject.SetActive(false);panelBody.text="Confirmation closed. Reopen Clear goal.";}
            goalClearRequest=null;
            if(chapterPrevious!=null&&chapterPrevious.TryGetComponent<JtnPanelPointerConsent>(out var previous))previous.Invalidate();
            if(chapterNext!=null&&chapterNext.TryGetComponent<JtnPanelPointerConsent>(out var next))next.Invalidate();
            if(sectionsButton!=null)
            {sectionsButton.interactable=false;if(sectionsButton.TryGetComponent<JtnPanelPointerConsent>(out var entry))entry.Invalidate();}
            if(sectionsHubRoot!=null)sectionsHubRoot.gameObject.SetActive(false);
            if(sectionMapRoot!=null)sectionMapRoot.gameObject.SetActive(false);
        }
        void RenderSections(ModelState state)
        {
            if(!sectionsWired)return;
            bool usable=!Busy&&!failed&&!InBackground;
            if(wavePanel==SectionProgress)
            {
                bool selected=state.chapter!=null?state.chapter.SelectedProjectId!=null:state.selectedStage!=0;
                // Healthy layout renders retain the explicitly opened no-goal action; invalidation clears it.
                if(!usable||!panel.activeSelf||selected)SetProgressGoalAction(usable&&panel.activeSelf&&selected);
            }
            sectionsButton.interactable=usable&&!panel.activeSelf&&!(state.chapter?.TransitionPending??false);
            bool hub=panel.activeSelf&&wavePanel==SectionHub&&!failed&&!Busy;
            bool map=panel.activeSelf&&wavePanel==SectionMap&&!failed&&!Busy;
            sectionsHubRoot.gameObject.SetActive(hub);sectionMapRoot.gameObject.SetActive(map);
            foreach(var button in sectionButtons)button.interactable=usable;
            for(int i=0;i<24;i++)sectionMapNodes[i].interactable=usable&&sectionNodeInspectable[i];
            sectionMapClose.interactable=sectionMapEra.interactable=usable;
            RenderDirectNavigation(usable);
            RenderExpansionVisibility(usable);
            RenderArcadeVisibility(usable);
        }
    }
}
