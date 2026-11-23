using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    // Six compact, static composites. Shapes rebuild only on stage, selection or rect changes.
    // No Button, materials, resource icon, per-frame animation or income implication.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JtnYardGraphic : UnityEngine.UI.Graphic
    {
        public int Family { get; private set; }=-2;
        public int PaidStage { get; private set; }=-1;
        public bool Selected { get; private set; }
        bool useHouseSprite;
        public bool UseHouseSprite {get=>useHouseSprite;set{if(useHouseSprite==value)return;useHouseSprite=value;SetVerticesDirty();}}
        [SerializeField] Image initialArtwork;
        static readonly string[] InitialNames={null,"workshop","recycler","loft","cart","power"};
        VertexHelper mesh;
        Rect bounds;
        static readonly Color32 Ink=new Color32(24,58,75,255),Wood=new Color32(229,164,71,255),
            Light=new Color32(255,211,74,255),Steel=new Color32(37,184,197,255),
            Cream=new Color32(255,240,206,255),Dark=new Color32(52,97,116,255),
            Cyan=new Color32(104,225,223,255),Rust=new Color32(239,62,54,255),
            Shadow=new Color32(14,28,35,70),Contact=new Color32(14,28,35,110);
        public void Configure(int family,int stage,bool selected=false)
        {
            raycastTarget=false;
            if(Family==family&&PaidStage==stage&&Selected==selected)return;
            Family=family;PaidStage=stage;Selected=selected;SetVerticesDirty();
            if(family>0&&stage==0&&initialArtwork==null)
            {
                var sprite=Resources.Load<Sprite>("Art/StyleC/Sites/"+InitialNames[family]);
                if(sprite!=null)
                {
                    var go=new GameObject("Style C initial site",typeof(RectTransform),typeof(Image));go.transform.SetParent(transform,false);
                    initialArtwork=go.GetComponent<Image>();initialArtwork.sprite=sprite;initialArtwork.preserveAspect=true;initialArtwork.raycastTarget=false;
                    var rect=initialArtwork.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                }
            }
            if(initialArtwork!=null)initialArtwork.enabled=stage==0;
            LayoutInitialArtwork();
        }
        protected override void OnRectTransformDimensionsChange()
        {base.OnRectTransformDimensionsChange();LayoutInitialArtwork();}
        void LayoutInitialArtwork()
        {
            if(initialArtwork==null)return;
            var size=initialArtwork.sprite.rect.size;var available=rectTransform.rect.size;
            size*=Mathf.Min(available.x/size.x,available.y*.9f/size.y);
            var rect=initialArtwork.rectTransform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.09f);rect.pivot=new Vector2(.5f,0);
            rect.sizeDelta=size;rect.anchoredPosition=Vector2.zero;
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();mesh=helper;bounds=rectTransform.rect;
            if(Family==-1){Ground();return;}
            if(Selected&&Family>=0)SelectionRing();
            if(Family==0){if(!UseHouseSprite)Home();return;}
            if(PaidStage==0&&initialArtwork!=null)
            {ContactShadow(.47f,.065f,Shadow);ContactShadow(.36f,.03f,Contact);return;}
            // Every site has a ground shadow; unfinished parts hint at its future use.
            Oval(.5f,.09f,.47f,.08f,Shadow);
            Oval(.5f,.09f,.36f,.045f,Contact);
            if(PaidStage==0){UnbuiltSite();return;}
            switch(Family){case 1:Workshop();break;case 2:Recycler();break;case 3:Loft();break;case 4:Cart();break;case 5:Power();break;}
        }
        void UnbuiltSite()
        {
            // Loose, low parts rather than usable buildings. No lit trim or stage-I facade.
            switch(Family)
            {
                case 1: // Workbench crate and an unfastened plank.
                    Box(.16f,.12f,.29f,.22f,Rust);Box(.19f,.19f,.23f,.035f,Dark);
                    Box(.66f,.12f,.12f,.13f,Dark);Beam(.35f,.29f,.84f,.37f,.055f,Steel);
                    break;
                case 2: // Separate sorting frame, can and sheet-metal offcut.
                    Box(.12f,.12f,.055f,.27f,Rust);Box(.36f,.12f,.055f,.20f,Rust);
                    Box(.12f,.12f,.295f,.05f,Dark);Beam(.16f,.37f,.39f,.30f,.045f,Steel);
                    Oval(.58f,.20f,.10f,.10f,Dark);Box(.50f,.27f,.16f,.04f,Steel);
                    Quad(.70f,.12f,.85f,.12f,.79f,.35f,.66f,.28f,Steel);
                    break;
                case 3: // Leaning perch posts; the enclosed loft has not been built.
                    Beam(.28f,.12f,.42f,.53f,.065f,Rust);Beam(.69f,.12f,.63f,.37f,.055f,Dark);
                    Beam(.24f,.42f,.63f,.47f,.05f,Steel);Box(.43f,.12f,.28f,.075f,Rust);
                    break;
                case 4: // Detached wheels beside a low packing crate.
                    Oval(.23f,.20f,.12f,.12f,Dark);Oval(.23f,.20f,.045f,.05f,Steel);
                    Oval(.75f,.18f,.09f,.10f,Dark);Oval(.75f,.18f,.035f,.04f,Steel);
                    Box(.39f,.12f,.23f,.24f,Rust);Beam(.41f,.15f,.60f,.33f,.04f,Dark);
                    Beam(.10f,.12f,.86f,.10f,.035f,Steel);
                    break;
                case 5: // An unwired coil and loose mounting bracket.
                    Oval(.45f,.25f,.20f,.16f,Steel);Oval(.45f,.25f,.12f,.085f,Dark);
                    Box(.13f,.12f,.14f,.12f,Rust);Box(.72f,.12f,.06f,.27f,Rust);
                    Beam(.61f,.16f,.83f,.23f,.04f,Dark);
                    break;
            }
        }
        void Home()
        {
            if(PaidStage<3)return;
            // Added domestic exterior, distinct from the pre-existing neon and annex.
            Oval(.5f,.12f,.46f,.06f,Shadow);
            Box(.04f,.12f,.92f,.13f,Wood);Box(.04f,.25f,.92f,.035f,Light);
            for(int i=0;i<4;i++){Box(.08f+i*.25f,.28f,.035f,.29f,Cream);Box(.06f+i*.25f,.55f,.075f,.04f,Light);}
            Box(.07f,.39f,.86f,.045f,Wood);
            if(PaidStage<4)return;
            Box(.24f,.02f,.52f,.09f,Steel);Box(.31f,.08f,.38f,.07f,Cream);
            // Bench, table and two planters turn the fitted porch into a domestic area.
            Box(.28f,.30f,.28f,.08f,Light);Box(.29f,.38f,.26f,.14f,Steel);
            Box(.30f,.25f,.035f,.07f,Wood);Box(.51f,.25f,.035f,.07f,Wood);
            Box(.62f,.29f,.15f,.05f,Cream);Box(.68f,.24f,.035f,.07f,Wood);
            Box(.08f,.26f,.10f,.14f,Rust);Oval(.13f,.44f,.10f,.09f,Steel);
            Box(.83f,.26f,.10f,.14f,Rust);Oval(.88f,.44f,.10f,.09f,Steel);
        }
        void Workshop()
        {
            Box(.12f,.17f,.08f,.34f,Wood);Box(.79f,.17f,.08f,.34f,Wood);
            Box(.06f,.48f,.88f,.10f,Light);Box(.10f,.41f,.81f,.06f,Wood);
            if(PaidStage<2){Box(.32f,.58f,.28f,.07f,Steel);return;}
            Box(.14f,.60f,.71f,.25f,Dark);Box(.17f,.63f,.65f,.025f,Wood);
            for(int i=0;i<3;i++){Beam(.27f+i*.2f,.67f,.27f+i*.2f,.80f,.035f,Cream);Box(.22f+i*.2f,.76f,.10f,.04f,Steel);}
            Box(.23f,.18f,.47f,.17f,Steel);Box(.27f,.29f,.38f,.025f,Light);
            if(PaidStage<3)return;
            Box(.05f,.16f,.04f,.76f,Wood);Box(.92f,.16f,.04f,.76f,Wood);
            Quad(.01f,.89f,.83f,1,.99f,.89f,.17f,.79f,Steel);Beam(.02f,.88f,.97f,.88f,.04f,Cream);
            if(PaidStage<4)return;
            Box(.28f,.58f,.15f,.16f,Cream);Box(.33f,.60f,.36f,.05f,Steel);Box(.62f,.59f,.07f,.13f,Wood);
            Box(.72f,.18f,.17f,.21f,Light);Box(.75f,.29f,.11f,.035f,Ink);
        }
        void Recycler()
        {
            for(int i=0;i<3;i++){Box(.05f+i*.32f,.14f,.26f,.29f,i==1?Wood:Steel);Box(.035f+i*.32f,.41f,.29f,.05f,Cream);}
            if(PaidStage<2)return;
            Box(.07f,.48f,.04f,.35f,Wood);Box(.88f,.48f,.04f,.35f,Wood);
            Box(.06f,.54f,.87f,.05f,Light);Box(.06f,.80f,.87f,.05f,Light);
            Beam(.17f,.60f,.37f,.76f,.07f,Steel);Box(.50f,.59f,.31f,.15f,Rust);
            if(PaidStage<3)return;
            for(int i=0;i<4;i++)Box(.04f+i*.30f,.13f,.035f,.40f,Light);
            Box(.02f,.87f,.94f,.065f,Steel);
            if(PaidStage<4)return;
            Quad(.12f,.03f,.76f,.03f,.85f,.14f,.23f,.14f,Cream);
            Box(.11f,.17f,.78f,.035f,Cyan);Beam(.12f,.09f,.26f,.27f,.035f,Steel);Beam(.79f,.08f,.88f,.27f,.035f,Steel);
        }
        void Loft()
        {
            Box(.46f,.13f,.09f,.48f,Wood);
            Box(.23f,.50f,.55f,PaidStage>=2?.31f:.22f,Wood);
            Quad(.12f,.72f,.50f,.98f,.89f,.72f,.50f,.82f,Steel);
            Oval(.50f,.64f,.11f,.09f,Ink);Box(.21f,.47f,.61f,.045f,Cream);
            if(PaidStage<2)return;
            Box(.12f,.41f,.76f,.06f,Light);Box(.29f,.74f,.14f,.04f,Cream);Box(.61f,.74f,.13f,.04f,Cream);
            if(PaidStage<3)return;
            Box(.06f,.19f,.04f,.44f,Steel);Box(.02f,.53f,.22f,.12f,Cream);
            Beam(.06f,.58f,.18f,.58f,.03f,Wood);Box(.72f,.25f,.22f,.10f,Cyan);Box(.81f,.13f,.035f,.15f,Wood);
            if(PaidStage<4)return;
            Beam(.26f,.45f,.48f,.21f,.06f,Steel);Beam(.76f,.45f,.53f,.21f,.06f,Steel);
            Box(.27f,.84f,.04f,.08f,Cream);Box(.70f,.81f,.04f,.12f,Cream);
            Box(.15f,.31f,.72f,.04f,Light);Box(.17f,.30f,.035f,.10f,Wood);Box(.82f,.30f,.035f,.10f,Wood);
        }
        void Cart()
        {
            Oval(.25f,.18f,.13f,.14f,Ink);Oval(.73f,.18f,.13f,.14f,Ink);
            Oval(.25f,.18f,.055f,.065f,Steel);Oval(.73f,.18f,.055f,.065f,Steel);
            Quad(.07f,.52f,.94f,.52f,.82f,.27f,.17f,.27f,Wood);Box(.07f,.51f,.87f,.055f,Light);
            if(PaidStage<2)return;
            for(int i=0;i<4;i++)Box(.10f+i*.26f,.51f,.035f,.24f,Steel);
            Box(.09f,.72f,.83f,.045f,Cream);Box(.10f,.60f,.81f,.035f,Steel);
            if(PaidStage<3)return;
            Beam(.14f,.34f,.01f,.86f,.05f,Steel);Box(0,.83f,.19f,.055f,Light);
            Box(.31f,.34f,.23f,.18f,Dark);Box(.37f,.41f,.11f,.035f,Cream);
            if(PaidStage<4)return;
            Box(.27f,.53f,.28f,.27f,Cream);Box(.60f,.53f,.22f,.18f,Steel);
            Beam(.27f,.54f,.55f,.79f,.035f,Wood);Beam(.54f,.54f,.28f,.79f,.035f,Wood);
        }
        void Power()
        {
            Box(.12f,.12f,.77f,.09f,Steel);Box(.29f,.21f,.41f,.34f,Wood);
            Oval(.50f,.40f,.14f,.15f,Ink);Oval(.50f,.40f,.06f,.07f,Cream);Box(.34f,.56f,.32f,.06f,Light);
            if(PaidStage<2)return;
            Box(.17f,.20f,.07f,.50f,Steel);Box(.77f,.20f,.07f,.50f,Steel);
            Quad(.12f,.69f,.67f,.79f,.90f,.69f,.34f,.60f,Dark);Box(.18f,.67f,.66f,.035f,Cream);
            if(PaidStage<3)return;
            Beam(.80f,.28f,.94f,.28f,.04f,Cyan);Beam(.94f,.28f,.94f,.57f,.04f,Cyan);
            Box(.04f,.42f,.22f,.22f,Cream);Box(.08f,.52f,.14f,.08f,Ink);Box(.13f,.44f,.04f,.04f,Rust);
            if(PaidStage<4)return;
            Box(.27f,.24f,.46f,.33f,Dark);
            for(int i=0;i<3;i++)Box(.32f,.30f+i*.07f,.22f,.025f,Steel);
            Box(.63f,.37f,.035f,.10f,Light);Box(.31f,.80f,.36f,.06f,Steel);
        }
        void Ground()
        {
            // Shared paths, not six framed cards. Keep the shell's overlay bands clear.
            Quad(.19f,.14f,.82f,.14f,.77f,.30f,.26f,.30f,new Color32(54,76,71,255));
            Quad(.41f,.24f,.59f,.24f,.55f,.45f,.44f,.45f,new Color32(80,95,79,255));
            for(int i=0;i<5;i++)Box(.46f,.27f+i*.033f,.075f,.018f,Dark);
        }
        void Box(float x,float y,float w,float h,Color32 c)=>Quad(x,y,x+w,y,x+w,y+h,x,y+h,c);
        void Beam(float ax,float ay,float bx,float by,float width,Color32 c)
        {var a=new Vector2(ax,ay);var b=new Vector2(bx,by);var n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;Quad(a.x+n.x,a.y+n.y,b.x+n.x,b.y+n.y,b.x-n.x,b.y-n.y,a.x-n.x,a.y-n.y,c);}
        void Oval(float x,float y,float rx,float ry,Color32 c)
        {for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;Tri(x,y,x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry,x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry,c);}}
        void ContactShadow(float rx,float ry,Color32 c)
        {
            int start=mesh.currentVertCount;Vertex(.5f,.09f,c);c.a=0;
            for(int i=0;i<=32;i++){float a=i*Mathf.PI/16;Vertex(.5f+Mathf.Cos(a)*rx,.09f+Mathf.Sin(a)*ry,c);}
            for(int i=0;i<32;i++)mesh.AddTriangle(start,start+i+1,start+i+2);
        }
        void SelectionRing()
        {
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6,ax=Mathf.Cos(a),ay=Mathf.Sin(a),bx=Mathf.Cos(b),by=Mathf.Sin(b);
                Quad(.5f+ax*.49f,.10f+ay*.09f,.5f+bx*.49f,.10f+by*.09f,
                    .5f+bx*.445f,.10f+by*.065f,.5f+ax*.445f,.10f+ay*.065f,Light);
            }
        }
        void Vertex(float x,float y,Color32 c)=>mesh.AddVert(new Vector3(bounds.x+x*bounds.width,bounds.y+y*bounds.height),c,Vector2.zero);
        void Tri(float ax,float ay,float bx,float by,float cx,float cy,Color32 c)
        {int n=mesh.currentVertCount;Vertex(ax,ay,c);Vertex(bx,by,c);Vertex(cx,cy,c);mesh.AddTriangle(n,n+1,n+2);}
        void Quad(float ax,float ay,float bx,float by,float cx,float cy,float dx,float dy,Color32 c)
        {int n=mesh.currentVertCount;Vertex(ax,ay,c);Vertex(bx,by,c);Vertex(cx,cy,c);Vertex(dx,dy,c);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
    }
}
