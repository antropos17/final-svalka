using TMPro;
using UnityEngine;
using Glyph = JunkToNeon.JtnProductIcon.Symbol;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        bool innerRuntimeCreated;

        // Root calls after CreateProductUi in Awake and stopped authoring. Existing
        // captions, listeners, consent and availability remain intact.
        public void CreateInnerUi()
        {
            if (panel == null || panelTitle == null || panelBody == null) return;
            if (Application.isPlaying && innerRuntimeCreated) return;
            var card = panel.transform.Find("Project Card");
            if (card != null) Surface(card.gameObject, ColorOf("F2EDDC"), 24, true);
            // ProductUi owns the modal/card fallback and its outer geometry.
            Place(panelTitle.rectTransform, panel.transform, new Vector2(.07f, .895f), new Vector2(.93f, .975f));
            panelTitle.color = ColorOf("142638");
            panelTitle.alignment = TextAlignmentOptions.MidlineLeft;
            panelTitle.fontStyle = FontStyles.Bold;
            panelTitle.enableAutoSizing = true;
            panelTitle.fontSizeMin = 18; panelTitle.fontSizeMax = 20; panelTitle.fontSize = 20;
            panelTitle.textWrappingMode = TextWrappingModes.Normal;
            panelTitle.overflowMode = TextOverflowModes.Overflow;
            panelTitle.raycastTarget = false;
            // The authored body starts well below the title. Expand upward only:
            // retain its width, lower boundary and all confirmation/button geometry.
            var bodyRect = panelBody.rectTransform;
            bodyRect.anchorMax = new Vector2(bodyRect.anchorMax.x, .865f);
            panelBody.color = ColorOf("263C50");
            panelBody.alignment = TextAlignmentOptions.TopLeft;
            panelBody.lineSpacing = 2;
            panelBody.fontStyle = FontStyles.Normal;
            panelBody.enableAutoSizing = true;
            panelBody.fontSizeMin = 10; panelBody.fontSizeMax = 14; panelBody.fontSize = 14;
            panelBody.textWrappingMode = TextWrappingModes.Normal;
            panelBody.overflowMode = TextOverflowModes.Overflow;
            panelBody.raycastTarget = false;

            InnerButton(selectButton, true, true);
            InnerButton(reloadButton, false, false);
            InnerButton(closeButton, false, false);
            InnerButton(chapterPrevious, false, false);
            InnerButton(chapterNext, false, false);
            if (waveChoices != null)
                foreach (var choice in waveChoices) InnerButton(choice, false, false);
            innerRuntimeCreated = Application.isPlaying;
        }

        void InnerButton(UnityEngine.UI.Button button, bool primary, bool confirmationIcon)
        {
            if (button == null) return;
            button.targetGraphic = Surface(button.gameObject, ColorOf(primary ? "F4B85C" : "263C50"), 16, true);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(1, .97f, .9f) : new Color(.9f, .96f, 1);
            colors.pressedColor = primary ? new Color(.88f, .78f, .62f) : new Color(.73f, .83f, .9f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.67f, .72f, .76f, 1);
            button.colors = colors;
            StyleCInnerAction(button, primary);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.color = ColorOf(primary ? "142638" : "F6F1E3");
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
                text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 16; text.fontSize = 16;
                text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
                // Only caption padding changes; the clickable target and temporary
                // Orders split stay exactly as their existing controllers define.
                Place(text.rectTransform, button.transform, new Vector2(confirmationIcon ? .15f : .04f, .06f), new Vector2(.96f, .94f));
            }
            if (confirmationIcon)
            {
                var mark = button.transform.Find("Inner Confirm Mark") as RectTransform;
                if (mark == null) mark = Area("Inner Confirm Mark", button.transform, new Vector2(.035f, .32f), new Vector2(.12f, .68f));
                var icon = mark.GetComponent<JtnProductIcon>() ?? mark.gameObject.AddComponent<JtnProductIcon>();
                icon.Shape = Glyph.Check; icon.color = ColorOf("142638"); icon.raycastTarget = false;
            }
        }
    }
}
