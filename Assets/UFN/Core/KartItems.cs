using System;
using System.Collections.Generic;

namespace UFN.Core
{
    public sealed class KartProjectile
    {
        public int Owner,Ticks,StunTicks,Bounces;
        public string Kind;
        public KartPoint Position,Direction;
        public float Speed;
        public bool Homing,Curve;
    }
    public sealed class KartEvent { public string Kind,Visual;public int Player,Target=-1;public KartPoint Position; }
    public sealed partial class KartRace
    {
        private readonly List<KartProjectile> projectiles=new List<KartProjectile>();
        public IReadOnlyList<KartProjectile> Projectiles=>projectiles;
        public List<KartEvent> Events{get;}=new List<KartEvent>();
        public static string ItemName(KartRacer racer)
        {
            switch(racer.Item){case KartItem.Turbo:return "TURBO";case KartItem.Shield:return "ESCUDO";case KartItem.Pulse:return "PULSO UFN";case KartItem.Oil:return "ÓLEO";case KartItem.Shock:return "CHOQUE";case KartItem.Capybara:return "CAPIVARA";case KartItem.Signature:return KartContent.Signatures[racer.CharacterId];default:return "—";}
        }
        private KartItem RollItem(int rank)
        {
            KartItem[] pool=rank<=2?new[]{KartItem.Pulse,KartItem.Shield,KartItem.Oil,KartItem.Signature,KartItem.Signature}:new[]{KartItem.Pulse,KartItem.Turbo,KartItem.Turbo,KartItem.Shield,KartItem.Shock,KartItem.Signature,KartItem.Signature};
            return pool[NextInt(pool.Length)];
        }
        private void Launch(KartRacer owner,KartPoint direction,string kind,float speed,int life,bool homing,bool curve,int bounces,int stun)
        {
            if(projectiles.Count>=64)projectiles.RemoveAt(0);
            projectiles.Add(new KartProjectile{Owner=owner.Index,Position=owner.Position+direction*2.5f,Direction=direction,Kind=kind,Speed=speed,Ticks=life,Homing=homing,Curve=curve,Bounces=bounces,StunTicks=stun});
        }
        private void AddHazard(KartRacer racer,KartPoint position,string kind,int life,float radius)
        {if(hazards.Count>=24)hazards.RemoveAt(0);hazards.Add(new KartHazard{Owner=racer.Index,Position=position,Kind=kind,Ticks=life,Radius=radius});}
        private void Signature(KartRacer racer,KartPoint direction)
        {
            int id=racer.CharacterId;racer.SignatureTicks=60;
            if(id==1||id==13)
            {float radius=id==1?9:5;foreach(var target in Racers)if(target!=racer&&KartPoint.Distance(target.Position,racer.Position)<radius)Hit(target,id==1?39:66);}
            else if(id==7)racer.BoostTicks=198;
            else if(id==9){racer.ShieldTicks=420;racer.SignatureTicks=420;}
            else if(id==3||id==11||id==12)AddHazard(racer,racer.Position-direction*3,id==3?"roots":id==11?"foam":"bricks",540,2.4f);
            else Launch(racer,direction,id==0?"dumbbell":id==2?"capybara":id==4?"drone":id==5?"football":id==6?"handball":id==8?"parabola":"data",id==2?43:49,240,id==2||id==4||id==10,id==5,id==0||id==6||id==8?2:0,69);
        }
        private void UpdateProjectiles()
        {
            for(int i=projectiles.Count-1;i>=0;i--)
            {
                KartProjectile shot=projectiles[i];if(--shot.Ticks<=0){projectiles.RemoveAt(i);continue;}
                KartRacer target=null;float best=45;
                foreach(var racer in Racers){KartPoint d=racer.Position-shot.Position;if(racer.Index!=shot.Owner&&d.Length<best&&KartPoint.Dot(d.Normalized,shot.Direction)>.25f){best=d.Length;target=racer;}}
                if(shot.Homing&&target!=null)shot.Direction=(shot.Direction*.96f+(target.Position-shot.Position).Normalized*.04f).Normalized;
                if(shot.Curve){float angle=.42f*DeltaTime;shot.Direction=new KartPoint(shot.Direction.X*(float)Math.Cos(angle)-shot.Direction.Y*(float)Math.Sin(angle),shot.Direction.X*(float)Math.Sin(angle)+shot.Direction.Y*(float)Math.Cos(angle));}
                KartPoint before=shot.Position;shot.Position+=shot.Direction*(shot.Speed*DeltaTime);
                var road=Track.Project(shot.Position);float offset=KartPoint.Distance(road.Position,shot.Position);
                if(offset>Track.Width*.5f+2.65f)
                {
                    if(shot.Bounces<=0){projectiles.RemoveAt(i);continue;}
                    var n=(shot.Position-road.Position).Normalized;shot.Direction=(shot.Direction-n*(2*KartPoint.Dot(shot.Direction,n))).Normalized;shot.Position=before;shot.Bounces--;
                }
                bool hit=false;foreach(var racer in Racers)if(racer.Index!=shot.Owner&&!racer.Finished&&SegmentDistance(before,shot.Position,racer.Position)<1.55f)
                {Hit(racer,shot.StunTicks);Events.Add(new KartEvent{Kind="hit",Visual=shot.Kind,Player=shot.Owner,Target=racer.Index,Position=racer.Position});hit=true;break;}
                if(hit)projectiles.RemoveAt(i);
            }
        }
        private static float SegmentDistance(KartPoint a,KartPoint b,KartPoint p){KartPoint d=b-a;float denom=KartPoint.Dot(d,d);float t=denom<.00001f?0:KartTrack.Clamp(KartPoint.Dot(p-a,d)/denom,0,1);return KartPoint.Distance(a+d*t,p);}
        private void ResolveTrackContact(KartRacer racer)
        {
            var sample=Track.Project(racer.Position);KartPoint delta=racer.Position-sample.Position;float distance=delta.Length;
            float boundary=Track.Width*.5f+2.65f-.95f;
            if(distance>boundary){KartPoint n=delta.Normalized;racer.Position-=n*Math.Min(.45f,distance-boundary);float outward=KartPoint.Dot(racer.Velocity,n);if(outward>0)racer.Velocity-=n*(outward*1.3f);racer.ForwardSpeed*=.97f;}
        }
        public KartRace NextChampionshipRace()
        {
            if(Mode!=KartRaceMode.Championship||!Completed||Track.Id>=3)throw new InvalidOperationException("O campeonato não tem uma próxima corrida disponível.");
            var next=new KartRace(initialSeed,Racers[0].CharacterId,Track.Id+1,Mode,HumanCount==2?Racers[1].CharacterId:-1,Racers[0].Skin,HumanCount==2?Racers[1].Skin:0,Difficulty);
            next.ChampionshipPoints=(int[])ChampionshipPoints.Clone();return next;
        }
    }
}
