using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        // One shared, store-free authoring path for the existing Canvas and fallback.
        public void CreateArcadeIntegrationUi()
        {
            CreateArcadeUi();CreateRewardFeedbackUi();
            if(sectionButtons==null||sectionButtons.Length!=7||sectionButtons.Any(b=>b==null)||expansionEntryButton==null)
                throw new InvalidOperationException("Create the existing seven sections and shop before Arcade.");
            for(int i=0;i<9;i++)
            {
                var button=i<7?sectionButtons[i]:i==7?expansionEntryButton:arcadeEntryButton;
                float left=i==8?.03f:.03f+(i%2)*.50f,top=.99f-(i/2)*.20f;
                var rect=(RectTransform)button.transform;
                rect.anchorMin=new Vector2(left,top-.18f);rect.anchorMax=new Vector2(i==8?.97f:left+.44f,top);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
            }
            var counters=arcadeHudCounters;
            if(counters==null)
                counters=spinButton.transform.parent.GetComponentsInChildren<TMP_Text>(true)
                    .Single(t=>t.name=="НАХОДКИ ДЛЯ ДВОРА"||t.name=="ArcadeHudCounters");
            counters.name="ArcadeHudCounters";
            counters.rectTransform.anchorMin=new Vector2(.04f,.90f);counters.rectTransform.anchorMax=new Vector2(.30f,.99f);
            counters.rectTransform.offsetMin=counters.rectTransform.offsetMax=Vector2.zero;
            counters.enableAutoSizing=true;counters.fontSize=10;counters.fontSizeMin=8;counters.fontSizeMax=10;counters.raycastTarget=false;
            if(!Application.isPlaying)counters.text="Diamonds · tickets";
            ConfigureArcadeHud(counters);
            ConfigureRewardFeedbackTargets(counters.rectTransform,expansionHudResources.rectTransform,counters.rectTransform);
        }
    }
}
