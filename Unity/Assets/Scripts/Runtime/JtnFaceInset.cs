using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    // Keeps the labels of a Style C button on the flat face of its artwork, clear of the dark rim and the bottom bevel.
    // Only margins change: the label rects, the button and its hit area stay where the layout put them.
    [AddComponentMenu("")]
    public sealed class JtnFaceInset : MonoBehaviour
    {
        // Rim and bevel of the Style C button sprites in sprite pixels (left, top, right, bottom); the 9-slice borders are 60/45.
        static readonly Vector4 Face=new Vector4(34,34,34,46);
        static readonly List<TMP_Text> Texts=new List<TMP_Text>();
        readonly Dictionary<TMP_Text,(Vector4 own,Vector4 written)> margins=new Dictionary<TMP_Text,(Vector4,Vector4)>();
        Button button;

        void LateUpdate()
        {
            if(button==null)button=GetComponent<Button>();
            var surface=button!=null?button.targetGraphic as JtnProductSurface:null;
            bool art=surface!=null&&surface.sprite!=null&&surface.isActiveAndEnabled;
            Rect face=default;
            if(art)
            {
                var r=surface.rectTransform.rect;float ppu=surface.pixelsPerUnit*surface.pixelsPerUnitMultiplier;
                // Like the sliced mesh, borders shrink together when the button is smaller than both of them.
                float kx=Mathf.Min(1,r.width*ppu/120),ky=Mathf.Min(1,r.height*ppu/90);
                face=Rect.MinMaxRect(r.xMin+Face.x/ppu*kx,r.yMin+Face.w/ppu*ky,r.xMax-Face.z/ppu*kx,r.yMax-Face.y/ppu*ky);
            }
            GetComponentsInChildren(Texts);
            foreach(var text in Texts)
            {
                // A margin written by layout code since the last pass becomes the label's own margin.
                var current=text.margin;
                var own=margins.TryGetValue(text,out var known)&&known.written==current?known.own:current;
                var target=own;
                if(art)
                {
                    var min=text.rectTransform.InverseTransformPoint(surface.rectTransform.TransformPoint(face.min));
                    var max=text.rectTransform.InverseTransformPoint(surface.rectTransform.TransformPoint(face.max));
                    var box=text.rectTransform.rect;
                    target=new Vector4(Mathf.Max(own.x,min.x-box.xMin),Mathf.Max(own.y,box.yMax-max.y),Mathf.Max(own.z,box.xMax-max.x),Mathf.Max(own.w,min.y-box.yMin));
                }
                if((target-current).sqrMagnitude>.0001f)text.margin=target;
                margins[text]=(own,target);
            }
            Texts.Clear();
        }
    }
}
