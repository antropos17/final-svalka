using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using TMPro;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int ExpansionHome=50,ExpansionCategory=51,ExpansionOffer=52,ExpansionSocket=53,ExpansionResult=54;
        [SerializeField] UnityEngine.UI.Button expansionEntryButton,expansionShopButton,expansionQuickBoostButton;
        [SerializeField] RectTransform expansionRoot,expansionHudRoot,expansionBonusFrame;
        [SerializeField] TMP_Text expansionTitle,expansionResources,expansionBody,expansionHudResources,expansionBonusResult,expansionBonusProgress;
        JtnProductSurface expansionBonusSurface;
        [SerializeField] UnityEngine.UI.Button[] expansionCards=new UnityEngine.UI.Button[6];
        [SerializeField] UnityEngine.UI.Button expansionPreviousButton,expansionNextButton,expansionPrimaryButton,expansionSecondaryButton;
        readonly string[] expansionCardIds=new string[6];
        readonly int[] expansionCardKinds=new int[6],expansionCardValues=new int[6];
        readonly bool[] expansionCardAvailable=new bool[6];
        bool expansionWired,expansionContextValid,expansionPresentationPending;
        bool expansionChangingPanel;
        int expansionFilteredCategory=-1;
        int expansionOwnedCategory=-1;
        int expansionCategory,expansionPage,expansionSocket,expansionReturnMode,expansionPrimaryAction,expansionSecondaryAction;
        string expansionInspectedId,expansionModuleToEquip,expansionAppliedTheme;
        ExpansionQuote expansionQuote;
        ExpansionQuote expansionQuickBoostQuote;
        UnityEngine.UI.Button.ButtonClickedEvent expansionTradeBaseSecondaryEvent;
        UnityEngine.UI.Button.ButtonClickedEvent expansionResultBasePrimaryEvent;
        UnityEngine.UI.Button.ButtonClickedEvent expansionBasePrimaryActionEvent,expansionBaseSecondaryActionEvent;
        string expansionResultActionKey;
        long expansionResultSequence;
        GameSession expansionHudSession;
        long expansionHudSequence=-1,expansionPresentedReceipt;
        readonly RectTransform[] expansionBaseReels=new RectTransform[3];
        readonly Vector2[] expansionBaseReelMin=new Vector2[3],expansionBaseReelMax=new Vector2[3];
        readonly Color[] expansionBaseReelColors=new Color[3];
        bool expansionReelLayoutCaptured,expansionReelLayoutApplied,expansionReelUnlocked;
        Vector2 expansionMessageMin,expansionMessageMax;
        bool expansionMessageAutoSize,expansionQuickBoostNeedsRefresh=true;
        float expansionMessageFont,expansionMessageFontMin,expansionMessageFontMax;

        public void CreateExpansionUi()
        {
            if(expansionWired&&Application.isPlaying)return;
            if(expansionEntryButton==null)expansionEntryButton=CreateSectionControl("ExpansionEntry","Slot & Shop",sectionsHubRoot,new Vector2(.53f,.01f),new Vector2(.97f,.24f),ColorOf("F4B85C"),OpenExpansionEntry);
            else if(Application.isPlaying)expansionEntryButton.onClick.AddListener(OpenExpansionEntry);
            AddSectionConsent(expansionEntryButton);FitChapterLabel(expansionEntryButton);
            if(expansionShopButton==null)expansionShopButton=CreateSectionControl("ExpansionShop","Shop",world,new Vector2(.40f,.68f),new Vector2(.65f,.81f),ColorOf("F2EAD2"),OpenExpansionShop);
            else if(Application.isPlaying)expansionShopButton.onClick.AddListener(OpenExpansionShop);
            AddSectionConsent(expansionShopButton);FitChapterLabel(expansionShopButton);
            if(expansionQuickBoostButton==null)expansionQuickBoostButton=CreateSectionControl("ExpansionQuickBoost","Boost",spinButton.transform.parent,new Vector2(.71f,.32f),new Vector2(.95f,.49f),ColorOf("F4B85C"),UseExpansionQuickBoost);
            else if(Application.isPlaying)expansionQuickBoostButton.onClick.AddListener(UseExpansionQuickBoost);
            AddSectionConsent(expansionQuickBoostButton);ExpansionFit(expansionQuickBoostButton,10,12);
            if(expansionRoot==null)expansionRoot=Area("ExpansionUi",panel.transform,new Vector2(.03f,.16f),new Vector2(.97f,.98f));
            if(expansionTitle==null)expansionTitle=ExpansionLabel("ExpansionTitle",expansionRoot,new Vector2(0,.91f),Vector2.one,18);
            if(expansionResources==null)expansionResources=ExpansionLabel("ExpansionResources",expansionRoot,new Vector2(0,.80f),new Vector2(1,.91f),12);
            if(expansionBody==null)expansionBody=ExpansionLabel("ExpansionBody",expansionRoot,new Vector2(.015f,.20f),new Vector2(.985f,.78f),14);
            ExpansionStyleHeader();
            if(expansionCards==null)expansionCards=new UnityEngine.UI.Button[6];
            else if(expansionCards.Length!=6)Array.Resize(ref expansionCards,6);
            for(int i=0;i<6;i++)
            {
                int index=i;float left=i%2*.52f,top=.78f-i/2*.195f;
                if(expansionCards[i]==null)expansionCards[i]=CreateSectionControl("ExpansionCard"+i,"",expansionRoot,new Vector2(left,top-.175f),new Vector2(left+.48f,top),ColorOf("B9CCBB"),()=>OpenExpansionCard(index));
                else if(Application.isPlaying)expansionCards[i].onClick.AddListener(()=>OpenExpansionCard(index));
                ExpansionFit(expansionCards[i],10,13);AddSectionConsent(expansionCards[i]);ExpansionStyleCard(expansionCards[i]);
            }
            ExpansionControl(ref expansionPreviousButton,"ExpansionPrevious","Previous",0,.48f,PreviousExpansionPage);
            // Paging reads "Previous" so it never repeats the Back caption below it.
            expansionPreviousButton.GetComponentInChildren<TMP_Text>(true).text="Previous";
            ExpansionControl(ref expansionNextButton,"ExpansionNext","Next",.52f,1,NextExpansionPage);
            ExpansionControl(ref expansionPrimaryButton,"ExpansionPrimary","Confirm",.52f,1,ExpansionPrimary);
            ExpansionControl(ref expansionSecondaryButton,"ExpansionSecondary","Inspect",0,.48f,ExpansionSecondary);
            ExpansionStyleControl(expansionPreviousButton,false);ExpansionStyleControl(expansionNextButton,false);
            ExpansionStyleControl(expansionPrimaryButton,true);ExpansionStyleControl(expansionSecondaryButton,false);
            if(expansionHudRoot==null)
            {
                var controls=spinButton.transform.parent;
                expansionHudRoot=Area("ExpansionResourcesHud",controls,new Vector2(.33f,.90f),new Vector2(.95f,.99f));
            }
            if(expansionHudResources==null)expansionHudResources=ExpansionLabel("ExpansionHudResources",expansionHudRoot,Vector2.zero,Vector2.one,10,ColorOf("D7E0C5"));
            if(expansionBonusFrame==null)
            {
                expansionBonusFrame=Box("ExpansionBonusReel",spinButton.transform.parent,new Vector2(.78f,.51f),new Vector2(.95f,.88f),ColorOf("B9CCBB"));
                var caption=ExpansionLabel("BonusCaption",expansionBonusFrame,new Vector2(.04f,.83f),new Vector2(.96f,.98f),10);caption.text="BONUS";caption.alignment=TextAlignmentOptions.Center;
            }
            if(expansionBonusResult==null)expansionBonusResult=ExpansionLabel("ExpansionBonusResult",expansionBonusFrame,new Vector2(.05f,.20f),new Vector2(.95f,.82f),11);
            if(expansionBonusProgress==null)expansionBonusProgress=ExpansionLabel("ExpansionBonusProgress",expansionBonusFrame,new Vector2(.05f,.02f),new Vector2(.95f,.18f),10);
            expansionBonusResult.alignment=expansionBonusProgress.alignment=TextAlignmentOptions.Center;
            StyleExpansionBonusReel();
            if(!expansionReelLayoutCaptured)
            {
                for(int i=0;i<3;i++)
                {
                    var frame=(RectTransform)reelImages[i].transform.parent;expansionBaseReels[i]=frame;
                    expansionBaseReelMin[i]=frame.anchorMin;expansionBaseReelMax[i]=frame.anchorMax;
                    expansionBaseReelColors[i]=frame.GetComponent<UnityEngine.UI.Image>().color;
                }
                expansionMessageMin=message.rectTransform.anchorMin;expansionMessageMax=message.rectTransform.anchorMax;
                expansionMessageAutoSize=message.enableAutoSizing;expansionMessageFont=message.fontSize;
                expansionMessageFontMin=message.fontSizeMin;expansionMessageFontMax=message.fontSizeMax;
                expansionReelLayoutCaptured=true;
            }
            expansionBonusFrame.gameObject.SetActive(false);
            expansionRoot.gameObject.SetActive(false);expansionEntryButton.gameObject.SetActive(false);
            expansionWired=Application.isPlaying;
        }
        void StyleExpansionBonusReel()
        {
            // Match the neighboring reel surfaces without changing their geometry or this reel's contents.
            var original=expansionBonusFrame.GetComponent<UnityEngine.UI.Image>();
            if(original!=null)original.enabled=false;
            var backdrop=expansionBonusFrame.Find("Bonus Surface") as RectTransform;
            if(backdrop==null)backdrop=Area("Bonus Surface",expansionBonusFrame,Vector2.zero,Vector2.one);
            backdrop.SetAsFirstSibling();
            expansionBonusSurface=backdrop.GetComponent<JtnProductSurface>();
            if(expansionBonusSurface==null)expansionBonusSurface=backdrop.gameObject.AddComponent<JtnProductSurface>();
            expansionBonusSurface.color=ColorOf("F2EDDC");expansionBonusSurface.Radius=18;expansionBonusSurface.raycastTarget=false;
            var caption=expansionBonusFrame.Find("BonusCaption").GetComponent<TMP_Text>();
            Place(caption.rectTransform,expansionBonusFrame,new Vector2(.08f,.83f),new Vector2(.92f,.96f));
            caption.fontStyle=FontStyles.Bold;caption.color=ColorOf("795019");
            Place(expansionBonusResult.rectTransform,expansionBonusFrame,new Vector2(.08f,.23f),new Vector2(.92f,.78f));
            expansionBonusResult.color=ColorOf("263C50");
            Place(expansionBonusProgress.rectTransform,expansionBonusFrame,new Vector2(.08f,.055f),new Vector2(.92f,.18f));
            expansionBonusProgress.color=ColorOf("426268");
        }
        TMP_Text ExpansionLabel(string name,Transform parent,Vector2 min,Vector2 max,float size,Color? color=null)
        {
            var text=Label("",parent,min,max,size,color??ColorOf("203A30"));text.name=name;
            text.enableAutoSizing=true;text.fontSizeMin=Mathf.Min(10,size);text.fontSizeMax=size;text.raycastTarget=false;
            return text;
        }
        void ExpansionStyleHeader()
        {
            var strip=expansionRoot.Find("Expansion Resource Strip") as RectTransform;
            if(strip==null)strip=Area("Expansion Resource Strip",expansionRoot,new Vector2(.01f,.795f),new Vector2(.99f,.91f));
            Surface(strip.gameObject,ColorOf("E6E1CF"),12);strip.SetAsFirstSibling();
            Place(expansionResources.rectTransform,expansionRoot,new Vector2(.04f,.805f),new Vector2(.96f,.905f));
            expansionResources.color=ColorOf("263C50");expansionResources.alignment=TextAlignmentOptions.MidlineLeft;
            expansionTitle.color=ColorOf("263C50");expansionTitle.alignment=TextAlignmentOptions.MidlineLeft;expansionTitle.fontStyle=FontStyles.Bold;
            expansionBody.color=ColorOf("263C50");
        }
        void ExpansionStyleCard(UnityEngine.UI.Button button)
        {
            button.targetGraphic=Surface(button.gameObject,ColorOf("263C50"),15,true);StyleCInnerCard(button);
            var text=button.GetComponentInChildren<TMP_Text>(true);
            Place(text.rectTransform,button.transform,new Vector2(.24f,.07f),new Vector2(.96f,.91f));
            text.color=ColorOf("F6F1E3");text.alignment=TextAlignmentOptions.TopLeft;
            var icon=button.transform.Find("Expansion Icon") as RectTransform;
            if(icon==null)icon=Area("Expansion Icon",button.transform,new Vector2(.035f,.30f),new Vector2(.19f,.70f));
            Place(icon,button.transform,new Vector2(.045f,.52f),new Vector2(.195f,.90f));
            var graphic=icon.GetComponent<JtnProductIcon>()??icon.gameObject.AddComponent<JtnProductIcon>();
            graphic.color=ColorOf("F8D986");graphic.raycastTarget=false;
        }
        void ExpansionStyleControl(UnityEngine.UI.Button button,bool primary)
        {
            button.targetGraphic=Surface(button.gameObject,ColorOf(primary?"F4B85C":"263C50"),15,true);StyleCInnerAction(button,primary);
            var text=button.GetComponentInChildren<TMP_Text>(true);text.color=ColorOf(primary?"263C50":"F6F1E3");
        }
        static JtnProductIcon.Symbol ExpansionCardIcon(string id,int kind,int value)
        {
            if(kind==1)
                return value==0?JtnProductIcon.Symbol.Box:value==1?JtnProductIcon.Symbol.Upgrade:value==2?JtnProductIcon.Symbol.Events:
                    value==3?JtnProductIcon.Symbol.Home:value==4?JtnProductIcon.Symbol.Leaf:JtnProductIcon.Symbol.Energy;
            if(kind==3)return JtnProductIcon.Symbol.Upgrade;
            if(id==null||id.StartsWith("module.",StringComparison.Ordinal))return JtnProductIcon.Symbol.Box;
            if(id.StartsWith("theme.",StringComparison.Ordinal))return JtnProductIcon.Symbol.Events;
            if(id.StartsWith("equipment.",StringComparison.Ordinal))return JtnProductIcon.Symbol.Home;
            if(id.StartsWith("potato.",StringComparison.Ordinal))return JtnProductIcon.Symbol.Leaf;
            if(id.StartsWith("trade.",StringComparison.Ordinal))return JtnProductIcon.Symbol.Shop;
            if(id.StartsWith("bonus.",StringComparison.Ordinal)||id.StartsWith("focus.",StringComparison.Ordinal))return JtnProductIcon.Symbol.Energy;
            return JtnProductIcon.Symbol.Upgrade;
        }
        void ExpansionControl(ref UnityEngine.UI.Button button,string name,string caption,float left,float right,UnityEngine.Events.UnityAction action)
        {
            if(button==null)button=CreateSectionControl(name,caption,expansionRoot,new Vector2(left,.015f),new Vector2(right,.16f),ColorOf("F4B85C"),action);
            else if(Application.isPlaying)button.onClick.AddListener(action);
            ExpansionFit(button,10,13);AddSectionConsent(button);button.gameObject.SetActive(false);
        }
        static void ExpansionFit(UnityEngine.UI.Button button,float min,float max)
        {
            var text=button.GetComponentInChildren<TMP_Text>(true);text.enableAutoSizing=true;text.fontSizeMin=min;text.fontSizeMax=max;text.raycastTarget=false;
        }
        bool IsExpansionPanel()=>wavePanel>=ExpansionHome&&wavePanel<=ExpansionResult;
        bool ExpansionCanOpen()=>CanOpenChapterPanel()&&!Session.RecoveryRequired&&!Session.Chapter.TransitionPending;
        void OpenExpansionEntry()
        {if(SectionAction(expansionEntryButton,SectionHub))ShowExpansionHome();}
        void OpenExpansionShop()
        {
            if(!CanOpenChapterPanel()||Session.RecoveryRequired||panel.activeSelf||!expansionShopButton.isActiveAndEnabled||!expansionShopButton.IsInteractable()||!PointerConsentAllows(expansionShopButton))return;
            if(ShowChapterTransition())return;
            ShowExpansionHome();
        }
        void BeginExpansion(int mode,string title,ExpansionState view)
        {
            ResetExpansionBatchPresentation();
            bool changing=expansionChangingPanel;expansionChangingPanel=true;
            try{BeginChapterPanel(mode,title);}finally{expansionChangingPanel=changing;}
            shownGuide=null;
            panelTitle.enabled=false;panelBody.enabled=false;expansionContextValid=true;
            expansionTitle.text=title;expansionResources.text=ExpansionBalances(view);expansionBody.text="";expansionBody.gameObject.SetActive(false);
            bool detail=mode==ExpansionOffer||mode==ExpansionResult;
            expansionBody.alignment=detail?TextAlignmentOptions.TopLeft:TextAlignmentOptions.MidlineLeft;
            expansionBody.margin=detail?new Vector4(8,6,8,0):Vector4.zero;
            for(int i=0;i<6;i++){expansionCards[i].gameObject.SetActive(false);expansionCardAvailable[i]=false;expansionCardIds[i]=null;expansionCardKinds[i]=0;}
            foreach(var button in new[]{expansionPreviousButton,expansionNextButton,expansionPrimaryButton,expansionSecondaryButton})button.gameObject.SetActive(false);
            expansionRoot.SetAsLastSibling();expansionRoot.gameObject.SetActive(true);
            SharedHeading(expansionTitle,"emblem-shop");
        }
        string ExpansionBalances(ExpansionState view)
        {return $"Coins: {Session.State.coins} · Parts: {view.Parts}/{view.PartsCap}\nPotatoes: {view.Potatoes}/{view.PotatoesCap} · Energy: {view.Energy}/{view.EnergyCap}";}
        string ExpansionName(string id)=>ExpansionCopy.Label(id)??id;
        string ExpansionDescription(string id)=>ExpansionCopy.Description(id)??"Check the actual cost and requirements.";
        void ExpansionCard(int index,string id,int kind,int value,string caption,bool available=true)
        {
            expansionCardIds[index]=id;expansionCardKinds[index]=kind;expansionCardValues[index]=value;expansionCardAvailable[index]=available;
            var button=expansionCards[index];button.GetComponentInChildren<TMP_Text>(true).text=InnerNavCaption(caption);button.interactable=available;button.gameObject.SetActive(true);
            var icon=button.transform.Find("Expansion Icon");
            if(icon!=null)
            {
                var graphic=icon.GetComponent<JtnProductIcon>();graphic.Shape=ExpansionCardIcon(id,kind,value);
                graphic.ArtworkOverride=SectionSprite(ExpansionArtwork(id,kind,value));
            }
        }
        void ExpansionAction(UnityEngine.UI.Button button,int action,string caption)
        {
            if(button==expansionPrimaryButton)expansionPrimaryAction=action;else expansionSecondaryAction=action;
            var original=button==expansionPrimaryButton?expansionBasePrimaryActionEvent:expansionBaseSecondaryActionEvent;
            if(original==null)
            {
                original=button.onClick;
                if(button==expansionPrimaryButton)expansionBasePrimaryActionEvent=original;else expansionBaseSecondaryActionEvent=original;
            }
            // Each visible action owns its event. A retained earlier event must
            // never dispatch the quote/action currently shown on this button.
            var visibleEvent=new UnityEngine.UI.Button.ButtonClickedEvent();
            visibleEvent.AddListener(()=>{if(ReferenceEquals(button.onClick,visibleEvent))original.Invoke();});
            button.onClick=visibleEvent;
            button.GetComponentInChildren<TMP_Text>(true).text=caption;button.interactable=true;button.gameObject.SetActive(true);
        }
        void ShowExpansionHome()
        {
            if(!ExpansionCanOpen())return;var view=Session.Expansion;
            BeginExpansion(ExpansionHome,"Slot & Shop",view);
            expansionFilteredCategory=-1;
            expansionOwnedCategory=-1;
            expansionReturnMode=ExpansionHome;
            expansionResources.text+="\nCurrent: "+ExpansionName(view.SpeedId)+" · "+view.SpinDurationSeconds.ToString("0.0",System.Globalization.CultureInfo.GetCultureInfo("en-US"))+" s";
            string[] titles={"Modules","Support slots","Appearance","Equipment","Potatoes & exchange","Bonus & Energy"};
            string[] details={"Buy and install",$"Open: {view.SocketCount}/3","Owned palettes","Six separate tracks",$"Yield: {view.PotatoYieldLevel}/2",view.BonusUnlocked?$"{view.BonusCadence-view.BonusProgress} spins left · workbench {view.BonusWorkbenchLevel}/2":"Bonus reel locked"};
            for(int i=0;i<6;i++)ExpansionCard(i,null,1,i,titles[i]+"\n"+details[i]);
            string speed=view.SpeedId=="speed.normal"?"speed.fast":"speed.normal";
            try{expansionQuote=Session.GetExpansionAction("speed",speed);}catch(InvalidOperationException){expansionQuote=null;}
            float seconds=speed=="speed.fast"?EconomyRules.ExpansionFastSpinSeconds:EconomyRules.ExpansionNormalSpinSeconds;
            if(expansionQuote!=null)ExpansionAction(expansionPrimaryButton,1,(speed=="speed.fast"?"Faster":"Normal")+" · "+seconds.ToString("0.0",System.Globalization.CultureInfo.GetCultureInfo("en-US"))+" s\nFree");
            ExpansionAction(expansionSecondaryButton,5,"About resources");
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        static string ExpansionCategoryTitle(int category)
        {return new[]{"Modules","Support slots","Appearance","Equipment","Potatoes & exchange","Bonus & Energy"}[category];}
        List<string> ExpansionCategoryIds(int category,ExpansionState view)
        {
            if(category==1)return new List<string>{"socket.1","socket.2","socket.3","preset.parts","preset.harvest","preset.energy"};
            if(category==2)return new[]{ExpansionCatalog.DefaultTheme}.Concat(ExpansionCatalog.Offers.Where(o=>o.Kind=="theme").Select(o=>o.Id)).ToList();
            if(category==3)return FamilyIds.All.Select(f=>"equipment."+f+"."+Math.Min(2,view.EquipmentLevel(f)+1)).ToList();
            if(category==5)return new List<string>{ExpansionCatalog.BonusUnlock,ExpansionCatalog.NextBonusBoost,
                "workbench.bonus."+Math.Min(2,view.BonusWorkbenchLevel+1),"focus.balanced","focus.parts","focus.potatoes"};
            return ExpansionCatalog.Offers.Where(o=>category==0?o.Kind=="module":o.Kind=="yield"||o.Kind=="trade").Select(o=>o.Id).ToList();
        }
        void ShowExpansionCategory(int category,int page=0)
        {
            if(category<0||category>5||!ExpansionCanOpen())return;
            var state=Session.State;var view=ExpansionEconomy.Project(state);var ids=ExpansionVisibleCategoryIds(category,state,view);
            int pages=Math.Max(1,(ids.Count+5)/6);page=Math.Max(0,Math.Min(page,pages-1));
            BeginExpansion(ExpansionCategory,ExpansionCategoryTitle(category)+(expansionFilteredCategory==category?" · available to buy":expansionOwnedCategory==category?" · owned":"")+(pages>1?$" · {page+1}/{pages}":""),view);
            expansionCategory=category;expansionPage=page;
            SharedHeading(expansionTitle,SectionCategoryArt[category]);
            // One immutable building snapshot for this synchronous list render only.
            FamilyStages equipmentStages=null;
            for(int i=0;i<6&&page*6+i<ids.Count;i++)
            {
                string id=ids[page*6+i];
                if(category==1&&id.StartsWith("socket.",StringComparison.Ordinal))
                {
                    int slot=int.Parse(id.Substring("socket.".Length),System.Globalization.CultureInfo.InvariantCulture);bool open=slot<=view.SocketCount;string module=open?view.ModuleAt(slot):null;
                    string label=$"Slot {slot}\n"+(open?(module==null?"Open · empty":ExpansionName(module)):"Locked · inspect price");
                    ExpansionCard(i,id,3,slot,label);continue;
                }
                ExpansionCard(i,id,2,0,ExpansionOfferCaption(id,state,view,ref equipmentStages));
            }
            expansionPreviousButton.gameObject.SetActive(page>0);expansionNextButton.gameObject.SetActive(page+1<pages);
            if(ExpansionFilterCategory(category))
            {
                string caption=expansionFilteredCategory==category?"All":"Available";
                if(page==0)ExpansionAction(expansionSecondaryButton,9,caption);
                else if(page+1==pages&&category!=0&&category!=2)ExpansionAction(expansionPrimaryButton,9,caption);
            }
            if((category==0||category==2)&&page+1==pages)ExpansionAction(expansionPrimaryButton,10,expansionOwnedCategory==category?"All":"Owned");
            if(ids.Count==0)
            {
                string prerequisites=category==3?ExpansionMissingEquipmentBuildings(state,view):null;
                expansionBody.gameObject.SetActive(true);expansionBody.text=expansionOwnedCategory==category?
                    "Nothing owned yet. Tap “All” or “Available”.":category==0||category==2?
                    "Nothing available to buy here. Open “Owned” to install a module or apply a palette, or “All” for other offers.":
                    category==3&&FamilyIds.All.All(f=>view.EquipmentLevel(f)==2)?
                    "All equipment is upgraded to 2/2. There are no further upgrades here. Tap “All” to see the effects.":
                    prerequisites??"Nothing available to buy here. Continue spinning or tap “All” to see owned and locked items.";
            }
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        string ExpansionMissingEquipmentBuildings(ModelState state,ExpansionState view)
        {
            var remaining=FamilyIds.All.Where(f=>view.EquipmentLevel(f)<2).ToArray();
            if(remaining.Length==0)return null;
            var stages=ChapterCatalog.Rules.Stages(ChapterEconomy.Project(state).Progress);
            if(remaining.Any(f=>stages.AbsoluteLevel(f)>=1))return null;
            return "New equipment requires a building: "+string.Join(", ",remaining.Select(FamilyName))+". Each has its own track. See “All” for requirements.";
        }
        static bool ExpansionFilterCategory(int category)=>category==0||category==2||category==3||category==4;
        List<string> ExpansionVisibleCategoryIds(int category,ModelState state,ExpansionState view)
        {
            var ids=ExpansionCategoryIds(category,view);
            if(expansionOwnedCategory==category&&(category==0||category==2))return ids.Where(id=>category==0?view.OwnsModule(id):view.OwnsTheme(id)).ToList();
            if(!ExpansionFilterCategory(category)||expansionFilteredCategory!=category)return ids;
            return ids.Where(id=>
            {
                if(id==ExpansionCatalog.DefaultTheme)return false;
                try{ExpansionEconomy.Quote(state,"buy",id);return true;}
                catch(InvalidOperationException){return false;}
            }).ToList();
        }
        static string ExpansionCostText(int parts,int potatoes,int energy,long coins,bool keepUnitsTogether=false)
        {
            var costs=new List<string>();if(parts>0)costs.Add(parts+" parts");if(potatoes>0)costs.Add(potatoes+" potatoes");
            if(energy>0)costs.Add(energy+" energy");if(coins>0)costs.Add(coins+" coins");
            if(keepUnitsTogether)for(int i=0;i<costs.Count;i++)costs[i]="<nobr>"+costs[i]+"</nobr>";
            return costs.Count==0?"Free":string.Join(" + ",costs);
        }
        static string ExpansionQuoteCost(ExpansionQuote quote)=>ExpansionCostText(quote.PartsCost,quote.PotatoesCost,quote.EnergyCost,quote.CoinsCost);
        static int ExpansionModuleSlot(ExpansionState view,string id)
        {for(int slot=1;slot<=view.SocketCount;slot++)if(view.ModuleAt(slot)==id)return slot;return 0;}
        static bool ExpansionOwned(JunkToNeon.Core.ExpansionOffer offer,ExpansionState view)
        {
            if(offer.Kind=="module")return view.OwnsModule(offer.Id);
            if(offer.Kind=="theme")return view.OwnsTheme(offer.Id);
            if(offer.Kind=="socket")return view.SocketCount>=offer.Level;
            if(offer.Kind=="yield")return view.PotatoYieldLevel>=offer.Level;
            if(offer.Kind=="equipment")return view.EquipmentLevel(offer.FamilyId)>=offer.Level;
            if(offer.Kind=="workbench")return view.BonusWorkbenchLevel>=offer.Level;
            return offer.Kind=="bonus"&&view.BonusUnlocked;
        }
        static string ExpansionMissing(ModelState state,ExpansionState view,int parts,int potatoes,int energy,long coins)
        {
            var missing=new List<string>();if(parts>view.Parts)missing.Add((parts-view.Parts)+" parts");
            if(potatoes>view.Potatoes)missing.Add((potatoes-view.Potatoes)+" potatoes");if(energy>view.Energy)missing.Add((energy-view.Energy)+" energy");
            if(coins>state.coins)missing.Add((coins-state.coins)+" coins");return missing.Count==0?null:"Missing: "+string.Join(", ",missing);
        }
        static string ExpansionOfferLock(JunkToNeon.Core.ExpansionOffer offer,ModelState state,ExpansionState view,FamilyStages equipmentStages=null)
        {
            if(offer.Kind=="socket"&&offer.Level>view.SocketCount+1)return "First unlock slot "+(offer.Level-1)+".";
            if(offer.Kind=="yield"&&offer.Level>view.PotatoYieldLevel+1)return "First buy the previous potato level.";
            if(offer.Kind=="workbench")
            {
                if(!view.BonusUnlocked)return "First unlock the bonus reel.";
                if(offer.Level>view.BonusWorkbenchLevel+1)return "First buy the previous workbench level.";
            }
            if(offer.Kind=="equipment")
            {
                if((equipmentStages??ChapterCatalog.Rules.Stages(ChapterEconomy.Project(state).Progress)).AbsoluteLevel(offer.FamilyId)<1)return "First build: "+FamilyName(offer.FamilyId)+".";
                if(offer.Level>view.EquipmentLevel(offer.FamilyId)+1)return "First buy the previous equipment level.";
            }
            return null;
        }
        string ExpansionOfferCaption(string id,ModelState state,ExpansionState view,ref FamilyStages equipmentStages)
        {
            if(id.StartsWith("focus.",StringComparison.Ordinal))return ExpansionName(id)+"\n"+(view.BonusWorkbenchLevel<1?"Locked · workbench I":view.BonusFocusId==id?"Selected":"Free · inspect");
            if(id.StartsWith("preset.",StringComparison.Ordinal))
            {
                var next=ExpansionEconomy.PreviewPreset(view,id);
                bool same=Enumerable.Range(1,3).All(slot=>view.ModuleAt(slot)==next[slot-1]);
                return ExpansionName(id)+"\n"+(same?"Slots already match":"Owned modules\nFree · inspect");
            }
            if(id==ExpansionCatalog.DefaultTheme)return ExpansionName(id)+"\n"+(view.ThemeId==id?"Applied":"Owned · apply");
            if(id==ExpansionCatalog.NextBonusBoost)return ExpansionName(id)+"\n"+(view.BoostArmed?"Active · awaiting bonus":!view.BonusUnlocked?"Locked · bonus required":ExpansionCostText(0,0,EconomyRules.ExpansionBoostCost,0,true)+"\n"+(view.Energy>=EconomyRules.ExpansionBoostCost?"Can activate":"Saving energy"));
            var offer=ExpansionCatalog.Get(id);bool owned=ExpansionOwned(offer,view);
            string name=ExpansionName(id);
            string title=offer.Kind=="equipment"?FamilyName(offer.FamilyId)+$" · {view.EquipmentLevel(offer.FamilyId)}/2":
                offer.Kind=="workbench"?name.Insert(name.LastIndexOf(' ')+1,"<nobr>")+$" · {view.BonusWorkbenchLevel}/2</nobr>":name+(offer.Kind=="yield"?" · "+offer.Level:"");
            string status;
            if(owned)
            {
                int slot=offer.Kind=="module"?ExpansionModuleSlot(view,id):0;
                status=slot>0?"Owned · in slot "+slot:offer.Kind=="theme"?(view.ThemeId==id?"Applied":"Owned · apply"):offer.Kind=="module"?"Owned · install":"Owned · inspect";
            }
            else
            {
                if(offer.Kind=="equipment"&&equipmentStages==null)equipmentStages=ChapterCatalog.Rules.Stages(ChapterEconomy.Project(state).Progress);
                string locked=ExpansionOfferLock(offer,state,view,equipmentStages),missing=ExpansionMissing(state,view,offer.PartsCost,offer.PotatoesCost,offer.EnergyCost,offer.CoinsCost);
                status=ExpansionCostText(offer.PartsCost,offer.PotatoesCost,offer.EnergyCost,offer.CoinsCost,true)+"\n"+(locked!=null?"Locked · inspect":missing!=null?"Not enough resources":"Available · inspect");
            }
            return title+"\n"+status;
        }
        string ExpansionEffect(string id,ExpansionState view)
        {
            if(id.StartsWith("workbench.bonus.",StringComparison.Ordinal))return ExpansionCatalog.Get(id).Level==1?
                "Unlocks free selection of bonus focus. The original total stays the same; distribution may reduce losses when storage is full.":
                $"Before focus and boost: +{EconomyRules.ExpansionWorkbenchBonusPerResource} part and +{EconomyRules.ExpansionWorkbenchBonusPerResource} potato per bonus. Storage limits apply.";
            if(id.StartsWith("focus.",StringComparison.Ordinal))return id=="focus.parts"?
                "All bonus resources go to parts; potatoes are 0. Overflow is lost.":id=="focus.potatoes"?
                "All bonus resources go to potatoes; parts are 0. Overflow is lost.":
                "Normal bonus resource distribution. Workbench II adds resources before focus; the boost applies afterward.";
            if(id.StartsWith("preset.",StringComparison.Ordinal))return "Replaces modules only in unlocked slots. Uses only owned modules, without duplicates or charges.";
            if(id==ExpansionCatalog.Salvage)return "+1 part per trash symbol, only when installed in a slot.";
            if(id==ExpansionCatalog.Packing)return "+1 part per box symbol, only when installed in a slot.";
            if(id==ExpansionCatalog.Harvest)return "+1 potato per potato symbol, only when installed in a slot.";
            if(id==ExpansionCatalog.Combo)return $"Pair or triple: +{EconomyRules.ExpansionComboYield} part and +{EconomyRules.ExpansionComboYield} potato once per spin. Installed module only.";
            if(id==ExpansionCatalog.Dynamo)return $"If there is a pigeon: +{EconomyRules.ExpansionDynamoEnergy} energy once per spin. Installed module only.";
            if(id==ExpansionCatalog.NextBonusBoost)return "Doubles resources for the next bonus only, within storage limits. Energy is charged once on activation. The three regular reels are unchanged.";
            if(id==ExpansionCatalog.BonusUnlock)return $"A separate resource bonus every {EconomyRules.ExpansionBonusCadence} new spins. Main coins and three symbols are unchanged.";
            if(id.StartsWith("potato.yield.",StringComparison.Ordinal))return ExpansionCatalog.Get(id).Level>view.PotatoYieldLevel+1?
                "After purchase, this level adds +1 Potato per Potato symbol on future spins. Level I is bought separately; module bonuses are separate.":
                $"Permanent potato increase: +{view.PotatoYieldLevel} → +{Math.Max(view.PotatoYieldLevel,ExpansionCatalog.Get(id).Level)} per potato symbol. The module is counted separately.";
            if(id.StartsWith("equipment.",StringComparison.Ordinal))
            {
                var offer=ExpansionCatalog.Get(id);int before=view.EquipmentLevel(offer.FamilyId),after=Math.Max(before,offer.Level);
                string effect=offer.FamilyId==FamilyIds.House?$"Storage capacity: Parts {view.PartsCap} → {view.PartsCap+(after-before)*EconomyRules.ExpansionHouseStoragePerLevel}; Potatoes {view.PotatoesCap} → {view.PotatoesCap+(after-before)*EconomyRules.ExpansionHouseStoragePerLevel}. No refill.":
                    offer.FamilyId==FamilyIds.PigeonLoft?$"Energy capacity: {view.EnergyCap} → {view.EnergyCap+(after-before)*EconomyRules.ExpansionPigeonEnergyPerLevel}. No refill.":
                    $"Increase: +{before} → +{after} "+(offer.FamilyId==FamilyIds.PowerSource?"energy per spin.":offer.FamilyId==FamilyIds.Workshop?"part per spin.":offer.FamilyId==FamilyIds.Recycler?"part per trash symbol.":"part per box symbol.");
                return effect+" Equipment is separate from the 24 buildings.";
            }
            if(id.StartsWith("trade.",StringComparison.Ordinal))return $"The exchange grants {ExpansionCatalog.Get(id).CoinsReward} coins. The selected construction will not be paid for or change price.";
            return ExpansionDescription(id);
        }
        void ShowExpansionOffer(string id,int category,int page=0,int returnMode=ExpansionCategory,int slot=0,string action=null)
        {
            if(!ExpansionCanOpen())return;var state=Session.State;var view=ExpansionEconomy.Project(state);
            if(action==null&&(id=="trade.parts"||id=="trade.potatoes"))
            {ShowExpansionBatchOffer(id,category,page,returnMode,1);return;}
            bool module=id.StartsWith("module.",StringComparison.Ordinal)&&id!="module.none";
            bool theme=id.StartsWith("theme.",StringComparison.Ordinal);
            bool ownedModule=module&&view.OwnsModule(id);
            string actualAction=action??(theme&&view.OwnsTheme(id)?"theme":id.StartsWith("focus.",StringComparison.Ordinal)?"focus":
                id.StartsWith("preset.",StringComparison.Ordinal)?"preset":id==ExpansionCatalog.NextBonusBoost?"boost":"buy");
            ExpansionQuote quote=null;string refusal=null;
            if(!ownedModule||actualAction=="equip")
            {
                try{quote=Session.GetExpansionAction(actualAction,id,slot);}
                catch(InvalidOperationException){refusal="Unavailable now. Reopen the preview after resources or requirements change.";}
            }
            var preset=actualAction=="preset"?ExpansionEconomy.PreviewPreset(view,id):null;
            bool samePreset=preset!=null&&Enumerable.Range(1,3).All(index=>view.ModuleAt(index)==preset[index-1]);
            if(samePreset||actualAction=="focus"&&view.BonusFocusId==id)quote=null;
            BeginExpansion(ExpansionOffer,ExpansionName(id),view);expansionCategory=category;expansionPage=page;expansionSocket=slot;
            SharedHeading(expansionTitle,ExpansionArtwork(id,0,category));
            expansionReturnMode=returnMode;expansionInspectedId=id;expansionQuote=quote;
            if(category==0&&actualAction=="equip")expansionModuleToEquip=id;
            expansionBody.gameObject.SetActive(true);
            string cost=quote==null?null:ExpansionQuoteCost(quote),status=refusal;
            if(actualAction=="buy"&&!ownedModule)
            {
                var offer=ExpansionCatalog.Get(id);cost=ExpansionCostText(offer.PartsCost,offer.PotatoesCost,offer.EnergyCost,offer.CoinsCost);
                if(ExpansionOwned(offer,view))status="Already owned. No repeat charge.";
                else status=ExpansionOfferLock(offer,state,view)??ExpansionMissing(state,view,offer.PartsCost,offer.PotatoesCost,offer.EnergyCost,offer.CoinsCost)??status;
            }
            if(actualAction=="boost")
            {
                cost=ExpansionCostText(0,0,EconomyRules.ExpansionBoostCost,0);
                status=view.BoostArmed?"Boost already active. Wait for the next bonus.":!view.BonusUnlocked?"First unlock the bonus reel.":ExpansionMissing(state,view,0,0,EconomyRules.ExpansionBoostCost,0)??status;
            }
            if(actualAction=="theme"&&view.ThemeId==id)status="Palette already applied. No repeat action.";
            if(actualAction=="focus")
            {cost=cost??"Free";status=view.BonusWorkbenchLevel<1?"Bonus workbench I is required first.":view.BonusFocusId==id?"This focus is already selected.":status;}
            if(actualAction=="preset")
            {cost=cost??"Free";status=samePreset?"Slots already match. No changes needed.":status;}
            if(actualAction=="equip"&&id!="module.none"&&ExpansionModuleSlot(view,id)>0)status="Module already installed. Remove it from its current slot first.";
            if(actualAction=="equip"&&quote!=null)
            {
                string current=view.ModuleAt(slot);
                status=$"Slot {slot}: "+(current==null?"empty":ExpansionName(current))+" → "+(id=="module.none"?"empty":ExpansionName(id))+". You will keep ownership of the previous module.";
            }
            if(ownedModule&&actualAction!="equip")
            {
                int placed=ExpansionModuleSlot(view,id);status=placed==0?"Module purchased. Its effect starts after explicit installation.":$"Installed in slot {placed}. Remove it there first to move it.";
                if(placed==0)ExpansionAction(expansionPrimaryButton,2,"Choose a slot");
                else{expansionSocket=placed;ExpansionAction(expansionPrimaryButton,3,"Slot "+placed+" · inspect");}
                cost="Already owned";
            }
            else if(quote!=null)ExpansionAction(expansionPrimaryButton,1,actualAction=="buy"?"Confirm purchase":actualAction=="equip"?(id=="module.none"?"Remove module":"Install"):
                actualAction=="theme"?"Apply palette":actualAction=="focus"?"Choose focus":actualAction=="preset"?"Apply preset":"Activate\n"+quote.EnergyCost+" energy");
            string available=actualAction=="buy"?(id.StartsWith("trade.",StringComparison.Ordinal)?"Exchange available.":"Purchase available."):
                actualAction=="equip"?"Installation available.":actualAction=="theme"?"Palette can be applied.":actualAction=="focus"?"Focus can be selected.":actualAction=="preset"?"Preset can be applied.":"Boost can be activated.";
            string replacement=preset==null?"":"\n<b>Slots open: "+view.SocketCount+"/3.</b>\n"+string.Join("\n",Enumerable.Range(1,3).Select(index=>
                index>view.SocketCount?$"Slot {index}: locked":$"{index}: "+(view.ModuleAt(index)==null?"empty":ExpansionName(view.ModuleAt(index)))+" → "+(preset[index-1]==null?"empty":ExpansionName(preset[index-1]))));
            string description=preset==null&&actualAction!="boost"?ExpansionDescription(id)+"\n":"";
            expansionBody.text=description+ExpansionEffect(id,view)+replacement+(cost==null?"":"\nCost: "+cost+".")+"\n"+(status??available);
            if(id.StartsWith("trade.",StringComparison.Ordinal))expansionBody.text+=ExpansionTradeGoal(state,quote);
            if(quote!=null&&quote.Action=="buy"&&quote.ExpectedSequence==state.sequence&&
                ExpansionCatalog.Get(quote.Id).Kind!="trade"&&!ExpansionOwned(ExpansionCatalog.Get(quote.Id),view))
                expansionBody.text+="\n"+ExpansionRemainingAfterPurchase(state,view,quote);
            if(quote!=null&&quote.ExpectedSequence==state.sequence&&(quote.Action=="equip"||quote.Action=="preset"))
            {
                IReadOnlyList<string> next=preset;
                if(quote.Action=="equip")
                {var replacementSlots=Enumerable.Range(1,3).Select(view.ModuleAt).ToArray();replacementSlots[quote.Slot-1]=quote.Id=="module.none"?null:quote.Id;next=replacementSlots;}
                expansionBody.text+="\n"+ExpansionModuleComparison(ExpansionEconomy.ModuleBonuses(view),ExpansionEconomy.ModuleBonuses(view,next),false).Replace("Module bonuses\n","<b>Module bonuses</b>\n");
                if(quote.Action=="preset"&&quote.Id==ExpansionCatalog.PresetEnergy&&!view.OwnsModule(ExpansionCatalog.Dynamo))
                    expansionBody.text+="\nDynamo not owned; module Energy stays +0. Ordinary Energy is separate.";
            }
            if(actualAction=="boost")expansionBody.text+="\n\n"+ExpansionBoostContext(state,view);
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        static string ExpansionModuleComparison(ExpansionModuleBonuses before,ExpansionModuleBonuses after,bool applied)
        {
            var rows=new List<string>();
            void Row(string name,int oldValue,int newValue){if(oldValue!=newValue)rows.Add($"{name}: +{oldValue} → +{newValue}");}
            Row("Parts per Trash",before.PartsPerTrash,after.PartsPerTrash);Row("Parts per Box",before.PartsPerBox,after.PartsPerBox);
            Row("Potatoes per Potato",before.PotatoesPerPotato,after.PotatoesPerPotato);
            Row("Parts per matching spin",before.PartsPerMatchingSpin,after.PartsPerMatchingSpin);
            Row("Potatoes per matching spin",before.PotatoesPerMatchingSpin,after.PotatoesPerMatchingSpin);
            Row("Energy per Pigeon spin",before.EnergyPerPigeonSpin,after.EnergyPerPigeonSpin);
            if(rows.Count==0)return "Module bonuses\nSlot arrangement "+(applied?"changed":"will change")+"; module production effects stay the same.";
            bool matching=before.PartsPerMatchingSpin!=after.PartsPerMatchingSpin||before.PotatoesPerMatchingSpin!=after.PotatoesPerMatchingSpin;
            bool pigeon=before.EnergyPerPigeonSpin!=after.EnergyPerPigeonSpin;
            return "Module bonuses\n"+string.Join("\n",rows)+"."+
                (matching?"\nMatching spin = pair or triple; bonus once.":"")+
                (pigeon?"\nPigeon spin = one or more Pigeons; bonus once.":"")+
                "\nFuture spins; storage limits apply. Ordinary production is separate.";
        }
        static string ExpansionAppliedModuleChange(ExpansionQuote quote,ModelState before,ExpansionState after)
        {
            if(quote.Action!="equip"&&quote.Action!="preset")return "";
            var previous=ExpansionEconomy.Project(before);
            if(Enumerable.Range(1,3).All(slot=>previous.ModuleAt(slot)==after.ModuleAt(slot)))return "";
            return "\n"+ExpansionModuleComparison(ExpansionEconomy.ModuleBonuses(previous),ExpansionEconomy.ModuleBonuses(after),true)+
                (quote.Action=="equip"&&quote.Id=="module.none"?" You keep the module.":"");
        }
        string ExpansionBoostContext(ModelState state,ExpansionState view)
        {
            // Informational comparison only: retain the displayed Energy quote.
            var offer=ArcadeCatalog.Get(ArcadeCatalog.BonusBoost);bool available=false;
            if(!view.BoostArmed)
            {
                try{available=Session.GetArcadeQuote("buy",offer.Id,state.sequence).ExpectedSequence==state.sequence;}
                catch(InvalidOperationException){ }
            }
            string text=view.BoostArmed?"Both methods activate the same boost; they do not stack.":
                $"The same boost costs {ArcadeNumber(offer.DiamondsCost)} diamonds in “Mini-games & Diamonds”.\n"+
                (available?"You can buy it separately and save energy.":"Unavailable for diamonds now.");
            if(view.BonusUnlocked)text+=$"\nUntil bonus: {view.BonusCadence-view.BonusProgress} new spins";
            if(view.Parts==view.PartsCap&&view.Potatoes==view.PotatoesCap)text+="\nParts and potato storage are currently full.";
            return text+"\n"+BonusFocusStorageContext(view);
        }
        static string ExpansionRemainingAfterPurchase(ModelState state,ExpansionState view,ExpansionQuote quote)
        {
            var remaining=new System.Collections.Generic.List<string>();
            if(quote.PartsCost>0)remaining.Add($"{view.Parts-quote.PartsCost} parts");
            if(quote.PotatoesCost>0)remaining.Add($"{view.Potatoes-quote.PotatoesCost} potatoes");
            if(quote.EnergyCost>0)remaining.Add($"{view.Energy-quote.EnergyCost} energy");
            if(quote.CoinsCost>0)remaining.Add($"{state.coins-quote.CoinsCost} Coins");
            return "After purchase: "+string.Join("; ",remaining)+".";
        }
        static string BonusFocusStorageContext(ExpansionState view)
        {
            int partsRoom=view.PartsCap-view.Parts,potatoRoom=view.PotatoesCap-view.Potatoes;
            string focus=view.BonusFocusId==ExpansionCatalog.FocusParts?"Parts":view.BonusFocusId==ExpansionCatalog.FocusPotatoes?"Potatoes":"Balanced";
            string text=$"Saved focus: {focus}. Room now: {partsRoom} parts; {potatoRoom} potatoes.";
            bool focusedFull=view.BonusFocusId==ExpansionCatalog.FocusParts&&partsRoom==0&&potatoRoom>0||
                view.BonusFocusId==ExpansionCatalog.FocusPotatoes&&potatoRoom==0&&partsRoom>0;
            if(focusedFull)text+=" If unchanged until the bonus, it can credit 0 materials and consume the charge.";
            else if(partsRoom>0||potatoRoom>0)text+=" Ordinary earnings can fill that room before the bonus.";
            return text+" Spin income is separate.";
        }
        void ShowExpansionModuleSockets(string id)
        {
            if(!ExpansionCanOpen())return;var view=Session.Expansion;
            BeginExpansion(ExpansionSocket,"Where should the module be installed?",view);expansionCategory=0;expansionModuleToEquip=id;
            SharedHeading(expansionTitle,"icon-sockets");
            for(int i=0;i<3;i++)
            {
                int slot=i+1;string current=slot<=view.SocketCount?view.ModuleAt(slot):null;
                ExpansionCard(i,null,3,slot,$"Slot {slot}\n"+(slot>view.SocketCount?"Locked · inspect price":current==null?"Empty · install":ExpansionName(current)+"\nReplace explicitly"));
            }
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        void ShowExpansionSocket(int slot,string module=null)
        {
            if(slot<1||slot>3||!ExpansionCanOpen())return;
            var view=Session.Expansion;
            if(slot>view.SocketCount)
            {
                ShowExpansionOffer("socket."+slot,module==null?1:0,0,module==null?ExpansionCategory:ExpansionSocket);
                if(module!=null&&wavePanel==ExpansionOffer)expansionModuleToEquip=module;
                return;
            }
            if(module!=null){ShowExpansionOffer(module,0,0,ExpansionSocket,slot,"equip");return;}
            string current=view.ModuleAt(slot);var owned=ExpansionCatalog.Offers.Where(o=>o.Kind=="module"&&view.OwnsModule(o.Id)).ToArray();
            BeginExpansion(ExpansionSocket,"Slot "+slot,view);expansionCategory=1;expansionSocket=slot;
            SharedHeading(expansionTitle,"icon-sockets");
            for(int i=0;i<owned.Length&&i<6;i++)
            {
                int placed=ExpansionModuleSlot(view,owned[i].Id);
                ExpansionCard(i,owned[i].Id,4,slot,ExpansionName(owned[i].Id)+"\n"+(placed==slot?"Installed here":placed>0?"Installed in slot "+placed:"Owned · install"));
            }
            if(owned.Length==0)
            {expansionBody.gameObject.SetActive(true);expansionBody.text="No owned modules yet. Open “Modules”; purchase and installation are separate actions.";}
            if(current!=null)ExpansionAction(expansionSecondaryButton,6,"Remove module");
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        void PreviousExpansionPage()
        {if(expansionContextValid&&SectionAction(expansionPreviousButton,ExpansionCategory)&&expansionPage>0)ShowExpansionCategory(expansionCategory,expansionPage-1);}
        void NextExpansionPage()
        {
            if(!expansionContextValid||!SectionAction(expansionNextButton,ExpansionCategory))return;
            var state=Session.State;int pages=(ExpansionVisibleCategoryIds(expansionCategory,state,ExpansionEconomy.Project(state)).Count+5)/6;
            if(expansionPage+1<pages)ShowExpansionCategory(expansionCategory,expansionPage+1);
        }
        void ExpansionPrimary()=>InvokeExpansionAction(expansionPrimaryButton,expansionPrimaryAction);
        void ExpansionSecondary()=>InvokeExpansionAction(expansionSecondaryButton,expansionSecondaryAction);
        void InvokeExpansionAction(UnityEngine.UI.Button button,int action)
        {
            // Result shortcuts are bound to their displayed immutable context,
            // not this reusable button's old generic event. Only the bound event acts.
            if(action==7||action==8)return;
            if(!expansionContextValid||!IsExpansionPanel()||!SectionAction(button,wavePanel))return;
            if((action==2||action==11)&&wavePanel==ExpansionResult&&expansionResultActionKey!=null&&Session.State.sequence!=expansionResultSequence)return;
            if(action==1)ExecuteExpansionQuote();
            else if(action==2)ShowExpansionModuleSockets(expansionInspectedId);
            else if(action==3)ShowExpansionSocket(expansionSocket);
            else if(action==5)ShowExpansionInformation();
            else if(action==6)ShowExpansionOffer("module.none",1,0,ExpansionSocket,expansionSocket,"equip");
            else if(action==9&&wavePanel==ExpansionCategory&&ExpansionFilterCategory(expansionCategory))
            {expansionOwnedCategory=-1;expansionFilteredCategory=expansionFilteredCategory==expansionCategory?-1:expansionCategory;ShowExpansionCategory(expansionCategory);}
            else if(action==10&&wavePanel==ExpansionCategory&&(expansionCategory==0||expansionCategory==2))
            {expansionFilteredCategory=-1;expansionOwnedCategory=expansionOwnedCategory==expansionCategory?-1:expansionCategory;ShowExpansionCategory(expansionCategory);}
            else if(action==11)ShowExpansionCategory(5);
        }
        void ShowExpansionInformation()
        {
            if(!ExpansionCanOpen())return;
            var state=Session.State;var view=ExpansionEconomy.Project(state);
            BeginExpansion(ExpansionResult,"Resources & setup",view);expansionReturnMode=ExpansionHome;
            string loadout=string.Join("\n",Enumerable.Range(1,3).Select(index=>$"Slot {index}: "+(index>view.SocketCount?"locked":view.ModuleAt(index)==null?"empty":ExpansionName(view.ModuleAt(index)))));
            string equipment=string.Join("\n",Enumerable.Range(0,3).Select(row=>string.Join(" · ",FamilyIds.All.Skip(row*2).Take(2).Select(f=>FamilyName(f)+" "+view.EquipmentLevel(f)+"/2"))));
            var unlock=ExpansionCatalog.Get(ExpansionCatalog.BonusUnlock);
            string missing=ExpansionMissing(state,view,unlock.PartsCost,unlock.PotatoesCost,unlock.EnergyCost,unlock.CoinsCost);
            string bonus=$"<b>Workbench {view.BonusWorkbenchLevel}/2</b> · focus: {ExpansionName(view.BonusFocusId)}.\n"+
                (view.BonusUnlocked?$"{view.BonusCadence-view.BonusProgress} spins until bonus":missing!=null?missing+" to unlock the bonus reel.":"The bonus reel can be unlocked in “Bonus & Energy”.");
            expansionBody.gameObject.SetActive(true);expansionBody.text="Spins are free. Parts, potatoes and energy do not accumulate while away.\nBase: parts come from trash and boxes; potatoes from their symbols.\nEnergy comes from spins and powers boosts; modules and equipment add their own effects.\n"+
                loadout+"\n<b>Equipment (separate from the chapter):</b>\n"+equipment+"\n"+bonus;
            var spinReceipt=ArcadeEconomy.Project(state).LastResourceReceipt;
            if(state.lastSpin!=null&&spinReceipt!=null&&spinReceipt.Kind=="spin"&&spinReceipt.Sequence==state.lastSpin.sequence)
                expansionBody.text+=$"\nLast spin · credited: +{spinReceipt.PartsAdded} parts, +{spinReceipt.PotatoesAdded} potatoes.\n"+
                    $"Including slot: +{spinReceipt.SlotPartsIncluded} parts, +{spinReceipt.SlotPotatoesIncluded} potatoes. Already counted.";
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        void ExecuteExpansionQuote()
        {
            var quote=expansionQuote;if(quote==null)return;
            int category=expansionCategory,page=expansionPage,slot=expansionSocket,returnMode=expansionReturnMode;
            int filteredCategory=expansionFilteredCategory;
            int ownedCategory=expansionOwnedCategory;
            string socketModule=returnMode==ExpansionSocket&&category==0&&quote.Action=="buy"&&quote.Id.StartsWith("socket.",StringComparison.Ordinal)?expansionModuleToEquip:null;
            var before=Session.State;
            // Retain the immutable request locally; Hide invalidates every UI action.
            HideWaveChoices();
            try
            {
                var after=Session.ExecuteExpansion(quote,UtcSeconds());var view=ExpansionEconomy.Project(after);var receipt=view.LastCommand;
                bool fresh=after.sequence>before.sequence&&after.sequence==quote.Sequence&&receipt!=null&&receipt.Sequence==after.sequence&&
                    receipt.Key==quote.Key&&receipt.Action==quote.Action&&receipt.Id==quote.Id&&receipt.Slot==quote.Slot;
                if(fresh)ObserveCommitted(before,after);
                if(fresh&&quote.Action=="speed")
                {ShowExpansionHome();Render("Speed saved. Spins remain free.");return;}
                BeginExpansion(ExpansionResult,fresh?"Action saved":"No new action",view);
                expansionCategory=category;expansionPage=page;expansionSocket=slot;expansionReturnMode=returnMode;
                expansionFilteredCategory=filteredCategory;
                expansionOwnedCategory=ownedCategory;
                if(fresh&&socketModule!=null)expansionModuleToEquip=socketModule;
                expansionBody.gameObject.SetActive(true);
                expansionBody.text=fresh?ExpansionCommittedText(quote,before,after,view):"No new charge or credit was confirmed. Reopen the category.";
                if(fresh&&quote.Action=="buy"&&ExpansionCatalog.Get(quote.Id).Kind=="trade")PrepareNextExpansionTrade(quote.Id,after,view);
                else if(fresh&&quote.Action=="buy")PrepareExpansionPurchaseUse(quote,after,view);
                closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
                Render(fresh?"Shop action saved.":"No new shop action.");
            }
            catch(Exception error)
            {
                if(Session.RecoveryRequired){ObserveOperationFailure();Error(error.Message);return;}
                BeginExpansion(ExpansionResult,"Offer changed",Session.Expansion);expansionCategory=category;expansionPage=page;expansionSocket=slot;expansionReturnMode=returnMode;
                expansionFilteredCategory=filteredCategory;
                expansionOwnedCategory=ownedCategory;
                expansionBody.gameObject.SetActive(true);expansionBody.text="Action not performed. Resources or requirements changed. Reopen the offer; there is no automatic retry.";
                closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;Render("Offer changed. Check your resources.");
            }
        }
        void PrepareNextExpansionTrade(string id,ModelState confirmed,ExpansionState view)
        {
            expansionBody.text+=ExpansionTradeGoal(confirmed);
            var offer=ExpansionCatalog.Get(id);ExpansionQuote next;
            try{next=Session.GetExpansionOffer(id);}
            catch(InvalidOperationException)
            {
                expansionBody.text+="\nNext exchange: "+ExpansionCostText(offer.PartsCost,offer.PotatoesCost,offer.EnergyCost,offer.CoinsCost)+" → "+offer.CoinsReward+" coins.\n"+
                    (ExpansionMissing(confirmed,view,offer.PartsCost,offer.PotatoesCost,offer.EnergyCost,offer.CoinsCost)??"Unavailable now. Reopen the category.");
                return;
            }
            if(next.ExpectedSequence!=confirmed.sequence)
            {expansionBody.text+="\nState changed. Reopen the category for the next exchange.";return;}
            expansionQuote=next;
            expansionBody.text+="\nNext exchange: "+ExpansionQuoteCost(next)+" → "+next.CoinsReward+" coins. Only a separate tap will perform it. The selected construction will not be paid for.";
            ExpansionAction(expansionSecondaryButton,7,"More "+ExpansionQuoteCost(next)+"\n→ "+next.CoinsReward+" coins");

            // Keep the original event (including authored listeners) intact. Each
            // repeat gets a new event object with a frozen context; queued old
            // event references cannot spend the next displayed quote.
            expansionTradeBaseSecondaryEvent=expansionSecondaryButton.onClick;
            var consentEvent=new UnityEngine.UI.Button.ButtonClickedEvent();
            string key=next.Key;long sequence=next.ExpectedSequence;var session=Session;
            consentEvent.AddListener(()=>
            {
                if(!ReferenceEquals(expansionSecondaryButton.onClick,consentEvent)||!ReferenceEquals(Session,session)||wavePanel!=ExpansionResult||expansionSecondaryAction!=7||expansionQuote==null||
                    expansionQuote.Key!=key||expansionQuote.ExpectedSequence!=sequence)return;
                InvokeExpansionAction(expansionSecondaryButton,1);
            });
            expansionSecondaryButton.onClick=consentEvent;
            InvalidateExpansionConsent(expansionSecondaryButton);
        }
        string ExpansionTradeGoal(ModelState state,ExpansionQuote quote=null)
        {
            var chapter=ChapterEconomy.Project(state);
            if(chapter.SelectedProjectId==null)return "";
            long price=ChapterEconomy.SelectedProjectPrice(chapter),shortage=Math.Max(0,price-state.coins);
            string current=shortage>0?$"Need {shortage} more "+(shortage==1?"coin.":"coins."):"Funded, unpaid · Slot → SPIN.";
            if(quote!=null&&quote.ExpectedSequence==state.sequence&&quote.Action=="buy"&&ExpansionCatalog.Get(quote.Id).Kind=="trade")
            {
                long remaining=Math.Max(0,price-checked(state.coins-quote.CoinsCost+quote.CoinsReward));
                current+=" After exchange: "+(remaining>0?$"need {remaining} "+(remaining==1?"coin.":"coins."):"funded, unpaid · Slot → SPIN.");
            }
            return $"\nGoal: {ProjectName(chapter.SelectedProjectId)} · saved {price} coins.\n"+current;
        }
        void PrepareExpansionPurchaseUse(ExpansionQuote purchased,ModelState confirmed,ExpansionState view)
        {
            var offer=ExpansionCatalog.Get(purchased.Id);
            if(offer.Kind=="module"&&view.OwnsModule(purchased.Id)&&ExpansionModuleSlot(view,purchased.Id)==0)
            {
                expansionInspectedId=purchased.Id;
                expansionBody.text+="\nYou can choose a slot separately. This does not install the module yet.";
                BindExpansionPurchaseUse(purchased.Key,confirmed.sequence,2,"Choose a slot");
            }
            else if(offer.Kind=="workbench"&&offer.Level==1&&view.BonusWorkbenchLevel>=1)
            {
                expansionBody.text+="\nThe workbench unlocked bonus focuses. Inspect the options; the free selection is separate.";
                BindExpansionPurchaseUse(purchased.Key,confirmed.sequence,11,"Inspect focuses");
            }
            else if(offer.Kind=="theme"&&view.OwnsTheme(purchased.Id)&&view.ThemeId!=purchased.Id)
            {
                ExpansionQuote apply;
                try{apply=Session.GetExpansionAction("theme",purchased.Id);}
                catch(InvalidOperationException){expansionBody.text+="\nReopen Appearance to apply.";return;}
                if(apply.ExpectedSequence!=confirmed.sequence)
                {expansionBody.text+="\nState changed. Reopen Appearance to apply.";return;}
                expansionQuote=apply;
                expansionBody.text+="\nApply: "+ExpansionQuoteCost(apply)+". Only a separate tap will change the palette.";
                BindExpansionPurchaseUse(apply.Key,confirmed.sequence,1,"Apply\n"+ExpansionQuoteCost(apply));
            }
        }
        void BindExpansionPurchaseUse(string key,long confirmedSequence,int action,string caption)
        {
            expansionResultActionKey=key;
            expansionResultSequence=confirmedSequence;
            ExpansionAction(expansionPrimaryButton,8,caption);
            expansionResultBasePrimaryEvent=expansionPrimaryButton.onClick;
            var consentEvent=new UnityEngine.UI.Button.ButtonClickedEvent();
            consentEvent.AddListener(()=>
            {
                if(!ReferenceEquals(expansionPrimaryButton.onClick,consentEvent)||wavePanel!=ExpansionResult||expansionPrimaryAction!=8||expansionResultActionKey!=key)return;
                if(action==1&&(expansionQuote==null||expansionQuote.Key!=key))return;
                InvokeExpansionAction(expansionPrimaryButton,action);
            });
            expansionPrimaryButton.onClick=consentEvent;
        }
        void RestoreExpansionResultEvents()
        {
            if(expansionTradeBaseSecondaryEvent!=null)
            {
                if(expansionSecondaryButton!=null)expansionSecondaryButton.onClick=expansionTradeBaseSecondaryEvent;
                expansionTradeBaseSecondaryEvent=null;
            }
            if(expansionResultBasePrimaryEvent!=null)
            {
                if(expansionPrimaryButton!=null)expansionPrimaryButton.onClick=expansionResultBasePrimaryEvent;
                expansionResultBasePrimaryEvent=null;
            }
            expansionResultActionKey=null;
            expansionResultSequence=0;
        }
        string ExpansionCommittedText(ExpansionQuote quote,ModelState before,ModelState after,ExpansionState view)
        {
            string action=quote.Action=="equip"?(quote.Id=="module.none"?"Module removed; ownership retained.":$"Module installed in slot {quote.Slot}."):
                quote.Action=="theme"?"Palette applied.":quote.Action=="boost"?"Boost activated for the next bonus.":
                quote.Action=="focus"?"Selected focus: "+ExpansionName(quote.Id)+".":quote.Action=="preset"?"Modules in unlocked slots replaced.":
                quote.Action=="buy"&&ExpansionCatalog.Get(quote.Id).Kind=="trade"?"Exchange completed.":"Purchased: "+ExpansionName(quote.Id)+".";
            return action+"\nSpent: "+ExpansionQuoteCost(quote)+"."+(quote.CoinsReward>0?$"\nCredited: {quote.CoinsReward} coins.":"")+
                $"\nCoins: {before.coins} → {after.coins}.\nParts: {view.Parts}; potatoes: {view.Potatoes}; energy: {view.Energy}."+
                (quote.Action=="buy"&&quote.Id.StartsWith("module.",StringComparison.Ordinal)?"\nInstalling the module is a separate action.":"")+
                ExpansionAcquiredEffect(quote,before,view)+ExpansionAppliedModuleChange(quote,before,view);
        }
        static string ExpansionAcquiredEffect(ExpansionQuote quote,ModelState before,ExpansionState after)
        {
            if(quote.Action!="buy")return "";
            var offer=ExpansionCatalog.Get(quote.Id);var previous=ExpansionEconomy.Project(before);
            if(offer.Kind=="equipment")
            {
                int oldLevel=previous.EquipmentLevel(offer.FamilyId),level=after.EquipmentLevel(offer.FamilyId);
                if(level!=oldLevel+1||level!=offer.Level)return "";
                if(offer.FamilyId==FamilyIds.House)return $"\nParts/Potatoes capacity: {previous.PartsCap}/{previous.PotatoesCap} → {after.PartsCap}/{after.PotatoesCap}; no refill.";
                if(offer.FamilyId==FamilyIds.PigeonLoft)return $"\nEnergy capacity: {previous.EnergyCap} → {after.EnergyCap}; no refill.";
                string resource=offer.FamilyId==FamilyIds.PowerSource?"energy":"parts";
                string trigger=offer.FamilyId==FamilyIds.Recycler?"Trash symbol":offer.FamilyId==FamilyIds.Cart?"Box symbol":"spin";
                return $"\nEquipment: +{oldLevel} → +{level} {resource} per future {trigger}; caps apply.";
            }
            if(offer.Kind=="yield"&&after.PotatoYieldLevel==previous.PotatoYieldLevel+1&&after.PotatoYieldLevel==offer.Level)
                return $"\nPotato yield: +{previous.PotatoYieldLevel} → +{after.PotatoYieldLevel} per future Potato symbol; caps apply.";
            if(offer.Kind=="workbench"&&offer.Level==2&&previous.BonusWorkbenchLevel==1&&after.BonusWorkbenchLevel==2)
                return $"\nWorkbench raw addition: 0/0 → +{EconomyRules.ExpansionWorkbenchBonusPerResource}/+{EconomyRules.ExpansionWorkbenchBonusPerResource} Parts/Potatoes per future bonus before focus, boost and caps.";
            if(offer.Kind=="bonus"&&!previous.BonusUnlocked&&after.BonusUnlocked)
                return $"\nUnlocked: resource bonus every {after.BonusCadence} new spins; no extra Coins.";
            return "";
        }
        bool TryReturnExpansion()
        {
            if(!IsExpansionPanel()||!expansionContextValid||!ExpansionCanOpen()||offlineSummaryPanel||autoHintPanel||exitPanel)return false;
            int mode=wavePanel,category=expansionCategory,page=expansionPage,slot=expansionSocket,returnMode=expansionReturnMode;
            string module=expansionModuleToEquip;
            if(mode==ExpansionHome)ShowSectionsHub();
            else if(mode==ExpansionCategory)ShowExpansionHome();
            else if(mode==ExpansionSocket)ShowExpansionCategory(category,page);
            else if(returnMode==ExpansionHome)ShowExpansionHome();
            else if(returnMode==ExpansionSocket)
            {if(module!=null&&module!="module.none")ShowExpansionModuleSockets(module);else if(slot>0)ShowExpansionSocket(slot);else ShowExpansionCategory(category,page);}
            else ShowExpansionCategory(category,page);
            return true;
        }
        void RenderExpansionHud(ModelState state)
        {
            if(!expansionWired||Session==null||state==null||failed||Session.RecoveryRequired)return;
            // A spin commits before its normal visual presentation. Never project
            // that after-state, its resources, bonus symbol or stored palette here.
            if(Busy)
            {
                expansionPresentationPending=true;
                expansionQuickBoostQuote=null;expansionQuickBoostNeedsRefresh=true;expansionQuickBoostButton.interactable=false;InvalidateExpansionConsent(expansionQuickBoostButton);
                if(expansionBonusFrame.gameObject.activeSelf)expansionBonusResult.text="…";
                return;
            }
            if(InBackground)
            {expansionQuickBoostQuote=null;expansionQuickBoostNeedsRefresh=true;expansionQuickBoostButton.interactable=false;return;}
            bool newSession=!ReferenceEquals(expansionHudSession,Session);
            if(newSession)
            {
                expansionHudSession=Session;expansionHudSequence=-1;expansionPresentationPending=false;expansionPresentedReceipt=0;
            }
            if(state.sequence==expansionHudSequence&&!expansionPresentationPending&&!expansionQuickBoostNeedsRefresh)return;
            var view=ExpansionEconomy.Project(state);var receipt=view.LastResourceReceipt;
            bool newPresented=expansionPresentationPending&&!newSession&&receipt!=null&&receipt.Sequence>expansionPresentedReceipt;
            expansionHudResources.text=$"Parts {view.Parts}/{view.PartsCap} · potatoes {view.Potatoes}/{view.PotatoesCap}\nEnergy {view.Energy}/{view.EnergyCap} · free spins";
            ApplyExpansionReelLayout(view.BonusUnlocked);
            CacheExpansionQuickBoost(state,view);
            if(view.BonusUnlocked)
            {
                expansionBonusProgress.text=$"{view.BonusCadence-view.BonusProgress} spins left";
                expansionBonusResult.text=newPresented&&receipt.BonusSymbol>=0?
                    $"Parts +{receipt.BonusParts}\nPotatoes +{receipt.BonusPotatoes}"+(receipt.Boosted?(receipt.BonusParts==0&&receipt.BonusPotatoes==0?"\nBoost used":"\n×2 included"):""):
                    view.BoostArmed?"Boost\nwaiting":"Resources\nper bonus";
            }
            ApplyExpansionPalette(view.ThemeId);
            if(receipt!=null)expansionPresentedReceipt=receipt.Sequence;
            expansionHudSequence=state.sequence;expansionPresentationPending=false;
        }
        void CacheExpansionQuickBoost(ModelState state,ExpansionState view)
        {
            ExpansionQuote quote=null;
            if(view.BonusUnlocked&&!view.BoostArmed&&view.Energy>=EconomyRules.ExpansionBoostCost&&!ChapterEconomy.Project(state).TransitionPending)
            {try{quote=ExpansionEconomy.Quote(state,"boost",ExpansionCatalog.NextBonusBoost);}catch(InvalidOperationException){}}
            if(expansionQuickBoostQuote?.Key!=quote?.Key)InvalidateExpansionConsent(expansionQuickBoostButton);
            expansionQuickBoostQuote=quote;expansionQuickBoostNeedsRefresh=false;
            expansionQuickBoostButton.GetComponentInChildren<TMP_Text>(true).text=!view.BonusUnlocked?"Bonus":view.BoostArmed?"Boost\nactive":
                quote!=null?"Boost\n"+EconomyRules.ExpansionBoostCost+" energy":view.Energy<EconomyRules.ExpansionBoostCost?
                "Need "+EconomyRules.ExpansionBoostCost+"\nenergy":"Boost\nunavailable";
        }
        // Product chrome calls this on layout and navigation changes; Busy keeps
        // the same visible target disabled rather than exposing a gap-only action.
        void RenderProductQuickBoost(bool rootVisible,bool usable)
        {
            if(expansionQuickBoostButton==null||productChrome==null)return;
            var rect=(RectTransform)expansionQuickBoostButton.transform;
            if(rect.parent!=productChrome)
            {
                var original=expansionQuickBoostButton.GetComponent<UnityEngine.UI.Image>();
                var tint=original==null?ColorOf("F4B85C"):original.color;
                rect.SetParent(productChrome,false);
                expansionQuickBoostButton.targetGraphic=Surface(expansionQuickBoostButton.gameObject,tint,18,true);
                var label=expansionQuickBoostButton.GetComponentInChildren<TMP_Text>(true);label.color=ColorOf("263C50");
            }
            // An authored button is already under the chrome; keep it above the goal plaque either way.
            Place(rect,productChrome,new Vector2(.82f,.581f),new Vector2(.97f,.658f));
            bool visible=rootVisible&&!failed&&!InBackground&&Session!=null&&!Session.RecoveryRequired&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel;
            expansionQuickBoostButton.gameObject.SetActive(visible);
            expansionQuickBoostButton.interactable=visible&&usable&&!Busy&&expansionQuickBoostQuote!=null;
            StyleCQuickBoost();
            if(!visible)InvalidateExpansionConsent(expansionQuickBoostButton);
        }
        void UseExpansionQuickBoost()
        {
            if(!CanOpenChapterPanel()||Session.RecoveryRequired||Session.Chapter.TransitionPending||panel.activeSelf||
                !expansionQuickBoostButton.isActiveAndEnabled||!expansionQuickBoostButton.IsInteractable()||!PointerConsentAllows(expansionQuickBoostButton))return;
            var quote=expansionQuickBoostQuote;if(quote==null)return;
            var before=Session.State;expansionQuickBoostQuote=null;expansionQuickBoostButton.interactable=false;expansionQuickBoostNeedsRefresh=true;
            InvalidateExpansionConsent(expansionQuickBoostButton);
            if(quote.ExpectedSequence!=before.sequence){Render("Boost offer changed. Check energy and tap again.");return;}
            try
            {
                var after=Session.ExecuteExpansion(quote,UtcSeconds());var receipt=ExpansionEconomy.Project(after).LastCommand;
                bool fresh=after.sequence>before.sequence&&after.sequence==quote.Sequence&&receipt!=null&&receipt.Sequence==after.sequence&&
                    receipt.Key==quote.Key&&receipt.Action=="boost"&&receipt.Id==quote.Id&&receipt.Slot==0;
                if(fresh)ObserveCommitted(before,after);
                Render(fresh?"Boost activated for the next bonus.":"No new boost. Check the current state.");
            }
            catch(Exception error)
            {
                if(Session.RecoveryRequired){ObserveOperationFailure();Error(error.Message);}
                else Render("Boost not activated. Energy or requirements changed.");
            }
        }
        void ApplyExpansionReelLayout(bool unlocked)
        {
            if(!expansionReelLayoutApplied||unlocked!=expansionReelUnlocked)
            {
                // The taller product slot block lets the four narrow cards use its height.
                float low=productChrome!=null?.45f:.51f,high=productChrome!=null?.92f:.88f;
                for(int i=0;i<3;i++)
                {
                    var frame=expansionBaseReels[i];
                    frame.anchorMin=unlocked?new Vector2(.05f+i*.24f,low):expansionBaseReelMin[i];
                    frame.anchorMax=unlocked?new Vector2(.27f+i*.24f,high):expansionBaseReelMax[i];
                }
                expansionBonusFrame.anchorMin=new Vector2(.78f,low);expansionBonusFrame.anchorMax=new Vector2(.95f,high);
                expansionReelLayoutApplied=true;expansionReelUnlocked=unlocked;
                message.rectTransform.anchorMin=expansionMessageMin;
                message.rectTransform.anchorMax=expansionMessageMax;
                message.enableAutoSizing=unlocked||expansionMessageAutoSize;
                message.fontSize=expansionMessageFont;message.fontSizeMin=unlocked?10:expansionMessageFontMin;message.fontSizeMax=unlocked?Math.Max(10,expansionMessageFont):expansionMessageFontMax;
            }
            expansionBonusFrame.gameObject.SetActive(unlocked);
        }
        void ApplyExpansionPalette(string id)
        {
            if(expansionAppliedTheme==id)return;
            expansionAppliedTheme=id;
            // Flat tints only; no bitmap, package, odds or reward changes.
            Color accent=ColorOf(id==ExpansionCatalog.Mint?"9CB77B":id==ExpansionCatalog.Violet?"A98CC7":id==ExpansionCatalog.Amber?"C19B64":"F4B85C");
            bool pastel=id=="theme.frost"||id=="theme.rose";
            if(pastel)accent=ColorOf(id=="theme.frost"?"ABD2D8":"DDA7B6");
            for(int i=0;i<3;i++)expansionBaseReels[i].GetComponent<UnityEngine.UI.Image>().color=pastel?ColorOf(id=="theme.frost"?"E2F2F3":"F8E5EA"):expansionBaseReelColors[i];
            spinButton.GetComponent<UnityEngine.UI.Image>().color=accent;
            if(spinButton.targetGraphic!=null)spinButton.targetGraphic.color=accent;
            if(spinButton.targetGraphic is JtnProductSurface art&&art.sprite!=null)art.ArtworkTint=id==ExpansionCatalog.DefaultTheme?Color.white:accent;
            foreach(var button in new[]{expansionPrimaryButton,expansionSecondaryButton,expansionPreviousButton,expansionNextButton,expansionEntryButton,expansionQuickBoostButton})
            {
                button.GetComponent<UnityEngine.UI.Image>().color=accent;
                if(button.targetGraphic!=null)button.targetGraphic.color=button==expansionPrimaryButton||button==expansionEntryButton||button==expansionQuickBoostButton?accent:ColorOf("263C50");
            }
            expansionBonusSurface.color=id==ExpansionCatalog.DefaultTheme?ColorOf("F2EDDC"):accent;
            expansionBonusFrame.Find("BonusCaption").GetComponent<TMP_Text>().color=ColorOf(id==ExpansionCatalog.DefaultTheme?"795019":"142638");
            expansionBonusResult.color=ColorOf(id==ExpansionCatalog.DefaultTheme?"263C50":"142638");
            expansionBonusProgress.color=ColorOf(id==ExpansionCatalog.DefaultTheme?"426268":"142638");
        }
        void OpenExpansionCard(int index)
        {
            if(index<0||index>=6||!expansionCardAvailable[index]||!expansionContextValid||!IsExpansionPanel()||!SectionAction(expansionCards[index],wavePanel))return;
            int kind=expansionCardKinds[index],value=expansionCardValues[index],category=expansionCategory,page=expansionPage;
            string id=expansionCardIds[index],module=expansionModuleToEquip;
            if(kind==1)ShowExpansionCategory(value);
            else if(kind==2)ShowExpansionOffer(id,category,page);
            else if(kind==3)ShowExpansionSocket(value,module);
            else if(kind==4)ShowExpansionOffer(id,category,page,ExpansionSocket,expansionSocket,"equip");
        }
        void InvalidateExpansionUi()
        {
            InvalidateExpansionBatch();
            RestoreExpansionResultEvents();
            if(expansionPrimaryButton!=null&&expansionBasePrimaryActionEvent!=null)expansionPrimaryButton.onClick=expansionBasePrimaryActionEvent;
            if(expansionSecondaryButton!=null&&expansionBaseSecondaryActionEvent!=null)expansionSecondaryButton.onClick=expansionBaseSecondaryActionEvent;
            if(!expansionChangingPanel){expansionFilteredCategory=-1;expansionOwnedCategory=-1;}
            expansionContextValid=false;expansionQuote=null;expansionPrimaryAction=expansionSecondaryAction=0;
            expansionQuickBoostQuote=null;expansionQuickBoostNeedsRefresh=true;
            if(expansionQuickBoostButton!=null)expansionQuickBoostButton.interactable=false;
            expansionInspectedId=null;expansionModuleToEquip=null;
            expansionCategory=expansionPage=expansionSocket=0;expansionReturnMode=ExpansionCategory;
            if(expansionRoot!=null)expansionRoot.gameObject.SetActive(false);
            if(expansionEntryButton!=null)expansionEntryButton.gameObject.SetActive(false);
            if(panelTitle!=null)panelTitle.enabled=true;if(panelBody!=null)panelBody.enabled=true;
            for(int i=0;i<6;i++)expansionCardAvailable[i]=false;
            if(expansionCards!=null)foreach(var button in expansionCards)InvalidateExpansionConsent(button);
            foreach(var button in new[]{expansionEntryButton,expansionShopButton,expansionQuickBoostButton,expansionPreviousButton,expansionNextButton,expansionPrimaryButton,expansionSecondaryButton})InvalidateExpansionConsent(button);
        }
        static void InvalidateExpansionConsent(UnityEngine.UI.Button button)
        {if(button!=null&&button.TryGetComponent<JtnPanelPointerConsent>(out var consent))consent.Invalidate();}
        void RenderExpansionVisibility(bool usable)
        {
            if(!expansionWired)return;
            expansionEntryButton.gameObject.SetActive(usable&&panel.activeSelf&&wavePanel==SectionHub);
            expansionEntryButton.interactable=usable;
            expansionShopButton.interactable=usable&&!panel.activeSelf;
            bool visible=usable&&panel.activeSelf&&IsExpansionPanel()&&expansionContextValid;
            expansionRoot.gameObject.SetActive(visible);
            for(int i=0;i<6;i++)expansionCards[i].interactable=visible&&expansionCardAvailable[i];
            foreach(var button in new[]{expansionPreviousButton,expansionNextButton,expansionPrimaryButton,expansionSecondaryButton})button.interactable=visible;
            if(usable&&panel.activeSelf&&IsExpansionPanel()&&!expansionContextValid&&!Busy&&!failed&&!InBackground&&
                Session!=null&&!Session.RecoveryRequired&&Session.State!=null&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel)
            {
                panelTitle.enabled=panelBody.enabled=true;
                panelTitle.text="Slot & Shop";
                panelBody.text="This window must be reopened. Tap “Close”, then choose “Sections” → “Slot & Shop”.";
                closeButton.GetComponentInChildren<TMP_Text>(true).text="Close";
            }
        }
    }
}
