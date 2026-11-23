using System;
using System.Collections.Generic;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using TMPro;
using UnityEngine;
using ArcadeGlyph=JunkToNeon.JtnProductIcon.Symbol;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int ArcadeHome=60,ArcadeCategory=61,ArcadePreview=62,ArcadeRound=63,ArcadeResult=64,ArcadeInfo=65;
        [SerializeField] UnityEngine.UI.Button arcadeEntryButton;
        [SerializeField] RectTransform arcadeRoot;
        [SerializeField] TMP_Text arcadeTitle,arcadeResources,arcadeBody,arcadeHudCounters;
        [SerializeField] UnityEngine.UI.Button[] arcadeCards=new UnityEngine.UI.Button[6];
        [SerializeField] UnityEngine.UI.Button arcadePrimaryButton,arcadeSecondaryButton;
        [SerializeField] RectTransform arcadeHeaderBand,arcadeResourceBand,arcadeDetailBand,arcadeRoundTrack,arcadeRoundFill,arcadeItemBand,arcadeResultBand;
        [SerializeField] TMP_Text arcadeSectionCaption,arcadeItemCaption,arcadeItemName,arcadeResultCaption,arcadeResultValue;
        readonly bool[] arcadeAvailable=new bool[6];
        bool arcadeWired,arcadeContextValid,arcadeAbandonPreview;
        long arcadeContextVersion;
        int arcadeCategory;
        ArcadeQuote arcadeQuote;
        GameSession arcadeHudSession;
        long arcadeHudSequence=-1;

        // Stopped authoring creates only existing-font controls and references.
        public void CreateArcadeUi()
        {
            if(arcadeWired&&Application.isPlaying)return;
            if(sectionsHubRoot==null||panel==null||font==null)throw new InvalidOperationException("Create the existing section Canvas before Arcade.");
            if(arcadeEntryButton==null)arcadeEntryButton=CreateSectionControl("ArcadeEntry","Mini-games & Diamonds",sectionsHubRoot,new Vector2(.03f,0),new Vector2(.97f,.18f),ColorOf("ABD2D8"),OpenArcadeEntry);
            else if(Application.isPlaying)arcadeEntryButton.onClick.AddListener(OpenArcadeEntry);
            AddSectionConsent(arcadeEntryButton);ExpansionFit(arcadeEntryButton,10,13);
            if(arcadeRoot==null)arcadeRoot=Area("ArcadeUi",panel.transform,new Vector2(.03f,.16f),new Vector2(.97f,.98f));
            if(arcadeTitle==null)arcadeTitle=ExpansionLabel("ArcadeTitle",arcadeRoot,new Vector2(0,.91f),Vector2.one,18);
            if(arcadeResources==null)arcadeResources=ExpansionLabel("ArcadeResources",arcadeRoot,new Vector2(0,.79f),new Vector2(1,.91f),12);
            if(arcadeBody==null)arcadeBody=ExpansionLabel("ArcadeBody",arcadeRoot,new Vector2(.015f,.20f),new Vector2(.985f,.78f),13);
            if(arcadeCards==null)arcadeCards=new UnityEngine.UI.Button[6];
            else if(arcadeCards.Length!=6)Array.Resize(ref arcadeCards,6);
            for(int i=0;i<6;i++)
            {
                if(arcadeCards[i]==null)arcadeCards[i]=CreateSectionControl("ArcadeCard"+i,"",arcadeRoot,Vector2.zero,Vector2.one,ColorOf("B9CCBB"),()=>{});
                AddSectionConsent(arcadeCards[i]);ExpansionFit(arcadeCards[i],10,13);
                arcadeCards[i].onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
            }
            CreateArcadeFooter(ref arcadePrimaryButton,"ArcadePrimary",.52f,1);
            CreateArcadeFooter(ref arcadeSecondaryButton,"ArcadeSecondary",0,.48f);
            ArcadeCreatePresentation();
            arcadeRoot.gameObject.SetActive(false);arcadeEntryButton.gameObject.SetActive(false);
            arcadeWired=Application.isPlaying;
        }
        void CreateArcadeFooter(ref UnityEngine.UI.Button button,string name,float left,float right)
        {
            if(button==null)button=CreateSectionControl(name,"",arcadeRoot,new Vector2(left,.015f),new Vector2(right,.16f),ColorOf("F4B85C"),()=>{});
            AddSectionConsent(button);ExpansionFit(button,10,13);button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.gameObject.SetActive(false);
        }
        RectTransform ArcadeBand(ref RectTransform band,string name,Vector2 min,Vector2 max,string fill)
        {
            if(band==null)band=arcadeRoot.Find(name) as RectTransform;
            if(band==null)band=Area(name,arcadeRoot,min,max);
            Place(band,arcadeRoot,min,max);Surface(band.gameObject,ColorOf(fill),18);
            return band;
        }
        void ArcadeText(ref TMP_Text text,string name,Transform parent,Vector2 min,Vector2 max,float size,Color color)
        {
            if(text==null){var existing=parent.Find(name);if(existing!=null)text=existing.GetComponent<TMP_Text>();}
            if(text==null)text=ProductText(name,parent,min,max,size,color);
            Place(text.rectTransform,parent,min,max);text.color=color;text.raycastTarget=false;text.enableAutoSizing=true;text.fontSizeMin=10;text.fontSizeMax=size;
        }
        JtnProductIcon ArcadeIcon(Transform parent,string name,ArcadeGlyph glyph,Vector2 min,Vector2 max,Color color)
        {
            var rect=parent.Find(name) as RectTransform;
            if(rect==null)rect=Area(name,parent,min,max);else Place(rect,parent,min,max);
            if(rect.GetComponent<CanvasRenderer>()==null)rect.gameObject.AddComponent<CanvasRenderer>();
            var icon=rect.GetComponent<JtnProductIcon>()??rect.gameObject.AddComponent<JtnProductIcon>();
            icon.Shape=glyph;icon.color=color;icon.raycastTarget=false;return icon;
        }
        void ArcadeVisibleSymbol(Transform parent,string name,string iconName,int symbol)
        {
            var icon=parent.Find(iconName) as RectTransform;if(icon==null)return;
            var rect=parent.Find(name) as RectTransform;
            if(rect==null)rect=Area(name,parent,icon.anchorMin,icon.anchorMax);else Place(rect,parent,icon.anchorMin,icon.anchorMax);
            if(rect.GetComponent<CanvasRenderer>()==null)rect.gameObject.AddComponent<CanvasRenderer>();
            var image=rect.GetComponent<UnityEngine.UI.Image>()??rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            bool known=SymbolIds.IsValid(symbol);
            image.sprite=known&&reelSymbols!=null&&symbol<reelSymbols.Length?reelSymbols[symbol]:null;
            image.preserveAspect=true;image.color=Color.white;image.raycastTarget=false;
            image.enabled=image.sprite!=null;rect.gameObject.SetActive(image.enabled);
            // A missing exact sprite leaves the literal name, never a guessed glyph.
            // Negative means a closed card or ordinary catalog, not a hidden face.
            icon.gameObject.SetActive(!known);
        }
        void ArcadeCreatePresentation()
        {
            ArcadeBand(ref arcadeDetailBand,"Arcade Detail",new Vector2(0,.19f),new Vector2(1,.765f),"F6F1E3");arcadeDetailBand.SetAsFirstSibling();
            // A transparent header row lined up with the panel title: icon, then the shared dark title.
            ArcadeBand(ref arcadeHeaderBand,"Arcade Header",new Vector2(PanelX(arcadeRoot,HeadingIconLeft),PanelY(arcadeRoot,HeadingBottom)),new Vector2(PanelX(arcadeRoot,HeadingTextRight),PanelY(arcadeRoot,HeadingTop)),"00000000");
            ArcadeBand(ref arcadeResourceBand,"Arcade Resource Strip",new Vector2(0,.77f),new Vector2(1,.895f),"E6E1CF");
            Place(arcadeTitle.rectTransform,arcadeHeaderBand,new Vector2(PanelX(arcadeHeaderBand,HeadingTextLeft),0),Vector2.one);
            SharedHeadingText(arcadeTitle);
            ArcadeText(ref arcadeSectionCaption,"Arcade Section Caption",arcadeHeaderBand,new Vector2(.16f,.02f),new Vector2(.97f,.28f),10,ColorOf("86E2E5"));
            // The shared heading has one line; the caption repeats the title or the body below it.
            arcadeSectionCaption.gameObject.SetActive(false);
            Place(arcadeResources.rectTransform,arcadeResourceBand,new Vector2(.035f,.075f),new Vector2(.965f,.925f));
            arcadeResources.color=ColorOf("263C50");arcadeResources.fontSizeMin=10;arcadeResources.fontSizeMax=12;
            ArcadeBand(ref arcadeRoundTrack,"Arcade Round Track",new Vector2(.025f,.648f),new Vector2(.975f,.657f),"263C50");
            if(arcadeRoundFill==null)arcadeRoundFill=arcadeRoundTrack.Find("Arcade Round Fill") as RectTransform;
            if(arcadeRoundFill==null)arcadeRoundFill=Area("Arcade Round Fill",arcadeRoundTrack,Vector2.zero,Vector2.one);
            Surface(arcadeRoundFill.gameObject,ColorOf("86E2E5"),2);
            ArcadeBand(ref arcadeItemBand,"Arcade Current Item",new Vector2(.02f,.50f),new Vector2(.98f,.64f),"263C50");
            ArcadeText(ref arcadeItemCaption,"Arcade Item Caption",arcadeItemBand,new Vector2(.24f,.59f),new Vector2(.96f,.94f),10,ColorOf("C8D8DB"));
            arcadeItemCaption.text="CURRENT FIND";
            ArcadeText(ref arcadeItemName,"Arcade Item Name",arcadeItemBand,new Vector2(.24f,.06f),new Vector2(.96f,.60f),19,ColorOf("F6F1E3"));
            arcadeItemName.fontStyle=FontStyles.Bold;
            ArcadeBand(ref arcadeResultBand,"Arcade Result Summary",new Vector2(.02f,.56f),new Vector2(.98f,.755f),"263C50");
            ArcadeText(ref arcadeResultCaption,"Arcade Result Caption",arcadeResultBand,new Vector2(.24f,.63f),new Vector2(.96f,.94f),10,ColorOf("C8D8DB"));
            ArcadeText(ref arcadeResultValue,"Arcade Result Value",arcadeResultBand,new Vector2(.24f,.07f),new Vector2(.96f,.65f),23,ColorOf("F8D986"));
            arcadeResultValue.fontStyle=FontStyles.Bold;arcadeResultValue.fontSizeMin=12;
            ArcadeHeading(ArcadeGlyph.Events,"YARD EVENTS");
            ArcadeIcon(arcadeItemBand,"Arcade Item Icon",ArcadeGlyph.Box,new Vector2(.045f,.16f),new Vector2(.20f,.84f),ColorOf("F8D986"));
            ArcadeVisibleSymbol(arcadeItemBand,"Arcade Item Symbol","Arcade Item Icon",-1);
            ArcadeIcon(arcadeResultBand,"Arcade Result Icon",ArcadeGlyph.Diamond,new Vector2(.045f,.22f),new Vector2(.20f,.82f),ColorOf("86E2E5"));
            for(int i=0;i<6;i++)ArcadeTileStyle(i,ArcadeGlyph.Events,ColorOf("263C50"),false);
            ArcadeResetPresentation();
            ArcadeActionStyle(arcadePrimaryButton,true,true);ArcadeActionStyle(arcadeSecondaryButton,false,true);
        }
        void ArcadeResetPresentation()
        {
            if(arcadeRoundTrack!=null)arcadeRoundTrack.gameObject.SetActive(false);
            if(arcadeItemBand!=null)arcadeItemBand.gameObject.SetActive(false);
            if(arcadeResultBand!=null)arcadeResultBand.gameObject.SetActive(false);
            Place(arcadeBody.rectTransform,arcadeRoot,new Vector2(.035f,.21f),new Vector2(.965f,.745f));
            arcadeBody.color=ColorOf("203448");arcadeBody.fontSizeMin=10;arcadeBody.fontSizeMax=14;
            arcadeBody.alignment=TextAlignmentOptions.MidlineLeft;
        }
        void ArcadeHeading(ArcadeGlyph glyph,string caption,string emblem=null)
        {
            arcadeSectionCaption.text=caption;
            var icon=ArcadeIcon(arcadeHeaderBand,"Arcade Header Icon",glyph,new Vector2(PanelX(arcadeHeaderBand,HeadingIconLeft),0),new Vector2(PanelX(arcadeHeaderBand,HeadingIconRight),1),ColorOf("86E2E5"));
            icon.ArtworkOverride=SectionSprite(emblem??(glyph==ArcadeGlyph.Gear?"emblem-settings":glyph==ArcadeGlyph.Shop?"emblem-shop":glyph==ArcadeGlyph.Upgrade?"emblem-upgrades":glyph==ArcadeGlyph.Energy?"emblem-energy":"emblem-events"));
        }
        void ArcadeScenario(string id)
        {ArcadeHeading(ArcadeGlyph.Events,id==ArcadeCatalog.Memory?"YARD FINDS":"SORTING SHIFT",id==ArcadeCatalog.Memory?"emblem-memory":"emblem-sorting");}
        void ArcadeActionStyle(UnityEngine.UI.Button button,bool primary,bool enabled)
        {
            button.targetGraphic=Surface(button.gameObject,ColorOf(!enabled?"C8D8DB":primary?"F4B85C":"C8D8DB"),16,true);
            var text=button.GetComponentInChildren<TMP_Text>(true);text.color=ColorOf("142638");text.fontStyle=FontStyles.Bold;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.84f,.96f,1);colors.pressedColor=new Color(.68f,.84f,.90f);colors.disabledColor=Color.white;button.colors=colors;
            // The footer actions share the inner-screen artwork; Parcels pieces keep their flat state colours.
            if((button==arcadePrimaryButton||button==arcadeSecondaryButton)&&StyleCInnerAction(button,primary)&&!primary)text.color=ColorOf("F6F1E3");
        }
        void ArcadeTileStyle(int index,ArcadeGlyph glyph,Color fill,bool round)
        {
            var button=arcadeCards[index];button.targetGraphic=Surface(button.gameObject,fill,16,true);
            var surface=(JtnProductSurface)button.targetGraphic;
            if(!round)
            {
                bool wasFlat=surface.sprite==null;StyleCInnerCard(button);
                // A colour tint left from the flat round state would darken the artwork.
                if(wasFlat&&surface.sprite!=null)surface.CrossFadeColor(Color.white,0,true,true);
            }
            else if(surface.sprite!=null)
            {
                // Round tiles stay flat: a Memory face or pair colour carries meaning.
                surface.sprite=null;surface.overrideSprite=null;surface.ArtworkTint=Color.white;
                button.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
            }
            var text=button.GetComponentInChildren<TMP_Text>(true);
            // Card text spans the whole tile beside a corner icon; JtnFaceInset keeps it off the artwork's rim and bevel.
            Place(text.rectTransform,button.transform,round?new Vector2(.04f,.04f):Vector2.zero,round?new Vector2(.96f,.43f):Vector2.one);
            text.color=ColorOf(round?"142638":"F6F1E3");text.fontSizeMin=round?10:9;text.fontSizeMax=round?12:13;
            text.alignment=round?TextAlignmentOptions.Center:TextAlignmentOptions.TopLeft;
            ArcadeIcon(button.transform,"Arcade Card Icon",glyph,round?new Vector2(.33f,.45f):new Vector2(.77f,.79f),round?new Vector2(.67f,.94f):new Vector2(.94f,.96f),ColorOf(round?"263C50":"86E2E5"));
            ArcadeVisibleSymbol(button.transform,"Arcade Card Symbol","Arcade Card Icon",-1);
        }
        void ArcadeRoundPresentation(ArcadeRunView run)
        {
            ArcadeScenario(run.Id);
            if(run.Ready)
            {if(run.ReadyReward==0)ArcadeResultPresentation("ROUND COMPLETE","No Diamonds earned",ArcadeGlyph.Check);
                else ArcadeResultPresentation("READY TO CLAIM · NOT CREDITED",run.ReadyReward+" diamonds",ArcadeGlyph.Diamond);return;}
            bool sorting=run.Id==ArcadeCatalog.Sorting;
            Place(arcadeBody.rectTransform,arcadeRoot,new Vector2(.035f,sorting?.62f:.668f),new Vector2(.965f,.765f));
            Place(arcadeRoundTrack,arcadeRoot,new Vector2(.025f,sorting?.60f:.648f),new Vector2(.975f,sorting?.609f:.657f));
            arcadeRoundTrack.gameObject.SetActive(true);
            float progress=run.Id==ArcadeCatalog.Memory?run.MatchedPairs/3f:run.Attempts/(float)EconomyRules.ArcadeSortingItems;
            arcadeRoundFill.anchorMin=Vector2.zero;arcadeRoundFill.anchorMax=new Vector2(Mathf.Clamp01(progress),1);arcadeRoundFill.offsetMin=arcadeRoundFill.offsetMax=Vector2.zero;
            if(run.Id==ArcadeCatalog.Sorting)
            {
                Place(arcadeItemBand,arcadeRoot,new Vector2(.02f,.455f),new Vector2(.98f,.59f));
                arcadeItemName.text=ArcadeCopy.SymbolLabel(run.CurrentSortingSymbol);arcadeItemBand.gameObject.SetActive(true);
                ArcadeVisibleSymbol(arcadeItemBand,"Arcade Item Symbol","Arcade Item Icon",run.CurrentSortingSymbol);
                for(int i=0;i<4;i++)
                {
                    float left=i%2*.52f,top=.435f-i/2*.13f;
                    Place((RectTransform)arcadeCards[i].transform,arcadeRoot,new Vector2(left,top-.12f),new Vector2(left+.48f,top));
                    ArcadeTileStyle(i,ArcadeGlyph.Box,ColorOf("C8D8DB"),true);
                    ArcadeVisibleSymbol(arcadeCards[i].transform,"Arcade Card Symbol","Arcade Card Icon",i);
                }
            }
        }
        void ArcadeResultPresentation(string caption,string value,ArcadeGlyph glyph)
        {
            arcadeResultCaption.text=caption;arcadeResultValue.text=value;arcadeResultBand.gameObject.SetActive(true);
            ArcadeIcon(arcadeResultBand,"Arcade Result Icon",glyph,new Vector2(.045f,.22f),new Vector2(.20f,.82f),ColorOf("86E2E5"));
            Place(arcadeBody.rectTransform,arcadeRoot,new Vector2(.035f,.215f),new Vector2(.965f,.535f));
        }
        public void ConfigureArcadeHud(TMP_Text counters)
        {arcadeHudCounters=counters;arcadeHudSession=null;arcadeHudSequence=-1;}
        bool IsArcadePanel()=>wavePanel>=ArcadeHome&&wavePanel<=ArcadeInfo;
        bool ArcadeCanOpen()=>CanOpenChapterPanel()&&!Session.RecoveryRequired&&!Session.Chapter.TransitionPending;
        void OpenArcadeEntry(){if(SectionAction(arcadeEntryButton,SectionHub))ShowArcadeHome();}
        static string ArcadeLabel(string id)=>id==ParcelsCatalog.Id?"Pigeon's Parcels":ArcadeCopy.Label(id)??id;
        static string ArcadeDescription(string id)=>ArcadeCopy.Description(id)??"";
        static string ArcadeNumber(long value)=>value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string ArcadeBalances(ArcadeView view)
        {
            var resources=Session.Expansion;
            return $"Diamonds: {ArcadeNumber(view.Diamonds)} · Tickets: {view.Tickets}/{view.TicketCap}\nParts: {resources.Parts}/{resources.PartsCap} · Potatoes: {resources.Potatoes}/{resources.PotatoesCap}\n"+ArcadeTicketProgress(view);
        }
        static string ArcadeTicketProgress(ArcadeView view)
        {
            return $"Cycle: {view.TicketProgress}/{view.TicketCadence} · {view.TicketCadence-view.TicketProgress} new spins left\n"+
                (view.Tickets>=view.TicketCap?"Storage full: the cycle ticket will not be added.":$"Limit {view.TicketCap}: extra tickets do not accumulate.");
        }
        void BeginArcade(int mode,string title,ArcadeView view)
        {
            ResetParcelsPresentation();
            BeginChapterPanel(mode,title);shownGuide=null;
            arcadeContextVersion++;arcadeContextValid=true;arcadeQuote=null;arcadeAbandonPreview=false;
            panelTitle.enabled=false;panelBody.enabled=false;
            arcadeTitle.text=title;arcadeResources.text=ArcadeBalances(view);arcadeBody.text="";arcadeBody.gameObject.SetActive(false);
            ArcadeResetPresentation();ArcadeHeading(ArcadeGlyph.Events,"YARD EVENTS");
            if(mode==ArcadePreview||mode==ArcadeInfo)arcadeBody.alignment=TextAlignmentOptions.TopLeft;
            for(int i=0;i<6;i++)
            {arcadeAvailable[i]=false;arcadeCards[i].onClick=new UnityEngine.UI.Button.ButtonClickedEvent();arcadeCards[i].gameObject.SetActive(false);}
            foreach(var button in new[]{arcadePrimaryButton,arcadeSecondaryButton})
            {button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.gameObject.SetActive(false);}
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
            arcadeRoot.SetAsLastSibling();arcadeRoot.gameObject.SetActive(true);
        }
        void BindArcade(UnityEngine.UI.Button button,string caption,ArcadeView view,Action action,bool enabled=true)
        {
            long version=arcadeContextVersion,sequence=view.Sequence;int mode=wavePanel;var session=Session;
            button.GetComponentInChildren<TMP_Text>(true).text=caption;
            button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
            if(enabled)button.onClick.AddListener(()=>
            {
                if(version!=arcadeContextVersion||!arcadeContextValid||!ReferenceEquals(session,Session)||Session.State.sequence!=sequence||!ArcadeCanOpen()||!SectionAction(button,mode))return;
                action();
            });
            button.interactable=enabled;button.gameObject.SetActive(true);
            if(button==arcadePrimaryButton||button==arcadeSecondaryButton)ArcadeActionStyle(button,button==arcadePrimaryButton,enabled);
        }
        void ArcadeCard(int index,string caption,ArcadeView view,Action action,bool enabled=true,bool round=false,Color? color=null,ArcadeGlyph glyph=ArcadeGlyph.Events)
        {
            float left=index%2*.52f,top=(round?.64f:.765f)-index/2*(round?.15f:.195f);
            var rect=(RectTransform)arcadeCards[index].transform;
            rect.anchorMin=new Vector2(left,top-(round?.135f:.185f));rect.anchorMax=new Vector2(left+.48f,top);rect.offsetMin=rect.offsetMax=Vector2.zero;
            arcadeAvailable[index]=enabled;BindArcade(arcadeCards[index],caption,view,action,enabled);
            var button=arcadeCards[index];ArcadeTileStyle(index,glyph,ColorOf("263C50"),round);
            if(round)button.targetGraphic.color=color??ColorOf("C8D8DB");
            else if(color.HasValue)button.transform.Find("Arcade Card Icon").GetComponent<JtnProductIcon>().color=color.Value;
            // Disabled Memory cards retain their semantic face/pair color. Input
            // stays disabled; hover/keyboard focus keeps the existing color block.
            var colors=button.colors;colors.disabledColor=Color.white;button.colors=colors;
        }
        ArcadeQuote ArcadeTryQuote(ArcadeView view,string action,string id,long instance=0,int target=-1)
        {
            try{return Session.GetArcadeQuote(action,id,view.Sequence,instance,target);}
            catch(InvalidOperationException){return null;}
        }
        void BindArcadeQuote(UnityEngine.UI.Button button,string caption,ArcadeView view,ArcadeQuote quote)
        {
            // Capture the displayed quote itself. A stale delegate never requotes.
            BindArcade(button,caption,view,()=>ExecuteArcadeQuote(quote),quote!=null);
        }
        void ShowArcadeHome()
        {
            if(!ArcadeCanOpen())return;var view=Session.Arcade;
            BeginArcade(ArcadeHome,"Mini-games & Diamonds",view);
            ArcadeOffer nextSlot=null;
            foreach(var offer in ArcadeCatalog.Offers)if(offer.Kind=="slot"&&offer.Level==view.SlotLevel+1){nextSlot=offer;break;}
            string nextStatus=nextSlot==null?"All levels unlocked":ArcadeOfferStatus(nextSlot,view,ArcadeTryQuote(view,"buy",nextSlot.Id)!=null);
            string slot=nextSlot==null?$"Slot {view.SlotLevel}/3\n"+nextStatus:$"Slot {view.SlotLevel}/3 · next {nextSlot.Level}\n"+
                ArcadeCost(nextSlot.PartsCost,nextSlot.PotatoesCost,nextSlot.DiamondsCost,0)+"\n"+nextStatus;
            ArcadeCard(0,slot,view,()=>ShowArcadeCategory(0),glyph:ArcadeGlyph.Upgrade);
            ArcadeCard(1,"Diamond shop\nParts & potatoes",view,()=>ShowArcadeCategory(1),glyph:ArcadeGlyph.Shop);
            ArcadeCard(2,"Bonus boost\nFor diamonds",view,()=>ShowArcadeCategory(2),glyph:ArcadeGlyph.Energy);
            foreach(var pair in new[]{new KeyValuePair<int,string>(3,ArcadeCatalog.Memory),new KeyValuePair<int,string>(4,ArcadeCatalog.Sorting)})
            {
                string id=pair.Value;var run=view.ActiveRun;string detail;
                if(run!=null&&run.Id==id)
                {
                    detail=run.Ready?(run.ReadyReward==0?"Finish · 0 diamonds\nRound complete":$"Ready to claim: {run.ReadyReward} diamonds\nConfirm result"):"Continue round";
                    detail+="\n"+ArcadeRunProgress(run);
                }
                else if(run!=null)
                    detail=$"Start: {EconomyRules.ArcadeStartTickets} tickets · unavailable now\n"+(run.Ready?"Result pending: ":"Saved round: ")+ArcadeLabel(run.Id);
                else if(Session.Parcels.ActiveRun!=null)detail="Unavailable · saved Pigeon's Parcels\nResume in Parcels";
                else
                {
                    bool canStart=ArcadeTryQuote(view,"start",id)!=null;
                    detail=$"Start: {EconomyRules.ArcadeStartTickets} tickets · rules\n"+(canStart?"Ready to start":view.Tickets<EconomyRules.ArcadeStartTickets?$"No ticket: {view.TicketCadence-view.TicketProgress} spins left":"Unavailable now");
                    if(view.LastClaim!=null&&view.LastClaim.GameId==id)detail+=view.LastClaim.Diamonds==0?"\nPrevious round finished without Diamonds.":$"\nPreviously received: {view.LastClaim.Diamonds} diamonds";
                }
                ArcadeCard(pair.Key,ArcadeLabel(id)+"\n"+detail,view,()=>ShowArcadeGame(id),glyph:id==ArcadeCatalog.Memory?ArcadeGlyph.Ticket:ArcadeGlyph.Box);
            }
            ArcadeCard(5,"Pigeon's Parcels\n"+ParcelsEntryStatus(Session.Parcels),view,ShowParcelsGame,glyph:ArcadeGlyph.Box);
            BindArcade(arcadeSecondaryButton,"Tickets & effects",view,ShowArcadeInformation);
        }
        void ShowArcadeCategory(int category)
        {
            if(!ArcadeCanOpen()||category<0||category>2)return;var view=Session.Arcade;
            BeginArcade(ArcadeCategory,category==0?"Slot levels":category==1?"Diamond shop":"Bonus boost",view);arcadeCategory=category;
            ArcadeHeading(category==0?ArcadeGlyph.Upgrade:category==1?ArcadeGlyph.Shop:ArcadeGlyph.Energy,category==0?"SLOT UPGRADES":"DIAMOND SHOP");
            int index=0;
            foreach(var offer in ArcadeCatalog.Offers)
            {
                if(category==0&&offer.Kind!="slot"||category==1&&offer.Kind!="pack"||category==2&&offer.Kind!="boost")continue;
                string id=offer.Id;bool owned=offer.Kind=="slot"&&view.SlotLevel>=offer.Level;
                bool canBuy=!owned&&ArcadeTryQuote(view,"buy",id)!=null;
                string status=ArcadeOfferStatus(offer,view,canBuy);
                string price=owned?"":"\n"+ArcadeCost(offer.PartsCost,offer.PotatoesCost,offer.DiamondsCost,0);
                ArcadeCard(index++,ArcadeLabel(id)+price+"\n"+status,view,()=>ShowArcadeOffer(id),color:ColorOf(owned?"B9CCBB":canBuy?"F4B85C":"D7E0C5"),glyph:offer.Kind=="slot"?ArcadeGlyph.Upgrade:offer.ArmsBonusBoost?ArcadeGlyph.Energy:offer.PartsCredit>0?ArcadeGlyph.Box:ArcadeGlyph.Leaf);
            }
            BindArcade(arcadeSecondaryButton,"Current effects",view,ShowArcadeInformation);
        }
        static string ArcadeCost(int parts,int potatoes,long diamonds,int tickets)
        {
            var terms=new List<string>(4);
            if(diamonds>0)terms.Add(ArcadeNumber(diamonds)+" diamonds");if(parts>0)terms.Add(parts+" parts");if(potatoes>0)terms.Add(potatoes+" potatoes");if(tickets>0)terms.Add(tickets+" tickets");
            return terms.Count==0?"No charge":string.Join(" + ",terms);
        }
        string ArcadeOfferStatus(ArcadeOffer offer,ArcadeView view,bool canBuy)
        {
            var resources=Session.Expansion;
            if(offer.Kind=="slot")
            {
                if(view.SlotLevel>=offer.Level)return "Already unlocked · inspect effects";
                if(view.SlotLevel!=offer.Level-1)return $"Level {offer.Level-1} required first";
                if(offer.Level==3&&!resources.BonusUnlocked)return "Bonus reel required";
            }
            if(offer.ArmsBonusBoost)
            {if(!resources.BonusUnlocked)return "Bonus reel required";if(resources.BoostArmed)return "Boost already active";}
            if(offer.PartsCredit>resources.PartsCap-resources.Parts)return $"Parts space: {resources.PartsCap-resources.Parts}/{offer.PartsCredit}";
            if(offer.PotatoesCredit>resources.PotatoesCap-resources.Potatoes)return $"Potato space: {resources.PotatoesCap-resources.Potatoes}/{offer.PotatoesCredit}";
            var missing=new List<string>(3);
            if(view.Diamonds<offer.DiamondsCost)missing.Add(ArcadeNumber(offer.DiamondsCost-view.Diamonds)+" diamonds");
            if(resources.Parts<offer.PartsCost)missing.Add((offer.PartsCost-resources.Parts)+" parts");
            if(resources.Potatoes<offer.PotatoesCost)missing.Add((offer.PotatoesCost-resources.Potatoes)+" potatoes");
            return missing.Count>0?"Missing: "+string.Join(", ",missing):canBuy?"Available to buy":"Unavailable now";
        }
        static string ArcadeRunProgress(ArcadeRunView run)
        {
            return run.Id==ArcadeCatalog.Memory?$"Pairs: {run.MatchedPairs}/3 · attempts: {run.Attempts}/{EconomyRules.ArcadeMemoryAttempts}":
                run.Ready?$"Correct: {run.Correct}/{EconomyRules.ArcadeSortingItems}":$"Completed: {run.Attempts}/{EconomyRules.ArcadeSortingItems} · correct: {run.Correct}";
        }
        static string ArcadeSlotIncrement(ArcadeOffer offer)
        {
            var selected=ArcadeCatalog.SlotAttributes(offer.Level);var previous=ArcadeCatalog.SlotAttributes(offer.Level-1);
            var increments=new List<string>();
            int parts=selected.BoxPresencePartBonus-previous.BoxPresencePartBonus;
            int potatoes=selected.PotatoPresencePotatoBonus-previous.PotatoPresencePotatoBonus;
            int rawParts=selected.RawBonusPartsAddition-previous.RawBonusPartsAddition;
            int rawPotatoes=selected.RawBonusPotatoesAddition-previous.RawBonusPotatoesAddition;
            if(parts>0)increments.Add($"parts +{parts} once per spin with a box.");
            if(potatoes>0)increments.Add($"potatoes +{potatoes} once per spin with a potato.");
            if(rawParts>0||rawPotatoes>0)increments.Add($"per bonus: base +{rawParts} parts, +{rawPotatoes} potatoes; before focus, boost and limits.");
            return $"Increase from level {offer.Level-1} to {offer.Level}: "+string.Join(" ",increments);
        }
        static string ArcadeAcquiredSlotEffect(ModelState before,ArcadeView after,ArcadeQuote quote)
        {
            var offer=ArcadeCatalog.Get(quote.Id);if(offer.Kind!="slot")return null;
            var previous=ArcadeEconomy.Project(before);
            if(after.SlotLevel!=previous.SlotLevel+1||after.SlotLevel!=offer.Level)return null;
            if(offer.Level==1)return $"Slot Parts: +{previous.BoxPresencePartBonus} → +{after.BoxPresencePartBonus} once per future spin with a Box; caps apply.";
            if(offer.Level==2)return $"Slot Potatoes: +{previous.PotatoPresencePotatoBonus} → +{after.PotatoPresencePotatoBonus} once per future spin with a Potato; caps apply.";
            return $"Slot raw addition: +{previous.RawBonusPartsAddition}/+{previous.RawBonusPotatoesAddition} → +{after.RawBonusPartsAddition}/+{after.RawBonusPotatoesAddition} Parts/Potatoes per future bonus before focus, boost and caps.";
        }
        string ArcadeOfferConditions(ArcadeOffer offer,ArcadeView view)
        {
            var resources=Session.Expansion;var facts=new List<string>();
            if(offer.Kind=="slot")
            {
                if(view.SlotLevel>=offer.Level)return "This level is already unlocked. It cannot be purchased again.";
                else if(view.SlotLevel!=offer.Level-1)facts.Add("The previous slot level is required. Its cost is separate.");
                if(offer.Level==3&&!resources.BonusUnlocked)facts.Add("An unlocked bonus reel is required.");
            }
            if(view.Diamonds<offer.DiamondsCost)facts.Add("Not enough diamonds: "+ArcadeNumber(offer.DiamondsCost-view.Diamonds)+".");
            if(resources.Parts<offer.PartsCost)facts.Add("Not enough parts: "+(offer.PartsCost-resources.Parts)+".");
            if(resources.Potatoes<offer.PotatoesCost)facts.Add("Not enough potatoes: "+(offer.PotatoesCost-resources.Potatoes)+".");
            if(offer.PartsCredit>resources.PartsCap-resources.Parts||offer.PotatoesCredit>resources.PotatoesCap-resources.Potatoes)facts.Add("Enough space is needed for the entire purchase.");
            if(offer.ArmsBonusBoost)
            {if(!resources.BonusUnlocked)facts.Add("An unlocked bonus reel is required.");if(resources.BoostArmed)facts.Add("Boost already active.");}
            return string.Join("\n",facts);
        }
        void ShowArcadeOffer(string id)
        {
            if(!ArcadeCanOpen())return;var view=Session.Arcade;var offer=ArcadeCatalog.Get(id);
            int category=offer.Kind=="slot"?0:offer.Kind=="pack"?1:2;
            BeginArcade(ArcadePreview,ArcadeLabel(id),view);arcadeCategory=category;
            ArcadeHeading(offer.Kind=="slot"?ArcadeGlyph.Upgrade:offer.ArmsBonusBoost?ArcadeGlyph.Energy:ArcadeGlyph.Shop,"OFFER PREVIEW");
            arcadeBody.gameObject.SetActive(true);
            bool owned=offer.Kind=="slot"&&view.SlotLevel>=offer.Level;
            string credit=offer.PartsCredit>0?$"\nReceive: {offer.PartsCredit} parts.":offer.PotatoesCredit>0?$"\nReceive: {offer.PotatoesCredit} potatoes.":"";
            // Current saved rates and the exact selected catalog level are separate
            // Core-owned projections. Locked targets never use current level + 1.
            string effects=offer.Kind!="slot"?"":$"Current: level {view.SlotLevel}/3.\nBox: +{view.BoxPresencePartBonus} parts; potato: +{view.PotatoPresencePotatoBonus} potatoes.\n"+
                $"Base bonus: +{view.RawBonusPartsAddition} parts, +{view.RawBonusPotatoesAddition} potatoes.";
            string target=offer.Kind=="slot"&&!owned?"\n\n"+ArcadeCopy.OfferAttributes(offer):"";
            string description=owned?"Unlocked level. Current slot effects:\n\n":offer.Kind=="slot"?"":ArcadeDescription(id)+credit;
            arcadeBody.text=description+effects+target+
                (offer.Kind=="slot"?"\n"+ArcadeSlotIncrement(offer):"")+
                (owned?"":"\n\nCost: "+ArcadeCost(offer.PartsCost,offer.PotatoesCost,offer.DiamondsCost,0)+(offer.Kind=="slot"?" (this level only).":""))+"\n"+ArcadeOfferConditions(offer,view);
            arcadeQuote=owned?null:ArcadeTryQuote(view,"buy",id);
            if(arcadeQuote!=null&&offer.Kind=="slot")
            {
                var resources=Session.Expansion;
                arcadeBody.text+=$"\nAfter purchase: {ArcadeNumber(view.Diamonds-arcadeQuote.DiamondsCost)} diamonds; {resources.Parts-arcadeQuote.PartsCost} parts; {resources.Potatoes-arcadeQuote.PotatoesCost} potatoes.";
            }
            if(arcadeQuote!=null&&(offer.Kind=="pack"||offer.ArmsBonusBoost))
                arcadeBody.text+="\n\n"+ArcadePurchaseContext(view,offer,arcadeQuote);
            if(offer.ArmsBonusBoost)arcadeBody.text+="\n"+BonusFocusStorageContext(ExpansionEconomy.Project(Session.State));
            BindArcadeQuote(arcadePrimaryButton,owned?"Already unlocked":"Buy\n"+ArcadeCost(offer.PartsCost,offer.PotatoesCost,offer.DiamondsCost,0),view,arcadeQuote);
            BindArcade(arcadeSecondaryButton,"To category",view,()=>ShowArcadeCategory(category));
        }
        string ArcadePurchaseContext(ArcadeView view,ArcadeOffer offer,ArcadeQuote quote)
        {
            // Current-snapshot opportunity cost, not a forecast of earnings/ROI.
            string text=$"After purchase: {ArcadeNumber(view.Diamonds-quote.DiamondsCost)} diamonds.";
            foreach(var next in ArcadeCatalog.Offers)
                if(next.Kind=="slot"&&next.Level==view.SlotLevel+1){text+=$" Level {next.Level}: {ArcadeNumber(next.DiamondsCost)} diamonds.";break;}
            if(offer.ArmsBonusBoost)
            {
                var resources=Session.Expansion;bool energyAvailable=false;
                try
                {
                    var alternative=Session.GetExpansionAction("boost",ExpansionCatalog.NextBonusBoost);
                    energyAvailable=alternative.ExpectedSequence==view.Sequence;
                }
                catch(InvalidOperationException){ }
                text+=$"\nEnergy: {resources.Energy}/{EconomyRules.ExpansionBoostCost}"+
                    (energyAvailable?" · a separate boost is available without diamonds.":" · a separate boost is unavailable now.")+
                    (resources.Parts==resources.PartsCap&&resources.Potatoes==resources.PotatoesCap?" Storage full: no room.":$" Until bonus: {resources.BonusCadence-resources.BonusProgress} spins.");
            }
            return text;
        }
        void ShowArcadeGame(string id)
        {
            if(!ArcadeCanOpen()||(id!=ArcadeCatalog.Memory&&id!=ArcadeCatalog.Sorting))return;var view=Session.Arcade;
            if(view.ActiveRun!=null&&view.ActiveRun.Id==id){ShowArcadeRound();return;}
            BeginArcade(ArcadePreview,ArcadeLabel(id),view);arcadeCategory=-1;arcadeBody.gameObject.SetActive(true);
            ArcadeScenario(id);
            arcadeBody.text=ArcadeDescription(id)+"\n\n"+(id==ArcadeCatalog.Memory?
                $"6 cards, 3 pairs. Up to {EconomyRules.ArcadeMemoryAttempts} attempts.\nAll pairs in 3–4 attempts: {EconomyRules.ArcadeMemoryReward(3,true)} diamonds; in 5–6: {EconomyRules.ArcadeMemoryReward(5,true)}; in 7–9: {EconomyRules.ArcadeMemoryReward(7,true)}. Incomplete: {EconomyRules.ArcadeMemoryReward(9,false)}.":
                $"{EconomyRules.ArcadeSortingItems} items, 4 bins.\n7–8 correct: {EconomyRules.ArcadeSortingReward(7)} diamonds; 5–6: {EconomyRules.ArcadeSortingReward(5)}; 3–4: {EconomyRules.ArcadeSortingReward(3)}; 0–2: {EconomyRules.ArcadeSortingReward(0)}.")+
                $"\nStart: {EconomyRules.ArcadeStartTickets} ticket. Reward is granted only after completion and confirmation.";
            if(view.ActiveRun!=null||Session.Parcels.ActiveRun!=null)
            {
                arcadeBody.text+="\nFirst finish or abandon the saved round.";
                BindArcade(arcadePrimaryButton,"To saved round",view,ShowArcadeRound);
            }
            else
            {
                arcadeQuote=ArcadeTryQuote(view,"start",id);
                BindArcadeQuote(arcadePrimaryButton,"Start for a ticket",view,arcadeQuote);
                if(view.Tickets<EconomyRules.ArcadeStartTickets)
                {
                    arcadeBody.text+="\n"+ArcadeCopy.TicketSummary(view);
                    BindArcade(arcadeSecondaryButton,"To game",view,ReturnArcadeToGame);
                }
            }
        }
        void ReturnArcadeToGame()
        {
            HideWaveChoices();panel.SetActive(false);Render();
        }
        static string ArcadeReachableReward(ArcadeRunView run)
        {
            if(run.Id==ArcadeCatalog.Memory)
            {
                int earliest=run.Attempts+(3-run.MatchedPairs);
                int best=earliest<=EconomyRules.ArcadeMemoryAttempts?EconomyRules.ArcadeMemoryReward(earliest,true):0;
                return best==0?"On completion: 0 diamonds. There are not enough attempts left to find all pairs.":$"For all pairs: up to {best} diamonds; otherwise 0.";
            }
            int minimum=EconomyRules.ArcadeSortingReward(run.Correct);
            int remaining=EconomyRules.ArcadeSortingItems-run.Attempts;
            int maximum=EconomyRules.ArcadeSortingReward(run.Correct+remaining);
            if(maximum>minimum)
                for(int correct=run.Correct+1;correct<=run.Correct+remaining;correct++)
                    if(EconomyRules.ArcadeSortingReward(correct)>=maximum)
                        return $"On completion: {minimum}-{maximum} diamonds; {correct-run.Correct} more correct for {maximum}.";
            return minimum==0&&maximum==0?"On completion: 0 diamonds · finish separately.":$"On completion: {minimum} diamonds · claim separately.";
        }
        void ShowArcadeRound()
        {
            if(ArcadeCanOpen()&&Session.Parcels.ActiveRun!=null){ShowParcelsRound();return;}
            if(!ArcadeCanOpen())return;var view=Session.Arcade;var run=view.ActiveRun;
            if(run==null){ShowArcadeHome();return;}
            BeginArcade(run.Ready?ArcadeResult:ArcadeRound,ArcadeLabel(run.Id)+(run.Ready?" · result":" · round"),view);arcadeCategory=-1;
            arcadeBody.gameObject.SetActive(true);
            if(run.Ready)
            {
                arcadeBody.text=$"Round complete.\n"+ArcadeRunProgress(run)+
                    (run.ReadyReward==0?"\n":$"\nReady to claim: {run.ReadyReward} diamonds\n")+ArcadeDescription(run.ReadyReward==0?"action.claim-zero":"reward.available");
                arcadeQuote=ArcadeTryQuote(view,"claim",run.Id,run.InstanceSequence);
                BindArcadeQuote(arcadePrimaryButton,run.ReadyReward==0?"Finish · 0 diamonds":$"Claim {run.ReadyReward} diamonds",view,arcadeQuote);
                if(arcadeQuote==null)arcadeBody.text+="\nThe reward does not fit in the balance yet. The result is saved; you can return later.";
            }
            else
            {
                arcadeBody.rectTransform.anchorMin=new Vector2(.015f,.66f);arcadeBody.rectTransform.anchorMax=new Vector2(.985f,.78f);
                if(run.Id==ArcadeCatalog.Memory)
                {
                    arcadeBody.text=ArcadeRunProgress(run)+"\n"+(run.Phase=="mismatch"?"No match. Tap “Next”.":run.Phase=="second"?"Turn over the second card.":"Turn over a card.")+"\n"+ArcadeReachableReward(run);
                    for(int i=0;i<6;i++)
                    {
                        int face=run.MemoryFaces[i];bool matched=(run.MatchedMask&(1<<i))!=0;
                        bool canPick=!matched&&face<0&&(run.Phase=="first"||run.Phase=="second");
                        var quote=canPick?ArcadeTryQuote(view,"pick",run.Id,run.InstanceSequence,i):null;
                        string state=matched?" · pair":face>=0?" · face up":"";
                        string color=matched?"B9CCBB":face>=0?"F4B85C":canPick?"ABD2D8":"D7E0C5";
                        ArcadeCard(i,$"{i+1}. "+(face<0?"Face down":ArcadeCopy.SymbolLabel(face))+state,view,()=>ExecuteArcadeQuote(quote),quote!=null,true,ColorOf(color),ArcadeGlyph.Ticket);
                        ArcadeVisibleSymbol(arcadeCards[i].transform,"Arcade Card Symbol","Arcade Card Icon",face);
                    }
                    if(run.Phase=="mismatch")BindArcadeQuote(arcadePrimaryButton,"Next · turn cards over",view,ArcadeTryQuote(view,"continue",run.Id,run.InstanceSequence));
                }
                else
                {
                    arcadeBody.text=$"Item {run.Attempts+1}/{EconomyRules.ArcadeSortingItems} · correct: {run.Correct}\nNow: "+ArcadeCopy.SymbolLabel(run.CurrentSortingSymbol)+"\n"+ArcadeReachableReward(run);
                    for(int i=0;i<4;i++)
                    {int target=i;var quote=ArcadeTryQuote(view,"sort",run.Id,run.InstanceSequence,target);ArcadeCard(i,ArcadeCopy.SymbolLabel(i),view,()=>ExecuteArcadeQuote(quote),quote!=null,true);}
                }
            }
            ArcadeRoundPresentation(run);
            BindArcade(arcadeSecondaryButton,"Abandon…",view,ShowArcadeAbandon);
            closeButton.GetComponentInChildren<TMP_Text>(true).text="Save and return to mini-games";
        }
        void ShowArcadeAbandon()
        {
            if(ArcadeCanOpen()&&Session.Parcels.ActiveRun!=null){ShowParcelsAbandon();return;}
            if(!ArcadeCanOpen())return;var view=Session.Arcade;var run=view.ActiveRun;if(run==null)return;
            var quote=ArcadeTryQuote(view,"abandon",run.Id,run.InstanceSequence);
            BeginArcade(ArcadePreview,"Abandon round?",view);arcadeCategory=-1;arcadeQuote=quote;arcadeAbandonPreview=true;
            ArcadeHeading(ArcadeGlyph.Events,"CONFIRM ABANDONMENT");
            bool forfeitsReward=run.Ready&&run.ReadyReward>0;
            arcadeBody.gameObject.SetActive(true);
            arcadeBody.text=forfeitsReward?$"Unclaimed reward: {run.ReadyReward} diamonds.\nAbandoning forfeits it. Diamonds in your wallet are not deducted; the ticket is not returned.":
                run.Ready?"Reward for this result: 0 diamonds; the ticket is not returned.\nThe result will close without a reward.":ArcadeDescription("action.abandon");
            BindArcadeQuote(arcadePrimaryButton,forfeitsReward?$"Forfeit {run.ReadyReward} diamonds":"Abandon without reward",view,quote);
            BindArcade(arcadeSecondaryButton,run.Ready?"To result":"Continue round",view,ShowArcadeRound);
            closeButton.GetComponentInChildren<TMP_Text>(true).text=run.Ready?"Cancel · to result":"Cancel · to round";
        }
        void ShowArcadeInformation()
        {
            if(!ArcadeCanOpen())return;var view=Session.Arcade;
            BeginArcade(ArcadeInfo,"Tickets & effects",view);arcadeBody.gameObject.SetActive(true);
            ArcadeHeading(ArcadeGlyph.Gear,"RULES & CURRENT EFFECTS");
            arcadeBody.text=ArcadeCopy.TicketSummary(view)+$" For every {view.TicketCadence} new spins.\nThe counter advances even when storage is full; extra tickets do not accumulate. No tickets are earned while away.\n\n"+
                $"Slot level: {view.SlotLevel}/3. Current effects:\nParts: +{view.BoxPresencePartBonus} once per spin with a Box.\nPotatoes: +{view.PotatoPresencePotatoBonus} once per spin with a Potato.\n"+
                $"Base bonus: +{view.RawBonusPartsAddition} parts, +{view.RawBonusPotatoesAddition} potatoes.\nStorage limits apply.\n\n"+
                ArcadeCopy.NextAttributes(view)+"\n\nReward animation is a visual setting for this session.";
            BindArcade(arcadePrimaryButton,rewardFeedbackReduced?"Enable animation":"Reduce effects",view,()=>{SetRewardFeedbackReduced(!rewardFeedbackReduced);ShowArcadeInformation();});
        }
        void ExecuteArcadeQuote(ArcadeQuote quote)
        {
            if(quote==null||!arcadeContextValid||!ArcadeCanOpen()||Session.State.sequence!=quote.ExpectedSequence)return;
            var before=Session.State;var source=arcadeRoot.TransformPoint(arcadeRoot.rect.center);
            HideWaveChoices();
            try
            {
                var after=Session.ExecuteArcade(quote,UtcSeconds());var view=Session.Arcade;var receipt=view.LastEvent;
                bool fresh=after.sequence>before.sequence&&after.sequence==quote.Sequence&&receipt!=null&&receipt.Sequence==after.sequence&&receipt.Kind==quote.Action&&
                    receipt.InstanceSequence==quote.ExpectedInstance;
                if(fresh)ObserveCommitted(before,after);
                if(fresh&&(quote.Action=="pick"||quote.Action=="sort"||quote.Action=="continue"||quote.Action=="start"))
                {
                    ShowArcadeRound();
                    if(quote.Action=="sort")
                    {
                        var previous=ArcadeEconomy.Project(before).ActiveRun;var current=view.ActiveRun;
                        if(previous!=null&&current!=null&&previous.Id==ArcadeCatalog.Sorting&&current.Id==previous.Id&&previous.InstanceSequence==quote.ExpectedInstance&&
                            current.InstanceSequence==previous.InstanceSequence&&current.Attempts==previous.Attempts+1)
                        {
                            int correct=current.Correct-previous.Correct;
                            if(correct==1)arcadeBody.text+="\nCorrect: score +1.";
                            else if(correct==0)arcadeBody.text+="\nIncorrect: item passed without a point.";
                        }
                    }
                }
                else
                {
                    BeginArcade(ArcadeResult,fresh?(quote.Action=="claim"?"Result confirmed":quote.Action=="abandon"?"Round abandoned":"Purchase saved"):"No new action",view);arcadeCategory=-1;
                    arcadeBody.gameObject.SetActive(true);
                    string acquired=fresh&&quote.Action=="buy"?ArcadeAcquiredSlotEffect(before,view,quote):null;
                    arcadeBody.text=fresh?(quote.Action=="claim"?(receipt.DiamondsAdded==0?"Round finished. No Diamonds earned.":$"Credited: {receipt.DiamondsAdded} diamonds."):quote.Action=="abandon"?"Round closed without a ticket refund or reward.":ArcadeLabel(quote.Id)+"\nSpent: "+ArcadeCost(quote.PartsCost,quote.PotatoesCost,quote.DiamondsCost,quote.TicketsCost)+
                        (receipt.PartsAdded>0?$"\nCredited: {receipt.PartsAdded} parts.":receipt.PotatoesAdded>0?$"\nCredited: {receipt.PotatoesAdded} potatoes.":"\n"+(acquired??"Effect saved."))):"No repeat charge or credit. Reopen the section.";
                    if(fresh&&quote.Action=="buy")
                    {
                        var offer=ArcadeCatalog.Get(quote.Id);int category=offer.Kind=="slot"?0:offer.Kind=="pack"?1:2;
                        BindArcade(arcadeSecondaryButton,"To category",view,()=>ShowArcadeCategory(category));
                    }
                    if(fresh&&quote.Action=="claim"&&receipt.DiamondsAdded>0)
                    {
                        ArcadeOffer nextSlot=null;
                        foreach(var offer in ArcadeCatalog.Offers)if(offer.Kind=="slot"&&offer.Level==view.SlotLevel+1){nextSlot=offer;break;}
                        if(nextSlot!=null)
                        {
                            string nextSlotId=nextSlot.Id;
                            BindArcade(arcadePrimaryButton,"Inspect\n"+ArcadeLabel(nextSlotId),view,()=>ShowArcadeOffer(nextSlotId));
                            arcadeBody.text+="\nPreview only; no purchase.";
                        }
                        else if(view.SlotLevel==3)arcadeBody.text+="\nAll three slot levels are unlocked. Further progress is optional.";
                        BindArcade(arcadeSecondaryButton,"To game",view,ReturnArcadeToGame);
                    }
                    if(fresh)
                    {
                        if(quote.Action=="claim")ArcadeResultPresentation(receipt.DiamondsAdded==0?"ROUND FINISHED":"RECEIVED",receipt.DiamondsAdded==0?"No Diamonds earned":receipt.DiamondsAdded+" diamonds",ArcadeGlyph.Check);
                        else if(quote.Action=="buy")ArcadeResultPresentation("PURCHASE SAVED",ArcadeLabel(quote.Id),ArcadeGlyph.Shop);
                        else if(quote.Action=="abandon")ArcadeResultPresentation("NO REWARD OR TICKET REFUND","Round closed",ArcadeGlyph.Events);
                    }
                }
                Render(fresh?"Action saved.":"No new action.");
                if(fresh&&(quote.Action=="claim"||quote.Action=="buy"))
                {QueueRewardFeedback(before,after,RewardFeedbackOrigin.Arcade,source);RevealRewardFeedback(after);}
            }
            catch(Exception error)
            {
                if(Session.RecoveryRequired){ClearRewardFeedback();ObserveOperationFailure();Error(error.Message);return;}
                var view=Session.Arcade;BeginArcade(ArcadeResult,"Action unavailable",view);arcadeCategory=-1;
                arcadeBody.gameObject.SetActive(true);arcadeBody.text="Requirements changed. Check the saved round or offer again; there is no automatic retry.";
                BindArcade(arcadeSecondaryButton,"To saved round",view,ShowArcadeRound,view.ActiveRun!=null);
                Render("Check the current state.");
            }
        }
        bool TryReturnArcade()
        {
            if(!IsArcadePanel())return false;
            if(!arcadeContextValid)return false;
            if(!ArcadeCanOpen()||offlineSummaryPanel||autoHintPanel||exitPanel)return true;
            if(wavePanel==ArcadePreview&&parcelsFinishPreview)ShowParcelsRound();
            else if(wavePanel==ArcadePreview&&arcadeAbandonPreview)ShowArcadeRound();
            else if(wavePanel==ArcadeHome)ShowSectionsHub();
            else if(wavePanel==ArcadePreview&&arcadeCategory>=0)ShowArcadeCategory(arcadeCategory);
            else ShowArcadeHome();
            return true;
        }
        void InvalidateArcadeUi()
        {
            ResetParcelsPresentation();
            arcadeContextVersion++;arcadeContextValid=false;arcadeQuote=null;arcadeAbandonPreview=false;
            if(arcadeRoot!=null)arcadeRoot.gameObject.SetActive(false);if(arcadeEntryButton!=null)arcadeEntryButton.gameObject.SetActive(false);
            if(arcadeBody!=null)ArcadeResetPresentation();
            for(int i=0;i<6;i++)arcadeAvailable[i]=false;
            if(arcadeCards!=null)foreach(var button in arcadeCards)ResetArcadeButton(button);
            ResetArcadeButton(arcadePrimaryButton);ResetArcadeButton(arcadeSecondaryButton);InvalidateExpansionConsent(arcadeEntryButton);
            if(panelTitle!=null)panelTitle.enabled=true;if(panelBody!=null)panelBody.enabled=true;
        }
        static void ResetArcadeButton(UnityEngine.UI.Button button)
        {if(button!=null){button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.interactable=false;InvalidateExpansionConsent(button);}}
        void RenderArcadeVisibility(bool usable)
        {
            if(!arcadeWired)return;
            arcadeEntryButton.gameObject.SetActive(usable&&panel.activeSelf&&wavePanel==SectionHub);arcadeEntryButton.interactable=usable;
            bool visible=usable&&panel.activeSelf&&IsArcadePanel()&&arcadeContextValid;
            arcadeRoot.gameObject.SetActive(visible);
            // Preserve unavailable/zero-headroom/overflow states established by binding.
            if(!visible){foreach(var button in arcadeCards)button.interactable=false;arcadePrimaryButton.interactable=arcadeSecondaryButton.interactable=false;}
            if(usable&&panel.activeSelf&&IsArcadePanel()&&!arcadeContextValid&&!Busy&&!failed&&!InBackground&&
                Session!=null&&!Session.RecoveryRequired&&Session.State!=null&&!offlineSummaryPanel&&!autoHintPanel&&!exitPanel)
            {
                panelTitle.enabled=panelBody.enabled=true;
                panelTitle.text="Mini-games";
                panelBody.text="This window must be reopened. Tap “Close”, then open “Events” in the bottom menu.";
                closeButton.GetComponentInChildren<TMP_Text>(true).text="Close";
            }
        }
        void RenderArcadeHud(ModelState state)
        {
            if(!arcadeWired||arcadeHudCounters==null||Session==null||state==null||Busy||failed||InBackground||Session.RecoveryRequired)return;
            if(ReferenceEquals(Session,arcadeHudSession)&&state.sequence==arcadeHudSequence)return;
            var view=ArcadeEconomy.Project(state);arcadeHudSession=Session;arcadeHudSequence=state.sequence;
            string diamonds=view.Diamonds>999999?"999999+":ArcadeNumber(view.Diamonds);
            arcadeHudCounters.text=$"Diamonds: {diamonds}\nTickets: {view.Tickets}/{view.TicketCap}";
            var activity=SavedArcadeActivity(state,view);
            arcadeHudCounters.enableAutoSizing=activity!=null;arcadeHudCounters.fontSizeMin=8;arcadeHudCounters.fontSizeMax=10;arcadeHudCounters.fontSize=10;
            if(activity!=null)arcadeHudCounters.text+="\n"+activity;
            ConfigureRewardFeedbackTargets(arcadeHudCounters.rectTransform,expansionHudResources?.rectTransform,arcadeHudCounters.rectTransform);
        }
    }
}
