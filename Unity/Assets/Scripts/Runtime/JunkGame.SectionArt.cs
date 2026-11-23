using TMPro;
using JunkToNeon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        static Sprite SectionSprite(string name)=>name==null?null:StyleC("Sections/"+name)??StyleC("Icons/"+name);
        static readonly string[][] SectionCardArt={
            new[]{"emblem-district","map","emblem-upgrades","icon-progress","icon-trades","icon-orders"},
            new[]{"emblem-memory","emblem-sorting","icon-orders","emblem-events","diamond","emblem-parcels"},
            new[]{"icon-modules","icon-sockets","icon-appearance","diamond","icon-trades","emblem-energy"},
            new[]{"spin","icon-equipment","leaf","icon-skills","emblem-energy","info"},
            new[]{"emblem-energy","diamond","emblem-energy","icon-equipment","parts","info"},
            new[]{"icon-progress","icon-appearance","sections","emblem-settings","info","info"},
            new[]{"emblem-events","help","icon-progress","parts","spin","info"}
        };
        static readonly string[] SectionCategoryArt={"icon-modules","icon-sockets","icon-appearance","icon-equipment","icon-trades","emblem-energy"};
        static readonly string[] SectionHubArt={"emblem-district","emblem-events","emblem-shop","emblem-upgrades","emblem-energy","emblem-profile","emblem-settings"};
        // Every return to the previous screen uses this caption; a plain dismissal stays "Close".
        const string BackCaption="Back";
        // Horizontal edges of the shared heading in panel space: icon, then title.
        const float HeadingIconLeft=.07f,HeadingIconRight=.19f,HeadingTextLeft=.23f,HeadingTextRight=.93f;
        // Vertical band of the shared heading in panel space (the panel title's own band).
        const float HeadingBottom=.895f,HeadingTop=.975f;
        static readonly Color CardTint=new Color(.35f,.52f,.65f,1),ActionTint=new Color(.30f,.42f,.52f,1);
        public void CreateSectionArtwork()
        {
            StyleCHeading(panelTitle,"emblem-district",HeadingIconLeft,HeadingIconRight,HeadingTextLeft);
            var heading=panelTitle.transform.parent.Find("Style C heading "+panelTitle.name);
            if(heading!=null)heading.gameObject.SetActive(false);
            if(expansionTitle!=null)SharedHeading(expansionTitle,"emblem-shop");
            StyleCSectionsHub();
            StyleCBackArtwork();
        }
        // Converts a panel-space x or y into the anchors of a descendant placed with stretched, zero-offset anchors.
        float PanelX(Transform parent,float x)=>PanelAxis(parent,x,0);
        float PanelY(Transform parent,float y)=>PanelAxis(parent,y,1);
        float PanelAxis(Transform parent,float value,int axis)
        {
            float min=0,max=1;
            for(var rect=parent as RectTransform;rect!=null&&rect!=panel.transform;rect=rect.parent as RectTransform)
            {float low=rect.anchorMin[axis],size=rect.anchorMax[axis]-low;min=low+min*size;max=low+max*size;}
            return (value-min)/(max-min);
        }
        // Expansion draws its own title; it lines up with the panel title and looks the same.
        void SharedHeading(TMP_Text title,string artwork)
        {
            var parent=title.transform.parent;var bounds=title.rectTransform;
            Place(bounds,parent,new Vector2(PanelX(parent,HeadingTextLeft),PanelY(parent,HeadingBottom)),new Vector2(PanelX(parent,HeadingTextRight),PanelY(parent,HeadingTop)));
            SharedHeadingText(title);
            StyleCHeading(title,artwork,PanelX(parent,HeadingIconLeft),PanelX(parent,HeadingIconRight),PanelX(parent,HeadingTextLeft));
        }
        static void SharedHeadingText(TMP_Text title)
        {
            title.color=ColorOf("142638");title.fontStyle=FontStyles.Bold;title.alignment=TextAlignmentOptions.MidlineLeft;
            title.enableAutoSizing=true;title.fontSizeMin=14;title.fontSizeMax=20;title.textWrappingMode=TextWrappingModes.Normal;title.raycastTarget=false;
        }
        // Cards on inner screens share the hub cards' glossy look; a focused card uses the cream artwork.
        static void StyleCInnerCard(UnityEngine.UI.Button button,bool focused=false)
        {
            if(StyleC("Buttons/secondary-normal")==null||!(button.targetGraphic is JtnProductSurface surface))return;
            StyleCButton(button,focused?"neutral":"secondary");surface.ArtworkTint=focused?Color.white:CardTint;FaceInset(button);
        }
        // Labels stay on the flat face of the artwork, clear of its rim and bevel.
        static void FaceInset(UnityEngine.UI.Button button){if(button.GetComponent<JtnFaceInset>()==null)button.gameObject.AddComponent<JtnFaceInset>();}
        // Inner-screen actions: the main action is the orange SPIN artwork, every other action a darker card.
        static bool StyleCInnerAction(UnityEngine.UI.Button button,bool primary)
        {
            if(StyleC("Buttons/primary-normal")==null||!(button.targetGraphic is JtnProductSurface surface))return false;
            StyleCButton(button,primary?"primary":"secondary");surface.ArtworkTint=primary?Color.white:ActionTint;FaceInset(button);
            return true;
        }
        void StyleCSectionsHub()
        {
            string[] artwork={"spin","map","emblem-district","icon-skills","icon-orders","icon-progress","help"};
            for(int i=0;i<sectionButtons.Length;i++)StyleCSectionButton(sectionButtons[i],artwork[i]);
            if(expansionEntryButton!=null)StyleCSectionButton(expansionEntryButton,"emblem-shop");
            if(arcadeEntryButton!=null)StyleCSectionButton(arcadeEntryButton,"emblem-events",true);
        }
        void StyleCSectionButton(UnityEngine.UI.Button button,string artwork,bool wide=false)
        {
            InnerButton(button,false,false);StyleCInnerCard(button);
            var label=button.GetComponentInChildren<TMP_Text>(true);label.alignment=TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMin=new Vector2(wide?.15f:.27f,.08f);label.rectTransform.anchorMax=new Vector2(.96f,.92f);
            var root=button.transform;var rect=root.Find("Style C section icon") as RectTransform;
            if(rect==null)rect=Box("Style C section icon",root,new Vector2(wide?.0275f:.055f,.19f),new Vector2(wide?.1075f:.215f,.81f),Color.white);
            var image=rect.GetComponent<Image>();image.sprite=SectionSprite(artwork);image.preserveAspect=true;image.raycastTarget=false;
        }
        void StyleCProductCardIcon(JtnProductIcon icon,int hub,int index)
        {
            string name=SectionCardArt[hub-ProductDistrict][index];
            if(hub==ProductEvents&&index==3)
                name=Session.Parcels.ActiveRun!=null?"emblem-parcels":Session.Arcade.ActiveRun?.Id==ArcadeCatalog.Memory?"emblem-memory":"emblem-sorting";
            icon.ArtworkOverride=SectionSprite(name);
        }
        static string ExpansionArtwork(string id,int kind,int value)
        {
            if(kind==1)return SectionCategoryArt[value];
            if(kind==3||id!=null&&id.StartsWith("socket.",System.StringComparison.Ordinal))return "icon-sockets";
            if(id!=null&&id.StartsWith("preset.",System.StringComparison.Ordinal))return "icon-sockets";
            if(id==null||id.StartsWith("module.",System.StringComparison.Ordinal))return "icon-modules";
            if(id.StartsWith("theme.",System.StringComparison.Ordinal))return "icon-appearance";
            if(id.StartsWith("equipment.",System.StringComparison.Ordinal))return "icon-equipment";
            if(id.StartsWith("trade.",System.StringComparison.Ordinal))return "icon-trades";
            if(id.StartsWith("potato.",System.StringComparison.Ordinal))return "leaf";
            if(id.StartsWith("speed.",System.StringComparison.Ordinal))return "spin";
            if(id.StartsWith("workbench.",System.StringComparison.Ordinal))return "icon-equipment";
            return "emblem-energy";
        }
        void StyleCHeading(TMP_Text title,string artwork,float iconLeft,float iconRight,float textLeft)
        {
            var parent=title.transform.parent;string name="Style C heading "+title.name;
            var rect=parent.Find(name) as RectTransform;var sprite=SectionSprite(artwork);
            if(sprite==null){if(rect!=null)rect.gameObject.SetActive(false);return;}
            var bounds=title.rectTransform;
            if(rect==null)rect=Box(name,parent,Vector2.zero,Vector2.one,Color.white);
            Place(rect,parent,new Vector2(iconLeft,bounds.anchorMin.y),new Vector2(iconRight,bounds.anchorMax.y));
            var image=rect.GetComponent<Image>();image.sprite=sprite;image.color=Color.white;image.preserveAspect=true;image.raycastTarget=false;
            rect.gameObject.SetActive(true);rect.SetSiblingIndex(title.transform.GetSiblingIndex());
            bounds.anchorMin=new Vector2(textLeft,bounds.anchorMin.y);bounds.offsetMin=bounds.offsetMax=Vector2.zero;
        }
        void RefreshSectionArtwork()
        {
            string key=null;
            if(panel.activeSelf&&!(wavePanel>=ExpansionHome&&wavePanel<=ArcadeInfo))
            {
                if(wavePanel>=ProductDistrict&&wavePanel<=ProductSettings)key=SectionHubArt[wavePanel-ProductDistrict];
                else if(wavePanel==NavigationSkills||wavePanel==ChapterSkill||wavePanel==ChapterBranches||wavePanel==ChapterTiers)key="icon-skills";
                else if(wavePanel==NavigationOrders||wavePanel==SectionOrdersLocked)key="icon-orders";
                else if(wavePanel==SectionProgress||wavePanel==SectionClearGoal)key="icon-progress";
                else if(wavePanel==SectionRules)key="help";
                else if(wavePanel==SectionHub)key="sections";
                else key="emblem-district";
            }
            StyleCHeading(panelTitle,key,.07f,.19f,.23f);
            StyleCBackArtwork();
        }
        void StyleCBackArtwork()
        {
            var root=closeButton.transform;var rect=root.Find("Style C back") as RectTransform;
            if(rect==null)rect=Box("Style C back",root,new Vector2(.07f,.26f),new Vector2(.15f,.74f),Color.white);
            var image=rect.GetComponent<Image>();image.sprite=StyleC("Icons/back");image.preserveAspect=true;image.raycastTarget=false;
            bool wide=((RectTransform)root).rect.width>200;rect.gameObject.SetActive(wide);
            var label=closeButton.GetComponentInChildren<TMP_Text>(true);var bounds=label.rectTransform;
            bounds.anchorMin=new Vector2(wide?.20f:.04f,bounds.anchorMin.y);bounds.offsetMin=Vector2.zero;
        }
        void StyleCNavigationArtwork(int index)
        {
            var button=navigationCards[index];var root=button.transform;
            var rect=root.Find("Style C navigation icon") as RectTransform;
            if(wavePanel==NavigationOrders){if(rect!=null)rect.gameObject.SetActive(false);return;}
            string key=wavePanel==NavigationSkills?"icon-skills":index==0?"yard-house":index==1?"icon-equipment":index==2?"icon-trades":index==3?"emblem-parcels":index==4?"parts":"emblem-energy";
            if(rect==null)rect=Box("Style C navigation icon",root,new Vector2(.055f,.57f),new Vector2(.18f,.90f),Color.white);
            var image=rect.GetComponent<Image>();image.sprite=SectionSprite(key);image.preserveAspect=true;image.raycastTarget=false;rect.gameObject.SetActive(image.sprite!=null);
            var label=button.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.anchorMin=new Vector2(.23f,label.rectTransform.anchorMin.y);
        }
    }
}
