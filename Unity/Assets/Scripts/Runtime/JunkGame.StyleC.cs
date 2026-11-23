using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        static Sprite StyleC(string path)=>Resources.Load<Sprite>("Art/StyleC/"+path);
        static void StyleCSurface(JtnProductSurface surface,string family,Color tint)
        {
            surface.sprite=StyleC("Buttons/"+family+"-normal");surface.type=Image.Type.Sliced;
            surface.pixelsPerUnitMultiplier=3;
            surface.ArtworkTint=tint;
        }
        static void StyleCButton(Button button,string family)
        {
            if(button==null||!(button.targetGraphic is JtnProductSurface surface))return;
            StyleCSurface(surface,family,Color.white);
            button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=surface.sprite,selectedSprite=surface.sprite,pressedSprite=StyleC("Buttons/"+family+"-pressed"),disabledSprite=StyleC("Buttons/"+family+"-disabled")};
        }
        static void StyleCRoundContent(Button button)
        {
            var icon=button.GetComponentInChildren<JtnProductIcon>(true);
            if(icon!=null)Place(icon.rectTransform,button.transform,new Vector2(.28f,.42f),new Vector2(.72f,.86f));
            var caption=button.GetComponentInChildren<TMP_Text>(true);
            Place(caption.rectTransform,button.transform,new Vector2(.12f,.14f),new Vector2(.88f,.39f));
            caption.alignment=TextAlignmentOptions.Center;caption.margin=Vector4.zero;
            caption.textWrappingMode=TextWrappingModes.NoWrap;caption.enableAutoSizing=true;caption.fontSizeMin=caption.fontSizeMax=10;
        }
        public void ApplyStyleCArt()
        {
            if(StyleC("Houses/house-s00")==null||productChrome==null)return;
            houseStages=Enumerable.Range(0,5).Select(i=>StyleC("Houses/house-s0"+i)).ToArray();
            reelSymbols=new[]{"potato","box","trash","pigeon"}.Select(id=>StyleC("Symbols/symbol-"+id)).ToArray();
            if(Camera.main!=null)Camera.main.backgroundColor=ColorOf("142638");
            var backdrop=world.Find("Style C courtyard") as RectTransform;
            if(backdrop==null)backdrop=Box("Style C courtyard",world,Vector2.zero,new Vector2(1,.78f),Color.white);
            Place(backdrop,world,Vector2.zero,new Vector2(1,.78f));
            var background=backdrop.GetComponent<Image>();background.sprite=StyleC("courtyard-background");background.color=Color.white;background.raycastTarget=false;
            backdrop.SetAsFirstSibling();
            foreach(var image in world.GetComponentsInChildren<Image>(true))if(image.name=="Sky"||image.name=="Yard")image.color=Color.clear;
            world.GetComponent<Image>().color=Color.clear;
            var calm=new Color(.35f,.52f,.65f,1);
            foreach(var button in new[]{productAvatar,productSettings,productMapShortcut,productEventShortcut,guideButton})
            {StyleCButton(button,"round");StyleCRoundContent(button);}
            foreach(var name in new[]{"Coin Wallet","Diamond Wallet"})
            {
                var target=productChrome.Find(name);if(target==null)continue;
                StyleCSurface(Surface(target.gameObject,Color.white),"secondary",calm);
                var walletIcon=target.GetComponentInChildren<JtnProductIcon>(true);
                Place(walletIcon.rectTransform,target,new Vector2(.08f,.22f),new Vector2(.29f,.78f));
                // The wallet shows the coin that flies into it, not the Slot tab glyph.
                if(name=="Coin Wallet"){walletIcon.ArtworkOverride=StyleC("Icons/coin");walletIcon.color=Color.white;}
            }
            Place(productCoins.rectTransform,productCoins.transform.parent,new Vector2(.33f,.10f),new Vector2(.94f,.90f));
            Place(productDiamonds.rectTransform,productDiamonds.transform.parent,new Vector2(.35f,.10f),new Vector2(.94f,.90f));
            if(productEnergy.targetGraphic is JtnProductSurface energy)StyleCSurface(energy,"secondary",calm);
            // Icons carry the currency colour; the three wallet texts share one colour and size.
            productCoins.color=productDiamonds.color=productEnergyText.color=ColorOf("FFF0CE");productCoins.fontSizeMax=productDiamonds.fontSizeMax=productEnergyText.fontSizeMax=16;
            if(productEventStatus!=null)
            {
                // The clear surface replaces the button image, so it must keep receiving taps.
                var target=Surface(productEventStatus.gameObject,Color.clear,14,true);target.sprite=target.overrideSprite=null;
                var badge=productEventStatus.transform.Find("Style C ticket badge") as RectTransform;
                if(badge==null)badge=Area("Style C ticket badge",productEventStatus.transform,Vector2.zero,new Vector2(1,47f/77));
                Surface(badge.gameObject,ColorOf("175D78"),18).raycastTarget=false;badge.SetAsFirstSibling();
            }
            StyleCSurface(Surface(productNarrativeSurface.gameObject,Color.white),"secondary",calm);
            StyleCButton(productGoalButton,"neutral");StyleCButton(spinButton,"primary");StyleCButton(autoButton,"secondary");
            // Keeps "AUTO" clear of the stroke on the right of the secondary sprite.
            autoLabel.margin=new Vector4(0,0,6,0);
            foreach(var image in reelImages)
            {
                var frame=Surface(image.transform.parent.gameObject,ColorOf("FFF0CE"),18);
                frame.sprite=frame.overrideSprite=null;
                image.rectTransform.anchorMin=new Vector2(.05f,.10f);image.rectTransform.anchorMax=new Vector2(.95f,.90f);image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
            }
            var strip=productChrome.Find("Style C materials") as RectTransform;
            if(strip==null)
            {
                strip=Area("Style C materials",productChrome,new Vector2(.035f,.817f),new Vector2(.965f,.847f));
                StyleCSurface(Surface(strip.gameObject,Color.white),"secondary",calm);
                Icon(strip,JtnProductIcon.Symbol.Box,new Vector2(.10f,.05f),new Vector2(.24f,.95f),Color.white);
            }
            StyleCSurface(Surface(strip.gameObject,Color.white),"secondary",calm);
            // The strip lives in the lower half of the compact header.
            Place(strip,productChrome,new Vector2(.184f,.933f),new Vector2(.816f,.962f));
            Place(productMaterials.rectTransform,strip,new Vector2(.27f,.04f),new Vector2(.98f,.96f));productMaterials.color=ColorOf("FFF0CE");
            productMaterials.alignment=TextAlignmentOptions.MidlineLeft;
            productIdentity.fontStyle=FontStyles.Bold;spinLabel.fontStyle=FontStyles.Bold;productGoalText.fontStyle=FontStyles.Bold;
            RefreshStyleCTabs();
            foreach(var icon in productChrome.GetComponentsInChildren<JtnProductIcon>(true))icon.SetAllDirty();
        }
        void StyleCQuickBoost()
        {
            if(StyleC("Icons/lock")==null)return;
            StyleCButton(expansionQuickBoostButton,"neutral");
            var root=expansionQuickBoostButton.transform;
            var icon=root.Find("Style C bonus") as RectTransform;
            if(icon==null)icon=Box("Style C bonus",root,new Vector2(.32f,.64f),new Vector2(.68f,.90f),Color.white);
            Place(icon,root,new Vector2(.32f,.64f),new Vector2(.68f,.90f));
            var image=icon.GetComponent<Image>();
            // Uses the last rendered bonus state: a blocked or recovering Session throws on Expansion, and this runs from Update.
            image.sprite=StyleC(expansionReelUnlocked?"Icons/bonus":"Icons/lock");image.preserveAspect=true;image.raycastTarget=false;
            var label=expansionQuickBoostButton.GetComponentInChildren<TMP_Text>(true);
            // One line sits between the sprite's stroke and its darker bottom rim; two lines need the room above the rim.
            bool twoLines=label.text.IndexOf('\n')>=0;
            Place(label.rectTransform,root,new Vector2(twoLines?.02f:.06f,twoLines?.25f:.12f),new Vector2(twoLines?.98f:.94f,twoLines?.65f:.52f));
            label.margin=Vector4.zero;label.alignment=TextAlignmentOptions.Center;
            // "15 energy" at 11 px is as wide as the button; cap two-line captions so they stay inside its border.
            label.enableAutoSizing=true;label.fontSizeMin=twoLines?8:10;label.fontSizeMax=twoLines?10:13;
        }
        readonly System.Collections.Generic.Dictionary<Button,(Color color,Sprite sprite)> styleCDisabledLooks=new System.Collections.Generic.Dictionary<Button,(Color,Sprite)>();
        // While only a spin blocks navigation its controls keep their normal look;
        // SPIN alone shows the busy state. Other blocks keep the disabled look.
        void StyleCSpinLook()
        {
            if(productTabs==null)return;
            bool calm=Busy&&!failed&&!InBackground;
            foreach(var button in productTabs.Concat(new[]{productAvatar,productSettings,productEnergy,productEventShortcut,productEventStatus,productMapShortcut,productGoalButton,guideButton}))
            {
                if(button==null)continue;
                // Captured once, before this method first changes the button.
                if(!styleCDisabledLooks.TryGetValue(button,out var look))styleCDisabledLooks[button]=look=(button.colors.disabledColor,button.spriteState.disabledSprite);
                var colors=button.colors;colors.disabledColor=calm?colors.normalColor:look.color;button.colors=colors;
                var states=button.spriteState;states.disabledSprite=calm&&button.targetGraphic is Image image?image.sprite:look.sprite;button.spriteState=states;
            }
        }
        void RefreshStyleCTabs()
        {
            var materials=productChrome.Find("Style C materials");
            if(materials!=null)materials.gameObject.SetActive(productMaterials.gameObject.activeSelf);
            if(productTabs==null||StyleC("Buttons/round-normal")==null)return;
            for(int i=0;i<productTabs.Length;i++)
            {
                var button=productTabs[i];if(button==null||!(button.targetGraphic is JtnProductSurface surface))continue;
                StyleCRoundContent(button);
                StyleCButton(button,"round");
                if(i==productSelectedTab)
                {
                    surface.sprite=StyleC("Buttons/round-pressed");
                    var states=button.spriteState;states.highlightedSprite=states.selectedSprite=surface.sprite;button.spriteState=states;
                }
                else{surface.sprite=null;surface.overrideSprite=null;button.transition=Selectable.Transition.ColorTint;}
            }
        }
    }
}
