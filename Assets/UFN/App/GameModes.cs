using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UFN.Core;
using UFN.Presentation;

namespace UFN.App
{
    [Serializable] public sealed class HostProgress
    {
        public int Version=1, Matches,P1Wins,P2Wins,BiggestCombo,PerfectBlocks,GuardBreaks,Supers,SurvivalRecord;
        public int[] MostPlayed=new int[16],TrackRecords=new int[5],LapRecords=new int[5];
        public string[] Achievements=Array.Empty<string>();
        public ClassicSnapshot Tower;
        public LocalBracketSnapshot Bracket;
        public KartChampionshipSnapshot Championship;
        public string MigratedOriginalJson="";
    }
    public sealed partial class GameApp
    {
        private HostProgress progress=new HostProgress();
        private ClassicRun classic;
        private LocalBracket bracket;
        private int towerSize=5,kartPlayers=1,kartTrack,opponentSkin,tournamentSize=4;
        private KartRaceMode kartMode;
        private readonly int[] reserves={0,1},reserveSkins=new int[2];
        private readonly int[] entryCharacters={3,4,1,0,2,5,6,7},entrySkins=new int[8];
        private readonly bool[] entryCpu={false,true,true,true,true,true,true,true};
        private float survivalHealth=1000,idleTime,endingTime,tutorialOrigin;
        private int survivalWave,survivalScore,matchWinner=-1,tutorialStep,tutorialHits;
        private bool trainingHealth=true,trainingMeter=true,trainingGuard=true;
        private bool IsTraining=>mode==PlayMode.Training||mode==PlayMode.Tutorial;
        private int SelectionId=>selectionPlayer==0?selected:selectionPlayer==1?opponent:reserves[selectionPlayer-2];
        private string SelectionTitle()=>mode==PlayMode.Teams?new[]{"J1 · ESCOLHA O TITULAR","J2 · ESCOLHA O TITULAR","J1 · ESCOLHA O PARCEIRO","J2 · ESCOLHA O PARCEIRO"}[Mathf.Clamp(selectionPlayer,0,3)]:selectionPlayer==0?"ESCOLHA SEU PERSONAGEM":"ESCOLHA O SEGUNDO PERSONAGEM";
        private void SetSelectionId(int value){if(selectionPlayer==0)selected=value;else if(selectionPlayer==1)opponent=value;else reserves[selectionPlayer-2]=value;}
        private int SelectionSkin=>selectionPlayer==0?skin:selectionPlayer==1?opponentSkin:reserveSkins[selectionPlayer-2];
        private void ToggleSelectionSkin(){if(SelectionId!=3)return;if(selectionPlayer==0)skin=1-skin;else if(selectionPlayer==1)opponentSkin=1-opponentSkin;else reserveSkins[selectionPlayer-2]=1-reserveSkins[selectionPlayer-2];}
        private void LoadProgress()
        {
            string path=Path.Combine(savePath,"progress.json");
            foreach(string candidate in new[]{path,path+".bak"})try{if(File.Exists(candidate)){var loaded=JsonUtility.FromJson<HostProgress>(File.ReadAllText(candidate));if(loaded!=null&&loaded.Version==1&&loaded.MostPlayed?.Length==16&&loaded.TrackRecords?.Length==5&&loaded.LapRecords?.Length==5){progress=loaded;return;}}}catch(Exception e){Debug.LogWarning("Progresso: "+e.Message);}
            if(!smoke)ImportLegacyProfile();
        }
        private void SaveProgress()
        {
            if(classic!=null&&mode==PlayMode.Tower)progress.Tower=classic.ExportSnapshot();
            if(bracket!=null)progress.Bracket=bracket.ExportSnapshot();
            if(Page==ScreenPage.Kart&&kart!=null&&kart.Mode==KartRaceMode.Championship)progress.Championship=kart.ExportChampionship();
            string path=Path.Combine(savePath,"progress.json"),tmp=path+".tmp";
            File.WriteAllText(tmp,JsonUtility.ToJson(progress,true));
            if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path);
        }
        private void StartClassic()
        {
            classic=ClassicRun.Create(mode==PlayMode.Tower,selected,skin,mode==PlayMode.Tower?towerSize:8,prefs.Difficulty,NewSeed());
            opponent=classic.Opponent;wins[0]=wins[1]=0;
            if(mode==PlayMode.Tower){Page=ScreenPage.TowerMap;SaveProfile();}else StartFight();
        }
        private void ResumeTower()
        {
            try{classic=ClassicRun.Restore(progress.Tower);selected=classic.Character;skin=classic.Skin;opponent=classic.Opponent;mode=PlayMode.Tower;campaign=null;bracket=null;wins[0]=wins[1]=0;Page=classic.Complete?ScreenPage.Ending:ScreenPage.TowerMap;endingTime=0;}
            catch(Exception e){Notify("Não foi possível retomar a torre: "+e.Message,7);}
        }
        private bool TournamentCpu(int side)=>mode==PlayMode.Tournament&&bracket!=null&&!bracket.Complete&&bracket.IsCpu(side==0?bracket.PairA:bracket.PairB);
        private void StartTournament()
        {
            int[] ids=new int[tournamentSize],skins=new int[tournamentSize];bool[] bots=new bool[tournamentSize];
            Array.Copy(entryCharacters,ids,tournamentSize);Array.Copy(entrySkins,skins,tournamentSize);Array.Copy(entryCpu,bots,tournamentSize);
            bracket=LocalBracket.Create(ids,skins,bots);Page=ScreenPage.Bracket;SaveProfile();
        }
        private void StartBracketMatch()
        {
            if(bracket==null||bracket.Complete)return;
            selected=bracket.Character(bracket.PairA);opponent=bracket.Character(bracket.PairB);skin=bracket.Skin(bracket.PairA);opponentSkin=bracket.Skin(bracket.PairB);
            wins[0]=wins[1]=0;StartFight();
        }
        private void FinishClassicMatch(int winner)
        {
            matchWinner=winner;progress.Matches++;if(winner==0){progress.P1Wins++;profile.TotalWins++;}else{progress.P2Wins++;profile.TotalLosses++;}
            progress.MostPlayed[selected]++;progress.MostPlayed[opponent]++;Unlock("PRIMEIRO CONFRONTO");
            if(progress.BiggestCombo>=5)Unlock("CINCO ACERTOS");
            if(mode==PlayMode.Survival&&winner==0){survivalHealth=Mathf.Min(1000,World.Fighters[0].Hp+180);survivalScore+=1000+(int)World.Fighters[0].Hp+Mathf.CeilToInt(World.TimerFrames/60f)*10;survivalWave++;profile.BestSurvival=Mathf.Max(profile.BestSurvival,survivalWave);progress.SurvivalRecord=Mathf.Max(progress.SurvivalRecord,survivalScore);}
            if(classic!=null&&winner==0){classic.AdvanceVictory();if(classic.Complete){if(mode==PlayMode.Tower)profile.TowerClears++;Unlock("CAMPEÃO DA TORRE");}}
            if(bracket!=null){bracket.RecordWinner(winner);if(bracket.Complete){profile.TournamentWins++;Unlock("CAMPEÃO DO TORNEIO");}}
            SaveProfile();Arena.gameObject.SetActive(false);menuCamera.enabled=true;Page=classic?.Complete==true?ScreenPage.Ending:ScreenPage.ClassicResult;endingTime=0;
            audioBus.Play(winner==0?"voice_player_one_wins":"voice_player_two_wins");audioBus.Music(8);
        }
        private void ContinueClassic()
        {
            wins[0]=wins[1]=0;
            if(bracket!=null){Page=ScreenPage.Bracket;return;}
            if(classic!=null){opponent=classic.Opponent;if(matchWinner==0)stage=(stage+1)%8;if(mode==PlayMode.Tower){Page=ScreenPage.TowerMap;return;}}
            if(mode==PlayMode.Survival){if(matchWinner==0){opponent=(opponent+3)%14;if(opponent==selected)opponent=(opponent+1)%14;}else{survivalHealth=1000;survivalWave=survivalScore=0;opponent=(selected+1)%14;}}
            StartFight();
        }
        private void Unlock(string value)
        {
            if(Array.IndexOf(progress.Achievements,value)>=0)return;var list=new List<string>(progress.Achievements){value};progress.Achievements=list.ToArray();Notify("CONQUISTA · "+value,4);
        }
        private void ConsumeProgressEvents(InputFrame p1)
        {
            if(IsTraining&&trainingGuard)foreach(var fighter in World.Fighters)fighter.Guard=100;
            foreach(var e in World.Events)
            {
                if(!IsTraining&&!replaying&&mode!=PlayMode.Attract)
                {if(e.Type=="perfect_block")progress.PerfectBlocks++;if(e.Type=="guard_break")progress.GuardBreaks++;if(e.Type=="super")progress.Supers++;}
                if(mode!=PlayMode.Tutorial)continue;
                int step=tutorialStep;
                if(step==1&&e.Type=="hit"&&e.Player==0)tutorialHits++;
                if(e.Player==0&&((step==2&&!p1.Down&&(e.Type=="block"||e.Type=="perfect_block"||e.Type=="parry"))||(step==3&&p1.Down&&(e.Type=="block"||e.Type=="perfect_block"||e.Type=="parry"))||(step==4&&e.Type=="throw")||(step==5&&(e.Type=="special"||e.Type=="projectile"))||(step==7&&e.Type=="super")||(step==8&&e.Type=="tag")))AdvanceTutorial();
            }
            if(!IsTraining&&!replaying&&mode!=PlayMode.Attract)foreach(var fighter in World.Fighters)progress.BiggestCombo=Math.Max(progress.BiggestCombo,fighter.Combo);
            if(mode==PlayMode.Tutorial)
            {
                var a=World.Fighters[0];
                if((tutorialStep==0&&Mathf.Abs(a.X-tutorialOrigin)>130)||(tutorialStep==1&&tutorialHits>=3)||(tutorialStep==5&&a.Move!=null&&a.Move.Cost>0&&a.Move.Family!="super")||(tutorialStep==6&&a.Combo>=2))AdvanceTutorial();
            }
        }
        private void AdvanceTutorial(){if(tutorialStep>=9)return;tutorialStep++;Notify("ETAPA CONCLUÍDA",2);audioBus.Play("confirm");if(tutorialStep==6){World.Fighters[0].Combo=0;World.Fighters[0].ComboTimer=0;}if(tutorialStep==8)World.TagCooldowns[0]=0;}
        private InputFrame TutorialDummy()
        {
            if(tutorialStep!=2&&tutorialStep!=3)return default;
            float delta=World.Fighters[0].X-World.Fighters[1].X;
            return new InputFrame{Left=delta< -95,Right=delta>95,Down=tutorialStep==3&&Mathf.Abs(delta)<140,Attack=World.Tick%85==0};
        }
        private void RecordKartProgress()
        {
            var player=kart.Racers[0];int milliseconds=Mathf.RoundToInt(player.FinishTick*1000f/60f),track=kart.Track.Id;
            if(kart.Mode==KartRaceMode.TimeAttack)
            {
                if(progress.TrackRecords[track]==0||milliseconds<progress.TrackRecords[track])progress.TrackRecords[track]=milliseconds;
                if(player.BestLapMilliseconds>0&&(progress.LapRecords[track]==0||player.BestLapMilliseconds<progress.LapRecords[track]))progress.LapRecords[track]=player.BestLapMilliseconds;
            }
            if(player.Place==1)Unlock("POLE DO CAMPUS");
            if(mode==PlayMode.Tutorial){Unlock("TUTORIAL CONCLUÍDO");Notify("Tutorial concluído!",8);}
            if(kart.Mode==KartRaceMode.Championship){progress.Championship=kart.ExportChampionship();if(kart.ChampionshipComplete)Unlock("QUATRO CIRCUITOS");}
        }
        private void ResumeKartChampionship()
        {
            try
            {
                kart=KartRace.RestoreChampionship(progress.Championship);
                mode=PlayMode.Kart;kartMode=KartRaceMode.Championship;kartPlayers=kart.HumanCount;kartTrack=kart.Track.Id;
                selected=kart.Racers[0].CharacterId;skin=kart.Racers[0].Skin;
                if(kartPlayers==2){opponent=kart.Racers[1].CharacterId;opponentSkin=kart.Racers[1].Skin;}
                Arena.gameObject.SetActive(false);if(kartView!=null)Destroy(kartView.gameObject);
                kartView=new GameObject("Kart Presentation").AddComponent<KartPresentation>();kartView.HorizontalSplit=prefs.HorizontalSplit;kartView.Bind(kart);
                menuCamera.enabled=false;Page=ScreenPage.Kart;paused=false;rewardRecorded=false;accumulator=0;input.Clear();audioBus.Music(4);
            }
            catch(Exception e){Notify("Não foi possível retomar o campeonato: "+e.Message,8);}
        }
        private void NextKartRace()
        {
            if(kart.Mode!=KartRaceMode.Championship||kart.ChampionshipComplete)return;
            kart=kart.NextChampionshipRace();Destroy(kartView.gameObject);kartView=new GameObject("Kart Presentation").AddComponent<KartPresentation>();kartView.HorizontalSplit=prefs.HorizontalSplit;kartView.Bind(kart);rewardRecorded=false;accumulator=0;input.Clear();
        }
        private void RestartKartRace()
        {
            if(kart!=null&&kart.Mode==KartRaceMode.Championship&&!kart.Completed)
            {
                progress.Championship=kart.ExportChampionship();ResumeKartChampionship();
            }
            else StartKart();
        }
        private void UpdateMenus(float dt)
        {
            if(mode==PlayMode.Attract&&Page==ScreenPage.Fight&&MenuInput.Any){GoHome();idleTime=0;return;}
            if(Page==ScreenPage.Ending)endingTime+=dt;
            if(Page==ScreenPage.Home)
            {
                idleTime=MenuInput.Any?0:idleTime+dt;
                if(MenuInput.Vertical!=0)menuIndex=(menuIndex+MenuInput.Vertical+15)%15;
                if(MenuInput.Confirm)ActivateHome(menuIndex);
                if(idleTime>40&&!smoke){mode=PlayMode.Attract;classic=null;campaign=null;bracket=null;selected=3;opponent=4;wins[0]=wins[1]=0;StartFight();idleTime=0;}
            }
            else if(Page!=ScreenPage.Fight)idleTime=0;
        }
        private void ActivateHome(int index)
        {
            switch(index){case 0:ChooseMode(PlayMode.Arcade);break;case 1:ChooseMode(PlayMode.Tower);break;case 2:ChooseMode(PlayMode.VersusLocal);break;case 3:ChooseMode(PlayMode.Teams);break;case 4:ChooseMode(PlayMode.Kart);break;case 5:ChooseMode(PlayMode.Training);break;case 6:ChooseMode(PlayMode.Tutorial);break;case 7:ChooseMode(PlayMode.Tournament);break;case 8:ChooseMode(PlayMode.Survival);break;case 9:Page=ScreenPage.Gallery;break;case 10:Page=ScreenPage.Statistics;break;case 11:Page=ScreenPage.Settings;break;case 12:Page=ScreenPage.Controls;break;case 13:Page=ScreenPage.Credits;break;case 14:Application.Quit();break;}
        }
    }
}
