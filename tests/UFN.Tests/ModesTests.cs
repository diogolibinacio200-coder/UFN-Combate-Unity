using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UFN.Core;

namespace UFN.Tests
{
    public static class ModesTests
    {
        public static int Checks { get; private set; }
        private static List<string> failures;
        private static void Check(bool condition, string label) { Checks++; if (!condition) failures.Add(label); }
        private static void Throws(Action action, string label) { try { action(); Check(false,label); } catch (ArgumentException) { Check(true,label); } catch (InvalidOperationException) { Check(true,label); } }
        public static List<string> Run()
        {
            Checks=0;failures=new List<string>();
            Campaign();Persistence();Kart();
            failures.AddRange(KartTests.Run()); Checks+=KartTests.Checks;
            return failures;
        }
        private static void Campaign()
        {
            var tower=CampaignRun.Create(CampaignMode.Tower,3,2718,new[]{0,1,14,15});
            Check(tower.OpponentId==0 && tower.Stage==0,"Tower begins at configured first opponent");
            tower.RecordVictory();
            Check(tower.AwaitingBenefit && tower.BenefitChoices.Length==3,"Victory offers three perks");
            Throws(()=>tower.RecordVictory(),"Cannot advance while a perk remains pending");
            var restored=CampaignRun.Restore(tower.Export());
            Check(restored.OpponentId==tower.OpponentId && restored.BenefitChoices[0]==tower.BenefitChoices[0],"Campaign restore preserves pending choice and next opponent");
            for(int round=1;round<4;round++)
            {
                tower.ChooseBenefit(0);restored.ChooseBenefit(0);
                Check(tower.OpponentId==new[]{0,1,14,15}[round],"Tower preserves configured route and both bosses");
                tower.RecordVictory();restored.RecordVictory();
                Check(tower.HealthMultiplier==restored.HealthMultiplier && tower.MeterBonus==restored.MeterBonus && tower.DamageMultiplier==restored.DamageMultiplier,"Saved campaign perks remain deterministic");
            }
            Check(tower.Complete && tower.Won && tower.OpponentId==-1 && tower.Wins==4,"Tower victory terminates after final boss");
            Throws(()=>tower.RecordDefeat(),"Completed campaign rejects duplicate results");
            Throws(()=>CampaignRun.Create(CampaignMode.Tower,3,2,new[]{0,1}),"Tower requires both bosses");
            var tournament=CampaignRun.Create(CampaignMode.Tournament,5,100);
            var enemies=new HashSet<int>();
            for(int round=0;round<4;round++)
            {
                Check(enemies.Add(tournament.OpponentId) && tournament.OpponentId!=5,"Tournament opponents are unique winners of bracket");
                tournament.RecordVictory();if(tournament.AwaitingBenefit)tournament.ChooseBenefit(1);
            }
            Check(tournament.Complete && tournament.Won && tournament.Wins==4,"Sixteen-entry tournament requires four wins");
            var survival=CampaignRun.Create(CampaignMode.Survival,3,7);
            bool sawBoss=false;
            for(int wave=0;wave<120;wave++)
            {
                sawBoss|=survival.OpponentId>=14;
                survival.RecordVictory();survival.ChooseBenefit(wave%3);
            }
            Check(sawBoss && !survival.Complete && survival.Wins==120,"Survival continues with periodic bosses");
            Check(survival.HealthMultiplier<=1.4f && survival.DamageMultiplier<=1.25f && survival.MeterBonus<=60,"Long survival benefits remain bounded");
            Check(survival.Export().Opponents.Length==1,"Long survival save keeps bounded opponent history");
            survival.RecordDefeat();Check(survival.Complete && !survival.Won,"Survival ends on defeat");
            var broken=tournament.Export();broken.Bracket=new[]{99};Throws(()=>CampaignRun.Restore(broken),"Restore rejects invalid roster entry");
            var snapshot=restored.Export();snapshot.Benefits[0]=99;Check(restored.HealthMultiplier<1.4f,"Snapshots do not expose mutable campaign internals");
        }

        private sealed class Codec : IProfileCodec
        {
            private readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true};
            public string Encode(ProfileData value)=>JsonSerializer.Serialize(value,options);
            public ProfileData Decode(string value)=>JsonSerializer.Deserialize<ProfileData>(value,options);
        }
        private static void Persistence()
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ufn-profile-tests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path=System.IO.Path.Combine(directory,"profile.json");
                var store=new ProfileStore(path,new Codec());var profile=store.Load();
                Check(profile.Version==ProfileData.CurrentVersion && profile.TotalWins==0,"New profile defaults load");
                profile.TotalWins=5;profile.Campaign=CampaignRun.Create(CampaignMode.Tower,3,51).Export();store.Save(profile);
                Check(store.Load().TotalWins==5 && File.Exists(path+".bak"),"First durable save includes backup");
                profile.TotalWins=6;store.Save(profile);
                File.WriteAllText(path,"{truncated");
                ProfileData recovered=store.Load();Check(recovered.TotalWins==5 && store.RecoveryMessage.Length>0,"Corrupt primary recovers previous durable backup");
                recovered.TotalWins=7;store.Save(recovered);
                File.WriteAllText(path,"{}");Check(store.Load().TotalWins==5,"Saving over corrupt primary preserves usable backup");
                File.WriteAllText(path,new Codec().Encode(new ProfileData{Version=1,TotalWins=8}));Check(store.Load().Version==2 && store.Load().TotalWins==8,"Version one profile migrates without progress loss");
                File.WriteAllText(path,new Codec().Encode(new ProfileData{Version=99,TotalWins=99}));Check(store.Load().TotalWins==5,"Unknown future profile cannot overwrite supported backup");
                Check(Directory.GetFiles(directory,"*.tmp-*").Length==0,"Atomic saves leave no temporary files");
            }
            finally { Directory.Delete(directory,true); }
        }

        private static void Kart()
        {
            var race=new KartRace(31,3);var replay=new KartRace(31,3);
            Check(race.Racers.Length==8 && race.Track.Length>500,"Original circuit supports eight racers");
            Check(KartPoint.Distance(race.Track.Sample(0).Position,race.Track.Sample(race.Track.Length).Position)<.001f,"Track closes continuously");
            var roster=new HashSet<int>();foreach(var racer in race.Racers)roster.Add(racer.CharacterId);Check(roster.Count==8,"Kart roster has no duplicate competitors");
            bool boosted=false;int itemUses=0;bool continuous=true;bool deterministic=true;
            for(int tick=0;tick<16000;tick++)
            {
                var previous=new KartPoint[8];for(int i=0;i<8;i++)previous[i]=race.Racers[i].Position;
                KartInput input=race.GetAIInput(0);race.Step(input);replay.Step(input);
                for(int i=0;i<8;i++)
                {
                    KartRacer a=race.Racers[i],b=replay.Racers[i];
                    continuous&=KartPoint.Distance(previous[i],a.Position)<2;
                    deterministic&=a.Position.X==b.Position.X && a.Position.Y==b.Position.Y && a.Lap==b.Lap && a.Item==b.Item;
                    boosted|=a.BoostTicks>0;
                }
                if(race.Completed)break;
            }
            var places=new HashSet<int>();
            foreach(KartRacer racer in race.Racers)
            {
                Check(racer.Finished ? racer.Lap==3 && racer.CheckpointsPassed==60 : racer.Lap<3,"Classified racer only finishes after sixty ordered checkpoints: "+racer.Index);
                itemUses+=racer.ItemsUsed;places.Add(racer.Place);
            }
            Check(continuous,"AI and player move continuously without path teleporting");
            Check(deterministic,"Same kart seed and input produce identical complete races");
            Check(places.Count==8,"Finish order assigns distinct places");
            Check(boosted && itemUses>0,"Real driving earns and activates boosts/items");
            var reverse=new KartRace(3,1);
            for(int i=0;i<3000;i++)reverse.Step(new KartInput{Throttle=-1});
            Check(reverse.Racers[0].Lap==0 && !reverse.Finished,"Reverse/off-course driving cannot fabricate ordered laps");
            reverse.Step(new KartInput{Throttle=float.NaN,Steering=float.PositiveInfinity,Brake=float.NaN});
            Check(!float.IsNaN(reverse.Racers[0].Position.X),"Invalid controls cannot poison physics");
            var profile=new ProfileData();profile.RecordKartResult(race);Check(profile.BestKartMilliseconds>0,"Completed kart race records a persistent time");
        }
    }
}
