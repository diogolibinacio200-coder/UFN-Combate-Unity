using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UFN.Core;

namespace UFN.Tests
{
    // Real driving tests use the same controls and collisions as humans. Reflection
    // is confined to item fixtures, so the shipping assembly has no cheat API.
    public static class KartTests
    {
        public static int Checks { get; private set; }
        private static List<string> failures;
        private static void Check(bool value,string text){Checks++;if(!value)failures.Add("Kart: "+text);}
        private static void Set<T>(KartRacer racer,string field,T value)=>typeof(KartRacer).GetProperty(field).SetValue(racer,value);
        private static void AdvanceCountdown(KartRace race){for(int i=0;i<240;i++)race.Step(default);}
        public static List<string> Run()
        {
            Checks=0;failures=new List<string>();
            TracksAndDriving();Championship();Items();SkinsAndControls();
            return failures;
        }
        private static void TracksAndDriving()
        {
            var lengths=new HashSet<int>();
            for(int track=0;track<5;track++)
            {
                var race=new KartRace(120+track,3,track,KartRaceMode.Quick,2,1,0,1);
                lengths.Add((int)race.Track.Length);
                Check(race.HumanCount==2&&race.Racers.Length==8,"two players and six CPUs on track "+track);
                var p1=race.Racers[0].Position;var p2=race.Racers[1].Position;
                for(int n=0;n<240;n++)race.Step(new KartInput{Throttle=1},new KartInput{Throttle=1});
                Check(race.Racers[0].Position.X==p1.X&&race.Racers[1].Position.Y==p2.Y,"four-second countdown prevents false starts on track "+track);
                bool continuity=true,finite=true,tooFar=false,items=false;
                var previous=new KartPoint[8];
                for(int tick=0;tick<16000&&!race.Completed;tick++)
                {
                    for(int n=0;n<8;n++)previous[n]=race.Racers[n].Position;
                    race.Step(race.GetAIInput(0),race.GetAIInput(1));
                    foreach(var racer in race.Racers)
                    {
                        continuity&=KartPoint.Distance(previous[racer.Index],racer.Position)<2;
                        finite&=!float.IsNaN(racer.Position.X)&&!float.IsInfinity(racer.Position.Y);
                        tooFar|=racer.OffroadDistance>race.Track.Width;
                        items|=racer.ItemsUsed>0;
                    }
                }
                Check(race.Completed&&race.Racers[0].Lap==3&&race.Racers[1].Lap==3,"both humans finish three laps with ordinary AI controls on track "+track);
                Check(race.Racers[0].CheckpointsPassed==60&&race.Racers[1].CheckpointsPassed==60,"twenty ordered checkpoints per lap on track "+track);
                Check(continuity&&finite&&!tooFar,"no teleport, nonfinite physics, or escaped circuit on track "+track);
                Check(items,"earned items activate during track "+track);
                Check(race.Racers.Select(r=>r.Place).Distinct().Count()==8,"classification has eight distinct positions on track "+track);
                int clock=race.ElapsedTicks;var result=race.Racers.Select(r=>r.Position.X+"/"+r.Position.Y+"/"+r.Place).ToArray();
                for(int n=0;n<600;n++)race.Step(race.GetAIInput(0),race.GetAIInput(1));
                Check(clock==race.ElapsedTicks&&result.SequenceEqual(race.Racers.Select(r=>r.Position.X+"/"+r.Position.Y+"/"+r.Place)),"finished result remains stable on track "+track);
            }
            Check(lengths.Count==5,"five distinct source-derived circuits");
            for(int difficulty=0;difficulty<3;difficulty++)
            {
                var clock=new KartRace(91,3,4,KartRaceMode.TimeAttack,2,1,1,difficulty);
                for(int tick=0;tick<16000&&!clock.Completed;tick++)clock.Step(clock.GetAIInput(0));
                Check(clock.Completed&&clock.Racers.Length==1&&clock.HumanCount==1,"time attack finishes solo at difficulty "+difficulty);
                Check(clock.Racers[0].Item==KartItem.None&&clock.Racers[0].ItemsUsed==0&&clock.Projectiles.Count==0&&clock.Hazards.Count==0,"time attack has no boxes or items at difficulty "+difficulty);
                Check(clock.Racers[0].LapTicks.All(n=>n>0)&&clock.Racers[0].BestLapMilliseconds>0,"time attack records all lap times at difficulty "+difficulty);
            }
        }
        private static void Championship()
        {
            var race=new KartRace(803,3,0,KartRaceMode.Championship,3,1,0);
            int[] characters=race.Racers.Select(r=>r.CharacterId).ToArray();
            var initial=race.ExportChampionship();
            var startRestored=KartRace.RestoreChampionship(initial);
            Check(startRestored.Track.Id==0&&startRestored.Racers.Select(r=>r.CharacterId).SequenceEqual(characters),"new championship saves and restores its original seeded grid");
            for(int round=0;round<4;round++)
            {
                Check(race.Track.Id==round&&race.Racers.Select(r=>r.CharacterId).SequenceEqual(characters),"championship keeps grid between stages "+round);
                Check(race.Racers[0].Skin==1&&race.Racers[1].Skin==0,"same Diogo can race with different skins in stage "+round);
                for(int tick=0;tick<16000&&!race.Completed;tick++)race.Step(race.GetAIInput(0),race.GetAIInput(1));
                Check(race.Completed,"championship stage completes "+round);
                Check(race.ChampionshipPoints.Sum()==58*(round+1),"championship awards one set of 15/12/10/8/6/4/2/1 points in stage "+round);
                int[] points=(int[])race.ChampionshipPoints.Clone();race.Step(default,default);
                Check(points.SequenceEqual(race.ChampionshipPoints),"championship cannot award duplicate points in stage "+round);
                if(round<3)
                {
                    Check(!race.ChampionshipComplete,"championship requires four stages");
                    var save=race.ExportChampionship();var resumed=KartRace.RestoreChampionship(save);
                    Check(resumed.Track.Id==round+1&&resumed.ChampionshipPoints.SequenceEqual(points)&&resumed.Racers.Select(r=>r.CharacterId).SequenceEqual(characters),"saving completed stage resumes next stage with points and seeded grid");
                    save.Points[0]++;Check(resumed.ChampionshipPoints.SequenceEqual(points)&&race.ChampionshipPoints.SequenceEqual(points),"championship snapshots do not expose mutable points");
                    race=resumed;
                }
            }
            Check(race.ChampionshipComplete,"fourth stage ends championship");
            Check(race.ExportChampionship()==null,"completed championship removes resumable checkpoint");
            bool threw=false;try{race.NextChampionshipRace();}catch(InvalidOperationException){threw=true;}Check(threw,"championship rejects a fifth stage");
            initial.Points[0]=15;threw=false;try{KartRace.RestoreChampionship(initial);}catch(ArgumentException){threw=true;}Check(threw,"championship rejects points from stages not raced");
            initial.Points[0]=0;initial.Track=4;threw=false;try{KartRace.RestoreChampionship(initial);}catch(ArgumentException){threw=true;}Check(threw,"championship rejects invalid saved stage");
        }
        private static KartRace ItemFixture(int character)
        {
            var race=new KartRace(79,character);AdvanceCountdown(race);
            var sample=race.Track.Sample(50);Set(race.Racers[0],nameof(KartRacer.Position),sample.Position);Set(race.Racers[0],nameof(KartRacer.Heading),(float)Math.Atan2(sample.Tangent.Y,sample.Tangent.X));
            for(int n=1;n<race.Racers.Length;n++)Set(race.Racers[n],nameof(KartRacer.Position),race.Track.Sample(100+n*25).Position);
            return race;
        }
        private static void Items()
        {
            string[] projectileKinds={"dumbbell","","capybara","","drone","football","handball","","parabola","","data","","",""};
            for(int character=0;character<14;character++)
            {
                var race=ItemFixture(character);var driver=race.Racers[0];
                var forward=new KartPoint((float)Math.Cos(driver.Heading),(float)Math.Sin(driver.Heading));
                if(character==1||character==13)Set(race.Racers[1],nameof(KartRacer.Position),driver.Position+forward*3);
                Set(driver,nameof(KartRacer.Item),KartItem.Signature);race.Step(new KartInput{UseItem=true});
                Check(driver.ItemsUsed==1&&driver.Item==KartItem.None&&driver.SignatureTicks>0,"signature consumes one item for "+KartContent.Names[character]);
                if(projectileKinds[character]!="")Check(race.Projectiles.Count==1&&race.Projectiles[0].Kind==projectileKinds[character],"distinct projectile for "+KartContent.Names[character]);
                if(character==2||character==4||character==10)Check(race.Projectiles[0].Homing,"guided projectile for "+KartContent.Names[character]);
                if(character==0||character==6||character==8)Check(race.Projectiles[0].Bounces==2,"two rebounds for "+KartContent.Names[character]);
                if(character==5)Check(race.Projectiles[0].Curve,"curving football");
                if(character==1||character==13)Check(race.Racers[1].StunTicks>0,"radial impact for "+KartContent.Names[character]);
                if(character==7)Check(driver.BoostTicks==198,"Gabriel engine turbo duration");
                if(character==9)Check(driver.ShieldTicks==420&&driver.SignatureTicks==420,"Vitor do Mal ally protection duration");
                if(character==3||character==11||character==12)Check(race.Hazards.Count==1&&race.Hazards[0].Kind==(character==3?"roots":character==11?"foam":"bricks"),"distinct track obstacle for "+KartContent.Names[character]);
            }
            var backwards=ItemFixture(4);var shooter=backwards.Racers[0];var direction=new KartPoint((float)Math.Cos(shooter.Heading),(float)Math.Sin(shooter.Heading));
            Set(shooter,nameof(KartRacer.Item),KartItem.Pulse);backwards.Step(new KartInput{UseItem=true,LaunchBack=true});
            Check(KartPoint.Dot(backwards.Projectiles[0].Direction,direction)<-.9f,"backward launch reverses projectile");
            Set(shooter,nameof(KartRacer.Item),KartItem.Turbo);backwards.Step(new KartInput{UseItem=true});Check(shooter.Item==KartItem.Turbo,"held item key does not activate new pickup");
            backwards.Step(default);backwards.Step(new KartInput{UseItem=true});Check(shooter.Item==KartItem.None&&shooter.BoostTicks>0,"release and press activates next item");
            var shield=ItemFixture(1);var attacker=shield.Racers[0];var target=shield.Racers[1];
            Set(target,nameof(KartRacer.Position),attacker.Position+new KartPoint(2.1f,0));Set(target,nameof(KartRacer.ShieldTicks),420);Set(attacker,nameof(KartRacer.Item),KartItem.Shock);shield.Step(new KartInput{UseItem=true});
            Check(target.ShieldTicks==0&&target.StunTicks==0&&target.InvincibleTicks>0,"shield absorbs impact and grants brief immunity");
            var hazard=ItemFixture(3);Set(hazard.Racers[0],nameof(KartRacer.Item),KartItem.Oil);hazard.Step(new KartInput{UseItem=true});
            Check(hazard.Hazards.Count==1&&hazard.Hazards[0].Ticks==959,"oil has sixteen-second lifetime");
            for(int n=0;n<1000;n++)hazard.Step(default);
            Check(hazard.Hazards.Count==0,"expired oil is removed");
        }
        private static void SkinsAndControls()
        {
            Check(KartAppearance.All.Length==15&&KartContent.Names.Length==14,"fourteen identities and fifteen visual variants");
            Check(KartAppearance.Get(3,1).Reference=="diogo_alt"&&KartAppearance.Get(3,0).Reference=="diogo","Diogo Careca is a Diogo skin");
            Check(KartAppearance.Get(3,1).HairStyle=="bald"&&KartAppearance.Get(3,1).Glasses=="dark"&&KartAppearance.Get(3,1).Detail=="tattoos","Diogo Careca preserves supplied visual identifiers");
            var standard=new KartRace(61,3,0,KartRaceMode.TimeAttack);var skin=new KartRace(61,3,0,KartRaceMode.TimeAttack,-1,1);
            bool same=true;
            for(int n=0;n<2400;n++){var input=standard.GetAIInput(0);standard.Step(input);skin.Step(input);same&=standard.Racers[0].Position.X==skin.Racers[0].Position.X&&standard.Racers[0].Position.Y==skin.Racers[0].Position.Y&&standard.Racers[0].Speed==skin.Racers[0].Speed;}
            Check(same,"cosmetic Diogo skin has identical driving attributes");
            var split=new KartRace(41,3,0,KartRaceMode.Quick,2);AdvanceCountdown(split);
            var parked=split.Racers[1].Position;for(int n=0;n<120;n++)split.Step(new KartInput{Throttle=1},default);
            Check(split.Racers[0].Speed>10&&split.Racers[1].Speed==0&&KartPoint.Distance(parked,split.Racers[1].Position)<.01f,"two human control streams remain independent");
            int checkpoints=split.Racers[0].CheckpointsPassed,lap=split.Racers[0].Lap;
            Check(split.Recover(0)&&split.Racers[0].StunTicks==60&&split.Racers[0].RecoveryTicks==180,"manual rescue applies original one-second penalty and three-second cooldown");
            Check(split.Racers[0].CheckpointsPassed==checkpoints&&split.Racers[0].Lap==lap&&split.Racers[0].Speed==0,"manual rescue does not award checkpoints or laps");
            Check(!split.Recover(0),"manual rescue cannot bypass cooldown");
        }
    }
}
