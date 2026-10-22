using System.Collections.Generic;
using UFN.Core;
using UnityEngine;

namespace UFN.Presentation
{
    /// <summary>Original campus circuit in a readable overhead 3D presentation.</summary>
    public sealed partial class KartPresentation : MonoBehaviour
    {
        public Texture2D[] CharacterPortraits { get; set; }
        public bool HorizontalSplit { get; set; }
        public bool ShowHud { get; set; }=true;
        private KartRace race;
        private Transform world;
        private Camera view;
        private readonly List<Camera> views=new List<Camera>();
        private readonly List<GameObject> shots=new List<GameObject>();
        private KartModel[] models;
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<(Color,bool),Material> materialCache=new Dictionary<(Color,bool),Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> hazards = new List<GameObject>();
        private Transform[] vehicles, shields, flames;
        private GameObject[] boxes;
        private static void Release(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        private static readonly Color[] Colors = { new Color(.95f,.64f,.17f), new Color(.25f,.7f,1), new Color(.58f,.45f,.95f), new Color(.32f,.85f,.48f), new Color(.14f,.85f,.88f), new Color(.97f,.24f,.28f), new Color(.91f,.43f,.7f), new Color(.99f,.84f,.2f), new Color(.58f,.7f,.97f), new Color(.84f,.23f,.84f), new Color(.13f,.67f,.83f), new Color(.88f,.63f,.25f), new Color(.99f,.49f,.24f), new Color(.75f,.47f,.27f) };

        public void Bind(KartRace value)
        {
            if (world != null) { world.gameObject.SetActive(false); Release(world.gameObject); }
            foreach (Material material in materials) Release(material);
            foreach (Mesh mesh in meshes) Release(mesh);
            materials.Clear(); meshes.Clear(); materialCache.Clear();
            shots.Clear(); hazards.Clear(); views.Clear(); signatureRings=null;
            vehicles=null; shields=null; flames=null; models=null; boxes=null; view=null;
            race = value;
            if (race == null) return;
            world = new GameObject("Campus Circuit").transform;
            world.SetParent(transform, false);
            var cameraObject = new GameObject("Kart Camera");
            cameraObject.transform.SetParent(world, false);
            view = cameraObject.AddComponent<Camera>();
            view.tag = "MainCamera";
            view.orthographic = false;
            view.fieldOfView = 47;
            view.nearClipPlane = .1f;
            view.farClipPlane = 600;
            view.backgroundColor = new Color(.045f,.085f,.13f);
            view.clearFlags = CameraClearFlags.SolidColor;
            views.Clear();views.Add(view);
            if(race.HumanCount==2){var other=new GameObject("Kart Camera P2").AddComponent<Camera>();other.transform.SetParent(world,false);other.CopyFrom(view);views.Add(other);}
            for(int i=0;i<views.Count;i++){views[i].fieldOfView=60;views[i].rect=race.HumanCount==1?new Rect(0,0,1,1):HorizontalSplit?new Rect(0,i==0?.5f:0,1,.5f):new Rect(i*.5f,0,.5f,1);}
            var lightObject = new GameObject("Campus Sun");
            lightObject.transform.SetParent(world, false);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.color = new Color(1,.93f,.82f);
            lightObject.transform.rotation = Quaternion.Euler(58,-25,0);
            RenderSettings.ambientLight = new Color(.48f,.58f,.69f);
            BuildCampus();
            vehicles = new Transform[race.Racers.Length]; shields = new Transform[race.Racers.Length]; flames = new Transform[race.Racers.Length];models=new KartModel[race.Racers.Length];
            for (int i = 0; i < race.Racers.Length; i++) BuildKart(i);
            UpdateView();
            for(int i=0;i<views.Count;i++){views[i].transform.position=TargetCameraPosition(i);AimCamera(i);}
        }

        private Material Material(Color color, bool unlit = false)
        {
            if(materialCache.TryGetValue((color,unlit),out var cached))return cached;
            var mat = KartMaterials.Create(color,unlit);
            materials.Add(mat);materialCache[(color,unlit)]=mat; return mat;
        }
        private Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>(); if (collider != null) Release(collider);
            return obj.transform;
        }

        private void BuildCampus()
        {
            var grass = Material(new Color(.14f,.27f,.24f));
            var walkway = Material(new Color(.43f,.5f,.51f));
            var dark = Material(new Color(.095f,.135f,.18f));
            var cyan = Material(new Color(.07f,.75f,.85f), true);
            var white = Material(new Color(.85f,.9f,.89f));
            Primitive(PrimitiveType.Cube, "Campus terrain", world, new Vector3(0,-1.2f,10), new Vector3(340,2,320), grass);
            BuildRoad(18, -.02f, walkway, "Sidewalk");
            BuildRoad(race.Track.Width, .02f, dark, "Asphalt");
            for (int i = 0; i < race.Track.Points.Length; i++)
            {
                KartPoint center = race.Track.Points[i];
                KartPoint next = race.Track.Points[(i+1)%race.Track.Points.Length];
                KartPoint tangent = (next-center).Normalized;
                KartPoint normal = new KartPoint(-tangent.Y,tangent.X);
                for (int edge = -1; edge <= 1; edge += 2)
                {
                    KartPoint p = (center+next)*.5f+normal*(race.Track.Width*.5f*edge);
                    if(KartPoint.Distance(p,race.Track.Project(p).Position)<race.Track.Width*.5f-.25f)continue;
                    Transform curb = Primitive(PrimitiveType.Cube,"Curb",world,new Vector3(p.X,.13f,p.Y),new Vector3(.55f,.2f,(next-center).Length+.05f),i%4<2?white:cyan);
                    curb.rotation = Quaternion.Euler(0,90-Mathf.Atan2(tangent.Y,tangent.X)*Mathf.Rad2Deg,0);
                }
                if (i%4==0)
                {
                    Transform dash = Primitive(PrimitiveType.Cube,"Road dash",world,new Vector3(center.X,.045f,center.Y),new Vector3(.13f,.02f,1.4f),white);
                    dash.rotation = Quaternion.Euler(0,90-Mathf.Atan2(tangent.Y,tangent.X)*Mathf.Rad2Deg,0);
                }
            }
            var building = Material(new Color(.65f,.72f,.73f));
            var glass = Material(new Color(.1f,.31f,.42f));
            var roof = Material(new Color(.16f,.23f,.31f));
            BuildTheme(building,glass,roof);
            var treeTrunk = Material(new Color(.28f,.2f,.14f));
            var treeLeaves = Material(new Color(.17f,.42f,.32f));
            for (int i = 0; i < 34; i++)
            {
                float distance = race.Track.Length*i/34;
                KartTrackSample sample = race.Track.Sample(distance);
                KartPoint normal = new KartPoint(-sample.Tangent.Y,sample.Tangent.X);
                KartPoint point = sample.Position+normal*(i%2==0?17:-18);
                Primitive(PrimitiveType.Cylinder,"Tree trunk",world,new Vector3(point.X,1.6f,point.Y),new Vector3(.65f,1.6f,.65f),treeTrunk);
                Primitive(PrimitiveType.Sphere,"Tree canopy",world,new Vector3(point.X,4.4f,point.Y),new Vector3(5,6,5),treeLeaves);
            }
            KartTrackSample start = race.Track.Sample(0);
            var finish = new GameObject("Start and finish").transform; finish.SetParent(world,false); finish.position=ToWorld(start.Position,.05f); finish.rotation=Quaternion.Euler(0,90-Mathf.Atan2(start.Tangent.Y,start.Tangent.X)*Mathf.Rad2Deg,0);
            for(int row=0;row<2;row++) for(int col=0;col<10;col++) Primitive(PrimitiveType.Cube,"Checker",finish,new Vector3(-6.3f+col*1.4f,.02f,row*1.4f),new Vector3(1.4f,.035f,1.4f),(row+col)%2==0?white:dark);
            Primitive(PrimitiveType.Cube,"Start left pillar",finish,new Vector3(-8,4,0),new Vector3(.7f,8,.7f),cyan);
            Primitive(PrimitiveType.Cube,"Start right pillar",finish,new Vector3(8,4,0),new Vector3(.7f,8,.7f),cyan);
            Primitive(PrimitiveType.Cube,"Start gantry",finish,new Vector3(0,7.8f,0),new Vector3(16.7f,1,.6f),roof);
            boxes=new GameObject[race.Mode==KartRaceMode.TimeAttack?0:4];
            var boxMaterial=Material(new Color(1,.64f,.1f),true);
            for(int i=0;i<boxes.Length;i++) { KartTrackSample sample=race.Track.Sample(i*race.Track.Length/4); boxes[i]=Primitive(PrimitiveType.Cube,"Item zone",world,ToWorld(sample.Position,2),new Vector3(1.5f,1.5f,1.5f),boxMaterial).gameObject; }
            Sign("UFN • KART",finish,new Vector3(0,7.8f,-.36f),Quaternion.identity,.17f);
        }

        private void Building(string label, Vector3 position, Vector3 size, Material body, Material windows, Material roof)
        {
            // Decorative buildings must never cover the drivable strip or its shoulder.
            // The tighter Vila Belga turn can pass through an otherwise plausible block.
            foreach(KartPoint roadPoint in race.Track.Points)
            {
                float dx=Mathf.Max(0,Mathf.Abs(roadPoint.X-position.x)-size.x*.5f-.5f);
                float dz=Mathf.Max(0,Mathf.Abs(roadPoint.Y-position.z)-size.z*.5f-.5f);
                if(dx*dx+dz*dz<(race.Track.Width*.5f+3)*(race.Track.Width*.5f+3))return;
            }
            Primitive(PrimitiveType.Cube,label,world,position+Vector3.up*size.y*.5f,size,body);
            Primitive(PrimitiveType.Cube,label+" roof",world,position+Vector3.up*(size.y+.25f),new Vector3(size.x+1,.5f,size.z+1),roof);
            for(float y=2;y<size.y-1;y+=3) Primitive(PrimitiveType.Cube,"Window band",world,position+new Vector3(0,y,-size.z*.5f-.05f),new Vector3(size.x-2,1.4f,.1f),windows);
            if(!string.IsNullOrEmpty(label))Sign(label,world,position+new Vector3(0,size.y*.68f,-size.z*.5f-.11f),Quaternion.identity,Mathf.Min(.10f,size.x/(Mathf.Max(1,label.Length)*9)));
        }

        private void BuildRoad(float width,float height,Material material,string name)
        {
            int count=race.Track.Points.Length;
            var vertices=new Vector3[(count+1)*2]; var uv=new Vector2[vertices.Length]; var triangles=new int[count*6];
            for(int i=0;i<=count;i++)
            {
                int n=i%count; KartPoint p=race.Track.Points[n];
                KartPoint tangent=(race.Track.Points[(n+1)%count]-race.Track.Points[(n+count-1)%count]).Normalized;
                KartPoint normal=new KartPoint(-tangent.Y,tangent.X);
                vertices[i*2]=ToWorld(p-normal*width*.5f,height); vertices[i*2+1]=ToWorld(p+normal*width*.5f,height);
                uv[i*2]=new Vector2(0,i*.1f); uv[i*2+1]=new Vector2(1,i*.1f);
                if(i<count) { int t=i*6,v=i*2; triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2; }
            }
            var mesh=new Mesh { name=name,vertices=vertices,uv=uv,triangles=triangles };mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject(name);obj.transform.SetParent(world,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        private void BuildKart(int index)
        {
            var root=new GameObject("Racer "+index).transform;root.SetParent(world,false);vehicles[index]=root;
            models[index]=KartModel.Build(root,race.Racers[index].CharacterId,race.Racers[index].Skin);
            shields[index]=Primitive(PrimitiveType.Cylinder,"Shield ring",root,new Vector3(0,.12f,0),new Vector3(3.1f,.06f,3.1f),Material(new Color(.12f,.75f,.95f),true));
            flames[index]=Primitive(PrimitiveType.Sphere,"Turbo exhaust",root,new Vector3(0,.6f,-1.9f),new Vector3(.7f,.7f,1.5f),Material(new Color(1,.43f,.09f),true));
        }

        private void LateUpdate() { UpdateView(); }
        public void UpdateView()
        {
            if(race==null || vehicles==null || view==null)return;
            for(int i=0;i<vehicles.Length;i++)
            {
                KartRacer racer=race.Racers[i];vehicles[i].position=ToWorld(racer.Position,.05f+Mathf.Sin(racer.JumpTicks/60f*Mathf.PI)*1.1f);vehicles[i].rotation=Quaternion.Euler(0,90-racer.Heading*Mathf.Rad2Deg,0);
                shields[i].gameObject.SetActive(racer.ShieldTicks>0);flames[i].gameObject.SetActive(racer.BoostTicks>0);
                models[i].Animate(racer.Steering,racer.Speed,racer.BoostTicks>0,racer.StunTicks>0,racer.JumpTicks,Mathf.Max(.001f,Time.unscaledDeltaTime));
            }
            for(int i=0;i<boxes.Length;i++) { boxes[i].transform.rotation=Quaternion.Euler(18,race.ElapsedTicks*1.5f,18); }
            UpdatePowerVisuals();
            for(int i=0;i<views.Count;i++){views[i].transform.position=Vector3.Lerp(views[i].transform.position,TargetCameraPosition(i),1-Mathf.Exp(-Time.unscaledDeltaTime*5));AimCamera(i);}
        }
        private Vector3 TargetCameraPosition(int player) {var r=race.Racers[player];return ToWorld(r.Position,7.5f)-new Vector3(Mathf.Cos(r.Heading),0,Mathf.Sin(r.Heading))*11.8f;}
        private void AimCamera(int player) {var r=race.Racers[player];views[player].transform.LookAt(ToWorld(r.Position,1.1f)+new Vector3(Mathf.Cos(r.Heading),0,Mathf.Sin(r.Heading))*5);}
        private static Vector3 ToWorld(KartPoint point,float height) => new Vector3(point.X,height,point.Y);

        private void OnDestroy() { foreach(Material material in materials)if(material!=null)Release(material);foreach(Mesh mesh in meshes)if(mesh!=null)Release(mesh); }
    }
}
