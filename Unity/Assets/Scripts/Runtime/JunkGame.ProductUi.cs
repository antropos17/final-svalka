using System;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Glyph=JunkToNeon.JtnProductIcon.Symbol;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int ProductDistrict=70,ProductEvents=71,ProductShop=72,ProductUpgrades=73,ProductEnergy=74,ProductProfile=75,ProductSettings=76;
        [SerializeField] RectTransform productChrome,productHubRoot,productFooter,productGoal,productGoalFill;
        [SerializeField] Button[] productTabs=new Button[5],productCards=new Button[6];
        [SerializeField] Button productAvatar,productSettings,productEnergy,productEventShortcut,productEventStatus,productMapShortcut,productGoalButton;
        [SerializeField] TMP_Text productIdentity,productCoins,productDiamonds,productEnergyText,productContext,productEventBadge,productGoalText,productMaterials;
        bool productWired,productContextValid,productLastPanelVisible,productLastBlocked;
        RectTransform productNarrativeSurface;
        CanvasGroup productSpinSurface;
        RectTransform productRecyclingCue,productPowerCue;
        GameSession productCueSession;
        bool productRecyclingReady,productPowerBuilt;
        long productVersion;
        int productSelectedTab=1,productReturnHub=-1,productReturnBoundary=-1;
        GameSession productRouteSession;
        string productCurrentGoalContext;
        GameSession productSpinReceiptSession;
        int productSpinReceiptGeneration;
        long productSpinReceiptSequence,productSpinReceiptIncome,productSpinReceiptDebit;
        int productSpinReceiptWorkshop;
        string productSpinReceiptPayment,productSpinReceiptExtras;
        bool productSpinReceiptHistorical;
        long productPendingSpinSequence;
        string[] productSpinBeforeText;
        string productSpinBeforeGoalContext;
        Vector2 productSpinBeforeGoalFill;

        static void Place(RectTransform r,Transform parent,Vector2 min,Vector2 max)
        {r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        static JtnProductSurface Surface(GameObject go,Color color,float radius=14,bool target=false)
        {
            var image=go.GetComponent<Image>();
            if(image!=null&&!(image is JtnProductSurface))
            {
                image.enabled=false;
                var child=go.transform.Find("Product Surface") as RectTransform;
                if(child==null)child=Area("Product Surface",go.transform,Vector2.zero,Vector2.one);
                child.SetAsFirstSibling();go=child.gameObject;
            }
            var shape=go.GetComponent<JtnProductSurface>()??go.AddComponent<JtnProductSurface>();shape.color=color;shape.Radius=radius;shape.raycastTarget=target;return shape;
        }
        void Icon(Transform parent,Glyph glyph,Vector2 min,Vector2 max,Color color)
        {var r=Area("Icon "+glyph,parent,min,max);var icon=r.gameObject.AddComponent<JtnProductIcon>();icon.Shape=glyph;icon.color=color;icon.raycastTarget=false;}
        TMP_Text ProductText(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color)
        {var t=Label("",parent,min,max,size,color);t.name=name;t.enableAutoSizing=true;t.fontSizeMin=Mathf.Min(10,size);t.fontSizeMax=size;return t;}
        Button ProductButton(string name,Transform parent,Vector2 min,Vector2 max,Glyph glyph,string caption,bool round=false)
        {
            var r=Area(name,parent,min,max);var b=r.gameObject.AddComponent<Button>();
            b.targetGraphic=Surface(r.gameObject,ColorOf("263C50"),round?100:15,true);AddSectionConsent(b);
            Icon(r,glyph,round?new Vector2(.23f,.32f):new Vector2(.08f,.27f),round?new Vector2(.77f,.87f):new Vector2(.25f,.77f),ColorOf("F8D986"));
            var t=ProductText("Caption",r,round?new Vector2(.02f,.04f):new Vector2(.29f,.09f),round?new Vector2(.98f,.32f):new Vector2(.95f,.91f),round?10:13,ColorOf("F6F1E3"));
            t.text=caption;t.alignment=round?TextAlignmentOptions.Center:TextAlignmentOptions.MidlineLeft;
            var colors=b.colors;colors.highlightedColor=new Color(.82f,.95f,1);colors.pressedColor=new Color(.65f,.81f,.9f);colors.disabledColor=new Color(.65f,.65f,.65f,.8f);b.colors=colors;return b;
        }
        void WireProduct(Button b,Action action)
        {if(Application.isPlaying)b.onClick.AddListener(()=>{if(ProductCanNavigate()&&b.isActiveAndEnabled&&b.IsInteractable()&&PointerConsentAllows(b))action();});}

        // The live Editor authors these ordinary Canvas objects; fallback uses the same layout.
        public void CreateProductUi()
        {
            if(productWired&&Application.isPlaying)return;
            var backdrop=safe.Find("Product Backdrop") as RectTransform;
            if(backdrop==null)backdrop=Area("Product Backdrop",safe,Vector2.zero,Vector2.one);
            Surface(backdrop.gameObject,ColorOf("142638"),0);backdrop.SetAsFirstSibling();
            foreach(var graphic in GetComponentsInChildren<Graphic>(true))
                if(graphic is JtnProductSurface||graphic is JtnProductIcon)
                {
                    if(graphic.GetComponent<CanvasRenderer>()==null)graphic.gameObject.AddComponent<CanvasRenderer>();
                    graphic.SetAllDirty();
                }
            if(productChrome==null)
            {
                productChrome=Area("Product Chrome",safe,Vector2.zero,Vector2.one);
                var header=Area("Player Header",productChrome,new Vector2(.035f,.923f),new Vector2(.965f,.995f));
                productAvatar=ProductButton("Player Avatar",header,Vector2.zero,new Vector2(.16f,1),Glyph.Person,"Profile",true);
                productSettings=ProductButton("Settings",header,new Vector2(.84f,0),Vector2.one,Glyph.Gear,"Settings",true);
                productIdentity=ProductText("Player Identity",header,new Vector2(.19f,.09f),new Vector2(.82f,.95f),17,ColorOf("F6F1E3"));
                var coins=Area("Coin Wallet",productChrome,new Vector2(.035f,.852f),new Vector2(.37f,.914f));Surface(coins.gameObject,ColorOf("263C50"));Icon(coins,Glyph.Spin,new Vector2(.04f,.16f),new Vector2(.24f,.84f),ColorOf("F8D986"));
                productCoins=ProductText("Coins",coins,new Vector2(.28f,.06f),new Vector2(.96f,.94f),16,ColorOf("F8D986"));
                var diamonds=Area("Diamond Wallet",productChrome,new Vector2(.39f,.852f),new Vector2(.65f,.914f));Surface(diamonds.gameObject,ColorOf("263C50"));Icon(diamonds,Glyph.Diamond,new Vector2(.04f,.17f),new Vector2(.28f,.83f),ColorOf("86E2E5"));
                productDiamonds=ProductText("Diamonds",diamonds,new Vector2(.32f,.06f),new Vector2(.96f,.94f),15,ColorOf("86E2E5"));
                productEnergy=ProductButton("Energy Section",productChrome,new Vector2(.67f,.852f),new Vector2(.965f,.914f),Glyph.Energy,"");productEnergyText=productEnergy.GetComponentInChildren<TMP_Text>(true);
                productFooter=Area("Bottom Navigation",productChrome,new Vector2(.02f,.005f),new Vector2(.98f,.102f));Surface(productFooter.gameObject,ColorOf("172B3D"),20);
                string[] captions={"District","Slot","Events","Shop","Upgrades"};Glyph[] glyphs={Glyph.Home,Glyph.Spin,Glyph.Events,Glyph.Shop,Glyph.Upgrade};
                for(int i=0;i<5;i++)productTabs[i]=ProductButton("Tab "+i,productFooter,new Vector2(i*.2f+.012f,.08f),new Vector2((i+1)*.2f-.012f,.94f),glyphs[i],captions[i],true);
                productEventShortcut=ProductButton("Event Shortcut",productChrome,new Vector2(.82f,.625f),new Vector2(.97f,.702f),Glyph.Ticket,"Events",true);
                productEventBadge=ProductText("Event Badge",productChrome,new Vector2(.77f,.701f),new Vector2(.98f,.748f),10,ColorOf("F8D986"));productEventBadge.alignment=TextAlignmentOptions.Center;
                productMapShortcut=ProductButton("Map Shortcut",productChrome,new Vector2(.03f,.625f),new Vector2(.18f,.702f),Glyph.Map,"Map",true);
                productGoal=Area("Current Goal",productChrome,new Vector2(.035f,.448f),new Vector2(.965f,.515f));
                productGoalButton=ProductButton("Goal Details",productGoal,Vector2.zero,Vector2.one,Glyph.Home,"");productGoalText=productGoalButton.GetComponentInChildren<TMP_Text>(true);
                productHubRoot=Area("Product Section",panel.transform,new Vector2(.045f,.18f),new Vector2(.955f,.755f));
                productContext=ProductText("Section Context",productHubRoot,new Vector2(.01f,.77f),new Vector2(.99f,1),13,ColorOf("28434C"));
                for(int i=0;i<6;i++)
                {
                    float left=i%2*.52f,top=.74f-i/2*.25f;
                    productCards[i]=ProductButton("Product Card "+i,productHubRoot,new Vector2(left,top-.23f),new Vector2(left+.48f,top),Glyph.Events,"");
                }
            }
            if(productMaterials==null){productMaterials=ProductText("Materials",productChrome,new Vector2(.20f,.795f),new Vector2(.80f,.835f),11,ColorOf("C8D8DB"));productMaterials.alignment=TextAlignmentOptions.Center;}
            Place(productMaterials.rectTransform,productChrome,new Vector2(.184f,.934f),new Vector2(.816f,.961f));
            if(productEventStatus==null)
            {
                productEventStatus=Button("Events Status","",productChrome,new Vector2(.77f,.704f),new Vector2(.98f,.781f),Color.clear,()=>{});
                productEventStatus.transition=UnityEngine.UI.Selectable.Transition.None;AddSectionConsent(productEventStatus);
            }
            // Separate the full-height target from Events; keep its badge below the narrative.
            Place((RectTransform)productEventStatus.transform,productChrome,new Vector2(.77f,.752f),new Vector2(.98f,.829f));
            Place(productEventBadge.rectTransform,productEventStatus.transform,Vector2.zero,new Vector2(1,47f/77));
            if(productGoalFill==null)
            {
                var track=Area("Goal Track",productGoal,new Vector2(.29f,.025f),new Vector2(.95f,.075f));Surface(track.gameObject,ColorOf("46606A"),2);
                productGoalFill=Area("Goal Fill",track,Vector2.zero,Vector2.one);Surface(productGoalFill.gameObject,ColorOf("F8D986"),2);
            }
            // One cream focus card connects the yard's next choice to its inspection list.
            // Apply to authored objects as well as fallback; keep all hit rectangles intact.
            productGoalButton.targetGraphic=Surface(productGoalButton.gameObject,ColorOf("F2EDDC"),15,true);
            productGoalText.color=ColorOf("142638");
            var goalIcon=productGoalButton.GetComponentInChildren<JtnProductIcon>(true);
            if(goalIcon!=null)goalIcon.color=ColorOf("795019");
            Surface(productGoalFill.parent.gameObject,ColorOf("D7D0BC"),2);
            Surface(productGoalFill.gameObject,ColorOf("426268"),2);
            CreateProductProgressCues();
            WireProduct(productAvatar,()=>ShowProductSection(ProductProfile));WireProduct(productSettings,()=>ShowProductSection(ProductSettings));WireProduct(productEnergy,()=>ShowProductSection(ProductEnergy));
            WireProduct(productEventShortcut,()=>ShowProductSection(ProductEvents));WireProduct(productEventStatus,()=>ShowProductSection(ProductEvents));WireProduct(productMapShortcut,()=>ProductRoute(ShowProgressMap,ProductDistrict));WireProduct(productGoalButton,()=>ProductRoute(ShowProductGoal,ProductDistrict));
            for(int i=0;i<5;i++){int tab=i;WireProduct(productTabs[i],()=>OpenProductTab(tab));}
            ApplyProductLayout();productHubRoot.gameObject.SetActive(false);productWired=Application.isPlaying;
        }
        void CreateProductProgressCues()
        {
            productRecyclingCue=productTabs[4].transform.Find("Affordable Skill") as RectTransform;
            if(productRecyclingCue==null)
            {
                productRecyclingCue=Area("Affordable Skill",productTabs[4].transform,new Vector2(.02f,.74f),new Vector2(.98f,.98f));
                Surface(productRecyclingCue.gameObject,ColorOf("86E2E5"),8);
                var text=ProductText("Skill Ready",productRecyclingCue,Vector2.zero,Vector2.one,10,ColorOf("142638"));
                text.text="Skill ready";text.fontSizeMin=10;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            }
            productPowerCue=productChrome.Find("Power Built") as RectTransform;
            if(productPowerCue==null)
            {
                productPowerCue=Area("Power Built",productChrome,new Vector2(.015f,.704f),new Vector2(.245f,.742f));
                Surface(productPowerCue.gameObject,ColorOf("426268"),8);
                Icon(productPowerCue,Glyph.Energy,new Vector2(.03f,.17f),new Vector2(.20f,.83f),ColorOf("F8D986"));
                var text=ProductText("Power Built Label",productPowerCue,new Vector2(.21f,.02f),new Vector2(.98f,.98f),10,ColorOf("F6F1E3"));
                text.text="Power I built";text.fontSizeMin=10;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            }
            productRecyclingCue.gameObject.SetActive(false);productPowerCue.gameObject.SetActive(false);
        }
        static bool RecyclingIReady(ModelState state,ChapterState chapter)=>!chapter.TransitionPending&&
            ChapterEconomy.QuoteSkill(state,JunkToNeon.Upgrades.SkillTree.RecyclingTier1).Status==OfferStatus.Available;
        void CompactProductUtility(Button button)
        {
            if(button==null)return;
            // The full existing button remains the hit target; only its visual weight changes.
            button.targetGraphic=Surface(button.gameObject,ColorOf("203448"),100,true);
            var icon=button.GetComponentInChildren<JtnProductIcon>(true);
            if(icon!=null)
            {
                Place(icon.rectTransform,button.transform,new Vector2(.30f,.38f),new Vector2(.70f,.80f));
                icon.color=ColorOf("C8D8DB");
            }
        }
        void LayoutProductHubCards(int mode=0)
        {
            // One presentation for authored and fallback hubs. Keep navigation
            // hit targets roomy while giving long descriptions more width.
            Place(productHubRoot,panel.transform,new Vector2(.045f,.18f),new Vector2(.955f,.875f));
            Place(productContext.rectTransform,productHubRoot,new Vector2(.01f,.82f),new Vector2(.99f,1));
            InnerNavContext(productContext);
            for(int i=0;i<productCards.Length;i++)
            {
                var button=productCards[i];float left=i%2*.52f,top=.79f-i/2*.265f;
                bool wide=mode==ProductSettings&&i==4;
                float width=wide?1:.48f,insetScale=.48f/width;
                Place((RectTransform)button.transform,productHubRoot,new Vector2(left,top-.245f),new Vector2(left+width,top));
                var icon=button.GetComponentInChildren<JtnProductIcon>(true);
                Place(icon.rectTransform,button.transform,new Vector2(.05f*insetScale,.24f),new Vector2(.30f*insetScale,.80f));
                icon.color=ColorOf("C8D8DB");icon.raycastTarget=false;
                var caption=button.GetComponentInChildren<TMP_Text>(true);
                Place(caption.rectTransform,button.transform,new Vector2(.34f*insetScale,.14f),new Vector2(1-.11f*insetScale,.90f));
                caption.alignment=TextAlignmentOptions.Left;caption.margin=Vector4.zero;
                caption.enableAutoSizing=true;caption.fontSizeMin=10;caption.fontSizeMax=13;caption.fontSize=13;
                caption.textWrappingMode=TextWrappingModes.Normal;caption.lineSpacing=0;caption.raycastTarget=false;
                // Hub cards use the same glossy artwork and pressed state as the slot chrome.
                StyleCInnerCard(button);
            }
        }
        void ApplyProductLayout()
        {
            CompactProductUtility(productAvatar);CompactProductUtility(productSettings);
            CompactProductUtility(productMapShortcut);CompactProductUtility(productEventShortcut);
            // Compact top: a one-row header carries identity and materials, so the
            // slot block below gains height. Authored scene objects are placed here too.
            var header=(RectTransform)productAvatar.transform.parent;
            Place(header,productChrome,new Vector2(.035f,.931f),new Vector2(.965f,.995f));
            Place((RectTransform)productAvatar.transform,header,Vector2.zero,new Vector2(.14f,1));
            Place((RectTransform)productSettings.transform,header,new Vector2(.86f,0),Vector2.one);
            Place(productIdentity.rectTransform,header,new Vector2(.16f,.48f),new Vector2(.84f,1));
            Place((RectTransform)productCoins.transform.parent,productChrome,new Vector2(.035f,.864f),new Vector2(.37f,.926f));
            Place((RectTransform)productDiamonds.transform.parent,productChrome,new Vector2(.39f,.864f),new Vector2(.65f,.926f));
            Place((RectTransform)productEnergy.transform,productChrome,new Vector2(.67f,.864f),new Vector2(.965f,.926f));
            Place((RectTransform)productEventShortcut.transform,productChrome,new Vector2(.82f,.673f),new Vector2(.97f,.750f));
            Place((RectTransform)productMapShortcut.transform,productChrome,new Vector2(.03f,.673f),new Vector2(.18f,.750f));
            Place(productGoal,productChrome,new Vector2(.035f,.496f),new Vector2(.965f,.563f));
            if(productPowerCue!=null)Place(productPowerCue,productChrome,new Vector2(.015f,.752f),new Vector2(.245f,.790f));
            // Give the next construction more reading space instead of a wide decorative icon column.
            var goalIcon=productGoalButton.GetComponentInChildren<JtnProductIcon>(true);
            if(goalIcon!=null)Place(goalIcon.rectTransform,productGoalButton.transform,new Vector2(.055f,.31f),new Vector2(.135f,.74f));
            Place(productGoalText.rectTransform,productGoalButton.transform,new Vector2(.18f,.12f),new Vector2(.95f,.91f));
            Place((RectTransform)productGoalFill.parent,productGoal,new Vector2(.18f,.055f),new Vector2(.95f,.085f));
            var controls=(RectTransform)spinButton.transform.parent;
            // Keep the running reel presentation alive, but conceal it behind rounded modal cards.
            productSpinSurface=controls.GetComponent<CanvasGroup>();
            if(productSpinSurface==null)productSpinSurface=controls.gameObject.AddComponent<CanvasGroup>();
            Place(world,safe,new Vector2(0,.533f),new Vector2(1,.894f));world.GetComponent<Image>().color=ColorOf("203448");
            foreach(var image in world.GetComponentsInChildren<Image>(true))
                if(image.name=="Sky"||image.name=="Yard")image.color=ColorOf(image.name=="Sky"?"263A50":"294B4C");
            Place(controls,safe,new Vector2(0,.11f),new Vector2(1,.488f));controls.GetComponent<Image>().color=ColorOf("142638");
            for(int i=0;i<3;i++)
            {
                var frame=(RectTransform)reelImages[i].transform.parent;Surface(frame.gameObject,ColorOf("F2EDDC"),18);
                // Wider and taller cards: the symbol is limited by card width.
                frame.anchorMin=new Vector2(.035f+i*.317f,.385f);frame.anchorMax=new Vector2(.332f+i*.317f,.94f);
                expansionBaseReelMin[i]=frame.anchorMin;expansionBaseReelMax[i]=frame.anchorMax;
            }
            Place((RectTransform)autoButton.transform,controls,new Vector2(.035f,.03f),new Vector2(.245f,.27f));
            Place((RectTransform)spinButton.transform,controls,new Vector2(.27f,.03f),new Vector2(.965f,.27f));
            autoButton.targetGraphic=Surface(autoButton.gameObject,ColorOf("426268"),19,true);spinButton.targetGraphic=Surface(spinButton.gameObject,ColorOf("F4B85C"),24,true);
            autoLabel.color=ColorOf("F6F1E3");spinLabel=spinButton.GetComponentInChildren<TMP_Text>();spinLabel.fontSize=21;
            Place(message.rectTransform,controls,new Vector2(.04f,.28f),new Vector2(.96f,.36f));message.enableAutoSizing=true;message.fontSizeMin=10;message.fontSizeMax=14;
            expansionMessageMin=message.rectTransform.anchorMin;expansionMessageMax=message.rectTransform.anchorMax;expansionMessageAutoSize=true;expansionMessageFont=14;expansionMessageFontMin=10;expansionMessageFontMax=14;
            foreach(var t in controls.GetComponentsInChildren<TMP_Text>(true))if(t.text.StartsWith("БЕСПЛАТНО · КАЖДЫЙ")||t.text.StartsWith("FREE · EVERY SPIN"))t.gameObject.SetActive(false);
            // Legacy routes stay available in the profile's explicit All Sections screen.
            foreach(var t in new[]{wallet,stageLabel,chapterBanner,waveStatus,workshopMarker,projectPrice,arcadeHudCounters,expansionHudResources})if(t!=null)t.gameObject.SetActive(false);
            Place(heading.rectTransform,productChrome,new Vector2(.035f,.800f),new Vector2(.965f,.858f));
            productNarrativeSurface=productChrome.Find("Narrative Surface") as RectTransform;
            if(productNarrativeSurface==null)productNarrativeSurface=Area("Narrative Surface",productChrome,heading.rectTransform.anchorMin,heading.rectTransform.anchorMax);
            Place(productNarrativeSurface,productChrome,heading.rectTransform.anchorMin,heading.rectTransform.anchorMax);
            Surface(productNarrativeSurface.gameObject,ColorOf("203448"),9,false);
            productNarrativeSurface.SetAsLastSibling();heading.transform.SetAsLastSibling();
            productNarrativeSurface.gameObject.SetActive(heading.gameObject.activeSelf);
            heading.color=ColorOf("F6F1E3");heading.alignment=TextAlignmentOptions.MidlineLeft;
            heading.margin=new Vector4(12,4,12,4);heading.textWrappingMode=TextWrappingModes.Normal;
            heading.enableAutoSizing=true;heading.fontSizeMin=11;heading.fontSizeMax=16;heading.raycastTarget=false;
            var oldGoal=world.Find("Goal Readout");if(oldGoal!=null)oldGoal.GetComponent<Image>().enabled=false;
            foreach(var b in new[]{projectButton,workshopButton,moreProjectsButton,sectionsButton,expansionShopButton})if(b!=null)b.gameObject.SetActive(false);
            foreach(var draft in new[]{workshopDraft,recyclerDraft,pigeonDraft,cartDraft,powerDraft})if(draft!=null)draft.gameObject.SetActive(false);
            var title=world.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="СВАЛКА → НЕОН");if(title!=null)title.gameObject.SetActive(false);
            if(guideButton!=null)
            {
                Place((RectTransform)guideButton.transform,productChrome,new Vector2(.03f,.581f),new Vector2(.18f,.658f));
                guideButton.targetGraphic=Surface(guideButton.gameObject,ColorOf("263C50"),100,true);
                var t=guideButton.GetComponentInChildren<TMP_Text>(true);Place(t.rectTransform,guideButton.transform,new Vector2(.02f,.04f),new Vector2(.98f,.32f));t.fontSize=t.fontSizeMin=t.fontSizeMax=10;t.color=ColorOf("F6F1E3");
                if(guideButton.transform.Find("Icon Events")==null)Icon(guideButton.transform,Glyph.Events,new Vector2(.23f,.32f),new Vector2(.77f,.87f),ColorOf("F8D986"));
                CompactProductUtility(guideButton);
            }
            // The card follows the backdrop in sibling order; rounding the panel behind it is invisible.
            var card=panel.transform.Find("Project Card");
            Surface(panel,card==null?ColorOf("F2EDDC"):Color.clear,card==null?24:0,card==null);
            if(card!=null)Surface(card.gameObject,ColorOf("F2EDDC"),24,true);
            if(!sceneUiAuthored||!Application.isPlaying)
            {
                // Keep every modal below the persistent wallet row (.852).
                Place((RectTransform)panel.transform,safe,new Vector2(.05f,.12f),new Vector2(.95f,.842f));
                if(!sceneUiAuthored)Place(panelTitle.rectTransform,panel.transform,new Vector2(.07f,.87f),new Vector2(.93f,.98f));
            }
            closeButton.targetGraphic=Surface(closeButton.gameObject,ColorOf("D6DBC8"),18,true);
            LayoutProductHubCards();
            productChrome.SetAsLastSibling();
            if(rewardFeedbackRoot!=null)rewardFeedbackRoot.SetAsLastSibling();
            ConfigureRewardFeedbackTargets(productDiamonds.rectTransform,productEnergyText.rectTransform,productEventBadge.rectTransform);rewardFeedbackCoinTarget=productCoins.rectTransform;
            RenderProductQuickBoost(!panel.activeSelf,false);
            ApplyStyleCArt();
        }
        bool ProductCanNavigate()=>CanOpenChapterPanel()&&!Session.RecoveryRequired&&!Session.Chapter.TransitionPending&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel;
        void OpenProductTab(int tab)
        {
            productSelectedTab=tab;
            if(tab==1){ClearProductRoute();HideWaveChoices();panel.SetActive(false);Render();return;}
            ShowProductSection(tab==0?ProductDistrict:tab==2?ProductEvents:tab==3?ProductShop:ProductUpgrades);
        }
        void ShowProductGoal()
        {
            var c=Session.Chapter;
            if(c.SelectedProjectId==null)
            {
                if(c.Progress.EraId==EraIds.Junkyard&&!c.Progress.CompletedUpgradeIds.Contains(GameIds.HouseRepair))ShowChapterFamily(FamilyIds.House,true);
                else if(c.Progress.EraId==EraIds.Junkyard&&c.Progress.CompletedUpgradeIds.Contains(GameIds.HouseRepair)&&!c.Progress.CompletedUpgradeIds.Contains(GameIds.WorkshopBuild))ShowChapterFamily(FamilyIds.Workshop,true);
                else if(ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress))ShowChapterEra();
                else if(c.Progress.CompletedUpgradeIds.Count==24)ShowSectionProgress();else ShowDirectConstruction();
                return;
            }
            var family=FamilyIds.All.FirstOrDefault(id=>Enumerable.Range(1,2).Any(level=>ChapterIds.Project(c.Progress.EraId,id,level)==c.SelectedProjectId));
            if(family!=null)ShowChapterFamily(family,true);else ShowSectionProgress();
        }
        void ProductRoute(Action action,int hub)
        {
            action();
            if(!Session.Chapter.TransitionPending)
            {
                productReturnHub=hub;productRouteSession=Session;productReturnBoundary=wavePanel;
                if(wavePanel>=ArcadeHome&&wavePanel<=ArcadeResult)productReturnBoundary=ArcadeHome;
                else if(wavePanel>=ExpansionHome&&wavePanel<=ExpansionResult)productReturnBoundary=ExpansionHome;
                // Closing the route's first screen returns to this hub, so it reads Back even where the screen left "Close".
                if(panel.activeSelf)closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
            }
            RefreshProductNavigation();
        }
        void ClearProductRoute(){productReturnHub=productReturnBoundary=-1;productRouteSession=null;}
        static string ClassicGameEntryDescription(ArcadeView view,string id,bool parcelsActive)
        {
            string description=id==ArcadeCatalog.Memory?"6 cards · find 3 pairs":"8 finds · choose the bins";
            var run=view.ActiveRun;
            if(run!=null&&run.Id==id)
                return description+"\n"+(run.Ready?(run.ReadyReward==0?"Finish · 0 Diamonds":$"Ready · {run.ReadyReward} "+(run.ReadyReward==1?"Diamond":"Diamonds")):"Resume free · round saved");
            string availability=run!=null||parcelsActive?"Other round saved · inspect":
                view.Tickets>=EconomyRules.ArcadeStartTickets?$"Start · {EconomyRules.ArcadeStartTickets} Ticket":"No Ticket · inspect rules";
            return description+"\nUp to 3 Diamonds\n"+availability;
        }
        void ShowProductSection(int mode)
        {
            if(!ProductCanNavigate())return;
            string title=mode==ProductDistrict?"My District":mode==ProductEvents?"Yard Events":mode==ProductShop?"Shop":mode==ProductUpgrades?"Upgrades":mode==ProductEnergy?"Energy & Bonus":mode==ProductProfile?"My Character":"Settings";
            ClearProductRoute();BeginChapterPanel(mode,title);productContextValid=true;
            panelBody.enabled=false;productHubRoot.gameObject.SetActive(true);
            var state=Session.State;var a=Session.Arcade;var e=Session.Expansion;var chapter=Session.Chapter;
            for(int i=0;i<6;i++)productCards[i].gameObject.SetActive(false);
            LayoutProductHubCards(mode);
            // Energy, Profile and Settings open from the header, so no bottom tab is theirs.
            if(mode>=ProductEnergy)productSelectedTab=-1;
            if(mode==ProductDistrict)
            {
                bool complete=chapter.Progress.CompletedUpgradeIds.Count==24;
                productSelectedTab=0;productContext.text=EraProgressSummary(chapter)+"\n"+
                    (complete?(chapter.Skills.Receipts.Count==JunkToNeon.Upgrades.SkillOwnership.MaximumReceipts?"District complete · all skills owned. Coin upgrades complete; resources and events remain.":"District complete. Skills and other activities are optional."):"Choose a goal, spin and transform the district.");
                ProductCard(0,Glyph.Home,"Construction","All six buildings",ShowDirectConstruction,mode);
                ProductCard(1,Glyph.Map,"District Map","Two eras · 24 stages",ShowProgressMap,mode);
                ProductCard(2,Glyph.Upgrade,"My Goal",chapter.SelectedProjectId==null?(chapter.Progress.CompletedUpgradeIds.Count==24?"District complete · 24/24":ChapterCatalog.Rules.CanUnlockWorkshopYard(chapter.Progress)?"Free era entry":"Choose a goal"):ProjectName(chapter.SelectedProjectId),ShowProductGoal,mode);
                ProductCard(3,Glyph.Check,"Progress","Buildings and milestones",ShowSectionProgress,mode);
                var trade=ProductGoalTrade(state,chapter);
                if(trade!=null)
                {
                    long shortage=ChapterEconomy.SelectedProjectPrice(chapter)-state.coins;
                    string effect=shortage<=trade.CoinsReward?"Trade covers the goal price":$"Still needed after trade: {shortage-trade.CoinsReward} coins";
                    string tradeId=trade.Id;
                    ProductCard(4,Glyph.Shop,"Trade for Goal",ExpansionQuoteCost(trade)+" → "+trade.CoinsReward+" coins\n"+effect,()=>ShowExpansionOffer(tradeId,4),mode);
                }
            }
            else if(mode==ProductEvents)
            {
                var parcels=Session.Parcels;
                productSelectedTab=2;
                productContext.text=ArcadeCopy.TicketSummary(a)+"\nA ticket starts a round. Resume free; claim rewards explicitly.";
                ProductCard(0,Glyph.Box,"Yard Finds",ClassicGameEntryDescription(a,ArcadeCatalog.Memory,parcels.ActiveRun!=null),()=>ShowArcadeGame(ArcadeCatalog.Memory),mode);
                ProductCard(1,Glyph.Leaf,"Sorting Shift",ClassicGameEntryDescription(a,ArcadeCatalog.Sorting,parcels.ActiveRun!=null),()=>ShowArcadeGame(ArcadeCatalog.Sorting),mode);
                ProductCard(2,Glyph.Ticket,"Neighbor Orders",ProductOrderStatus(state,chapter),ShowDirectOrders,mode);
                if(a.ActiveRun!=null)ProductCard(3,Glyph.Events,a.ActiveRun.Ready?(a.ActiveRun.ReadyReward==0?"Finish · 0 Diamonds":"Result ready"):"Resume round",a.ActiveRun.Ready?(a.ActiveRun.ReadyReward==0?"Round complete · finish explicitly":$"To claim: {a.ActiveRun.ReadyReward} Diamonds"):ArcadeLabel(a.ActiveRun.Id),ShowArcadeRound,mode);
                else if(parcels.ActiveRun!=null)ProductCard(3,Glyph.Events,"Pigeon's Parcels",SavedArcadeActivity(state,a),ShowArcadeRound,mode);
                ProductCard(4,Glyph.Diamond,"Tickets & Rewards","Mini-game rules",ShowArcadeInformation,mode);
                ProductCard(5,Glyph.Box,"Pigeon's Parcels",ParcelsEntryStatus(parcels),ShowParcelsGame,mode);
            }
            else if(mode==ProductShop)
            {
                int modules=ExpansionCatalog.Offers.Count(o=>o.Kind=="module"&&e.OwnsModule(o.Id));
                int moduleTotal=ExpansionCatalog.Offers.Count(o=>o.Kind=="module");
                int themes=ExpansionCatalog.Offers.Count(o=>o.Kind=="theme"&&e.OwnsTheme(o.Id));
                int themeTotal=ExpansionCatalog.Offers.Count(o=>o.Kind=="theme");
                int socketMax=ExpansionCatalog.Offers.Where(o=>o.Kind=="socket").Max(o=>o.Level);
                productSelectedTab=3;productContext.text=$"Parts {e.Parts}/{e.PartsCap} · Potatoes {e.Potatoes}/{e.PotatoesCap}\nChoose what you need. Review price and effect before confirming.";
                ProductCard(0,Glyph.Box,"Modules",modules==moduleTotal?"All owned · install separately":$"Owned {modules}/{moduleTotal} · install separately",()=>ShowExpansionCategory(0),mode);
                ProductCard(1,Glyph.Upgrade,"Sockets & Presets",$"Unlocked {e.SocketCount}/{socketMax} · presets separate",()=>ShowExpansionCategory(1),mode);
                ProductCard(2,Glyph.Events,"Appearance",themes==themeTotal?"All palettes owned":$"Owned {themes}/{themeTotal} palettes",()=>ShowExpansionCategory(2),mode);
                ProductCard(3,Glyph.Diamond,"Spend Diamonds","Resource packs",()=>ShowArcadeCategory(1),mode);
                ProductCard(4,Glyph.Leaf,"Resources & Trades","Potatoes · trade for coins",()=>ShowExpansionCategory(4),mode);
                ProductCard(5,Glyph.Energy,"Boosts","Bonus, workbench and focus",()=>ShowProductSection(ProductEnergy),mode);
            }
            else if(mode==ProductUpgrades)
            {
                int equipment=FamilyIds.All.Sum(f=>e.EquipmentLevel(f));
                int equipmentMax=ExpansionCatalog.Offers.Count(o=>o.Kind=="equipment");
                int slotMax=ArcadeCatalog.Offers.Where(o=>o.Kind=="slot").Max(o=>o.Level);
                int yieldMax=ExpansionCatalog.Offers.Where(o=>o.Kind=="yield").Max(o=>o.Level);
                int workbenchMax=ExpansionCatalog.Offers.Where(o=>o.Kind=="workbench").Max(o=>o.Level);
                int skillMax=JunkToNeon.Upgrades.SkillOwnership.MaximumReceipts;
                productSelectedTab=4;productContext.text=$"Slot {a.SlotLevel}/{slotMax} · skills {chapter.Skills.Receipts.Count}/{skillMax}\nPermanent upgrades. District construction is counted separately.";
                ProductCard(0,Glyph.Spin,"Slot Level",a.SlotLevel==slotMax?"All levels unlocked":$"Unlocked {a.SlotLevel}/{slotMax} · attributes",()=>ShowArcadeCategory(0),mode);
                ProductCard(1,Glyph.Home,"Equipment",equipment==equipmentMax?"All levels unlocked":$"Unlocked {equipment}/{equipmentMax} · building prerequisites",()=>ShowExpansionCategory(3),mode);
                ProductCard(2,Glyph.Leaf,"Potato Yield",e.PotatoYieldLevel==yieldMax?"All levels owned":$"Level {e.PotatoYieldLevel}/{yieldMax} · effects",()=>ShowExpansionCategory(4),mode);
                var recycling=ChapterEconomy.QuoteSkill(state,JunkToNeon.Upgrades.SkillTree.RecyclingTier1);
                ProductCard(3,Glyph.Upgrade,"Skills",!chapter.TransitionPending&&recycling.Status==OfferStatus.Available?
                    $"Recycling I ready · {recycling.Node.Price} Coins\nOptional · review before buying":
                    chapter.Skills.Receipts.Count==skillMax?"All skills owned":$"Owned {chapter.Skills.Receipts.Count}/{skillMax} · optional",ShowDirectSkills,mode);
                ProductCard(4,Glyph.Energy,"Bonus Workbench",!e.BonusUnlocked?"Unlock the bonus first":e.BonusWorkbenchLevel==workbenchMax?"All levels owned · focus":$"Level {e.BonusWorkbenchLevel}/{workbenchMax} · focus",()=>ShowProductSection(ProductEnergy),mode);
            }
            else if(mode==ProductEnergy)
            {
                var unlock=ExpansionCatalog.Get(ExpansionCatalog.BonusUnlock);
                string unlockCost=ExpansionCostText(unlock.PartsCost,unlock.PotatoesCost,unlock.EnergyCost,unlock.CoinsCost);
                string shortage=ExpansionMissing(state,e,unlock.PartsCost,unlock.PotatoesCost,unlock.EnergyCost,unlock.CoinsCost);
                int remaining=e.BonusCadence-e.BonusProgress;
                productContext.text=e.BonusUnlocked?
                    $"Energy {e.Energy}/{e.EnergyCap} · bonus in {remaining} spins\nThe ×2 boost respects storage caps; it does not reset the counter.":
                    $"Bonus reel locked: {unlockCost}.\n"+(shortage??"Ready to unlock. Spins are free.");
                if(e.BonusUnlocked)
                    ProductCard(0,Glyph.Energy,"Use Energy",e.BoostArmed?"Already armed":$"{EconomyRules.ExpansionBoostCost} Energy · inspect",()=>ShowExpansionOffer(ExpansionCatalog.NextBonusBoost,5),mode);
                else
                    ProductCard(0,Glyph.Spin,"Unlock Bonus",unlockCost+" · "+(shortage??"available"),()=>ShowExpansionOffer(ExpansionCatalog.BonusUnlock,5),mode);
                ProductCard(1,Glyph.Diamond,"Use a Diamond",!e.BonusUnlocked?"Unlock the bonus first":e.BoostArmed?"Shared boost armed":"Same boost · inspect",()=>ShowArcadeOffer(ArcadeCatalog.BonusBoost),mode);
                ProductCard(2,Glyph.Spin,"Bonus Reel",e.BonusUnlocked?$"Bonus in {remaining} spins":"Unlock · price and prerequisites",()=>{if(e.BonusUnlocked)ShowExpansionCategory(5);else ShowExpansionOffer(ExpansionCatalog.BonusUnlock,5);},mode);
                ProductCard(3,Glyph.Upgrade,"Workbench & Focus",$"Workbench {e.BonusWorkbenchLevel}/2",()=>ShowExpansionCategory(5),mode);
                ProductCard(4,Glyph.Box,"My Resources","Storage and current effects",ShowExpansionInformation,mode);
            }
            else if(mode==ProductProfile)
            {
                productContext.text=$"Collector · {EraName(chapter.Progress.EraId)}\nDistrict {chapter.Progress.CompletedUpgradeIds.Count}/24 · slot {a.SlotLevel}/3 · skills {chapter.Skills.Receipts.Count}/6";
                ProductCard(0,Glyph.Check,"My Progress","Buildings and milestones",ShowSectionProgress,mode);
                ProductCard(1,Glyph.Events,"My Style","Apply an owned palette",()=>ShowExpansionCategory(2),mode);
                ProductCard(2,Glyph.Map,"All Sections","Browse all destinations",ShowSectionsHub,mode);
                ProductCard(3,Glyph.Gear,"Settings","Effects, rules and save",()=>ShowProductSection(ProductSettings),mode);
            }
            else
            {
                productContext.text="Presentation settings\nSound and effects apply this session. <nobr>Spin speed is saved.</nobr>";
                ProductCard(0,Glyph.Events,"Sound & Effects",RewardFeedbackReduced?"Off · enable":"On · disable",()=>{SetRewardFeedbackReduced(!RewardFeedbackReduced);ShowProductSection(ProductSettings);},mode,false);
                ProductCard(1,Glyph.Ticket,"How to Play","Spins, buildings and resources",()=>ShowSectionRules(),mode);
                ProductCard(2,Glyph.Check,"Progress & Save","Facts from this save",ShowSectionProgress,mode);
                ProductCard(3,Glyph.Box,"Resources & Effects","How the economy works",ShowExpansionInformation,mode);
                string normal=EconomyRules.ExpansionNormalSpinSeconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
                string fast=EconomyRules.ExpansionFastSpinSeconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
                ProductCard(4,Glyph.Spin,"Spin Speed",$"Now: {(e.SpeedId=="speed.fast"?"fast "+fast:"normal "+normal)} s\nNormal {normal} · fast {fast} s · free",ShowExpansionHome,mode);
            }
            productHubRoot.SetAsLastSibling();closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;RefreshProductNavigation();
        }
        static string ProductOrderStatus(ModelState state,ChapterState chapter)
        {
            if(!ChapterCatalog.Rules.IsChapterComplete(chapter.Progress))return "Unlock after 24/24";
            var order=JourneyEconomy.Project(state).Orders;
            if(order.ActiveOrderId==null)return "Accept · manual spins · claim";
            var definition=OrderCatalog.Get(order.ActiveOrderId);
            string progress=NavigationOrderTitle(order.ActiveOrderId)+$" · {order.Progress}/{definition.Target}";
            return order.Ready?progress+$"\nReady · {definition.RewardCoins} coins · claim":progress+"\nIn progress · manual spins";
        }
        static ExpansionQuote ProductGoalTrade(ModelState state,ChapterState chapter)
        {
            if(chapter.SelectedProjectId==null||state.coins>=ChapterEconomy.SelectedProjectPrice(chapter))return null;
            foreach(string id in new[]{"trade.parts","trade.potatoes"})
            {
                try
                {
                    var quote=ExpansionEconomy.Quote(state,"buy",id);
                    if(quote.CoinsCost==0&&quote.CoinsReward>0)return quote;
                }
                catch(InvalidOperationException){}
                catch(OverflowException){}
            }
            return null;
        }
        static string ProductCompletedOrder(ModelState state)
        {
            var order=JourneyEconomy.Project(state).Orders;
            if(order.ActiveOrderId==null)return "Keep playing if you like  →";
            var definition=OrderCatalog.Get(order.ActiveOrderId);
            return "Optional: "+NavigationOrderTitle(order.ActiveOrderId)+"\n"+(order.Ready?$"Ready · {definition.RewardCoins} coins unclaimed →":
                $"{order.Progress}/{definition.Target} {NavigationOrderUnit(order.ActiveOrderId)} →");
        }
        void ProductCard(int index,Glyph glyph,string title,string subtitle,Action action,int hub,bool route=true)
        {
            var b=productCards[index];var icon=b.GetComponentInChildren<JtnProductIcon>(true);icon.Shape=glyph;
            StyleCProductCardIcon(icon,hub,index);
            b.GetComponentInChildren<TMP_Text>(true).text="<b><color=#F8D986>"+title+"</color></b>\n<size=90%><color=#F6F1E3>"+subtitle+"</color></size>";
            long version=productVersion,sequence=Session.State.sequence;var session=Session;var visible=new Button.ButtonClickedEvent();
            visible.AddListener(()=>{if(!ReferenceEquals(b.onClick,visible)||!productContextValid||version!=productVersion||!ReferenceEquals(Session,session)||Session.State.sequence!=sequence||!SectionAction(b,hub))return;if(route)ProductRoute(action,hub);else action();});
            b.onClick=visible;b.interactable=true;b.gameObject.SetActive(true);
        }
        bool TryReturnProduct()
        {
            if(!ProductCanNavigate())return false;
            // A hub opened from another hub (Settings from Profile) goes back there; any other hub closes.
            if(wavePanel>=ProductDistrict&&wavePanel<=ProductSettings&&wavePanel==productReturnBoundary&&productReturnHub>=ProductDistrict&&productReturnHub!=wavePanel&&ReferenceEquals(productRouteSession,Session))
            {int hub=productReturnHub;ShowProductSection(hub);return true;}
            if(wavePanel>=ProductDistrict&&wavePanel<=ProductSettings){ClearProductRoute();HideWaveChoices();panel.SetActive(false);productSelectedTab=1;Render();return true;}
            bool root=wavePanel==productReturnBoundary||(productReturnBoundary==SectionMap&&wavePanel==SectionHub);
            if(root&&productReturnHub>=ProductDistrict&&ReferenceEquals(productRouteSession,Session)){int hub=productReturnHub;ShowProductSection(hub);return true;}
            return false;
        }
        void InvalidateProductUi()
        {
            productContextValid=false;productVersion++;
            if(productHubRoot!=null)productHubRoot.gameObject.SetActive(false);
            if(productCards!=null)foreach(var b in productCards)if(b!=null){b.interactable=false;InvalidateExpansionConsent(b);}
            InvalidateProductChrome();
        }
        void InvalidateProductChrome()
        {
            if(productTabs!=null)foreach(var b in productTabs)if(b!=null)InvalidateExpansionConsent(b);
            foreach(var b in new[]{productAvatar,productSettings,productEnergy,productEventShortcut,productEventStatus,productMapShortcut,productGoalButton,guideButton})if(b!=null)InvalidateExpansionConsent(b);
        }
        void RefreshProductNavigation()
        {
            if(productChrome==null)return;bool usable=ProductCanNavigate();
            bool blocked=ProductBlocked();if(blocked&&!productLastBlocked)InvalidateProductChrome();
            productLastPanelVisible=panel.activeSelf;productLastBlocked=blocked;
            bool root=!panel.activeSelf;
            if(productSpinSurface!=null)
            {
                productSpinSurface.alpha=root?1:0;
                productSpinSurface.interactable=root;productSpinSurface.blocksRaycasts=root;
            }
            if(!root&&guideButton!=null)guideButton.gameObject.SetActive(false);
            if(heading!=null)heading.gameObject.SetActive(root&&!failed&&!InBackground&&Session!=null&&!Session.RecoveryRequired&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel);
            if(productNarrativeSurface!=null)productNarrativeSurface.gameObject.SetActive(heading!=null&&heading.gameObject.activeSelf);
            foreach(var b in new[]{productEventShortcut,productEventStatus,productMapShortcut,productGoalButton})b.gameObject.SetActive(root);
            productEventBadge.gameObject.SetActive(root);productMaterials.gameObject.SetActive(root);productGoal.gameObject.SetActive(root);
            foreach(var b in productTabs)b.interactable=usable;
            foreach(var b in new[]{productAvatar,productSettings,productEnergy,productEventShortcut,productEventStatus,productMapShortcut,productGoalButton})b.interactable=usable;
            for(int i=0;i<5;i++)productTabs[i].targetGraphic.color=ColorOf(i==productSelectedTab?"426268":"172B3D");
            RefreshStyleCTabs();StyleCSpinLook();
            bool skillCue=root&&usable&&productRecyclingReady;
            if(productRecyclingCue!=null)productRecyclingCue.gameObject.SetActive(skillCue);
            var upgradeIcon=productTabs[4].GetComponentInChildren<JtnProductIcon>(true);
            if(upgradeIcon!=null)Place(upgradeIcon.rectTransform,productTabs[4].transform,
                skillCue?new Vector2(.27f,.33f):new Vector2(.23f,.32f),skillCue?new Vector2(.73f,.70f):new Vector2(.77f,.87f));
            if(productPowerCue!=null)productPowerCue.gameObject.SetActive(root&&!failed&&!InBackground&&Session!=null&&
                !Session.RecoveryRequired&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel&&productPowerBuilt);
            RenderProductQuickBoost(root,usable);
            RefreshSectionArtwork();
        }
        bool ProductBlocked()=>Busy||failed||InBackground||Session==null||Session.RecoveryRequired||offlineSummaryPanel||autoHintPanel||exitPanel;
        void ObserveProductPanel()
        {ObserveYardRoute();if(productChrome!=null&&(productLastPanelVisible!=panel.activeSelf||productLastBlocked!=ProductBlocked()))RefreshProductNavigation();}
        void ClearProductSpinReceipt()
        {
            productSpinReceiptSequence=productSpinReceiptIncome=productSpinReceiptDebit=0;
            productSpinReceiptWorkshop=0;productSpinReceiptPayment=productSpinReceiptExtras=null;productSpinReceiptHistorical=false;
        }
        void ResetProductSpinReceipt()
        {
            productSpinReceiptGeneration++;productSpinReceiptSession=null;ClearProductSpinReceipt();
            productPendingSpinSequence=0;productSpinBeforeText=null;productSpinBeforeGoalContext=null;
        }
        void RetainProductSpinBeforeText(ModelState state)
        {
            if(!Busy||productPendingSpinSequence==0||!ReferenceEquals(productSpinReceiptSession,Session))return;
            // The first Busy Render receives the explicit before-state. Later
            // resize/pause renders may receive the committed after-state instead.
            // Retain only these strings, never a mutable economic snapshot.
            var labels=new[]{stageLabel,chapterBanner,projectLabel,projectPrice,workshopLabel,workshopMarker,productIdentity,productGoalText};
            if(productSpinBeforeText==null)
            {
                if((state.lastSpin?.sequence??0)>=productPendingSpinSequence)return;
                productSpinBeforeText=labels.Select(label=>label==null?null:label.text).ToArray();
                productSpinBeforeGoalContext=productCurrentGoalContext;
                if(productGoalFill!=null)productSpinBeforeGoalFill=productGoalFill.anchorMax;
            }
            else
            {
                for(int i=0;i<labels.Length;i++)if(labels[i]!=null)labels[i].text=productSpinBeforeText[i];
                productCurrentGoalContext=productSpinBeforeGoalContext;
                if(productGoalFill!=null)productGoalFill.anchorMax=productSpinBeforeGoalFill;
            }
            // RenderJourney separately retains only an already presented
            // completion heading during Busy and remains its sole ACK owner.
        }
        static bool ProductSpinReceiptAuthority(ModelState state,ArcadeView arcade,ExpansionState expansion,ChapterState chapter)
        {
            var spin=state.lastSpin;var frozen=spin?.chapter;
            if(frozen?.Kind!="spin"||frozen.Sequence!=spin.sequence||frozen.Effects==null)return false;
            // Passive ACK/clock/settlement sequence advances do not erase history.
            return ChapterEconomy.Same(chapter,frozen.After)&&(expansion.LastCommand?.Sequence??0)<=spin.sequence&&
                (arcade.LastEvent==null||arcade.LastEvent.Kind=="spin"||arcade.LastEvent.Sequence<=spin.sequence)&&
                (ParcelsEconomy.Project(state).LastCommand?.Sequence??0)<=spin.sequence&&
                (state.journey?.Orders.LastCommandSequence??0)<=spin.sequence;
        }
        void RememberProductSpinReceipt(ModelState state,bool historical)
        {
            ClearProductSpinReceipt();productSpinReceiptSession=Session;
            if(Session==null||state==null)return;
            var spin=state.lastSpin;var frozen=spin?.chapter;
            if(frozen?.Kind!="spin"||frozen.Sequence!=spin.sequence||frozen.Effects==null)return;
            var a=ArcadeEconomy.Project(state);
            productSpinReceiptSequence=spin.sequence;productSpinReceiptHistorical=historical;
            productSpinReceiptIncome=(long)frozen.BaseCoins+frozen.WorkshopCoins+frozen.Effects.TotalCoins;
            productSpinReceiptDebit=frozen.After.Progress.CompletedUpgradeIds.Count>frozen.Before.Progress.CompletedUpgradeIds.Count?frozen.Price:0;
            var arcadeSpin=a.LastResourceReceipt;
            if(arcadeSpin?.Kind=="spin"&&arcadeSpin.Sequence==spin.sequence)
            {
                productSpinReceiptIncome=arcadeSpin.CoinIncome;productSpinReceiptDebit=arcadeSpin.ConstructionDebit;
                // One symbol can pay Coins and a resource; name each credited amount.
                productSpinReceiptExtras=(arcadeSpin.PartsAdded>0?$" · +{arcadeSpin.PartsAdded} parts":"")+(arcadeSpin.PotatoesAdded>0?$" · +{arcadeSpin.PotatoesAdded} potatoes":"")+
                    (arcadeSpin.EnergyAdded>0?$" · +{arcadeSpin.EnergyAdded} energy":"");
            }
            productSpinReceiptWorkshop=frozen.WorkshopCoins;
            if(productSpinReceiptDebit>0&&ChapterCatalog.Rules.TryGetUpgrade(frozen.ActionId,out var built))
            {
                var stage=ChapterCatalog.Rules.Definition.Eras.SelectMany(era=>era.Catalog.Stages).First(item=>item.Id==built.ToStageId);
                productSpinReceiptPayment=$"Paid {productSpinReceiptDebit} → {FamilyName(stage.FamilyId)} {stage.Level}/2 · saved";
            }
        }
        void RenderProductSpinMessage(ModelState state,string status,ArcadeView a,ExpansionState e,ChapterState c,long? savedReward)
        {
            if(failed||Session?.RecoveryRequired==true)return;
            if(InBackground){message.text=status??"Paused";return;}
            bool receipt=productSpinReceiptSequence>0&&ReferenceEquals(productSpinReceiptSession,Session)&&
                state.lastSpin!=null&&state.lastSpin.sequence>=productSpinReceiptSequence;
            if(receipt&&!Busy)receipt=state.lastSpin.sequence==productSpinReceiptSequence&&ProductSpinReceiptAuthority(state,a,e,c);
            if(Busy)
            {
                message.text=(status??"Gathering materials…")+(receipt?"\n"+(productSpinReceiptHistorical?"Saved":"Previous")+
                    $" spin: +{productSpinReceiptIncome} coins"+(productSpinReceiptDebit>0?$"; paid {productSpinReceiptDebit} coins":""):"");
                return;
            }
            if(status!=null){message.text=status;return;}
            if(c.TransitionPending||offlineSummaryPanel||autoHintPanel||exitPanel)return;
            // The Events badge already projected this same display state in this render.
            string savedActivity=a.ActiveRun!=null||!savedReward.HasValue?null:savedReward.Value<0?"Round saved":
                savedReward.Value==0?"Ready: 0 · finish":$"Ready to claim: {savedReward.Value}";
            string next=a.ActiveRun!=null?(a.ActiveRun.Ready?(a.ActiveRun.ReadyReward==0?"Zero result: finish in Events.":$"Result ready: {a.ActiveRun.ReadyReward} Diamonds · Events"):
                "Round saved · resume in Events."):savedActivity!=null?savedActivity+" · Events":a.Tickets>0?"Ticket: Events are optional.":"FREE SPINS · A TICKET EVERY "+a.TicketCadence+" SPINS";
            bool cadenceOnly=a.ActiveRun==null&&savedActivity==null&&a.Tickets<=0;
            message.text=receipt?(productSpinReceiptHistorical?"Saved":"Last")+$" spin: +{productSpinReceiptIncome} coins"+
                (productSpinReceiptWorkshop>0?$" (Workshop {productSpinReceiptWorkshop} included)":"")+
                // Resource amounts join the receipt only when it is the single line; with a payment or event line they would overflow.
                (productSpinReceiptPayment==null&&cadenceOnly?productSpinReceiptExtras:"")+
                // The yard badge already shows ticket cadence; keep the receipt on one readable line.
                (productSpinReceiptPayment!=null?"\n"+productSpinReceiptPayment:cadenceOnly?"":"\n"+next):next;
        }
        long productCoinBalance;

        void RenderProductCoinCounter()
        {
            if(productCoins!=null)productCoins.text=(rewardImpactDisplayedCoins??productCoinBalance)+"\n<size=65%>COINS</size>";
        }

        void RenderProductUi(ModelState state,string status)
        {
            if(productChrome==null)return;
            var a=ArcadeEconomy.Project(state);var e=ExpansionEconomy.Project(state);var c=ChapterEconomy.Project(state);
            if(!ReferenceEquals(productCueSession,Session))
            {productCueSession=Session;productRecyclingReady=productPowerBuilt=false;}
            // Keep only presented facts. Resize/pause may Render the committed
            // after-state during Busy; neither cue may reveal its new outcome.
            if(!Busy)
            {
                productRecyclingReady=!failed&&!Session.RecoveryRequired&&RecyclingIReady(state,c);
                productPowerBuilt=c.Progress.CompletedUpgradeIds.Contains(ChapterIds.Project(EraIds.Junkyard,FamilyIds.PowerSource,1));
            }
            productIdentity.text="JUNK <color=#86E2E5>→ NEON</color>  <size=65%>"+EraName(c.Progress.EraId)+" · "+c.Progress.CompletedUpgradeIds.Count+"/24</size>";
            // A redraw during the next spin can see its committed result before reveal.
            if(!Busy)productCoinBalance=state.coins;
            RenderProductCoinCounter();
            productDiamonds.text=a.Diamonds+"\n<size=65%>DIAMONDS</size>";productEnergyText.text=e.Energy+"/"+e.EnergyCap+"\n<size=65%>ENERGY</size>";
            productMaterials.text=$"Parts {e.Parts}/{e.PartsCap} · Potatoes {e.Potatoes}/{e.PotatoesCap}";
            string goal=c.SelectedProjectId==null?null:ProjectName(c.SelectedProjectId);
            long goalPrice=goal==null?0:ChapterEconomy.SelectedProjectPrice(c);
            long goalShortfall=goal==null||state.coins>=goalPrice?0:goalPrice-state.coins;
            bool inspectHouse=goal==null&&c.Progress.EraId==EraIds.Junkyard&&!c.Progress.CompletedUpgradeIds.Contains(GameIds.HouseRepair);
            bool inspectWorkshop=goal==null&&c.Progress.EraId==EraIds.Junkyard&&c.Progress.CompletedUpgradeIds.Contains(GameIds.HouseRepair)&&!c.Progress.CompletedUpgradeIds.Contains(GameIds.WorkshopBuild);
            bool eraReady=ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress),chapterComplete=ChapterCatalog.Rules.IsChapterComplete(c.Progress);
            productCurrentGoalContext=goal!=null?$"Now: {state.coins} coins · goal: {goal}, price {goalPrice} coins. " +
                (goalShortfall>0?$"Need {goalShortfall} more coins. Press SPIN.":"Enough coins; construction is not yet paid. Press SPIN."):
                $"Now: {state.coins} coins · "+(inspectHouse?$"Inspect House: {ProjectPrice(GameIds.HouseRepair)} coins; choose separately.":
                inspectWorkshop?$"Inspect Workshop: {ProjectPrice(GameIds.WorkshopBuild)} coins; choose separately.":
                eraReady?"Junkyard complete: 12/24. Inspect free entry to the Workshop Yard.":
                chapterComplete?"Chapter complete: 24/24. Continuing is optional. Inspect Progress.":"No goal selected. Inspect buildings and choose one.");
            productGoalFill.anchorMax=new Vector2(goal==null?(c.Progress.CompletedUpgradeIds.Count==24?1:0):(float)Math.Min(1d,(double)state.coins/goalPrice),1);
            long? savedReward=SavedArcadeReward(state,a);
            var spinsLeft=a.TicketCadence-a.TicketProgress;
            productEventBadge.text=savedReward.HasValue?(savedReward.Value<0?"ROUND SAVED":(savedReward.Value==0?"FINISH · 0":$"CLAIM {savedReward.Value} DIAM.")+"\nEVENTS →"):a.Tickets>0?$"{a.Tickets} "+(a.Tickets==1?"TICKET":"TICKETS")+"\nEVENTS →":$"TICKET IN\n{spinsLeft} "+(spinsLeft==1?"SPIN":"SPINS");
            // Future optional availability stays secondary; saved rounds and usable tickets retain their accent.
            productEventBadge.color=ColorOf(savedReward.HasValue||a.Tickets>0?"F8D986":"C8D8DB");
            productGoalText.text=goal!=null?"<b>Goal: "+goal+$"</b>\n<size=75%>{Math.Min(state.coins,goalPrice)} / {goalPrice} coins · now {state.coins}\n<b>"+(goalShortfall>0?$"Need {goalShortfall} more · SPIN":"Enough · unpaid · SPIN")+"  →</b></size>":
                inspectHouse?$"<b>Repair House · {ProjectPrice(GameIds.HouseRepair)} coins</b>\n<size=80%>Tap to inspect and choose →</size>":
                inspectWorkshop?$"<b>Next: Inspect Workshop</b>\n<size=80%>{ProjectPrice(GameIds.WorkshopBuild)} coins · +{EconomyRules.WorkshopBonus} on later spins →</size>":
                eraReady?"<b>Workshop Yard unlocked</b>\n<size=80%>Inspect free entry  →</size>":
                c.Progress.CompletedUpgradeIds.Count==24?"<b>District complete · 24/24</b>\n<size=80%>"+ProductCompletedOrder(state)+"</size>":"<b>Next goal</b>\n<size=80%>Choose a building  →</size>";
            RenderProductSpinMessage(state,status,a,e,c,savedReward);
            // Legacy HUD rendering runs first; flights must finish at the visible product wallets.
            ConfigureRewardFeedbackTargets(productDiamonds.rectTransform,productMaterials.rectTransform,productEventBadge.rectTransform);
            rewardFeedbackCoinTarget=productCoins.rectTransform;
            // Root-only duplicate labels stay out of the product layout even after legacy rendering.
            foreach(var b in new[]{projectButton,workshopButton,moreProjectsButton,sectionsButton,expansionShopButton})if(b!=null)b.gameObject.SetActive(false);
            if(panel.activeSelf&&wavePanel>=ProductDistrict&&wavePanel<=ProductSettings&&!productContextValid&&!Busy&&!failed&&!InBackground&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel)
            {
                panelTitle.enabled=panelBody.enabled=true;
                panelBody.text=wavePanel==ProductProfile?"Reopen My Character using the avatar above.":
                    wavePanel==ProductSettings?"Reopen Settings using the gear above.":
                    wavePanel==ProductEnergy?"Reopen Energy using the Energy balance above.":"Reopen this section from the bottom menu.";
                closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
            }
            RefreshProductNavigation();
        }
    }
}
