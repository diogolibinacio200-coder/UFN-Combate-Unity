using System.Collections.Generic;
using UFN.Core;
using UnityEngine;

namespace UFN.Presentation
{
    /// <summary>Seated articulated pilots authored from the 15 supplied turnarounds.</summary>
    public sealed class KartModel : MonoBehaviour
    {
        public int CharacterId { get; private set; }
        public int Skin { get; private set; }
        public string Reference { get; private set; }
        public Transform UpperBody { get; private set; }
        public Transform Head { get; private set; }
        public Transform SteeringWheel { get; private set; }
        public float SeatHeight=>.76f;
        private KartAppearance look;
        private readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private readonly List<Mesh> ownedMeshes=new List<Mesh>();
        private readonly List<Transform> arms=new List<Transform>(),forearms=new List<Transform>(),hands=new List<Transform>(),elbows=new List<Transform>();
        private float previousSpeed;
        private static void Release(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}

        public static KartModel Build(Transform parent,int character,int skin=0)
        {
            var root=new GameObject(KartContent.Names[character]+(character==3&&skin>0?" · CARECA":""));root.transform.SetParent(parent,false);
            var model=root.AddComponent<KartModel>();model.CharacterId=character;model.Skin=skin;model.look=KartAppearance.Get(character,skin);model.Reference=model.look.Reference;model.BuildChassis();model.BuildDriver();model.Animate(0,0,false,false,0,1);return model;
        }
        private Material Mat(string hex)
        {
            if(materials.TryGetValue(hex,out var mat))return mat;
            ColorUtility.TryParseHtmlString("#"+hex,out var color);mat=KartMaterials.Create(color);mat.SetFloat("_Metallic",.05f);materials.Add(hex,mat);return mat;
        }
        private Transform Group(string name,Transform parent,Vector3 position)
        {var node=new GameObject(name).transform;node.SetParent(parent,false);node.localPosition=position;return node;}
        private Transform Shape(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 size,string color)
        {var node=GameObject.CreatePrimitive(type).transform;node.name=name;node.SetParent(parent,false);node.localPosition=position;node.localScale=size;node.GetComponent<Renderer>().sharedMaterial=Mat(color);Release(node.GetComponent<Collider>());return node;}
        private Transform Ball(Transform p,Vector3 at,Vector3 size,string color,string name="Detail")=>Shape(PrimitiveType.Sphere,name,p,at,size,color);
        private Transform Box(Transform p,Vector3 at,Vector3 size,string color,string name="Detail")=>Shape(PrimitiveType.Cube,name,p,at,size,color);
        private Transform Link(Transform p,Vector3 a,Vector3 b,float radius,string color,string name="Joint")
        {Transform node=Shape(PrimitiveType.Cylinder,name,p,Vector3.zero,Vector3.one,color);PoseLink(node,a,b,radius);return node;}
        private static void PoseLink(Transform node,Vector3 a,Vector3 b,float radius)
        {Vector3 d=b-a;node.localPosition=(a+b)*.5f;node.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized);node.localScale=new Vector3(radius*2,d.magnitude*.5f,radius*2);}

        private void BuildChassis()
        {
            Transform chassis=Group("Open cockpit chassis",transform,Vector3.zero);
            Box(chassis,new Vector3(0,.27f,0),new Vector3(1.92f,.22f,2.75f),"171d25","Floor pan");
            Box(chassis,new Vector3(0,.69f,-.39f),new Vector3(.84f,.22f,.9f),"151b23","Seat cushion");
            var seat=Box(chassis,new Vector3(0,1.04f,-.76f),new Vector3(.89f,.84f,.16f),"232932","Seat back");seat.localRotation=Quaternion.Euler(-10,0,0);
            for(int side=-1;side<=1;side+=2)
            {
                Box(chassis,new Vector3(side*.96f,.48f,-.1f),new Vector3(.32f,.52f,1.76f),look.Kart,"Side fairing");
                Box(chassis,new Vector3(side*.975f,.63f,-.08f),new Vector3(.34f,.055f,1.58f),look.Accent,"Body stripe");
                Box(chassis,new Vector3(side*.67f,.47f,1.39f),new Vector3(1.1f,.31f,.35f),look.Kart,"Front bumper");
                Box(chassis,new Vector3(side*.67f,.57f,1.58f),new Vector3(.93f,.07f,.035f),look.Accent,"Front stripe");
                for(int axle=-1;axle<=1;axle+=2)
                {
                    var tire=Shape(PrimitiveType.Cylinder,"Tire",chassis,new Vector3(side*1.18f,.46f,axle*.93f),new Vector3(.87f,.27f,.87f),"14181e");tire.localRotation=Quaternion.Euler(0,0,90);
                    var rim=Shape(PrimitiveType.Cylinder,"Colored rim",chassis,new Vector3(side*1.46f,.46f,axle*.93f),new Vector3(.6f,.017f,.6f),look.Rim);rim.localRotation=Quaternion.Euler(0,0,90);
                    var hub=Shape(PrimitiveType.Cylinder,"Hub",chassis,new Vector3(side*1.48f,.46f,axle*.93f),new Vector3(.39f,.019f,.39f),"212730");hub.localRotation=Quaternion.Euler(0,0,90);
                    for(int spoke=0;spoke<5;spoke++){float a=spoke*Mathf.PI*2/5;Link(chassis,new Vector3(side*1.50f,.46f,axle*.93f),new Vector3(side*1.50f,.46f+Mathf.Cos(a)*.22f,axle*.93f+Mathf.Sin(a)*.22f),.022f,look.Rim,"Rim spoke");}
                }
                Link(chassis,new Vector3(side*.75f,.68f,-.85f),new Vector3(side*.96f,1.28f,-1.3f),.12f,"56616d","Exhaust");
                Link(chassis,new Vector3(side*.94f,1.2f,-1.24f),new Vector3(side*1.0f,1.38f,-1.4f),.15f,look.Accent,"Exhaust cap");
                Ball(chassis,new Vector3(side*1.0f,1.4f,-1.42f),new Vector3(.22f,.16f,.15f),"11171d","Exhaust outlet");
            }
            var nose=Box(chassis,new Vector3(0,.69f,.87f),new Vector3(.67f,.21f,1.3f),look.Kart,"Sloping nose");nose.localRotation=Quaternion.Euler(24,0,0);
            for(int side=-1;side<=1;side+=2){var edge=Box(chassis,new Vector3(side*.33f,.76f,.84f),new Vector3(.037f,.03f,1.22f),look.Accent);edge.localRotation=Quaternion.Euler(24,0,0);}
            Crown(chassis,new Vector3(0,.87f,.75f),look.Accent);
            Box(chassis,new Vector3(0,.75f,-1.13f),new Vector3(.95f,.42f,.4f),"313943","Engine block");
            for(int n=0;n<5;n++)Box(chassis,new Vector3(0,.94f,-1.24f+n*.05f),new Vector3(.87f,.04f,.024f),"99a5ad","Engine fins");
            if(look.Detail=="splatter")for(int n=0;n<12;n++)Box(chassis,new Vector3(Mathf.Sin(n*4.3f)*.23f,.84f,.39f+n%4*.1f),new Vector3(.045f,.014f,.07f),"202328","Diogo paint marks");
            Batch(chassis);
        }
        private void Crown(Transform p,Vector3 center,string color)
        {
            // Actual geometry, so the emblem remains crisp from any camera angle.
            Vector3[] v={new Vector3(-.19f,0,-.12f),new Vector3(-.21f,0,.13f),new Vector3(-.08f,0,.015f),new Vector3(0,0,.18f),new Vector3(.08f,0,.015f),new Vector3(.21f,0,.13f),new Vector3(.19f,0,-.12f)};
            int[] t={0,1,2,0,2,6,2,4,6,2,3,4,4,5,6};var mesh=new Mesh{vertices=v,triangles=t};mesh.RecalculateNormals();ownedMeshes.Add(mesh);
            var crown=Group("Crown badge",p,center);crown.localRotation=Quaternion.Euler(24,0,0);crown.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;crown.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mat(color);
        }
        private void BuildDriver()
        {
            var legs=Group("Seated legs",transform,Vector3.zero);
            Ball(legs,new Vector3(0,.8f,-.4f),new Vector3(.64f,.27f,.56f),look.Pants,"Hips in seat");
            bool bare=look.Style=="tee"||look.Style=="shorts_tee";
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 hip=new Vector3(side*.19f,.83f,-.36f),knee=new Vector3(side*.27f,.84f,.19f),ankle=new Vector3(side*.27f,.57f,.67f);
                Link(legs,hip,knee,.145f,look.Pants,"Thigh");Link(legs,knee,ankle,.115f,look.Style=="shorts_tee"?look.Skin:look.Pants,"Shin");
                Ball(legs,knee,new Vector3(.29f,.27f,.27f),look.Style=="shorts_tee"?look.Skin:look.Pants,"Knee");
                Ball(legs,new Vector3(side*.27f,.52f,.76f),new Vector3(.28f,.17f,.44f),look.Shoes,"Shoe on pedal");
                Box(legs,new Vector3(side*.27f,.45f,.77f),new Vector3(.27f,.035f,.43f),"e5e6e0","Sole");
                for(int n=0;n<3;n++)Box(legs,new Vector3(side*.27f,.6f,.68f+n*.06f),new Vector3(.15f,.013f,.018f),"e5e6e0","Laces");
                if(look.Style=="tracksuit")for(int stripe=-1;stripe<=1;stripe+=2){Vector3 offset=new Vector3(side*.145f,0,stripe*.03f);Link(legs,hip+offset,knee+offset,.012f,"eeeee8","Tracksuit stripe");Link(legs,knee+offset,ankle+offset,.012f,"eeeee8","Tracksuit stripe");}
            }
            Batch(legs);
            UpperBody=Group("Upper body",transform,new Vector3(0,.83f,-.4f));
            Ball(UpperBody,new Vector3(0,.35f,.025f),new Vector3(.64f*look.Width,.8f,.46f),look.Shirt,"Torso");
            Link(UpperBody,new Vector3(0,.68f,0),new Vector3(0,.85f,0),.115f,look.Skin,"Neck");
            if(look.Style.Contains("hoodie")||look.Style=="puffer")
            {
                Ball(UpperBody,new Vector3(0,.63f,-.15f),new Vector3(.5f,.34f,.31f),look.Shirt,"Hood");
                for(int side=-1;side<=1;side+=2)Link(UpperBody,new Vector3(side*.12f,.67f,.13f),new Vector3(side*.16f,.4f,.25f),.013f,"b9bdc2","Hood cord");
            }
            if(look.Style=="puffer")for(int n=0;n<5;n++)Ball(UpperBody,new Vector3(0,.13f+n*.12f,.025f),new Vector3(.7f*look.Width,.11f,.52f),look.Shirt,"Padded jacket panel");
            if(look.Style=="open_hoodie"){Box(UpperBody,new Vector3(0,.46f,.25f),new Vector3(.15f,.37f,.025f),look.Skin,"Open jacket");}
            if(look.Style=="blazer")
            {
                Box(UpperBody,new Vector3(0,.42f,.245f),new Vector3(.19f,.4f,.035f),"172639","Inner shirt");
                for(int side=-1;side<=1;side+=2){var lapel=Box(UpperBody,new Vector3(side*.12f,.5f,.25f),new Vector3(.085f,.3f,.035f),"354b73","Lapel");lapel.localRotation=Quaternion.Euler(0,0,side*22);Link(UpperBody,new Vector3(side*.07f,.67f,.19f),new Vector3(side*.04f,.33f,.29f),.017f,"217abb","UFN lanyard");}
                Box(UpperBody,new Vector3(0,.28f,.30f),new Vector3(.13f,.16f,.024f),"21609c","ID card");Box(UpperBody,new Vector3(0,.3f,.316f),new Vector3(.09f,.035f,.006f),"e0e5e4");
            }
            if(look.Detail=="straps")for(int side=-1;side<=1;side+=2)Link(UpperBody,new Vector3(side*.2f,.65f,.15f),new Vector3(side*.23f,.17f,.25f),.035f,"3d444a","Backpack strap");
            if(look.Detail=="splatter")for(int n=0;n<22;n++)Box(UpperBody,new Vector3(Mathf.Sin(n*8.17f)*.23f,.09f+(n%5)*.057f,.255f),new Vector3(.035f+n%3*.01f,.06f,.013f),"222326","Shirt print");
            if(look.Detail=="collar")for(int side=-1;side<=1;side+=2){var collar=Box(UpperBody,new Vector3(side*.075f,.68f,.18f),new Vector3(.12f,.13f,.028f),"c1d0d6","Shirt collar");collar.localRotation=Quaternion.Euler(0,0,side*25);}
            if(look.Detail=="chain"||look.Detail=="tattoos")for(int n=0;n<12;n++){float t=n/11f;Ball(UpperBody,new Vector3((t-.5f)*.29f,.68f-Mathf.Sin(t*Mathf.PI)*.13f,.2f+Mathf.Sin(t*Mathf.PI)*.055f),Vector3.one*.031f,"c9c9b9","Chain");}
            if(look.Detail=="zip")Link(UpperBody,new Vector3(0,.13f,.255f),new Vector3(0,.65f,.23f),.012f,"b1b6b9","Zipper");
            if(look.Detail=="chest_mark")Box(UpperBody,new Vector3(0,.52f,.269f),new Vector3(.23f,.022f,.007f),"bdbfb7","Chest lettering");
            if(look.Style=="tracksuit"){Box(UpperBody,new Vector3(0,.48f,.26f),new Vector3(.18f,.33f,.02f),look.Inner);for(int side=-1;side<=1;side+=2)for(int stripe=0;stripe<2;stripe++)Link(UpperBody,new Vector3(side*(.13f+stripe*.045f),.67f,.1f),new Vector3(side*(.28f+stripe*.04f),.54f,.07f),.012f,"eeeeea","Shoulder stripe");}
            Batch(UpperBody);
            Head=Group("Head",UpperBody,new Vector3(0,.94f,.01f));BuildHead();Batch(Head);
            SteeringWheel=Group("Steering wheel",transform,new Vector3(0,1.12f,.49f));SteeringWheel.localRotation=Quaternion.Euler(57,0,0);
            for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16,b=(i+1)*Mathf.PI*2/16;Link(SteeringWheel,new Vector3(Mathf.Cos(a)*.32f,Mathf.Sin(a)*.23f,0),new Vector3(Mathf.Cos(b)*.32f,Mathf.Sin(b)*.23f,0),.035f,"161d24","Wheel rim");}
            for(int side=-1;side<=1;side+=2)Link(SteeringWheel,Vector3.zero,new Vector3(side*.28f,0,0),.03f,"52606a","Wheel spoke");Batch(SteeringWheel);
            for(int side=-1;side<=1;side+=2)
            {
                arms.Add(Link(transform,Vector3.zero,Vector3.up,.125f*look.Width,look.Shirt,"Upper arm"));
                var forearm=Link(transform,Vector3.zero,Vector3.up,.09f,bare?look.Skin:look.Shirt,"Forearm");forearms.Add(forearm);
                hands.Add(Ball(transform,Vector3.zero,new Vector3(.16f,.14f,.18f),look.Skin,"Hand gripping wheel"));elbows.Add(Ball(transform,Vector3.zero,Vector3.one*.2f,bare?look.Skin:look.Shirt,"Elbow"));
                if(look.Detail=="tattoos")for(int n=0;n<6;n++){var mark=Box(forearm,new Vector3(side*.91f,-.7f+n*.28f,.13f),new Vector3(.1f,.14f,.7f),"514138","Arm tattoo");mark.localRotation=Quaternion.Euler(0,n*29,0);}
                if(look.Style=="tracksuit")for(int n=-1;n<=1;n+=2)Box(forearm,new Vector3(side*.87f,0,n*.25f),new Vector3(.12f,1.8f,.12f),"eeeeea","Sleeve stripe");
            }
        }
        private void BuildHead()
        {
            Ball(Head,Vector3.zero,new Vector3(.45f,.53f,.43f),look.Skin,"Face");
            for(int side=-1;side<=1;side+=2)
            {
                Ball(Head,new Vector3(side*.224f,0,0),new Vector3(.055f,.1f,.08f),look.Skin,"Ear");
                Box(Head,new Vector3(side*.087f,.043f,.209f),new Vector3(.07f,.03f,.017f),"ede4d9","Eye");Box(Head,new Vector3(side*.09f,.043f,.22f),new Vector3(.028f,.025f,.01f),"20232a","Pupil");
                Box(Head,new Vector3(side*.09f,.09f,.202f),new Vector3(.086f,.025f,.021f),look.Hair,"Eyebrow");
            }
            Ball(Head,new Vector3(0,-.005f,.227f),new Vector3(.067f,.095f,.083f),look.Skin,"Nose");Box(Head,new Vector3(0,-.085f,.202f),new Vector3(.09f,.014f,.02f),"8b5945","Mouth");
            if(look.Beard!="none")
            {
                string beard=look.Beard=="gray"?"8c9094":look.Hair;
                Ball(Head,new Vector3(0,-.132f,.09f),new Vector3(.34f,.18f,.24f),beard,"Beard");
                if(look.Beard!="goatee")for(int side=-1;side<=1;side+=2)Link(Head,new Vector3(side*.18f,.01f,.08f),new Vector3(side*.115f,-.15f,.17f),.045f,beard,"Sideburn");
                Box(Head,new Vector3(0,-.048f,.22f),new Vector3(.15f,.027f,.018f),beard,"Moustache");
            }
            if(look.Glasses!="none")for(int side=-1;side<=1;side+=2)
            {
                Vector3 center=new Vector3(side*.104f,.049f,.239f);
                for(int edge=-1;edge<=1;edge+=2){Box(Head,center+new Vector3(0,edge*.047f,0),new Vector3(.172f,.016f,.023f),"131a21","Glasses frame");Box(Head,center+new Vector3(edge*.079f,0,0),new Vector3(.016f,.095f,.023f),"131a21","Glasses frame");}
                if(look.Glasses=="dark")Box(Head,center,new Vector3(.14f,.074f,.018f),"17212b","Dark lens");Link(Head,center+new Vector3(side*.084f,0,0),new Vector3(side*.24f,.04f,0),.012f,"131a21","Glasses arm");
            }
            if(look.Glasses!="none")Box(Head,new Vector3(0,.059f,.239f),new Vector3(.047f,.018f,.024f),"131a21","Glasses bridge");
            if(look.HairStyle=="bald")return;
            Ball(Head,new Vector3(0,.18f,-.03f),new Vector3(.49f,look.HairStyle=="buzz"?.16f:.23f,.46f),look.Hair,"Hair cap");
            if(look.HairStyle=="long")
            {
                // Overlapping connected locks run behind the neck to the shoulders.
                for(int n=0;n<11;n++){float a=n*Mathf.PI/10;Vector3 top=new Vector3(Mathf.Cos(a)*.23f,.16f,-Mathf.Sin(a)*.17f-.025f);Vector3 bottom=new Vector3(Mathf.Cos(a)*.34f,-.55f,-Mathf.Sin(a)*.21f-.085f);Link(Head,top,bottom,.084f,look.Hair,"Long hair");}
            }
            else if(look.HairStyle!="buzz")
            {
                for(int n=0;n<(look.HairStyle=="curls"?20:13);n++){float angle=n*2.39996f,radius=.17f*Mathf.Sqrt((n+1)/20f);Vector3 pos=new Vector3(Mathf.Cos(angle)*radius,.22f+(n%2)*.025f,Mathf.Sin(angle)*radius+.025f);if(look.HairStyle=="receding"&&pos.z>.025f)continue;var hair=Ball(Head,pos,new Vector3(.14f,look.HairStyle=="spiky"?.20f:.13f,.14f),look.Hair,"Hair lock");if(look.HairStyle=="swept")hair.localRotation=Quaternion.Euler(0,0,32);}
                for(int side=-1;side<=1;side+=2)Ball(Head,new Vector3(side*.205f,.105f,-.046f),new Vector3(.082f,.2f,.23f),look.Hair,"Side hair");
            }
        }
        public void Animate(float steer,float speed,bool boost,bool stunned,int jumpTicks,float dt)
        {
            float acceleration=(speed-previousSpeed)/Mathf.Max(.001f,dt);previousSpeed=speed;
            float lean=-steer*.08f*Mathf.Clamp01(speed/20);UpperBody.localRotation=Quaternion.Euler(Mathf.Clamp(acceleration,-25,25)*-.07f+(boost?-4:0),0,lean*Mathf.Rad2Deg);
            Head.localRotation=Quaternion.Euler(0,steer*8,-lean*15);SteeringWheel.localRotation=Quaternion.Euler(57,0,-steer*24);
            for(int i=0;i<2;i++){float side=i==0?-1:1;Vector3 shoulder=transform.InverseTransformPoint(UpperBody.TransformPoint(new Vector3(side*.28f*look.Width,.56f,.06f)));Vector3 elbow=transform.InverseTransformPoint(UpperBody.TransformPoint(new Vector3(side*.38f*look.Width,.3f,.26f)));Vector3 hand=transform.InverseTransformPoint(SteeringWheel.TransformPoint(new Vector3(side*.27f,0,0)));PoseLink(arms[i],shoulder,elbow,.125f*look.Width);PoseLink(forearms[i],elbow,hand,.09f);hands[i].localPosition=hand;elbows[i].localPosition=elbow;}
        }
        private void Batch(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var sources=new List<GameObject>();
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>()){var mr=mf.GetComponent<MeshRenderer>();if(mr==null||mf.sharedMesh==null)continue;Material mat=mr.sharedMaterial;if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();groups[mat].Add(new CombineInstance{mesh=mf.sharedMesh,transform=root.worldToLocalMatrix*mf.transform.localToWorldMatrix});sources.Add(mf.gameObject);}
            foreach(var pair in groups){var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true);ownedMeshes.Add(mesh);var node=Group("Batched details",root,Vector3.zero);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;}
            foreach(var source in sources){source.SetActive(false);Release(source);}
        }
        private void OnDestroy(){foreach(var mat in materials.Values)Release(mat);foreach(var mesh in ownedMeshes)Release(mesh);}
    }
}
