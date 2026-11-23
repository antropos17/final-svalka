using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JtnProductIcon : MaskableGraphic
    {
        public enum Symbol { Home,Spin,Events,Shop,Upgrade,Energy,Person,Gear,Map,Diamond,Ticket,Box,Leaf,Check,YardRuin,YardHouse,YardNeon }
        [SerializeField] Symbol symbol;
        [SerializeField] Sprite artworkOverride;
        public Sprite ArtworkOverride {get=>artworkOverride;set{if(artworkOverride==value)return;artworkOverride=value;SetAllDirty();}}
        public Symbol Shape { get=>symbol;set{symbol=value;artworkOverride=null;SetAllDirty();} }
        static readonly string[] ArtNames={"home","spin","events","shop","upgrade","energy","profile","settings","map","diamond","ticket","parts","leaf","check","yard-ruin","yard-house","yard-neon"};
        static readonly Sprite[] ArtSprites=new Sprite[ArtNames.Length];
        Sprite Artwork=>artworkOverride!=null?artworkOverride:ArtSprites[(int)symbol]??(ArtSprites[(int)symbol]=Resources.Load<Sprite>("Art/StyleC/Icons/"+ArtNames[(int)symbol]));
        public override Texture mainTexture=>Artwork!=null?Artwork.texture:base.mainTexture;
        VertexHelper mesh;
        Vector2 Point(float x,float y){var r=rectTransform.rect;float n=Mathf.Min(r.width,r.height);return r.center+new Vector2(x-.5f,y-.5f)*n;}
        void Line(float x,float y,float a,float b,float width=.065f)
        {
            var p=Point(x,y);var q=Point(a,b);var d=(q-p).normalized;
            var n=new Vector2(-d.y,d.x)*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*width*.5f;
            int i=mesh.currentVertCount;mesh.AddVert(p+n,color,Vector2.zero);mesh.AddVert(p-n,color,Vector2.zero);mesh.AddVert(q-n,color,Vector2.zero);mesh.AddVert(q+n,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        void Ring(float x,float y,float radius,int segments=24)
        {for(int i=0;i<segments;i++){float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,.055f);}}
        void YardHouse(bool improved)
        {
            Line(.12f,.53f,.5f,.87f);Line(.5f,.87f,.88f,.53f);
            Line(.23f,.53f,.23f,.18f);Line(.23f,.18f,.77f,.18f);Line(.77f,.18f,.77f,.53f);
            Line(.43f,.18f,.43f,.43f);Line(.43f,.43f,.59f,.43f);Line(.59f,.43f,.59f,.18f);
            Line(.29f,.38f,.37f,.38f,.04f);Line(.37f,.38f,.37f,.49f,.04f);Line(.37f,.49f,.29f,.49f,.04f);Line(.29f,.49f,.29f,.38f,.04f);
            if(!improved)return;
            Line(.16f,.48f,.5f,.78f,.035f);Line(.5f,.78f,.84f,.48f,.035f);
            Line(.65f,.38f,.73f,.38f,.04f);Line(.73f,.38f,.73f,.49f,.04f);Line(.73f,.49f,.65f,.49f,.04f);Line(.65f,.49f,.65f,.38f,.04f);
            Line(.77f,.18f,.91f,.18f,.045f);Line(.91f,.18f,.91f,.4f,.045f);Line(.91f,.4f,.77f,.4f,.045f);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();mesh=vh;
            var art=Artwork;
            if(art!=null)
            {
                var r=rectTransform.rect;var size=art.rect.size;
                var half=size*Mathf.Min(r.width/size.x,r.height/size.y)*.5f;var c=r.center;
                var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(art);var tint=new Color(1,1,1,color.a);
                vh.AddVert(c+new Vector2(-half.x,-half.y),tint,new Vector2(uv.x,uv.y));
                vh.AddVert(c+new Vector2(-half.x,half.y),tint,new Vector2(uv.x,uv.w));
                vh.AddVert(c+new Vector2(half.x,half.y),tint,new Vector2(uv.z,uv.w));
                vh.AddVert(c+new Vector2(half.x,-half.y),tint,new Vector2(uv.z,uv.y));
                vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);return;
            }
            switch(symbol)
            {
                case Symbol.Home:Line(.15f,.5f,.5f,.82f);Line(.5f,.82f,.85f,.5f);Line(.25f,.53f,.25f,.2f);Line(.25f,.2f,.75f,.2f);Line(.75f,.2f,.75f,.53f);Line(.45f,.2f,.45f,.43f);break;
                case Symbol.YardRuin:
                    Line(.2f,.17f,.76f,.17f);Line(.23f,.17f,.23f,.4f);Line(.74f,.17f,.79f,.39f);
                    Line(.13f,.5f,.33f,.65f);Line(.33f,.65f,.56f,.42f);Line(.65f,.7f,.88f,.5f);
                    Line(.34f,.2f,.63f,.43f,.045f);Line(.35f,.43f,.63f,.2f,.045f);break;
                case Symbol.YardHouse:YardHouse(false);break;
                case Symbol.YardNeon:YardHouse(true);break;
                case Symbol.Spin:Ring(.5f,.5f,.32f);Line(.57f,.82f,.79f,.83f);Line(.79f,.83f,.79f,.62f);Line(.45f,.35f,.62f,.5f);Line(.62f,.5f,.45f,.65f);break;
                case Symbol.Events:Line(.5f,.88f,.6f,.62f);Line(.6f,.62f,.87f,.53f);Line(.87f,.53f,.62f,.42f);Line(.62f,.42f,.5f,.14f);Line(.5f,.14f,.4f,.42f);Line(.4f,.42f,.13f,.53f);Line(.13f,.53f,.4f,.62f);Line(.4f,.62f,.5f,.88f);break;
                case Symbol.Shop:Line(.22f,.69f,.78f,.69f);Line(.22f,.69f,.16f,.52f);Line(.16f,.52f,.84f,.52f);Line(.84f,.52f,.78f,.69f);Line(.25f,.48f,.25f,.2f);Line(.25f,.2f,.75f,.2f);Line(.75f,.2f,.75f,.48f);Line(.5f,.2f,.5f,.42f);break;
                case Symbol.Upgrade:Line(.18f,.25f,.18f,.43f);Line(.4f,.25f,.4f,.59f);Line(.62f,.25f,.62f,.76f);Line(.49f,.65f,.62f,.79f);Line(.62f,.79f,.76f,.65f);break;
                case Symbol.Energy:Line(.58f,.87f,.28f,.48f);Line(.28f,.48f,.51f,.48f);Line(.51f,.48f,.42f,.13f);Line(.42f,.13f,.76f,.57f);Line(.76f,.57f,.54f,.57f);Line(.54f,.57f,.58f,.87f);break;
                case Symbol.Person:Ring(.5f,.66f,.17f);Line(.2f,.2f,.25f,.36f);Line(.25f,.36f,.5f,.44f);Line(.5f,.44f,.75f,.36f);Line(.75f,.36f,.8f,.2f);Line(.2f,.2f,.8f,.2f);break;
                case Symbol.Gear:Ring(.5f,.5f,.23f);Ring(.5f,.5f,.075f);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(.5f+Mathf.Cos(a)*.24f,.5f+Mathf.Sin(a)*.24f,.5f+Mathf.Cos(a)*.36f,.5f+Mathf.Sin(a)*.36f,.1f);}break;
                case Symbol.Map:Line(.15f,.25f,.15f,.75f);Line(.15f,.75f,.39f,.65f);Line(.39f,.65f,.63f,.77f);Line(.63f,.77f,.85f,.65f);Line(.85f,.65f,.85f,.15f);Line(.85f,.15f,.63f,.27f);Line(.63f,.27f,.39f,.15f);Line(.39f,.15f,.15f,.25f);Line(.39f,.15f,.39f,.65f);Line(.63f,.27f,.63f,.77f);break;
                case Symbol.Diamond:Line(.15f,.6f,.35f,.8f);Line(.35f,.8f,.65f,.8f);Line(.65f,.8f,.85f,.6f);Line(.85f,.6f,.5f,.15f);Line(.5f,.15f,.15f,.6f);Line(.15f,.6f,.85f,.6f);Line(.35f,.8f,.5f,.15f);Line(.65f,.8f,.5f,.15f);break;
                case Symbol.Ticket:Line(.17f,.25f,.83f,.25f);Line(.83f,.25f,.83f,.75f);Line(.83f,.75f,.17f,.75f);Line(.17f,.75f,.17f,.25f);for(int i=0;i<3;i++)Line(.6f,.34f+i*.13f,.6f,.4f+i*.13f);break;
                case Symbol.Box:Line(.18f,.68f,.5f,.83f);Line(.5f,.83f,.82f,.68f);Line(.82f,.68f,.82f,.3f);Line(.82f,.3f,.5f,.15f);Line(.5f,.15f,.18f,.3f);Line(.18f,.3f,.18f,.68f);Line(.18f,.68f,.5f,.53f);Line(.5f,.53f,.82f,.68f);Line(.5f,.53f,.5f,.15f);break;
                case Symbol.Leaf:Ring(.5f,.5f,.29f);Line(.28f,.25f,.73f,.75f);Line(.47f,.46f,.47f,.67f);break;
                case Symbol.Check:Line(.2f,.48f,.43f,.25f,.1f);Line(.43f,.25f,.82f,.76f,.1f);break;
            }
        }
    }
}
