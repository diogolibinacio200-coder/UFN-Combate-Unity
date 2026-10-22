using System;
using System.IO;
using UFN.Core;
using UnityEngine;

namespace UFN.Presentation
{
    /// <summary>
    /// Unity-native presentation of the 60 Hz core. This component never writes to
    /// fighters, collision boxes, combat timers or random state. Call
    /// CaptureSimulationStep immediately after every CombatWorld.Step.
    /// </summary>
    public sealed class ArenaPresentation : MonoBehaviour
    {
        public float CameraShake=0.6f;
        public float ParticleIntensity=0.7f;
        public float FlashIntensity=0.5f;
        public bool ShowHitboxes;
        public Camera Camera {get;private set;}
        public int ActiveEffectCount=>effects==null?0:effects.ActiveCount;
        public int EffectCapacity=>112;
        public int LoadedAtlasCount=>library==null?0:library.LoadedAtlasCount;
        public string LastPresentedSkin(int player)=>views[Mathf.Clamp(player,0,1)].Skin;
        public string LastPresentedFrame(int player)=>views[Mathf.Clamp(player,0,1)].Body.sprite?.name??"";
        public float BodyOpacity(int player)=>views[Mathf.Clamp(player,0,1)].Body.color.a;
        static readonly string[] Stages={"patio","container","rua","entrada","quadra","laboratorio","corredor","vilabelga"};
        static readonly int[] VitorStrikes={1,25,31,37,43,49,55,61,67,73,79,85,91,97,111};
        static readonly int[] RobotStrikes={10,27,44,61,78,92};
        static readonly int FlashId=Shader.PropertyToID("_Flash"),RimId=Shader.PropertyToID("_Rim"),RimColorId=Shader.PropertyToID("_RimColor");
        const int ProjectileCapacity=48;
        sealed class FighterView
        {
            public SpriteRenderer Body,Shadow,Contact,Aura,Bear,Ally;
            public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
            public Vector2 Previous,Current,Display;
            public float Gait,Landing;
            public int Identity=-1,SkinIndex=-1;
            public string Skin="";
        }
        sealed class ProjectileView
        {
            public SpriteRenderer Body,Glow;
            public TrailRenderer Trail;
            public int Id=-1;
        }
        GameCatalog catalog;
        CombatWorld world;
        AtlasLibrary library;
        ShapeBank shapes;
        ImpactPool effects;
        FighterView[] views=new FighterView[2];
        ProjectileView[] projectiles=new ProjectileView[ProjectileCapacity];
        RobotView[] robots=new RobotView[4];
        SpriteRenderer stage,vignette,topShade,cinemaShade;
        LineRenderer[] debugBoxes=new LineRenderer[5];
        readonly Vector3[] debugPoints=new Vector3[5];
        Vector3 cameraVelocity,cameraBase;
        float cameraSizeVelocity,clock,shake,cinemaWeight,ambientTimer,lastDebugAge;
        HitRect lastDebugBox;
        int lastTick=-1,stageId;
        bool initialized;
        public static Vector2 WorldPoint(float x,float y)=>new Vector2((x-640f)*.01f,(590f-y)*.01f);
        public void Initialize(GameCatalog gameCatalog,string contentPath)
        {
            if(initialized)throw new InvalidOperationException("ArenaPresentation.Initialize called twice");
            catalog=gameCatalog??throw new ArgumentNullException(nameof(gameCatalog));library=new AtlasLibrary(contentPath);shapes=new ShapeBank();
            var cameraObject=new GameObject("Arena camera");cameraObject.transform.SetParent(transform,false);Camera=cameraObject.AddComponent<Camera>();
            Camera.tag="MainCamera";Camera.orthographic=true;Camera.orthographicSize=3.6f;Camera.nearClipPlane=.01f;Camera.farClipPlane=100;
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.012f,.025f,.046f);Camera.transparencySortMode=TransparencySortMode.Orthographic;Camera.allowHDR=false;Camera.allowMSAA=false;
            Camera.transform.localPosition=cameraBase=new Vector3(0,2.3f,-10);
            stage=ShapeBank.Renderer(transform,"Original arena",null,shapes.Solid,-100);
            topShade=ShapeBank.Renderer(transform,"Stage atmosphere",shapes.Glow,shapes.Solid,-80);topShade.color=new Color(.015f,.055f,.11f,.27f);topShade.transform.localPosition=new Vector3(0,5.6f);topShade.transform.localScale=new Vector3(20,7,1);
            cinemaShade=ShapeBank.Renderer(transform,"Cinematic stage dim",shapes.Plate,shapes.Solid,-50);cinemaShade.transform.localPosition=new Vector3(0,2.3f);cinemaShade.transform.localScale=new Vector3(18,12,1);cinemaShade.color=Color.clear;
            vignette=ShapeBank.Renderer(transform,"Lens edge shading",shapes.Vignette,shapes.Solid,80);
            for(int p=0;p<2;p++)
            {
                var v=new FighterView();views[p]=v;
                v.Body=ShapeBank.Renderer(transform,"Fighter "+p,null,shapes.Fighter,20+p);
                v.Shadow=ShapeBank.Renderer(transform,"Soft ground shadow "+p,shapes.Shadow,shapes.Solid,4);
                v.Contact=ShapeBank.Renderer(transform,"Foot contact shadow "+p,shapes.Shadow,shapes.Solid,5);
                v.Aura=ShapeBank.Renderer(transform,"Character aura "+p,shapes.Glow,shapes.Additive,12);
                v.Bear=ShapeBank.Renderer(transform,"Fernando bear "+p,null,shapes.Fighter,22);
                v.Ally=ShapeBank.Renderer(transform,"Vitor summon "+p,null,shapes.Fighter,23);
            }
            var poolRoot=new GameObject("Bounded effect pool").transform;poolRoot.SetParent(transform,false);effects=new ImpactPool(poolRoot,shapes,EffectCapacity);
            for(int i=0;i<ProjectileCapacity;i++)
            {
                var p=new ProjectileView();projectiles[i]=p;
                p.Body=ShapeBank.Renderer(transform,"Projectile "+i,shapes.Disc,shapes.Solid,31);p.Body.enabled=false;
                p.Glow=ShapeBank.Renderer(transform,"Projectile glow "+i,shapes.Glow,shapes.Additive,30);p.Glow.enabled=false;
                p.Trail=p.Body.gameObject.AddComponent<TrailRenderer>();p.Trail.sharedMaterial=shapes.Additive;p.Trail.time=.13f;p.Trail.minVertexDistance=.06f;p.Trail.widthMultiplier=.14f;p.Trail.numCapVertices=2;p.Trail.sortingOrder=29;p.Trail.emitting=false;p.Trail.enabled=false;
            }
            for(int i=0;i<robots.Length;i++)robots[i]=new RobotView(transform,shapes,i);
            for(int i=0;i<debugBoxes.Length;i++)
            {
                var go=new GameObject("Debug collision "+i);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();debugBoxes[i]=line;
                line.sharedMaterial=shapes.Solid;line.useWorldSpace=false;line.positionCount=5;line.widthMultiplier=.018f;line.sortingOrder=70;line.enabled=false;
            }
            initialized=true;SetStage(0);
        }
        public void SetWorld(CombatWorld combatWorld,int stageIndex=0)
        {
            if(!initialized)throw new InvalidOperationException("Initialize presentation before SetWorld");
            world=combatWorld??throw new ArgumentNullException(nameof(combatWorld));clock=0;lastTick=world.Tick;shake=cinemaWeight=0;effects.Reset();
            SetStage(stageIndex);
            for(int p=0;p<2;p++)
            {
                var f=world.Fighters[p];var v=views[p];v.Previous=v.Current=WorldPoint(f.X,f.Y);v.Gait=v.Landing=0;v.Identity=f.Id;v.SkinIndex=f.Skin;
                library.Get(SkinFor(f));if(f.Id==4)library.Get("mirkos_armor");
            }
            foreach(var p in projectiles){p.Id=-1;p.Body.enabled=p.Glow.enabled=p.Trail.enabled=p.Trail.emitting=false;p.Trail.Clear();}
            cameraBase=new Vector3(0,2.3f,-10);cameraVelocity=Vector3.zero;Camera.orthographicSize=3.6f;
            Render(1,0);
        }
        public void SetStage(int index)
        {
            stageId=((index%Stages.Length)+Stages.Length)%Stages.Length;stage.sprite=library.Stage(Stages[stageId]);stage.transform.localPosition=new Vector3(0,2.3f,0);
            // A small overscan prevents camera impact shake exposing texture edges.
            float factor=12.8f/stage.sprite.bounds.size.x*1.035f;stage.transform.localScale=Vector3.one*factor;
        }
        public string SkinFor(FighterState f)
        {
            if(f.Id==4&&f.TransformFrames>0)return "mirkos_armor";
            if(f.Id==3&&f.Skin==1)return "diogo_alt";
            return catalog.Character(f.Id).Key;
        }
        public void CaptureSimulationStep()
        {
            if(world==null)return;
            bool frozen=world.Hitstop>0||(world.Cinematic.Active&&world.Cinematic.ImpactPause>0);
            if(!frozen)clock+=1/60f;
            for(int p=0;p<2;p++)
            {
                var f=world.Fighters[p];var v=views[p];v.Previous=v.Current;v.Current=WorldPoint(f.X,f.Y);
                if(v.Identity!=f.Id||v.SkinIndex!=f.Skin){v.Identity=f.Id;v.SkinIndex=f.Skin;v.Previous=v.Current;v.Gait=0;}
                if(v.Previous.y>.05f&&v.Current.y<.02f)v.Landing=.15f;
                if(!frozen){v.Landing=Mathf.Max(0,v.Landing-1/60f);v.Gait+=Mathf.Abs(v.Current.x-v.Previous.x)/(f.State=="walk"?1.26f:1.68f);}
            }
            foreach(var e in world.Events)Consume(e);
            lastTick=world.Tick;
        }
        void Consume(CombatEvent e)
        {
            Color color=CharacterColor(world.Fighters[Mathf.Clamp(e.Player,0,1)].Id);Vector2 p=WorldPoint(e.X,e.Y);
            bool heavy=e.Final||e.Type=="ko"||e.Type=="guard_break";
            if(e.Type=="hit"||e.Type=="counter"||e.Type=="super_hit"||e.Type=="guard_break"||e.Type=="ko")
            {
                effects.Hit(p,color,heavy,ParticleIntensity,FlashIntensity);shake=Mathf.Max(shake,heavy?.20f:.065f);
                lastDebugBox=e.Hitbox;lastDebugAge=.16f;
            }
            else if(e.Type=="block"||e.Type=="parry"||e.Type=="perfect_block")
            {
                effects.Hit(p,new Color(.53f,.86f,1),false,ParticleIntensity*.65f);shake=Mathf.Max(shake,.027f);
            }
            else if(e.Type=="land"||e.Type=="jump"||e.Type=="dodge")effects.Dust(WorldPoint(world.Fighters[e.Player].X,590),1,ParticleIntensity);
            else if(e.Type=="super"||e.Type=="buff")
            {
                var f=world.Fighters[e.Player];Vector2 center=WorldPoint(f.X,f.Y-120);
                effects.Emit(shapes.Ring,center,Vector2.zero,Vector2.one*.3f,color,.6f,6);
                effects.Emit(shapes.Glow,center,Vector2.zero,new Vector2(2.5f,3.5f),new Color(color.r,color.g,color.b,.6f),.45f,1);
            }
        }
        public void Render(float alpha,float dt)
        {
            if(!initialized||world==null)return;
            // Convenience fallback for tools that render a manually stepped world.
            if(world.Tick!=lastTick)CaptureSimulationStep();
            alpha=Mathf.Clamp01(alpha);dt=Mathf.Max(0,Mathf.Min(dt,.1f));
            bool cinema=world.Cinematic.Active;
            for(int i=0;i<robots.Length;i++)robots[i].Hide();
            for(int p=0;p<2;p++)
            {
                var f=world.Fighters[p];var v=views[p];v.Display=Vector2.Lerp(v.Previous,v.Current,alpha);
                PresentFighter(f,v,clock,v.Display,null,false,0);
            }
            if(cinema)PresentCinematic();
            PresentProjectiles(alpha);
            effects.Step(dt);shake=Mathf.MoveTowards(shake,0,dt*.72f);lastDebugAge=Mathf.Max(0,lastDebugAge-dt);
            cinemaWeight=Mathf.MoveTowards(cinemaWeight,cinema?1:0,dt*5);cinemaShade.color=new Color(.012f,.025f,.055f,cinemaWeight*.34f);
            UpdateCamera(dt,cinema);UpdateDebug();
            ambientTimer+=dt;
            if(ambientTimer>.22f&&ParticleIntensity>.1f)
            {
                ambientTimer=0;float x=Mathf.Repeat(clock*1.17f+world.Tick*.173f,12.8f)-6.4f;
                effects.Emit(shapes.Glow,new Vector2(x,1.8f+Mathf.Repeat(clock*.73f,2.5f)),new Vector2(.07f,.12f),Vector2.one*.035f,new Color(.7f,.87f,1,.25f*ParticleIntensity),2.3f,0);
            }
        }
        void PresentFighter(FighterState f,FighterView v,float time,Vector2 position,string explicitFrame,bool armorPreview,float angle)
        {
            string skin=armorPreview&&f.Id==4?"mirkos_armor":SkinFor(f);v.Skin=skin;var atlas=library.Get(skin);
            string pose=f.Animation??"idle";float phase=Mathf.Repeat(time*(.68f+f.Id*.022f),1);
            if(f.State=="walk"||f.State=="evade")
            {
                phase=Mathf.Repeat(v.Gait,1);if(f.VelocityX*f.Face<0)pose=f.State=="walk"?"backwalk":"backdash";
            }
            else if(f.State=="jump")phase=Mathf.Clamp01((f.VelocityY+14.4f)/28.8f);
            else if(f.State=="hitstun"||f.State=="blockstun")phase=Mathf.Clamp01(f.Frame/15f);
            else if(f.State=="ko"||f.State=="knockdown")phase=.7f;
            else if(f.Move!=null&&(f.State=="attack"||f.State=="throw"))
            {
                var m=f.Move;float fr=f.Frame;
                phase=fr<m.Startup?fr/Mathf.Max(1,m.Startup)*.34f:fr<m.Startup+m.Active?.34f+(fr-m.Startup)/Mathf.Max(1,m.Active)*.3f:.64f+Mathf.Clamp01((fr-m.Startup-m.Active)/Mathf.Max(1,m.Recovery))*.36f;
                if(m.Kind=="rush_red")phase=Mathf.Repeat(time*2.5f,1);
            }
            if(f.Id==13&&f.Move!=null)pose=phase<.2f?"guard":"victory";
            if(!atlas.Animations.ContainsKey(pose))pose=PoseAlias(pose);
            v.Body.sprite=explicitFrame==null?atlas.Sample(pose,Mathf.Min(.9999f,phase)):atlas.Frame(explicitFrame);
            v.Body.flipX=f.Face<0;
            bool grounded=position.y<.03f;
            float breath=(f.State=="idle"||f.State=="block")?Mathf.Sin(time*3.6f+f.Id*.7f)*.005f:0;
            float landing=Mathf.Sin(v.Landing/.15f*Mathf.PI)*.032f;
            v.Body.transform.localPosition=position;
            v.Body.transform.localScale=new Vector3(1,1+breath-landing,1);
            v.Body.transform.localRotation=Quaternion.Euler(0,0,angle);
            v.Body.color=f.UltimateTint?new Color(1,.28f,.34f,1):Color.white;
            v.Properties.SetFloat(FlashId,f.HitFlash>0?.62f*Mathf.Clamp01(FlashIntensity):0);v.Properties.SetFloat(RimId,f.TransformFrames>0?.4f:.15f);v.Properties.SetColor(RimColorId,CharacterColor(f.Id));v.Body.SetPropertyBlock(v.Properties);
            float altitude=Mathf.Clamp01(position.y/3.3f);
            v.Shadow.transform.localPosition=new Vector3(position.x,-.01f,0);v.Shadow.transform.localScale=new Vector3(Mathf.Lerp(1.9f,1.1f,altitude),.23f,1);v.Shadow.color=new Color(.005f,.012f,.02f,Mathf.Lerp(.65f,.25f,altitude));
            v.Contact.transform.localPosition=new Vector3(position.x,-.015f,0);v.Contact.transform.localScale=new Vector3(1.25f,.1f,1);v.Contact.color=new Color(0,0,0,grounded?.43f:0);
            Color c=CharacterColor(f.Id);float power=f.UltimateTint?.47f:f.TransformFrames>0?.22f:f.Invuln>0?.12f:0;
            if(f.UltimateTint)c=new Color(1,.13f,.22f);v.Aura.enabled=power>0;v.Aura.transform.localPosition=position+new Vector2(0,1.25f);v.Aura.transform.localScale=new Vector3(2.4f,3.5f,1);v.Aura.color=new Color(c.r,c.g,c.b,power);
            v.Bear.enabled=f.Id==13;v.Ally.enabled=false;
            if(f.Id==13)
            {
                int bearFrame=f.BearStun>0?7:f.Move!=null?(f.Move.Kind=="throw"?6:f.Move.Level=="low"?5:f.Move.Damage>80?4:3):f.State=="walk"?1+(int)(time*7)%2:0;
                v.Bear.sprite=library.Bear(bearFrame);v.Bear.transform.localPosition=WorldPoint(f.BearX,590);v.Bear.transform.localScale=Vector3.one*(250f/444f);v.Bear.flipX=f.Face<0;
            }
            if(f.Id==9&&(f.FeintFrames>0||(f.Move!=null&&f.Move.Visual=="ally"&&f.Move.Kind=="counter")))
            {
                v.Ally.enabled=true;v.Ally.sprite=library.Ally("guard");v.Ally.transform.localPosition=position+new Vector2(f.Face*.8f,0);v.Ally.transform.localScale=Vector3.one*.51f;v.Ally.flipX=f.Face<0;
            }
        }
        static string PoseAlias(string pose)
        {
            switch(pose){case "punch":return "light";case "uppercut":return "heavy";case "block":return "guard";case "run":return "dash";case "slide":case "sweep":return "lowkick";case "projectile":case "cast":case "aim":return "specialA";case "spin":return "specialB";case "charge":return "intro";default:return pose;}
        }
        void PresentCinematic()
        {
            var c=world.Cinematic;var a=world.Fighters[c.Attacker];var d=world.Fighters[c.Defender];var av=views[c.Attacker];var dv=views[c.Defender];int frame=c.Frame;
            var strikes=c.Character==4?RobotStrikes:VitorStrikes;int beat=0;for(int i=0;i<strikes.Length;i++)if(frame>=strikes[i])beat=i;
            float since=frame-strikes[beat],hit=Mathf.Clamp01(1-since/5f);
            if(c.Character==1||c.Character==9)
            {
                string punch=frame<7||frame>=110?"punch2":frame<25?"guard":frame<103?(since<3?(beat%3==0?"punch1":beat%3==1?"punch2":"punch0"):"guard"):"guard";
                var target=dv.Display;var attacker=target-new Vector2(a.Face*(1.83f-hit*.12f),0);
                if(frame>111)attacker=av.Display;
                PresentFighter(a,av,clock,attacker,punch,false,0);PresentFighter(d,dv,clock,target,frame>111?"knockback":beat%3==0?"guard":"hurt",false,a.Face*(hit*3.5f));
                av.Display=attacker;
                if(c.Character==9)
                {
                    dv.Ally.enabled=true;dv.Ally.sprite=library.Ally(frame>=111?"kick":since<4?beat%2==0?"punch0":"punch1":"guard");dv.Ally.transform.localPosition=target+new Vector2(a.Face*1.65f,0);dv.Ally.flipX=a.Face>0;dv.Ally.transform.localScale=Vector3.one*.51f;
                }
            }
            else if(c.Character==4)
            {
                PresentFighter(a,av,clock,av.Display,frame<117?"intro":"guard",frame>=140,0);
                PresentFighter(d,dv,clock,dv.Display,frame>103?"land":"hurt",false,hit*4);
                for(int i=0;i<2;i++)
                {
                    int face=i==0?1:-1;var target=dv.Display-new Vector2(face*.76f,0);
                    if(frame<104){float enter=Mathf.SmoothStep(0,1,Mathf.InverseLerp(i*2,8+i*2,frame));robots[i].Render(Vector2.Lerp(target+new Vector2(-face*1.35f,1.9f),target,enter),face,beat%2==i&&frame>=10?hit:0,clock);}
                    else if(frame<117){float recall=Mathf.SmoothStep(0,1,(frame-103)/14f);robots[i].Render(target+new Vector2(-face*1.7f,8)*recall,face,0,clock);}
                }
            }
        }
        void PresentProjectiles(float alpha)
        {
            int visible=0,robot=world.Cinematic.Active?2:0;
            foreach(var p in world.Projectiles)
            {
                if(p.Dead)continue;
                if(p.Kind=="robot_strike")
                {
                    if(robot<robots.Length)robots[robot++].Render(WorldPoint(p.X-(1-alpha)*p.VelocityX,590),p.Face,Mathf.Max(0,Mathf.Sin(p.Age*.22f)),clock);continue;
                }
                if(visible>=ProjectileCapacity)break;var v=projectiles[visible++];bool fresh=v.Id!=p.Id;
                Vector2 pos=WorldPoint(p.X-(1-alpha)*p.VelocityX,p.Y-(1-alpha)*p.VelocityY);Color color=CharacterColor(world.Fighters[p.Owner].Id);
                v.Body.enabled=v.Glow.enabled=true;v.Body.transform.localPosition=pos;v.Body.flipX=p.Face<0;v.Body.color=color;
                v.Body.sprite=shapes.Disc;v.Body.transform.localScale=Vector3.one*.3f;v.Body.transform.localRotation=Quaternion.Euler(0,0,p.Age*8);
                bool trail=p.Kind=="projectile";string visual=p.Visual??"";
                if(visual=="ally"||visual=="allies"||visual=="entourage")
                {
                    v.Body.sprite=library.Ally(p.Age%12<6?"run0":"run1");v.Body.transform.localPosition=WorldPoint(p.X,590);v.Body.transform.localScale=Vector3.one*.51f;v.Body.transform.localRotation=Quaternion.identity;v.Body.color=Color.white;trail=false;
                }
                else if(visual=="football"||visual=="soccer"||visual=="goal"||visual=="ball") {v.Body.sprite=shapes.Football;v.Body.color=Color.white;v.Body.transform.localScale=Vector3.one*.4f;}
                else if(visual=="handball"||visual=="bounce") {v.Body.sprite=shapes.Football;v.Body.color=new Color(1,.57f,.23f);v.Body.transform.localScale=Vector3.one*.38f;}
                else if(visual=="brick"||visual=="wall"||visual=="pillar"||visual=="packet"||visual=="network") {v.Body.sprite=shapes.Plate;v.Body.transform.localScale=new Vector3(.35f,p.Kind=="barrier"?1.75f:.28f,1);}
                else if(p.Kind=="trap"||p.Kind=="barrier"||visual=="firewall"||visual=="shield") {v.Body.sprite=shapes.Ring;v.Body.transform.localScale=new Vector3(visual=="shield"?.65f:1.45f,p.Kind=="trap"?.25f:1.85f,1);v.Body.transform.localRotation=Quaternion.identity;}
                else if(visual=="bullet")v.Body.transform.localScale=new Vector3(.52f,.07f,1);
                else if(visual=="vine"||visual=="root"||visual=="roots") {v.Body.sprite=shapes.Streak;v.Body.transform.localScale=new Vector3(.2f,1.4f,1);v.Body.color=new Color(.35f,.75f,.37f);v.Body.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*8)*12);}
                v.Glow.transform.localPosition=pos;v.Glow.transform.localScale=Vector3.one*(p.Kind=="trap"?1.7f:.9f);v.Glow.color=new Color(color.r,color.g,color.b,.36f);
                v.Trail.enabled=trail;v.Trail.emitting=trail;if(fresh){v.Trail.Clear();v.Id=p.Id;}
                v.Trail.startColor=new Color(color.r,color.g,color.b,.5f);v.Trail.endColor=new Color(color.r,color.g,color.b,0);
            }
            for(int i=visible;i<projectiles.Length;i++){var p=projectiles[i];p.Id=-1;p.Body.enabled=p.Glow.enabled=p.Trail.enabled=p.Trail.emitting=false;p.Trail.Clear();}
        }
        void UpdateCamera(float dt,bool cinema)
        {
            Vector2 a=views[0].Display,b=views[1].Display;float aspect=Mathf.Max(1,Camera.aspect);
            float wanted=Mathf.Clamp((Mathf.Abs(a.x-b.x)+3.6f)/(2*aspect),cinema?2.8f:3.15f,3.6f);
            float size=dt>0?Mathf.SmoothDamp(Camera.orthographicSize,wanted,ref cameraSizeVelocity,.32f,20,dt):wanted;Camera.orthographicSize=size;
            float halfWidth=size*aspect;float maxX=Mathf.Max(0,6.4f-halfWidth);float x=Mathf.Clamp((a.x+b.x)*.5f,-maxX,maxX);
            float y=Mathf.Clamp(2.3f+Mathf.Max(a.y,b.y)*.15f,-1.3f+size,5.9f-size);var target=new Vector3(x,y,-10);
            cameraBase=dt>0?Vector3.SmoothDamp(cameraBase,target,ref cameraVelocity,.19f,15,dt):target;
            float amount=shake*Mathf.Clamp01(CameraShake);var offset=new Vector3(Mathf.Sin(clock*123.7f),Mathf.Cos(clock*147.2f),0)*amount;
            Camera.transform.localPosition=cameraBase+offset;
            vignette.transform.localPosition=new Vector3(Camera.transform.localPosition.x,Camera.transform.localPosition.y,0);vignette.transform.localScale=new Vector3(size*aspect*2.04f,size*2.04f,1);
        }
        void UpdateDebug()
        {
            for(int i=0;i<debugBoxes.Length;i++)debugBoxes[i].enabled=false;if(!ShowHitboxes)return;
            for(int p=0;p<2;p++)
            {
                var f=world.Fighters[p];DrawDebug(debugBoxes[p],f.Hurtbox,new Color(.25f,1,.67f,.85f));
                if(f.Move!=null&&f.Frame>=f.Move.Startup&&f.Frame<f.Move.Startup+f.Move.Active)
                {
                    var m=f.Move;DrawDebug(debugBoxes[p+2],new HitRect(f.Face>0?f.X+15:f.X-m.Range,f.Y+m.OffsetY,m.Range,m.Height),new Color(1,.22f,.28f,.85f));
                }
            }
            if(lastDebugAge>0&&lastDebugBox.Width>0)DrawDebug(debugBoxes[4],lastDebugBox,new Color(1,.8f,.2f,.9f));
        }
        void DrawDebug(LineRenderer line,HitRect box,Color color)
        {
            line.enabled=true;line.startColor=line.endColor=color;
            debugPoints[0]=WorldPoint(box.X,box.Y);debugPoints[1]=WorldPoint(box.X+box.Width,box.Y);debugPoints[2]=WorldPoint(box.X+box.Width,box.Y+box.Height);debugPoints[3]=WorldPoint(box.X,box.Y+box.Height);debugPoints[4]=debugPoints[0];line.SetPositions(debugPoints);
        }
        Color CharacterColor(int id)
        {
            return ColorUtility.TryParseHtmlString("#"+catalog.Character(id).ColorHex,out var color)?color:new Color(.45f,.8f,1);
        }
        /// <summary>Real camera readback; optional UI is captured by the app's ScreenCapture path.</summary>
        public void SaveScreenshot(string path,int width=1280,int height=720)
        {
            if(Camera==null)throw new InvalidOperationException("Camera not initialized");
            var prior=Camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            Texture2D pixels=null;
            try {Camera.targetTexture=rt;Camera.Render();RenderTexture.active=rt;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();string folder=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(folder);File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally {Camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Destroy(pixels);}
        }
        void OnDestroy(){library?.Dispose();shapes?.Dispose();}
    }
}
