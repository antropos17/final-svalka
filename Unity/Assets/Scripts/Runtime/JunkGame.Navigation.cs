using System;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;
using TMPro;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int NavigationConstruction=30,NavigationSkills=31,NavigationOrders=32;
        [SerializeField] RectTransform navigationRoot;
        [SerializeField] TMP_Text navigationContext,navigationHubContext;
        [SerializeField] UnityEngine.UI.Button navigationEraButton;
        [SerializeField] UnityEngine.UI.Button[] navigationCards=new UnityEngine.UI.Button[6];
        readonly bool[] navigationCardAvailable=new bool[6];
        readonly string[] navigationOrderIds=new string[6];
        bool navigationWired,navigationEraAvailable,navigationContextValid;

        // Ordinary authored uGUI objects, with a fallback for a pre-navigation scene.
        // Creation is idempotent; state projections happen only on explicit opens.
        public void CreateDirectNavigationUi()
        {
            if(navigationWired&&Application.isPlaying)return;
            if(navigationRoot==null)navigationRoot=Area("DirectNavigation",panel.transform,new Vector2(.06f,.17f),new Vector2(.94f,.84f));
            if(navigationContext==null)navigationContext=NavigationLabel("Context",navigationRoot,new Vector2(0,.80f),Vector2.one);
            if(navigationHubContext==null)navigationHubContext=NavigationLabel("HubContext",panel.transform,new Vector2(.07f,.73f),new Vector2(.93f,.80f));
            InnerNavContext(navigationContext);InnerNavContext(navigationHubContext);
            if(navigationCards==null)navigationCards=new UnityEngine.UI.Button[6];
            else if(navigationCards.Length!=6)Array.Resize(ref navigationCards,6);
            for(int i=0;i<6;i++)
            {
                int index=i;float left=i%2*.52f,top=.77f-i/2*.20f;
                if(navigationCards[i]==null)
                    navigationCards[i]=CreateSectionControl("NavigationCard"+i,"",navigationRoot,new Vector2(left,top-.18f),new Vector2(left+.48f,top),ColorOf("263C50"),()=>OpenNavigationCard(index));
                else if(Application.isPlaying)navigationCards[i].onClick.AddListener(()=>OpenNavigationCard(index));
                AddSectionConsent(navigationCards[i]);
                var label=navigationCards[i].GetComponentInChildren<TMP_Text>(true);
                label.enableAutoSizing=true;label.fontSizeMin=10;label.fontSizeMax=13;label.fontSize=13;
                label.raycastTarget=false;
                InnerNavCard(navigationCards[i],label);
            }
            if(navigationEraButton==null)navigationEraButton=CreateSectionControl("NavigationEra","Era · inspect",navigationRoot,new Vector2(0,0),new Vector2(1,.15f),ColorOf("F4B85C"),InspectNavigationEra);
            else if(Application.isPlaying)navigationEraButton.onClick.AddListener(InspectNavigationEra);
            AddSectionConsent(navigationEraButton);FitChapterLabel(navigationEraButton);
            InnerNavEra();LayoutDirectNavigation(NavigationConstruction);
            navigationRoot.gameObject.SetActive(false);InnerNavContextVisible(navigationHubContext,false);
            navigationWired=Application.isPlaying;
        }
        void LayoutDirectNavigation(int mode)
        {
            // Apply the same grid to existing authored objects and fallback UI.
            // Skills and Orders use the space otherwise reserved for the era action.
            Place(navigationRoot,panel.transform,new Vector2(.06f,.17f),new Vector2(.94f,.875f));
            bool orders=mode==NavigationOrders,skills=mode==NavigationSkills;
            float contextBottom=orders?.80f:.84f;
            Place(navigationContext.rectTransform,navigationRoot,new Vector2(0,contextBottom),Vector2.one);
            InnerNavContext(navigationContext);
            float top=orders?.77f:.81f,step=skills?.265f:orders?.20f:.22f,height=skills?.245f:orders?.18f:.205f;
            for(int i=0;i<navigationCards.Length;i++)
            {
                float left=i%2*.52f,rowTop=top-i/2*step;
                var rect=(RectTransform)navigationCards[i].transform;
                if(orders&&i<4)
                {
                    // Four order descriptions need room for wrapped titles and
                    // retained reward receipts; no era action occupies this space.
                    rowTop=.77f-i/2*.315f;
                    Place(rect,navigationRoot,new Vector2(left,rowTop-.295f),new Vector2(left+.48f,rowTop));
                }
                else if(orders&&i==4)
                    Place(rect,navigationRoot,Vector2.zero,new Vector2(1,.14f));
                else if(orders)
                    // The unused sixth card stays clear of every visible target.
                    Place(rect,navigationRoot,new Vector2(.52f,.78f),new Vector2(1,.79f));
                else
                    Place(rect,navigationRoot,new Vector2(left,rowTop-height),new Vector2(left+.48f,rowTop));
                var label=navigationCards[i].GetComponentInChildren<TMP_Text>(true);
                bool orderDescription=orders&&i<4;
                Place(label.rectTransform,rect,new Vector2(.08f,orderDescription?.04f:.09f),new Vector2(.95f,orderDescription?.96f:.89f));
                label.lineSpacing=orderDescription?0:3;
            }
            Place((RectTransform)navigationEraButton.transform,navigationRoot,Vector2.zero,new Vector2(1,.15f));
        }
        TMP_Text NavigationLabel(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var label=Label("",parent,min,max,12,ColorOf("F6F1E3"));label.name=name;
            label.enableAutoSizing=true;label.fontSizeMin=10;label.fontSizeMax=12;label.raycastTarget=false;
            return label;
        }
        RectTransform InnerNavDecoration(string name,Transform parent,Vector2 min,Vector2 max,Color color,float radius)
        {
            var rect=parent.Find(name) as RectTransform;
            if(rect==null)rect=Area(name,parent,min,max);
            Place(rect,parent,min,max);
            Surface(rect.gameObject,color,radius).raycastTarget=false;
            return rect;
        }
        void InnerNavContext(TMP_Text label)
        {
            var backdrop=InnerNavDecoration(label.name+" Inner Navigation Context Surface",label.transform.parent,label.rectTransform.anchorMin,label.rectTransform.anchorMax,ColorOf("E6E1CF"),12);
            int labelIndex=label.transform.GetSiblingIndex();
            backdrop.SetSiblingIndex(backdrop.GetSiblingIndex()<labelIndex?labelIndex-1:labelIndex);
            label.color=ColorOf("263C50");label.alignment=TextAlignmentOptions.MidlineLeft;
            label.margin=new Vector4(12,6,12,6);label.raycastTarget=false;
        }
        void InnerNavContextVisible(TMP_Text label,bool visible)
        {
            label.gameObject.SetActive(visible);
            var backdrop=label.transform.parent.Find(label.name+" Inner Navigation Context Surface");
            if(backdrop!=null)backdrop.gameObject.SetActive(visible);
        }
        void InnerNavCard(UnityEngine.UI.Button button,TMP_Text label)
        {
            button.targetGraphic=Surface(button.gameObject,ColorOf("263C50"),14,true);
            InnerNavDecoration("Inner Navigation Status",button.transform,new Vector2(.025f,.12f),new Vector2(.04f,.88f),ColorOf("C8D8DB"),3);
            Place(label.rectTransform,button.transform,new Vector2(.08f,.09f),new Vector2(.95f,.89f));
            // Short order texts sit in the middle of the card instead of leaving its lower half empty.
            label.color=ColorOf("F6F1E3");label.alignment=TextAlignmentOptions.Left;
            label.lineSpacing=3;label.margin=Vector4.zero;label.raycastTarget=false;
            var colors=button.colors;colors.normalColor=Color.white;
            colors.highlightedColor=new Color(.90f,.96f,1);colors.pressedColor=new Color(.72f,.84f,.91f);
            colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.78f,.83f,.87f,1);button.colors=colors;
            StyleCInnerCard(button);
        }
        void InnerNavEra()
        {
            navigationEraButton.targetGraphic=Surface(navigationEraButton.gameObject,ColorOf("F4B85C"),14,true);
            StyleCInnerAction(navigationEraButton,true);
            var label=navigationEraButton.GetComponentInChildren<TMP_Text>(true);
            Place(label.rectTransform,navigationEraButton.transform,new Vector2(.06f,.08f),new Vector2(.94f,.92f));
            label.color=ColorOf("142638");label.fontStyle=FontStyles.Bold;label.raycastTarget=false;
            var colors=navigationEraButton.colors;colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1,.96f,.88f);colors.pressedColor=new Color(.88f,.78f,.61f);
            colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.80f,.80f,.76f,1);navigationEraButton.colors=colors;
        }
        string InnerNavCaption(string caption,bool focused=false)
        {
            int firstLine=caption.IndexOf('\n');
            string titleColor=focused?"795019":"F8D986";
            return firstLine<0?"<b><color=#"+titleColor+">"+caption+"</color></b>":
                "<b><color=#"+titleColor+">"+caption.Substring(0,firstLine)+"</color></b>\n"+caption.Substring(firstLine+1);
        }
        string NavigationContextText()
        {
            var c=Session.Chapter;long coins=Session.State.coins;
            string goal=c.SelectedProjectId==null?"No goal selected":$"Goal: {ProjectName(c.SelectedProjectId)} · {coins}/{ChapterEconomy.SelectedProjectPrice(c)} Coins";
            return $"Wallet: {coins} Coins · {c.Progress.CompletedUpgradeIds.Count}/24 built\n"+goal;
        }
        void ShowNavigationHubContext()
        {
            navigationHubContext.text=NavigationContextText();InnerNavContextVisible(navigationHubContext,true);
            panelBody.enabled=false;
        }
        void BeginDirectNavigation(int mode,string title)
        {
            BeginChapterPanel(mode,title);shownGuide=null;LayoutDirectNavigation(mode);
            navigationContextValid=true;
            navigationContext.text=NavigationContextText();panelBody.enabled=false;
            navigationEraAvailable=false;navigationEraButton.gameObject.SetActive(false);
            for(int i=0;i<6;i++){navigationCardAvailable[i]=false;navigationOrderIds[i]=null;navigationCards[i].gameObject.SetActive(false);}
            navigationRoot.SetAsLastSibling();navigationRoot.gameObject.SetActive(true);
            RememberSectionReturn(SectionReturn.Hub,mode);
        }
        void SetNavigationCard(int index,string caption,bool inspectable,Color color,bool focused=false)
        {
            var button=navigationCards[index];var label=button.GetComponentInChildren<TMP_Text>(true);
            label.text=InnerNavCaption(caption,focused);label.color=ColorOf(focused?"142638":"F6F1E3");
            button.targetGraphic.color=ColorOf(focused?"F2EDDC":"263C50");
            StyleCInnerCard(button,focused);
            // The flat focused cream surface shares the panel fill; retain a visible card edge.
            // The cream artwork draws its own edge.
            var surface=(JtnProductSurface)button.targetGraphic;bool edge=focused&&surface.sprite==null;
            var outline=surface.GetComponent<UnityEngine.UI.Outline>();
            if(edge&&outline==null)outline=surface.gameObject.AddComponent<UnityEngine.UI.Outline>();
            if(outline!=null)
            {
                outline.effectColor=ColorOf("795019");outline.effectDistance=new Vector2(1,-1);outline.enabled=edge;
            }
            button.transform.Find("Inner Navigation Status").GetComponent<JtnProductSurface>().color=color;
            navigationCardAvailable[index]=inspectable;button.interactable=inspectable;button.gameObject.SetActive(true);
            StyleCNavigationArtwork(index);
        }
        void ShowDirectConstruction()
        {
            if(!CanOpenChapterPanel()||Session.RecoveryRequired)return;
            if(ShowChapterTransition())return;
            BeginDirectNavigation(NavigationConstruction,"Buildings · all six");
            navigationContext.text+="\nTap a card to inspect.";
            var c=Session.Chapter;long coins=Session.State.coins;
            var available=ChapterCatalog.Rules.AvailableUpgrades(c.Progress);
            for(int i=0;i<6;i++)
            {
                string family=FamilyIds.All[i];int level=ChapterCatalog.Rules.FamilyStage(c.Progress,family).Stage.Level;
                string status;bool selected=false;
                if(level==2)status="Complete · inspect";
                else
                {
                    string id=ChapterIds.Project(c.Progress.EraId,family,level+1);long offer=ProjectPrice(id);
                    selected=c.SelectedProjectId==id;
                    if(!available.Contains(id))status="Locked · repair house";
                    else if(selected)
                    {
                        long saved=ChapterEconomy.SelectedProjectPrice(c);
                        status=$"Saved {saved} · "+(coins>=saved?"funded":$"need {saved-coins}");
                        if(saved!=offer)status+=$"\nReselect: {offer}";
                    }
                    else status=$"Available · {offer}\n"+(coins>=offer?"Enough Coins":$"Need {offer-coins} Coins");
                }
                SetNavigationCard(i,$"{FamilyName(family)} · {level}/2\n{status}",true,ColorOf(level==2?"86E2E5":selected?"F4B85C":"C8D8DB"),selected);
            }
            navigationEraAvailable=true;navigationEraButton.gameObject.SetActive(true);navigationEraButton.interactable=true;
            navigationEraButton.GetComponentInChildren<TMP_Text>(true).text=ChapterCatalog.Rules.IsChapterComplete(c.Progress)?"Chapter complete · inspect":
                ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress)?"Free era entry · inspect":"Era and progress · inspect";
        }
        void InspectNavigationEra()
        {
            if(!navigationEraAvailable||!SectionAction(navigationEraButton,NavigationConstruction))return;
            if(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress))
            {BeginChapterPanel(JourneyGuide,"Chapter complete");shownGuide=null;panelBody.text=ChapterEndMilestones();RememberSectionReturn(SectionReturn.Construction,JourneyGuide);}
            else {ShowChapterEra();RememberSectionReturn(SectionReturn.Construction,ChapterEra);}
        }
        void ShowDirectSkills()
        {
            if(!CanOpenChapterPanel()||Session.RecoveryRequired)return;
            if(ShowChapterTransition())return;
            BeginDirectNavigation(NavigationSkills,"Skills · optional");
            navigationContext.text+="\nTap a card to inspect.";
            for(int i=0;i<6;i++)
            {
                var quote=ChapterEconomy.QuoteSkill(Session.State,SkillTree.Nodes[i].Id);var node=quote.Node;
                string title=new[]{"Coins","Recycling","Delivery"}[node.Branch]+(node.Tier==1?" I":" II");
                string status;
                if(quote.Owned)
                {
                    var receipt=Session.Chapter.Skills.Receipts.First(r=>r.Request.NodeId==node.Id);
                    status=$"Owned · paid {receipt.FrozenPrice}";
                }
                else if(quote.Status==OfferStatus.Locked)
                    status="Locked · "+(node.Tier==1?FamilyName(node.RequiredFamilyId):"inspect requirements")+$"\nPrice: {node.Price}";
                else status=$"Available · {node.Price}\n"+(quote.Status==OfferStatus.Available?"Enough Coins":$"Need {node.Price-Session.State.coins} Coins");
                SetNavigationCard(i,title+"\n"+status,true,ColorOf(quote.Owned?"86E2E5":"C8D8DB"));
            }
        }
        void ShowDirectOrders()
        {
            if(!CanOpenChapterPanel()||Session.RecoveryRequired)return;
            if(ShowChapterTransition())return;
            BeginDirectNavigation(NavigationOrders,"Orders · optional");
            var state=Session.Journey.Orders;var chapter=Session.Chapter;bool unlocked=ChapterCatalog.Rules.IsChapterComplete(chapter.Progress);
            // Optional new definitions appear only after the Core catalog is integrated.
            // Stable IDs preserve the old two choices and their existing operations.
            string[] supported={"order.sort-salvage","order.neighborhood-supply","order.gather-potatoes","order.pack-boxes"};
            int index=0;
            foreach(string id in supported)
            {
                var order=OrderCatalog.Choices.FirstOrDefault(o=>o.Id==id);if(order==null)continue;
                navigationOrderIds[index]=id;bool mine=state.ActiveOrderId==id;
                string status=!unlocked?"Locked · requires 24/24":mine?(state.Ready?"Ready · collect explicitly":$"In progress · {state.Progress}/{order.Target}"):
                    state.ActiveOrderId!=null?"Busy · inspect":"Available · accept explicitly";
                string paid=state.LatestClaim?.OrderId==id?"\n<size=90%><color=#C8D8DB>Previous reward collected</color></size>":"";
                SetNavigationCard(index++,NavigationOrderTitle(id)+$"\n{order.Target} {NavigationOrderUnit(id)} · +{order.RewardCoins}\n"+status+paid,unlocked,ColorOf(mine?"F4B85C":"C8D8DB"));
            }
            navigationContext.text=unlocked?navigationContext.text.Replace("\nNo goal selected","")+"\nOne order; canceling gives no reward.\n"+NavigationOrderPurpose(chapter):
                navigationContext.text+"\nUnlocks at 24/24. Optional to finish.";
            if(unlocked)SetNavigationCard(4,"Chapter and free play\n24/24 · you may finish",true,ColorOf("86E2E5"));
        }
        static string NavigationOrderPurpose(ChapterState chapter)
        {
            return chapter.Skills.Receipts.Count<6?"Coins can buy remaining optional skills.":
                "6/6 skills. Orders are free play for Coins.";
        }
        void ShowClaimedOrders(OrderClaimRecord claim,ChapterState chapter)
        {
            ProductRoute(ShowDirectOrders,ProductEvents);
            if(wavePanel==NavigationOrders&&navigationContextValid)
                navigationContext.text=$"Order complete: +{claim.RewardCoins} Coins.\n"+
                    (Guidance.ChapterNarrative.OrderClaimed(claim.OrderId)??"Continue if you wish.")+"\n"+NavigationOrderPurpose(chapter);
        }
        static string NavigationOrderTitle(string id)
        {return id=="order.sort-salvage"?"Sort Trash":id=="order.neighborhood-supply"?"Neighborhood Delivery":id=="order.gather-potatoes"?"Gather Potatoes":"Pack Boxes";}
        static string NavigationOrderUnit(string id)
        {return id=="order.sort-salvage"?"Trash":id=="order.neighborhood-supply"?"deliveries":id=="order.gather-potatoes"?"Potatoes":"Boxes";}
        void OpenNavigationCard(int index)
        {
            if(index<0||index>=6||!navigationCardAvailable[index])return;
            int mode=wavePanel;if(mode!=NavigationConstruction&&mode!=NavigationSkills&&mode!=NavigationOrders)return;
            if(!SectionAction(navigationCards[index],mode))return;
            // HideWaveChoices clears transient arrays/origins; capture the stable ID first.
            string orderId=navigationOrderIds[index];
            if(mode==NavigationConstruction)
            {ShowChapterFamily(FamilyIds.All[index],true);RememberSectionReturn(SectionReturn.Construction,ChapterFamily);}
            else if(mode==NavigationSkills)
            {ShowChapterSkill(SkillTree.Nodes[index].Id);RememberSectionReturn(SectionReturn.Skills,ChapterSkill);}
            else if(orderId!=null&&ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress))
            {ShowOrder(orderId);RememberSectionReturn(SectionReturn.Orders,JourneyOrder);}
            else if(index==4&&ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress))
            {BeginChapterPanel(JourneyGuide,"Chapter complete");shownGuide=null;panelBody.text=ChapterEndMilestones();RememberSectionReturn(SectionReturn.Orders,JourneyGuide);}
        }
        // Parent may call this at the end of ShowOrder so old guidance entry points
        // receive the same factual four-order copy, without changing lifecycle logic.
        void PresentNavigationOrder(string id)
        {
            var order=OrderCatalog.Get(id);var state=Session.Journey.Orders;bool mine=state.ActiveOrderId==id;
            panelTitle.text=NavigationOrderTitle(id);
            string goal=id=="order.neighborhood-supply"?"completed deliveries of 3 Pigeons":$"{NavigationOrderUnit(id)} appearances";
            string introduction=JunkToNeon.Guidance.ChapterNarrative.OrderIntroduction(id);
            panelBody.text=(introduction==null?"":introduction+"\n\n")+$"Goal: {order.Target} {goal}.\nReward: {order.RewardCoins} Coins. No deadline or fee.\n"+
                (state.ActiveOrderId==null?"Only new results after explicit acceptance.":mine?$"Progress: {state.Progress}/{order.Target}. "+(state.Ready?JunkToNeon.Guidance.ChapterNarrative.OrderReady(id)??"Collect the reward explicitly.":"Keep making ordinary spins."):"Finish or cancel the current order first.");
            if(state.LatestClaim?.OrderId==id)panelBody.text+=$"\nPrevious order already paid {state.LatestClaim.RewardCoins} Coins.";
            panelBody.text+="\n\nSpins yield materials based on results and caps. Orders do not increase yield; upgrades do not change order credit.";
        }
        // Parent's PreviousJourneyPage captures this before BeginChapterPanel, then
        // restores the Orders origin for JourneyCancel. No economic action occurs.
        bool NavigationReturnsToOrders()=>sectionReturnOrigin==SectionReturn.Orders;
        void RememberNavigationOrderCancel()=>RememberSectionReturn(SectionReturn.Orders,JourneyCancel);
        bool IsDirectNavigationPanel()=>wavePanel==NavigationConstruction||wavePanel==NavigationSkills||wavePanel==NavigationOrders;
        void InvalidateDirectNavigation()
        {
            navigationContextValid=false;
            if(navigationRoot!=null)navigationRoot.gameObject.SetActive(false);
            if(navigationHubContext!=null)InnerNavContextVisible(navigationHubContext,false);
            if(panelBody!=null)panelBody.enabled=true;
            navigationEraAvailable=false;
            if(navigationEraButton!=null&&navigationEraButton.TryGetComponent<JtnPanelPointerConsent>(out var eraConsent))eraConsent.Invalidate();
            for(int i=0;i<navigationCardAvailable.Length;i++)navigationCardAvailable[i]=false;
            if(navigationCards!=null)foreach(var button in navigationCards)
                if(button!=null&&button.TryGetComponent<JtnPanelPointerConsent>(out var consent))consent.Invalidate();
        }
        void RenderDirectNavigation(bool usable)
        {
            if(!navigationWired)return;
            bool visible=panel.activeSelf&&(wavePanel==NavigationConstruction||wavePanel==NavigationSkills||wavePanel==NavigationOrders);
            navigationRoot.gameObject.SetActive(visible&&usable&&navigationContextValid);
            InnerNavContextVisible(navigationHubContext,panel.activeSelf&&wavePanel==SectionHub&&usable);
            for(int i=0;i<6;i++)navigationCards[i].interactable=usable&&visible&&navigationContextValid&&navigationCardAvailable[i];
            navigationEraButton.interactable=usable&&visible&&navigationContextValid&&navigationEraAvailable;
            if(usable&&visible&&!navigationContextValid&&!Busy&&!failed&&!InBackground&&
                Session!=null&&!Session.RecoveryRequired&&Session.State!=null&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel)
            {
                string destination=wavePanel==NavigationConstruction?"Buildings":wavePanel==NavigationSkills?"Skills":"Orders";
                string route=productChrome==null?"All Sections → "+destination:
                    wavePanel==NavigationConstruction?"District → Construction":wavePanel==NavigationSkills?"Upgrades → Skills":"Events → Neighbor Orders";
                panelTitle.enabled=panelBody.enabled=true;panelTitle.text=destination;
                panelBody.text=$"Reopen this list. Press Close, then {route}.";
                closeButton.GetComponentInChildren<TMP_Text>(true).text="Close";
            }
        }
    }
}
