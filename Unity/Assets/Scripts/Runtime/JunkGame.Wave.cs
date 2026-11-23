using System;
using System.Collections.Generic;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        [SerializeField] Button moreProjectsButton;
        [SerializeField] Button[] waveChoices = new Button[3];
        [SerializeField] TMP_Text waveStatus;
        [SerializeField] Image recyclerDraft, pigeonDraft;
        int wavePanel;

        // Also called once by the live Editor to serialize ordinary editable controls.
        public void CreateWaveUi()
        {
            if (moreProjectsButton == null)
            {
                moreProjectsButton = Button("More projects", "Buildings and shop", world, new Vector2(.06f,.69f), new Vector2(.94f,.745f), ColorOf("F2EAD2"), ShowMoreProjects);
                moreProjectsButton.GetComponentInChildren<TMP_Text>().fontSize=14;
                var label=moreProjectsButton.GetComponentInChildren<TMP_Text>();
                label.enableAutoSizing=true;label.fontSizeMin=8;label.fontSizeMax=14;
            }
            else moreProjectsButton.onClick.AddListener(ShowMoreProjects);
            if(!moreProjectsButton.TryGetComponent<JtnPanelPointerConsent>(out _))moreProjectsButton.gameObject.AddComponent<JtnPanelPointerConsent>();
            if (waveStatus == null)
            {
                waveStatus = Label("", world, new Vector2(.06f,.65f), new Vector2(.94f,.69f), 9, ColorOf("203A30"));
                waveStatus.enableAutoSizing=true;waveStatus.fontSizeMin=8;waveStatus.fontSizeMax=9;
            }
            if (waveChoices == null || waveChoices.Length != 3) waveChoices = new Button[3];
            string[] labels = { "Recycler", "Pigeon Loft", "Shop: slot upgrade" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                UnityEngine.Events.UnityAction action = () => OpenWaveChoice(index);
                if (waveChoices[i] == null)
                    waveChoices[i] = Button("Wave choice " + i, labels[i], panel.transform, new Vector2(.07f,.60f-i*.14f), new Vector2(.93f,.70f-i*.14f), ColorOf("F4B85C"), action);
                else waveChoices[i].onClick.AddListener(action);
                if(!waveChoices[i].TryGetComponent<JtnPanelPointerConsent>(out _))waveChoices[i].gameObject.AddComponent<JtnPanelPointerConsent>();
                waveChoices[i].gameObject.SetActive(false);
            }
            if(recyclerDraft==null)recyclerDraft=WaveDraft("Recycler draft","TRASH",.14f);
            if(pigeonDraft==null)pigeonDraft=WaveDraft("Pigeon draft","PIGEONS",.29f);
        }
        Image WaveDraft(string name,string label,float x)
        {
            float ground=house.rectTransform.anchorMin.y;
            var rect=Box(name,world,new Vector2(x,ground),new Vector2(x,ground),ColorOf("8B9588"));
            rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(45,40);
            Label(label,rect,new Vector2(0,.2f),new Vector2(1,.8f),7,ColorOf("203A30"),TextAlignmentOptions.Center);
            return rect.GetComponent<Image>();
        }
        void HideWaveChoices()
        {
            InvalidatePanelActions();
            wavePanel=0;ResetJourneyActionLayout();HideChapterNavigation();if(closeButton!=null)closeButton.GetComponentInChildren<TMP_Text>().text="Close";
            if(waveChoices!=null)foreach(var choice in waveChoices)if(choice!=null)choice.gameObject.SetActive(false);
        }
        public void ShowMoreProjects()
        {if(moreProjectsButton!=null&&moreProjectsButton.isActiveAndEnabled&&moreProjectsButton.IsInteractable()&&PointerConsentAllows(moreProjectsButton))ShowChapterHome();}
        void OpenWaveChoice(int index)
        {
            if(index<0||index>=waveChoices.Length||!PanelActionAvailable(waveChoices[index])||!PointerConsentAllows(waveChoices[index]))return;
            OpenChapterChoice(index);
        }
        bool SelectWave() { return SelectChapterPanel(); }
        void RenderWave(ModelState s,string status) { RenderChapter(s,status); }
        static string WaveReceipt(ModelState s)
        {
            if(s.lastSpin?.wave==null)return "";
            var w=s.lastSpin.wave;
            return $"Slot +{w.SlotCoins}\nTrash +{w.Effects.RecyclerCoins} · delivery +{w.Effects.DeliveryCoins}. ";
        }
        static void ValidateCatalogResources()
        {
            var observed=new List<string>();
            foreach(var key in ChapterCatalog.RequiredAssetKeys)if(Resources.Load<Sprite>(key)!=null)observed.Add(key);
            ChapterCatalog.ValidateResources(observed);
        }
    }
}
