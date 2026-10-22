using System;
using System.Collections.Generic;
using UnityEngine;

namespace UFN.Presentation
{
    internal sealed class ShapeBank : IDisposable
    {
        public readonly Sprite Disc, Glow, Ring, Streak, Plate, Shadow, Vignette, Football;
        public readonly Material Solid, Additive, Fighter;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        public ShapeBank()
        {
            Solid=Material("Sprites/Default");Additive=Material("UFN/Glow");Fighter=Material("UFN/Fighter");
            Disc=Make("disc",64,(x,y)=>new Color(1,1,1,1-Edge(.91f,1,new Vector2(x,y).magnitude)));
            Glow=Make("soft-glow",96,(x,y)=>new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-new Vector2(x,y).magnitude),2.7f)));
            Ring=Make("ring",96,(x,y)=>{float d=new Vector2(x,y).magnitude;return new Color(1,1,1,Edge(.77f,.84f,d)*(1-Edge(.91f,.98f,d)));});
            Streak=Make("streak",64,(x,y)=>new Color(1,1,1,Mathf.Pow(1-Mathf.Abs(y),2)*Mathf.Clamp01(1-Mathf.Abs(x))));
            Plate=Make("plate",48,(x,y)=>new Color(1,1,1,(Mathf.Abs(x)+Mathf.Abs(y)<1.85f)?1:0));
            Shadow=Make("ground-shadow",96,(x,y)=>new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-new Vector2(x,y).magnitude),1.7f)));
            Vignette=Make("vignette",128,(x,y)=>new Color(.015f,.025f,.05f,Edge(.35f,1.3f,new Vector2(x*.78f,y).magnitude)*.57f));
            Football=Make("football",64,(x,y)=>{float r=new Vector2(x,y).magnitude;if(r>1)return Color.clear;float angle=Mathf.Atan2(y,x);bool black=r<.26f||(r>.56f&&Mathf.Cos(angle*5)>.54f);return new Color(black?.08f:.91f,black?.13f:.94f,black?.19f:.97f,1);});
        }
        static float Edge(float min,float max,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(min,max,value));
        Material Material(string shader)
        {
            var found=Shader.Find(shader);if(found==null)throw new InvalidOperationException("Required built-in presentation shader missing: "+shader);
            var material=new Material(found){name=shader};owned.Add(material);return material;
        }
        Sprite Make(string name,int size,Func<float,float,Color> pixel)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)colors[y*size+x]=pixel((x+.5f)/size*2-1,(y+.5f)/size*2-1);
            tex.SetPixels(colors);tex.Apply(false,true);owned.Add(tex);
            var sprite=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size,0,SpriteMeshType.FullRect);sprite.name=name;owned.Add(sprite);return sprite;
        }
        public static SpriteRenderer Renderer(Transform parent,string name,Sprite sprite,Material material,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;r.sortingOrder=order;return r;
        }
        public void Dispose(){foreach(var item in owned)UnityEngine.Object.Destroy(item);owned.Clear();}
    }

    /// <summary>All event particles are preallocated; effects cannot increase scene object count.</summary>
    internal sealed class ImpactPool
    {
        struct Particle { public SpriteRenderer Sprite;public Vector2 Position,Velocity,Scale;public Color Color;public float Age,Life,Spin,Rotation,Growth,Gravity; }
        readonly Particle[] particles;readonly ShapeBank shapes;int cursor;uint random=0x9e3779b9;
        public int ActiveCount{get{int count=0;for(int i=0;i<particles.Length;i++)if(particles[i].Age<particles[i].Life)count++;return count;}}
        public ImpactPool(Transform parent,ShapeBank shapes,int capacity=112)
        {
            this.shapes=shapes;particles=new Particle[capacity];
            for(int i=0;i<capacity;i++){particles[i].Sprite=ShapeBank.Renderer(parent,"Pooled impact "+i,shapes.Glow,shapes.Additive,45);particles[i].Sprite.enabled=false;}
        }
        float Next(){random^=random<<13;random^=random>>17;random^=random<<5;return(random&0xffffff)/16777215f;}
        public void Emit(Sprite sprite,Vector2 p,Vector2 velocity,Vector2 scale,Color color,float life,float growth=0,float spin=0,float gravity=0,bool additive=true)
        {
            int slot=cursor++%particles.Length;var renderer=particles[slot].Sprite;renderer.enabled=true;renderer.sprite=sprite;renderer.sharedMaterial=additive?shapes.Additive:shapes.Solid;
            particles[slot]=new Particle{Sprite=renderer,Position=p,Velocity=velocity,Scale=scale,Color=color,Life=life,Growth=growth,Spin=spin,Gravity=gravity,Rotation=Next()*360};
        }
        public void Hit(Vector2 p,Color color,bool heavy,float intensity,float flashes=0.5f)
        {
            flashes=Mathf.Clamp01(flashes);intensity=Mathf.Clamp01(intensity);
            if(flashes>0)Emit(shapes.Glow,p,Vector2.zero,Vector2.one*(heavy?2.1f:1.05f),new Color(color.r,color.g,color.b,.85f*flashes),.18f,1.8f);
            if(intensity>0)Emit(shapes.Ring,p,Vector2.zero,Vector2.one*.22f,new Color(color.r,color.g,color.b,intensity),heavy?.4f:.24f,heavy?9:5);
            int count=Mathf.RoundToInt((heavy?17:10)*Mathf.Clamp01(intensity));
            for(int i=0;i<count;i++){float a=Next()*Mathf.PI*2;var v=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(2+Next()*5);Emit(shapes.Streak,p,v,new Vector2(.3f+Next()*.3f,.025f+Next()*.02f),Color.Lerp(color,Color.white,.5f),.15f+Next()*.19f,-.7f,0,5);}
        }
        public void Dust(Vector2 p,float strength,float intensity)
        {
            int count=Mathf.RoundToInt(9*Mathf.Clamp01(intensity));
            for(int i=0;i<count;i++)Emit(shapes.Glow,p+new Vector2((Next()-.5f)*.65f,0),new Vector2((Next()-.5f)*2.5f,.3f+Next()*.6f),new Vector2(.35f,.17f)*strength,new Color(.61f,.70f,.75f,.35f),.45f,1.8f,0,-.3f,false);
        }
        public void Reset(){for(int i=0;i<particles.Length;i++){particles[i].Age=particles[i].Life;particles[i].Sprite.enabled=false;}}
        public void Step(float dt)
        {
            for(int i=0;i<particles.Length;i++)
            {
                var p=particles[i];if(p.Age>=p.Life)continue;p.Age+=dt;if(p.Age>=p.Life){p.Sprite.enabled=false;particles[i]=p;continue;}
                p.Velocity.y-=p.Gravity*dt;p.Position+=p.Velocity*dt;p.Rotation+=p.Spin*dt;
                float q=p.Age/p.Life;var c=p.Color;c.a*=Mathf.Pow(1-q,1.5f);p.Sprite.color=c;
                p.Sprite.transform.localPosition=new Vector3(p.Position.x,p.Position.y,0);p.Sprite.transform.localScale=new Vector3(p.Scale.x,p.Scale.y,1)*Mathf.Max(.05f,1+p.Growth*p.Age);
                float angle=p.Sprite.sprite==shapes.Streak?Mathf.Atan2(p.Velocity.y,p.Velocity.x)*Mathf.Rad2Deg:p.Rotation;
                p.Sprite.transform.localRotation=Quaternion.Euler(0,0,angle);particles[i]=p;
            }
        }
    }

    /// <summary>Two articulated support robots; this is never overlaid as Mirkos' armor.</summary>
    internal sealed class RobotView
    {
        readonly Transform root;readonly SpriteRenderer[] pieces;readonly ShapeBank shapes;
        public RobotView(Transform parent,ShapeBank shapes,int index)
        {
            this.shapes=shapes;root=new GameObject("Robot "+index).transform;root.SetParent(parent,false);pieces=new SpriteRenderer[24];
            for(int i=0;i<pieces.Length;i++)pieces[i]=ShapeBank.Renderer(root,"Robot part "+i,shapes.Plate,shapes.Solid,29);
            root.gameObject.SetActive(false);
        }
        public void Hide()=>root.gameObject.SetActive(false);
        void Plate(int n,Vector2 p,Vector2 scale,Color color,bool glow=false)
        {
            var r=pieces[n];r.transform.localPosition=p;r.transform.localScale=new Vector3(scale.x,scale.y,1);r.transform.localRotation=Quaternion.identity;r.color=color;r.sprite=glow?shapes.Disc:shapes.Plate;r.sharedMaterial=glow?shapes.Additive:shapes.Solid;
        }
        void Limb(int n,Vector2 a,Vector2 b,float width,Color color)
        {
            Plate(n,(a+b)*.5f,new Vector2((b-a).magnitude,width),color);pieces[n].transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
        }
        public void Render(Vector2 p,int face,float strike,float clock)
        {
            root.gameObject.SetActive(true);root.localPosition=p;root.localScale=Vector3.one;
            Color dark=new Color(.055f,.095f,.14f),steel=new Color(.34f,.49f,.57f),plate=new Color(.13f,.25f,.34f),cyan=new Color(.34f,.9f,1);
            for(int side=0;side<2;side++)
            {
                float s=side==0?-1:1;var hip=new Vector2(s*.12f,.61f);var knee=new Vector2(s*.20f,.30f);var foot=new Vector2(s*.27f,.05f);
                Limb(side*3,hip,knee,.19f,plate);Limb(side*3+1,knee,foot,.13f,steel);Plate(side*3+2,foot,new Vector2(.30f,.12f),dark);
            }
            Plate(6,new Vector2(0,.91f),new Vector2(.53f,.67f),dark);Plate(7,new Vector2(0,.94f),new Vector2(.46f,.56f),plate);
            Plate(8,new Vector2(0,.93f),Vector2.one*.15f,cyan,true);Plate(9,new Vector2(0,1.42f),new Vector2(.39f,.36f),dark);Plate(10,new Vector2(0,1.43f),new Vector2(.33f,.28f),steel);
            Plate(11,new Vector2(0,1.45f),new Vector2(.27f,.075f),cyan,true);
            for(int side=0;side<2;side++)
            {
                float s=side==0?-1:1;var shoulder=new Vector2(s*face*.29f,1.07f);
                var elbow=shoulder+new Vector2(face*(.21f+strike*.14f),-.2f);
                var fist=elbow+new Vector2(face*(.11f+strike*.33f),.08f+strike*.16f);if(side==0)fist=elbow+new Vector2(-face*.05f,-.17f);
                int n=12+side*5;Limb(n,shoulder,elbow,.20f,plate);Limb(n+1,elbow,fist,.15f,steel);Plate(n+2,shoulder,new Vector2(.30f,.29f),plate);Plate(n+3,fist,new Vector2(.25f,.21f),steel);Plate(n+4,elbow,Vector2.one*.075f,cyan,true);
            }
            Plate(22,new Vector2(0,1.63f),new Vector2(.035f,.17f),steel);Plate(23,new Vector2(0,1.72f),Vector2.one*.075f,cyan,true);
        }
    }
}
