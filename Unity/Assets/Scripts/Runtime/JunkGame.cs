using System;
using System.Collections;
using System.IO;
using JunkToNeon.Core;
using JunkToNeon.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JunkToNeon
{
    // Runtime shell only. Economic transitions are committed by GameSession before any effects.
    public sealed partial class JunkGame : MonoBehaviour
    {
        public static JunkGame Instance { get; private set; }
        public GameSession Session { get; private set; }
        public bool Busy { get; private set; }
        public bool AutoRunning { get; private set; }
        public bool InBackground { get; private set; }
        public int PresentedFrames { get; private set; }
        [SerializeField] RectTransform safe, world;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] TMP_Text wallet, heading, stageLabel, message, projectLabel, projectPrice, autoLabel;
        TMP_Text spinLabel;
        [SerializeField] TMP_Text[] reels = new TMP_Text[3];
        [SerializeField] UnityEngine.UI.Image[] reelImages = new UnityEngine.UI.Image[3];
        [SerializeField] Sprite[] reelSymbols;
        JtnSafeArea safeFitter;
        [SerializeField] Button spinButton, autoButton, projectButton;
        [SerializeField] Image house;
        [SerializeField] Sprite[] houseStages;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text panelTitle, panelBody;
        [SerializeField] Button selectButton, closeButton, reloadButton;
        Button.ButtonClickedEvent sharedSelectOriginalEvent;
        [SerializeField] Button workshopButton;
        [SerializeField] TMP_Text workshopLabel, workshopMarker;
        [SerializeField] UnityEngine.UI.Image workshopDraft;
        [SerializeField] bool sceneUiAuthored;
        bool ownsFont, ownsSprites;
        bool failed, exitPanel, autoHintPanel, restoreConfirmation, previewWorkshop;
        bool exitSelectBorrowed, exitSelectPreviousInteractable;
        bool offlineSummaryPanel;
        double presentationElapsed, presentationSampleAt;
        bool presentationClockActive;
        OfflineReceipt pendingOfflineSummary;
        Rect lastSafe;
        Vector2 lastScreenSize;
        float lastWorldHeight;
        readonly string[] names = { "POTATO", "BOX", "TRASH", "PIGEON" };

        void Awake()
        {
            Instance = this;
            StartDiagnostics();
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            if(!sceneUiAuthored)
            {
                font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("LiberationSans"));
                font.name = "Prototype runtime font";ownsFont=true;
                BuildUi();
            }
            else
            {
                spinButton.onClick.AddListener(Spin);autoButton.onClick.AddListener(ToggleAuto);projectButton.onClick.AddListener(ShowProject);
                selectButton.onClick.AddListener(SelectProjectFromUi);closeButton.onClick.AddListener(ClosePanelFromUi);reloadButton.onClick.AddListener(ReloadFromUi);
            }
            if(Application.isPlaying&&!panel.TryGetComponent<JtnPanelFade>(out _))panel.AddComponent<JtnPanelFade>();
            spinLabel=spinButton.GetComponentInChildren<TMP_Text>();
            foreach(var action in new[]{selectButton,closeButton,reloadButton})
                if(action!=null&&!action.TryGetComponent<JtnPanelPointerConsent>(out _))action.gameObject.AddComponent<JtnPanelPointerConsent>();
            if(workshopButton==null)CreateWorkshopUi();
            else workshopButton.onClick.AddListener(ShowWorkshop);
            // Authored RectTransforms stay intact; BuildUi supplies fallback defaults.
            safeFitter=safe.GetComponent<JtnSafeArea>();
            if(safeFitter==null)safeFitter=safe.gameObject.AddComponent<JtnSafeArea>();
            CreateWaveUi();CreateChapterUi();CreateJourneyUi();CreateSectionsUi();CreateExpansionUi();
            CreateArcadeIntegrationUi();
            CreateProductUi();
            CreateInnerUi();
            Canvas.ForceUpdateCanvases();
            OpenSession();
            if(!TryGetComponent<JunkToNeon.Presentation.JtnBackNavigation>(out var back))
                back=gameObject.AddComponent<JunkToNeon.Presentation.JtnBackNavigation>();
            back.Configure(
                () => failed ? (restoreConfirmation ? JunkToNeon.Presentation.BackContext.RestoreConfirmation : JunkToNeon.Presentation.BackContext.BlockingError)
                    : (panel.activeSelf ? JunkToNeon.Presentation.BackContext.DismissiblePanel : JunkToNeon.Presentation.BackContext.Root),
                ClosePanel, Reload, ShowExitConfirmation, StopAuto);
        }
        void OpenSession()
        {
            ReleaseExitSelect();
            ResetProductSpinReceipt();Busy=false;presentationClockActive=false;ResetReelMotion();
            ClearProductRoute();
            ClearRewardFeedback();
            Session=null;offlineSummaryPanel=false;pendingOfflineSummary=null;
            bool catalogReady=false;
            try
            {
                ValidateCatalogResources();
                catalogReady=true;
                string saveDirectory=Application.persistentDataPath;
#if UNITY_EDITOR
                // A private per-process folder keeps Editor verification away from normal saves.
                saveDirectory=Environment.GetEnvironmentVariable("JTN_EDITOR_SAVE_DIRECTORY")??saveDirectory;
#endif
                Session = GameSession.Open(new GameStore(new FileSnapshotStorage(saveDirectory), new JsonStateCodec()),
                    unchecked((uint)DateTime.UtcNow.Ticks) | 1u,initializeJourney:true);
                failed = Session.RecoveryRequired;
                if (failed) { ObserveOperationFailure(); Error(Session.Error); return; }
                var settled=Session.SettleOffline(UtcSeconds());
                pendingOfflineSummary=settled.lastSettlement?.coins>0&&settled.lastSettlement.sequence>settled.lastSettlementSeen?settled.lastSettlement:null;
                RememberProductSpinReceipt(settled,true);
                Render();
                if(!ShowOfflineSummary())ShowAutoHint();
            }
            catch (Exception e) { if(!catalogReady)ObserveRequiredResourceFailure();else ObserveOperationFailure(); Error(e.Message); }
        }
        void BuildUi()
        {
            var canvasObject = new GameObject("Portrait Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844); scaler.matchWidthOrHeight = .5f;
            safe = Area("Safe Area", canvasObject.transform, Vector2.zero, Vector2.one);
            if(safe.GetComponent<JtnSafeArea>()==null)safe.gameObject.AddComponent<JtnSafeArea>();
            world = Box("World 60%", safe, new Vector2(0,.4f), Vector2.one, ColorOf("B9CCBB"));
            Box("Sky", world, new Vector2(0,.3f), new Vector2(1,.8f), ColorOf("DBE2C6"));
            Box("Yard", world, Vector2.zero, new Vector2(1,.3f), ColorOf("A9B39B"));
            Label("JUNK → NEON", world, new Vector2(.06f,.91f), new Vector2(.86f,.98f), 20, ColorOf("203A30")).name="СВАЛКА → НЕОН";
            Label("01 / A NEW START", world, new Vector2(.06f,.86f), new Vector2(.9f,.91f), 10, ColorOf("506B56"));
            wallet = Label("37 COINS", world, new Vector2(.06f,.75f), new Vector2(.94f,.85f), 24, ColorOf("203A30"));
            stageLabel = Label("ABANDONED YARD", world, new Vector2(.06f,.51f), new Vector2(.95f,.56f), 11, ColorOf("506B56"));
            heading = Label("Every place has a future.", world, new Vector2(.06f,.56f), new Vector2(.95f,.65f), 20, ColorOf("203A30"));
            var houseRect = Area("House draft", world, new Vector2(.5f,.13f), new Vector2(.5f,.13f));
            house = houseRect.gameObject.AddComponent<Image>(); house.raycastTarget = false;
            var tex = Resources.Load<Texture2D>("house-stages");
            if ((houseStages==null||houseStages.Length!=3) && tex != null)
            {
                ownsSprites=true;
                houseStages = new Sprite[3];
                int[] starts = {0,652,1248}, widths = {652,596,694};
                for (int i=0;i<3;i++) houseStages[i] = Sprite.Create(tex, new Rect(starts[i],0,widths[i],809), new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            }
            else if(houseStages==null||houseStages.Length!=3) { house.color = ColorOf("8F7560"); }
            projectButton = Button("Project", "Repair house", world, new Vector2(.06f,.015f), new Vector2(.94f,.12f), ColorOf("F2EAD2"), ShowProject);
            projectLabel = projectButton.GetComponentInChildren<TMP_Text>();
            projectPrice = Label("40", world, new Vector2(.70f,.12f),new Vector2(.94f,.17f),12,ColorOf("203A30"),TextAlignmentOptions.Right);
            var controls = Box("Controls 40%", safe, Vector2.zero, new Vector2(1,.4f), ColorOf("172C29"));
            Label("YARD FINDS",controls,new Vector2(.06f,.9f),new Vector2(.94f,.98f),11,ColorOf("ADC3AB")).name="НАХОДКИ ДЛЯ ДВОРА";
            for(int i=0;i<3;i++)
            {
                float left=.05f+i*.31f;
                var frame = Box("Reel "+i, controls,new Vector2(left,.51f),new Vector2(left+.28f,.88f),ColorOf("F0EAD4"));
                reels[i]=Label(names[i],frame,new Vector2(.04f,.08f),new Vector2(.96f,.92f),14,ColorOf("31574D"),TextAlignmentOptions.Center);
                var icon=Area("Symbol",frame,new Vector2(.5f,.5f),new Vector2(.5f,.5f));icon.sizeDelta=new Vector2(64,64);
                reelImages[i]=icon.gameObject.AddComponent<UnityEngine.UI.Image>();reelImages[i].preserveAspect=true;reelImages[i].raycastTarget=false;reelImages[i].enabled=false;
            }
            message = Label("Loading…",controls,new Vector2(.05f,.32f),new Vector2(.95f,.49f),12,ColorOf("D7E0C5"),TextAlignmentOptions.Center);
            autoButton=Button("Auto","AUTO",controls,new Vector2(.05f,.09f),new Vector2(.29f,.30f),ColorOf("71846B"),ToggleAuto);
            autoLabel=autoButton.GetComponentInChildren<TMP_Text>();
            spinButton=Button("Spin","SPIN",controls,new Vector2(.32f,.09f),new Vector2(.95f,.30f),ColorOf("F4B85C"),Spin);
            spinLabel=spinButton.GetComponentInChildren<TMP_Text>();
            Label("FREE · EVERY SPIN EARNS COINS",controls,new Vector2(.03f,.01f),new Vector2(.97f,.075f),8,ColorOf("ADC3AB"),TextAlignmentOptions.Center);
            var panelRect=Box("Project Panel",safe,new Vector2(.06f,.27f),new Vector2(.94f,.73f),ColorOf("F2EAD2"));panel=panelRect.gameObject;
            panelTitle=Label("Project",panelRect,new Vector2(.07f,.74f),new Vector2(.93f,.93f),23,ColorOf("203A30"));
            panelBody=Label("",panelRect,new Vector2(.07f,.36f),new Vector2(.93f,.74f),15,ColorOf("203A30"));
            selectButton=Button("Select Project","Start project",panelRect,new Vector2(.07f,.19f),new Vector2(.93f,.34f),ColorOf("F4B85C"),SelectProjectFromUi);
            closeButton=Button("Close Panel","Close",panelRect,new Vector2(.07f,.025f),new Vector2(.93f,.17f),ColorOf("B9CCBB"),ClosePanelFromUi);
            reloadButton=Button("Reload Save","Retry loading",panelRect,new Vector2(.07f,.19f),new Vector2(.93f,.34f),ColorOf("F4B85C"),ReloadFromUi);
            panel.SetActive(false);
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            UpdateSafeArea();
        }
        // Called once by the editor setup. The saved Canvas is then ordinary editable scene content.
        public void CreateEditableUi(TMP_FontAsset sharedFont,Sprite[] sharedHouseStages)
        {
            if(Application.isPlaying||transform.Find("Portrait Canvas")!=null)throw new InvalidOperationException("Build editable UI only once in Edit Mode.");
            font=sharedFont;houseStages=sharedHouseStages;BuildUi();sceneUiAuthored=true;
            Canvas.ForceUpdateCanvases();Render("Choose the house repair, then spin.",ModelState.Fresh(1));
        }
        // Called by the Editor once to retain ordinary editable scene controls and a test graphic.
        public void CreateWorkshopUi()
        {
            if(workshopButton!=null)return;
            var project=projectButton.GetComponent<RectTransform>();
            float left=project.anchorMin.x,right=project.anchorMax.x,split=(left+right)*.5f;
            var min=project.anchorMin;var max=project.anchorMax;
            project.anchorMax=new Vector2(split-.02f,max.y);
            workshopButton=Button("Workshop Project","Workshop",world,new Vector2(split+.02f,min.y),max,ColorOf("F2EAD2"),ShowWorkshop);
            workshopLabel=workshopButton.GetComponentInChildren<TMP_Text>();
            float ground=house.rectTransform.anchorMin.y;
            var draft=Box("Workshop draft",world,new Vector2(.82f,ground),new Vector2(.82f,ground),ColorOf("918471"));
            draft.pivot=new Vector2(.5f,0);draft.sizeDelta=new Vector2(58,52);workshopDraft=draft.GetComponent<UnityEngine.UI.Image>();
            Box("Roof",draft,new Vector2(-.06f,.8f),new Vector2(1.06f,1),ColorOf("4E6157"));
            Box("Door",draft,new Vector2(.35f,0),new Vector2(.65f,.6f),ColorOf("D5C198"));
            workshopMarker=Label("WORKSHOP · 120",world,new Vector2(.81f,ground+.13f),new Vector2(.81f,ground+.13f),9,ColorOf("203A30"),TextAlignmentOptions.Center);
            workshopMarker.rectTransform.sizeDelta=new Vector2(132,36);workshopMarker.rectTransform.pivot=new Vector2(.5f,0);
            workshopLabel.fontSize=14;projectLabel.fontSize=14;
        }
        public void Spin()
        {
            if(Busy||failed||InBackground||panel.activeSelf||Session?.State==null)return;
            try
            {
                var before=Session.State;
                var next=Session.ExecuteChapterSpin(before.sequence+1,UtcSeconds(),!AutoRunning,TutorialFacts);
                ObserveCommitted(before,next);
                QueueRewardFeedback(before,next,RewardFeedbackOrigin.Spin);
                productPendingSpinSequence=next.lastSpin.sequence;productSpinBeforeText=null;productSpinBeforeGoalContext=null;
                Busy=true;Render("Gathering materials…",before);StartCoroutine(Present(before,next));
            }
            catch(Exception e){ObserveOperationFailure();Error(e.Message);}
        }
        IEnumerator Present(ModelState before,ModelState next)
        {
            var presentationSession=Session;int generation=productSpinReceiptGeneration;
            // Capture the explicit saved presentation preference for this spin.
            // The frozen economic outcome and normal default remain unchanged.
            float duration=ExpansionEconomy.Project(before).SpinDurationSeconds;
            BeginReelMotion(before);
            presentationElapsed=0;presentationSampleAt=Time.realtimeSinceStartupAsDouble;presentationClockActive=true;
            while(presentationElapsed<duration||InBackground)
            {
                if(generation!=productSpinReceiptGeneration||!ReferenceEquals(presentationSession,Session)||failed)yield break;
                AdvancePresentationClock();
                if(presentationElapsed>=duration&&!InBackground)break;
                if(!InBackground)
                {
                    AnimateReels((float)presentationElapsed,duration,next.lastSpin.symbols);
                }
                yield return null;
            }
            if(generation!=productSpinReceiptGeneration||!ReferenceEquals(presentationSession,Session)||failed)yield break;
            presentationClockActive=false;
            ResetReelMotion();
            productPendingSpinSequence=0;productSpinBeforeText=null;productSpinBeforeGoalContext=null;
            Busy=false;
            var revealed=Session.State;
            if(revealed.lastSpin?.sequence!=next.lastSpin?.sequence){Render(null,revealed);yield break;}
            var construction=revealed.lastSpin?.chapter;
            if(AutoRunning&&construction?.Kind=="spin"&&
                construction.After.Progress.CompletedUpgradeIds.Count>construction.Before.Progress.CompletedUpgradeIds.Count)
                StopAuto();
            RememberProductSpinReceipt(revealed,false);
            Render(null,revealed);
            if(!ShowOfflineSummary())ShowAutoHint();
            RevealRewardFeedback(next);
            if(AutoRunning&&!InBackground&&!panel.activeSelf){yield return new WaitForSecondsRealtime(EconomyRules.AutoGapSeconds);if(AutoRunning)Spin();}
        }
        void AdvancePresentationClock()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(!InBackground)presentationElapsed+=Math.Max(0,now-presentationSampleAt);
            presentationSampleAt=now;
        }
        bool PanelActionAvailable(Button action)
        { return !InBackground&&panel!=null&&panel.activeInHierarchy&&action!=null&&action.isActiveAndEnabled&&action.IsInteractable(); }
        bool PointerConsentAllows(Button action)
        { return action!=null&&(!action.TryGetComponent<JtnPanelPointerConsent>(out var consent)||consent.AllowInvocation()); }
        void ReleaseExitSelect()
        {
            if(!exitSelectBorrowed)return;
            if(selectButton!=null)selectButton.interactable=exitSelectPreviousInteractable;
            exitSelectBorrowed=false;
        }
        void InvalidatePanelActions()
        {
            // Release before a replacement owner configures the shared action.
            ReleaseExitSelect();
            if(selectButton!=null)
            {
                if(sharedSelectOriginalEvent==null)sharedSelectOriginalEvent=selectButton.onClick;
                var original=sharedSelectOriginalEvent;
                var visibleEvent=new Button.ButtonClickedEvent();
                // Old visible confirmations cannot dispatch the action of a later panel.
                visibleEvent.AddListener(()=>{if(selectButton!=null&&ReferenceEquals(selectButton.onClick,visibleEvent))original.Invoke();});
                selectButton.onClick=visibleEvent;
            }
            InvalidateSections();
            foreach(var action in new[]{selectButton,closeButton,reloadButton,moreProjectsButton})
                if(action!=null&&action.TryGetComponent<JtnPanelPointerConsent>(out var consent))consent.Invalidate();
        }
        void SelectProjectFromUi() { if(PointerConsentAllows(selectButton))SelectProject(); }
        void ClosePanelFromUi() { if(PointerConsentAllows(closeButton))ClosePanel(); }
        public void SelectProject()
        {
            // A queued UI callback must still belong to an actionable foreground panel.
            // Keep recovery before the healthy-state guard: its loaded state can be null.
            if(!PanelActionAvailable(selectButton))return;
            if(failed){RestoreBackup();return;}
            if(Busy||failed||Session?.State==null)return;
            ClearSectionReturn();
            if(exitPanel){Application.Quit();return;}
            if(SelectWave())return;
            try
            {
                var s=Session.State;int target=previewWorkshop?EconomyRules.WorkshopTarget:s.houseStage+1;
                if(autoHintPanel||(!previewWorkshop&&s.houseStage>=2)||(s.selectedStage==target&&s.coins<GameLogic.TargetPrice(target))||!GameLogic.Available(s,target))return;
                var next=Session.SelectProject(target,s.sequence+1,UtcSeconds());panel.SetActive(false);
                Render(next.selectedStage==0?"Project paid and complete. Choose your next goal.":"Goal selected. Paid once when you have enough coins.");ShowAutoHint();
            }
            catch(Exception e){ObserveOperationFailure();Error(e.Message);}
        }
        public void ToggleAuto()
        {
            if(AutoRunning){StopAuto();return;}
            if(Busy||failed||InBackground||panel.activeSelf||Session?.State==null||Session.State.houseStage<1||ChapterStopsAuto(Session.State))return;
            AutoRunning=true;Render();Spin();
        }
        public void StopAuto(){AutoRunning=false;RenderAutoLabel();}
        bool autoHouseRepaired;
        void RenderAutoLabel(ModelState display=null)
        {
            if(display!=null)autoHouseRepaired=display.houseStage>=1;
            if(autoLabel==null)return;
            autoLabel.text=AutoRunning?"STOP":!autoHouseRepaired?"AUTO\n<size=62%>after House</size>":"AUTO";
            // The locked button uses the pale disabled surface; light text disappears on it.
            if(productChrome!=null)autoLabel.color=ColorOf(AutoRunning||autoHouseRepaired?"F6F1E3":"263C50");
        }
        public void ShowProject()
        { ShowProjectDetails(false); }
        public void ShowWorkshop()
        { ShowProjectDetails(true); }
        void ShowProjectDetails(bool workshop)
        { ShowChapterFamily(workshop?JunkToNeon.Contracts.FamilyIds.Workshop:JunkToNeon.Contracts.FamilyIds.House); }
        void ShowAutoHint()
        {
            if(failed||Busy||Session?.State==null||!HouseGoal.NeedsAutoHint(Session.State))return;
            // Opening the introduction consumes transient flights, not their receipt.
            ClearRewardFeedback();
            StopAuto();HideWaveChoices();exitPanel=false;autoHintPanel=true;
            panelTitle.text="AUTO unlocked";
            panelBody.text="House repaired! AUTO spins for free and pauses when your selected building is complete. Choose your next project or start AUTO again. Press STOP to end it sooner.";
            selectButton.gameObject.SetActive(false);reloadButton.gameObject.SetActive(false);closeButton.gameObject.SetActive(true);panel.SetActive(true);
        }
        bool ShowOfflineSummary()
        {
            if(ShowChapterTransition())return true;
            if(failed||Busy||pendingOfflineSummary==null)return false;
            StopAuto();HideWaveChoices();exitPanel=false;autoHintPanel=false;offlineSummaryPanel=true;
            panelTitle.text="Welcome back";
            panelBody.text=OfflineSummaryText(pendingOfflineSummary);
            if(!string.IsNullOrEmpty(productCurrentGoalContext))panelBody.text+="\n\n"+productCurrentGoalContext;
            panelBody.text+="\n\nTo return to the game, press Close first.";
            selectButton.gameObject.SetActive(false);reloadButton.gameObject.SetActive(false);closeButton.gameObject.SetActive(true);panel.SetActive(true);
            pendingOfflineSummary=null;return true;
        }
        // Receipt timestamps describe a saved clock interval, not verified real-world absence.
        static string SavedInterval(long seconds)
        {
            long days=seconds/86400,hours=seconds/3600%24,minutes=seconds/60%60,remainder=seconds%60;
            if(days>0)return $"{days}d {hours}h {minutes}m {remainder}s";
            if(hours>0)return $"{hours}h {minutes}m {remainder}s";
            return $"{minutes}m {remainder}s";
        }
        static string OfflineSummaryText(OfflineReceipt receipt)
        {
            return $"Already credited: {receipt.coins} coins.\nSaved interval: {SavedInterval(receipt.to-receipt.from)}. Income for {receipt.seconds/EconomyRules.OfflineSecondsPerCoin} full minutes; cap: {EconomyRules.OfflineCapSeconds/3600}h.\nThis credit does not build or change your goal or its price.";
        }
        static long UtcSeconds()
        {
#if UNITY_EDITOR
            if(Environment.GetEnvironmentVariable("JTN_EDITOR_SAVE_DIRECTORY")!=null&&long.TryParse(Environment.GetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS"),out var utc))return utc;
#endif
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        public void ClosePanel()
        {
            if(failed||InBackground||panel==null||!panel.activeInHierarchy)return;
            if(TryReturnYardActivity())return;
            if(TryReturnProduct())return;
            if(TryReturnArcade())return;
            if(TryReturnExpansion())return;
            if(TryReturnSection())return;
            if(CloseChapterTransition())return;
            HideWaveChoices();
            if(offlineSummaryPanel)
            {
                try{Session.AcknowledgeOffline(Session.State.sequence+1,UtcSeconds());}
                catch(Exception e){ObserveOperationFailure();Error(e.Message);return;}
            }
            if(autoHintPanel)
            {
                try{Session.AcknowledgeAutoHint(Session.State.sequence+1,UtcSeconds());autoHintPanel=false;}
                catch(Exception e){ObserveOperationFailure();Error(e.Message);return;}
            }
            bool returned=offlineSummaryPanel;offlineSummaryPanel=false;
            panel.SetActive(false);exitPanel=false;Render();if(returned)ShowAutoHint();
        }
        // UI callbacks require a visible foreground control; the trusted loader stays unchanged.
        void ReloadFromUi() { if(PointerConsentAllows(reloadButton)&&PanelActionAvailable(reloadButton))Reload(); }
        void Reload()
        {
            ClearRewardFeedback();
            InvalidatePanelActions();
            if(restoreConfirmation){Error(Session?.Error);return;}
            StopAuto();autoHintPanel=false;panel.SetActive(false);OpenSession();
        }
        void RestoreBackup()
        {
            if(!failed||InBackground||Session==null||!Session.BackupAvailable)return;
            if(!restoreConfirmation)
            {
                InvalidatePanelActions();
                restoreConfirmation=true;
                panelTitle.text="Restore backup?";
                panelBody.text="Recent progress may be lost. The current file and backup will be kept separately. Restore the validated backup?";
                selectButton.GetComponentInChildren<TMP_Text>().text="Restore";
                reloadButton.GetComponentInChildren<TMP_Text>().text="Cancel";
                return;
            }
            try
            {
                Session.RestoreBackup();failed=false;restoreConfirmation=false;autoHintPanel=false;
                ResetProductSpinReceipt();
                var restored=Session.SettleOffline(UtcSeconds());
                pendingOfflineSummary=restored.lastSettlement?.coins>0&&restored.lastSettlement.sequence>restored.lastSettlementSeen?restored.lastSettlement:null;
                RememberProductSpinReceipt(restored,true);
                panel.SetActive(false);Render("Backup restored. Recent progress may have been lost.");if(!ShowOfflineSummary())ShowAutoHint();
            }
            catch(Exception e){ObserveOperationFailure();Error(e.Message);}
        }
        void Error(string detail)
        {
            ResetProductSpinReceipt();presentationClockActive=false;ResetReelMotion();
            ClearRewardFeedback();
            HideWaveChoices();if(moreProjectsButton!=null)moreProjectsButton.interactable=false;
            failed=true;Busy=false;restoreConfirmation=false;offlineSummaryPanel=false;pendingOfflineSummary=null;StopAuto();
            wallet.text="COINS —";stageLabel.text="PROGRESS NEEDS RECOVERY";heading.text="Progress protected.";
            message.text="Actions paused until recovery or reload.";
            panelTitle.text="Progress protected";panelBody.text="Could not load or save data. Original records are preserved. "+detail;
            selectButton.GetComponentInChildren<TMP_Text>().text="Restore backup";
            selectButton.gameObject.SetActive(Session?.BackupAvailable==true);
            // Reuse existing serialized controls; Retry/Cancel occupies the hidden Close row.
            var retry=reloadButton.GetComponent<RectTransform>();var close=closeButton.GetComponent<RectTransform>();
            retry.anchorMin=close.anchorMin;retry.anchorMax=close.anchorMax;retry.offsetMin=close.offsetMin;retry.offsetMax=close.offsetMax;
            reloadButton.GetComponentInChildren<TMP_Text>().text="Retry loading";
            reloadButton.gameObject.SetActive(true);closeButton.gameObject.SetActive(false);panel.SetActive(true);
            if(spinButton!=null)spinButton.interactable=false;if(autoButton!=null)autoButton.interactable=false;if(projectButton!=null)projectButton.interactable=false;
            Debug.LogWarning("Junk to Neon storage stopped: "+detail);
        }
        void Render(string status=null,ModelState display=null)
        {
            var s=display??Session?.State;if(s==null)return;
            wallet.text=s.coins+" COINS";
            bool complete=ChapterStopsAuto(s);
            if(complete)StopAuto();
            heading.text=complete?"All buildings complete.":s.houseStage==2?"House complete. Choose a building.":new[]{"Every place has a future.","The house feels warm again."}[s.houseStage];
            stageLabel.text=HouseGoal.Marker(s);
            lastWorldHeight=world.rect.height;
            projectLabel.text=s.houseStage==2?"House complete":(s.selectedStage==s.houseStage+1?"Selected · ":"")+(s.houseStage==0?"Repair house":"Upgrade house");
            if(workshopButton!=null)
            {
                workshopLabel.text=s.workshopBuilt==1?$"Built · +{EconomyRules.WorkshopBonus} per spin":s.selectedStage==3?"Selected · Workshop":$"Workshop · {EconomyRules.WorkshopCost}";
                workshopMarker.text=HouseGoal.WorkshopMarker(s);
                workshopButton.interactable=!Busy&&!failed&&!InBackground;
            }
            projectPrice.text=s.houseStage==2?"COMPLETE":$"{s.coins} / {EconomyRules.Prices[s.houseStage]}";
            if(!Busy){for(int i=0;i<3;i++)SetReel(i,s.lastSpin==null?i:s.lastSpin.symbols[i]);}
            string receipt=s.lastSpin==null?"":$"Base +{s.lastSpin.reward} · Workshop +{s.lastSpin.bonus}"+(s.lastSpin.wave==null?". ":" · "+WaveReceipt(s));
            message.text=complete?receipt+"Phase complete. Shop is optional.":Busy?status??"Gathering materials…":receipt+(status??(s.selectedStage==0?"Choose one goal.":"Goal selected. Paid when you have enough coins."));
            RenderWave(s,status);
            RenderJourney(s);
            spinButton.interactable=!Busy&&!failed&&!InBackground;
            // The duration appears only while a faster speed is active; the default needs no number.
            var spinSeconds=ExpansionEconomy.Project(s).SpinDurationSeconds;
            spinLabel.text=Busy?"SPINNING…":spinSeconds==EconomyRules.ExpansionNormalSpinSeconds?"SPIN":"SPIN · "+spinSeconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" s";
            projectButton.interactable=!Busy&&!failed&&!InBackground;
            autoButton.interactable=!failed&&!InBackground&&s.houseStage>=1&&!complete&&(!Busy||AutoRunning);
            RenderAutoLabel(s);
            RenderSections(s);
            RenderExpansionHud(s);
            RenderArcadeHud(s);
            RenderProductUi(s,status);
            RenderYard(s);
            RetainProductSpinBeforeText(s);
            RefreshExitReadiness();
        }
        void OnApplicationPause(bool paused)
        {
            ClearProductRoute();
            ClearRewardFeedback();
            InvalidatePanelActions();
            // Sample the outgoing foreground interval before changing state. A
            // resume resets the anchor without crediting absent background frames.
            if(presentationClockActive)AdvancePresentationClock();
            InBackground=paused;if(paused)StopAuto();
            if(Session==null||failed)return;
            try
            {
                if(paused)Session.Checkpoint(UtcSeconds());
                else
                {
                    var s=Session.SettleOffline(UtcSeconds());
                    if(s.lastSettlement?.coins>0&&s.lastSettlement.sequence>s.lastSettlementSeen)pendingOfflineSummary=s.lastSettlement;
                }
                Render(paused?"Paused":"Welcome back.");if(!paused)ShowOfflineSummary();
            }
            catch(Exception e){ObserveOperationFailure();Error(e.Message);}
        }
        void OnApplicationQuit()
        {
            ClearRewardFeedback();
            ClearSectionReturn();
            if(Session==null||failed||InBackground)return;
            try{Session.Checkpoint(UtcSeconds());}catch(Exception e){ObserveOperationFailure();Error(e.Message);}
        }
        void Update()
        {
            ObserveProductPanel();
            if(panel.activeSelf)StopSlotFeedback();
            PresentedFrames++;
            if(Screen.safeArea!=lastSafe||lastScreenSize!=new Vector2(Screen.width,Screen.height)){UpdateSafeArea();if(Session!=null&&!failed)Render();}
            if(Mathf.Abs(world.rect.height-lastWorldHeight)>.1f&&Session!=null&&!failed)Render();
        }
        void ShowExitConfirmation()
        {
            HideWaveChoices();exitPanel=true;panelTitle.text="Exit game?";panelBody.text="Your results are already saved.";
            selectButton.gameObject.SetActive(true);selectButton.GetComponentInChildren<TMP_Text>().text="Exit";
            reloadButton.gameObject.SetActive(false);closeButton.gameObject.SetActive(true);panel.SetActive(true);
            RefreshExitReadiness();
        }
        void RefreshExitReadiness()
        {
            // Error/recovery can retain exitPanel; that flag alone is not ownership.
            if(!exitPanel||failed||restoreConfirmation||autoHintPanel||offlineSummaryPanel||wavePanel!=0||Session?.State==null||
                panel==null||!panel.activeInHierarchy||selectButton==null||!selectButton.isActiveAndEnabled)return;
            if(!exitSelectBorrowed)
            {exitSelectPreviousInteractable=selectButton.interactable;exitSelectBorrowed=true;}
            selectButton.interactable=!Busy&&!InBackground;
            if(InBackground)return;
            panelBody.text=Busy?"Finishing the spin. Exit is temporarily unavailable.":"Your results are already saved.";
            selectButton.GetComponentInChildren<TMP_Text>().text=Busy?"Finishing spin…":"Exit";
        }
        void UpdateSafeArea()
        {
            lastSafe=Screen.safeArea;lastScreenSize=new Vector2(Screen.width,Screen.height);
            if(safeFitter==null)safeFitter=safe.GetComponent<JtnSafeArea>();
            if(safeFitter!=null)safeFitter.Refresh();
        }
        void SetReel(int reel,int symbol)
        {
            var icon=reelImages!=null&&reel<reelImages.Length?reelImages[reel]:null;
            var sprite=reelSymbols!=null&&symbol<reelSymbols.Length?reelSymbols[symbol]:null;
            bool showIcon=icon!=null&&sprite!=null;
            if(icon!=null){icon.sprite=sprite;icon.enabled=showIcon;}
            reels[reel].enabled=!showIcon;
            reels[reel].text=names[symbol];
        }
        void OnDisable(){StopSlotFeedback();}
        void OnDestroy(){ClearRewardFeedback();StopDiagnostics();Canvas.willRenderCanvases-=ObserveGuideCanvas;if(Instance==this)Instance=null;if(ownsFont&&font!=null)Destroy(font);if(ownsSprites&&houseStages!=null)foreach(var s in houseStages)Destroy(s);}
        static Color ColorOf(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        static RectTransform Area(string name,Transform parent,Vector2 min,Vector2 max){var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static RectTransform Box(string name,Transform parent,Vector2 min,Vector2 max,Color color){var r=Area(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return r;}
        TMP_Text Label(string value,Transform parent,Vector2 min,Vector2 max,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft){var r=Area(value,parent,min,max);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.textWrappingMode=TextWrappingModes.Normal;text.raycastTarget=false;return text;}
        Button Button(string name,string value,Transform parent,Vector2 min,Vector2 max,Color color,UnityEngine.Events.UnityAction action){var r=Box(name,parent,min,max,color);r.GetComponent<Image>().raycastTarget=true;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(action);Label(value,r,new Vector2(.04f,.04f),new Vector2(.96f,.96f),16,ColorOf("203A30"),TextAlignmentOptions.Center);return b;}
    }
}
