using UFN.Core;
using UnityEngine;

namespace UFN.Presentation
{
    public sealed partial class KartPresentation
    {
        private Font signFont;
        private Transform[] signatureRings;
        private void Sign(string text,Transform parent,Vector3 position,Quaternion rotation,float size)
        {
            if(signFont==null)signFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(parent,false);label.transform.localPosition=position;label.transform.localRotation=rotation;label.text=text;label.font=signFont;label.fontSize=64;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Color.white;label.GetComponent<MeshRenderer>().sharedMaterial=signFont.material;
        }
        private void BuildTheme(Material building,Material glass,Material roof)
        {
            int id=race.Track.Id;
            if(id==4)
            {
                Building("UFN • CONJUNTO II",new Vector3(-20,0,-39),new Vector3(69,14,17),building,glass,roof);
                Building("ENGENHARIAS",new Vector3(-47,0,-10),new Vector3(15,11,38),building,glass,roof);
                Building("ITEC / UFN",new Vector3(17,0,-20),new Vector3(17,17,28),building,glass,roof);
                Building("UNIVERSIDADE FRANCISCANA",new Vector3(-32,0,36),new Vector3(42,12,16),building,glass,roof);
                Building("SANTA MARIA",new Vector3(44,0,29),new Vector3(25,9,25),Material(new Color(.82f,.72f,.62f)),glass,roof);
                string[] streets={"RUA DOS ANDRADAS","R. SERAFIM VALANDRO","RUA SILVA JARDIM","R. DUQUE DE CAXIAS"};
                for(int n=0;n<9;n++)for(int side=-1;side<=1;side+=2)Building(n%2==0?"CAFÉ ANDRADAS":"LIVRARIA",new Vector3(-93+n*22,0,side*100),new Vector3(19,8+n%4*3,18),Material(Color.Lerp(new Color(.85f,.77f,.65f),new Color(.68f,.78f,.8f),n%3*.35f)),glass,roof);
                for(int i=0;i<4;i++){var sample=race.Track.Sample(i*race.Track.Length/4);var n=new KartPoint(-sample.Tangent.Y,sample.Tangent.X);var p=sample.Position+n*12;Primitive(PrimitiveType.Cube,streets[i],world,ToWorld(p,4.6f),new Vector3(8.4f,.8f,.18f),roof);Sign(streets[i],world,ToWorld(p,4.6f)+new Vector3(0,0,-.12f),Quaternion.identity,.07f);}
            }
            else if(id==0)
            {
                Building("UNIVERSIDADE FRANCISCANA",new Vector3(0,0,-20),new Vector3(30,17,14),building,glass,roof);
                Building("UFN • INOVAÇÃO",new Vector3(-24,0,12),new Vector3(20,10,16),Material(new Color(.9f,.85f,.74f)),glass,roof);
                Building("BIBLIOTECA",new Vector3(30,0,35),new Vector3(17,8,10),building,glass,roof);
                for(int n=0;n<3;n++){Building("CONTAINER CAFÉ",new Vector3(-23+n*17,0,-83),new Vector3(14,4.5f,8),Material(new Color(.1f,.3f,.43f)),glass,roof);}
            }
            else if(id==1)
            {
                string[] labels={"VILA BELGA","PATRIMÔNIO HISTÓRICO","SANTA MARIA"};
                for(int n=0;n<10;n++){var tint=Material(Color.Lerp(new Color(.81f,.49f,.36f),new Color(.93f,.82f,.56f),n%3*.4f));Building(labels[n%3],new Vector3(-90+n*20,0,-88),new Vector3(17,6+n%3,12),tint,glass,roof);Building("VILA BELGA",new Vector3(-34+n%4*19,0,7+n/4*15),new Vector3(14,5,10),tint,glass,roof);}
            }
            else if(id==2)
            {
                Building("CENTRO • SANTA MARIA",new Vector3(-12,0,-23),new Vector3(35,22,15),building,glass,roof);
                Building("PRAÇA SALDANHA MARINHO",new Vector3(-25,0,30),new Vector3(26,4,17),Material(new Color(.85f,.77f,.65f)),glass,roof);
                for(int n=0;n<8;n++)Building(n%2==0?"COMÉRCIO":"CENTRO",new Vector3(-80+n*23,0,-94),new Vector3(18,10+n%4*5,18),building,glass,roof);
            }
            else
            {
                Building("DESAFIO UNIVERSITÁRIO",new Vector3(0,0,-30),new Vector3(30,9,17),building,glass,roof);
                Building("UFN • ESPORTES",new Vector3(-25,0,15),new Vector3(25,7,15),Material(new Color(.18f,.4f,.6f)),glass,roof);
                for(int n=0;n<3;n++){var sample=race.Track.Sample(n*race.Track.Length/3);var ramp=Primitive(PrimitiveType.Cube,"Ramp",world,ToWorld(sample.Position,.25f),new Vector3(12,.35f,3),Material(new Color(.19f,.53f,.7f)));ramp.rotation=Quaternion.Euler(-9,90-Mathf.Atan2(sample.Tangent.Y,sample.Tangent.X)*Mathf.Rad2Deg,0);}
            }
            // Rails align with the same analytical boundary used by the C# simulation.
            var railA=Material(new Color(.86f,.89f,.86f));var railB=Material(new Color(.12f,.42f,.61f));
            for(int i=0;i<race.Track.Points.Length;i++)
            {
                KartPoint a=race.Track.Points[i],b=race.Track.Points[(i+1)%race.Track.Points.Length],d=(b-a).Normalized,n=new KartPoint(-d.Y,d.X);
                for(int side=-1;side<=1;side+=2)
                {
                    KartPoint offset=n*(race.Track.Width*.5f+2.65f)*side;
                    bool crossesRoad=false;
                    for(int sample=0;sample<=4;sample++)
                    {
                        KartPoint point=a+(b-a)*(sample/4f)+offset;
                        if(KartPoint.Distance(point,race.Track.Project(point).Position)<race.Track.Width*.5f+.2f){crossesRoad=true;break;}
                    }
                    if(crossesRoad)continue;
                    KartPoint p=(a+b)*.5f+offset;
                    var rail=Primitive(PrimitiveType.Cube,"Contact barrier",world,ToWorld(p,.7f),new Vector3(.32f,.66f,(b-a).Length+.2f),i%4<2?railA:railB);
                    rail.rotation=Quaternion.Euler(0,90-Mathf.Atan2(d.Y,d.X)*Mathf.Rad2Deg,0);
                }
            }
        }
        private GameObject PowerShape(string kind)
        {
            var root=new GameObject(kind);root.transform.SetParent(world,false);var p=root.transform;
            var blue=Material(new Color(.15f,.85f,1),true);var dark=Material(new Color(.13f,.18f,.23f));var tan=Material(new Color(.62f,.45f,.28f));
            if(kind=="capybara")
            {Primitive(PrimitiveType.Sphere,"Body",p,Vector3.zero,new Vector3(1.2f,.75f,1.7f),tan);Primitive(PrimitiveType.Sphere,"Head",p,new Vector3(0,.2f,.78f),new Vector3(.75f,.6f,.8f),tan);for(int side=-1;side<=1;side+=2)Primitive(PrimitiveType.Sphere,"Ear",p,new Vector3(side*.24f,.52f,.7f),Vector3.one*.19f,tan);}
            else if(kind=="drone")
            {Primitive(PrimitiveType.Cube,"Drone",p,Vector3.zero,new Vector3(1.4f,.3f,1),dark);for(int side=-1;side<=1;side+=2)Primitive(PrimitiveType.Cylinder,"Rotor",p,new Vector3(side*.88f,.1f,0),new Vector3(.85f,.06f,.85f),blue);}
            else if(kind=="dumbbell")
            {Primitive(PrimitiveType.Cube,"Bar",p,Vector3.zero,new Vector3(1.5f,.12f,.12f),dark);for(int side=-1;side<=1;side+=2)Primitive(PrimitiveType.Sphere,"Weight",p,new Vector3(side*.64f,0,0),Vector3.one*.65f,dark);}
            else if(kind=="roots")for(int n=0;n<5;n++){var rootPart=Primitive(PrimitiveType.Cylinder,"Root",p,new Vector3((n-2)*.5f,.25f,0),new Vector3(.3f,.45f,.3f),Material(new Color(.24f,.52f,.3f)));rootPart.localRotation=Quaternion.Euler(0,0,(n-2)*20);}
            else if(kind=="bricks")for(int n=0;n<3;n++)for(int row=0;row<2;row++)Primitive(PrimitiveType.Cube,"Brick",p,new Vector3((n-1)*.7f,row*.43f,0),new Vector3(.67f,.4f,.5f),Material(new Color(.67f,.34f,.22f)));
            else if(kind=="foam")for(int n=0;n<6;n++)Primitive(PrimitiveType.Sphere,"Foam",p,new Vector3(Mathf.Sin(n*1.2f),0,Mathf.Cos(n*1.2f)),new Vector3(1.25f,.25f,1.25f),Material(new Color(.94f,.94f,.85f)));
            else if(kind=="oil")Primitive(PrimitiveType.Cylinder,"Oil",p,Vector3.zero,new Vector3(4,.03f,4),Material(new Color(.055f,.06f,.1f)));
            else Primitive(kind=="data"||kind=="parabola"?PrimitiveType.Cube:PrimitiveType.Sphere,kind,p,Vector3.zero,Vector3.one*.8f,kind=="football"?Material(Color.white):kind=="handball"?Material(new Color(1,.6f,.1f)):blue);
            return root;
        }
        private void UpdatePowerVisuals()
        {
            while(hazards.Count<race.Hazards.Count)hazards.Add(null);
            for(int i=0;i<hazards.Count;i++)
            {
                if(i>=race.Hazards.Count){if(hazards[i]!=null)hazards[i].SetActive(false);continue;}
                var h=race.Hazards[i];if(hazards[i]==null||hazards[i].name!=h.Kind){if(hazards[i]!=null)Release(hazards[i]);hazards[i]=PowerShape(h.Kind);}hazards[i].SetActive(true);hazards[i].transform.position=ToWorld(h.Position,h.Kind=="oil"?.08f:.35f);
            }
            while(shots.Count<race.Projectiles.Count)shots.Add(null);
            for(int i=0;i<shots.Count;i++)
            {
                if(i>=race.Projectiles.Count){if(shots[i]!=null)shots[i].SetActive(false);continue;}var p=race.Projectiles[i];if(shots[i]==null||shots[i].name!=p.Kind){if(shots[i]!=null)Release(shots[i]);shots[i]=PowerShape(p.Kind);}shots[i].SetActive(true);shots[i].transform.position=ToWorld(p.Position,p.Kind=="drone"?2.6f:.9f);shots[i].transform.rotation=Quaternion.Euler(0,90-Mathf.Atan2(p.Direction.Y,p.Direction.X)*Mathf.Rad2Deg,0);
            }
            if(signatureRings==null){signatureRings=new Transform[race.Racers.Length];for(int i=0;i<signatureRings.Length;i++)signatureRings[i]=Primitive(PrimitiveType.Cylinder,"Signature field",world,Vector3.zero,new Vector3(1,.02f,1),Material(new Color(.5f,.85f,.4f),true));}
            for(int i=0;i<signatureRings.Length;i++){var r=race.Racers[i];bool show=r.SignatureTicks>0&&(r.CharacterId==1||r.CharacterId==13);signatureRings[i].gameObject.SetActive(show);if(show){signatureRings[i].position=ToWorld(r.Position,.04f);float diameter=(r.CharacterId==1?18:10)*(1-r.SignatureTicks/60f);signatureRings[i].localScale=new Vector3(diameter,.02f,diameter);}}
        }
    }
}
