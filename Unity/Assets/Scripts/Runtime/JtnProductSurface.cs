using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    // Editable rounded surfaces, with optional sliced artwork on the product screen.
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JtnProductSurface : Image
    {
        [SerializeField] float radius=14;
        [SerializeField] Color artworkTint=Color.white;
        public Color ArtworkTint { get=>artworkTint;set{artworkTint=value;SetVerticesDirty();} }
        public float Radius { get=>radius; set { radius=value;SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if(sprite!=null)
            {
                base.OnPopulateMesh(vh);
                var vertex=new UIVertex();
                for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);vertex.color=artworkTint;vh.SetUIVertex(vertex,i);}
                return;
            }
            vh.Clear();var r=rectTransform.rect;float corner=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            if(corner<=0)
            {
                vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);
                vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);
                vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);return;
            }
            // A narrow alpha fringe smooths overlay Canvas edges without camera MSAA.
            float inset=Mathf.Min(1.25f/(canvas==null?1:canvas.scaleFactor),corner*.5f);
            var fade=color;fade.a=0;const int steps=12,count=4*(steps+1);
            vh.AddVert(r.center,color,Vector2.zero);
            for(int c=0;c<4;c++)
            {
                var center=new Vector2(c==0||c==3?r.xMax-corner:r.xMin+corner,c<2?r.yMax-corner:r.yMin+corner);
                for(int i=0;i<=steps;i++)
                {
                    float angle=(c*90+i*90f/steps)*Mathf.Deg2Rad;
                    var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    vh.AddVert(center+direction*(corner-inset),color,Vector2.zero);
                    vh.AddVert(center+direction*corner,fade,Vector2.zero);
                }
            }
            for(int i=0;i<count;i++)
            {
                int inner=1+i*2,next=1+(i+1)%count*2;
                vh.AddTriangle(0,inner,next);vh.AddTriangle(inner,inner+1,next+1);vh.AddTriangle(inner,next+1,next);
            }
        }
    }
}
