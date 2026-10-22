using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;
using UFN.Core;
using UFN.Presentation;

namespace UFN.App
{
    public enum ScreenPage { Home, Select, ArenaSelect, Fight, Rewards, RunFinished, Kart, Settings, Guide, Credits, Setup, TowerMap, Bracket, Gallery, Statistics, Controls, TrainingOptions, ClassicResult, Ending }
    public enum PlayMode { VersusCPU, VersusLocal, Training, Tower, Survival, Tournament, Kart, Arcade, Teams, Tutorial, Evolution, Attract }

    public sealed partial class GameApp : MonoBehaviour
    {
        public GameCatalog Catalog { get; private set; }
        public CombatWorld World { get; private set; }
        public ScreenPage Page { get; private set; }=ScreenPage.Home;
        public string ContentPath { get; private set; }
        public ArenaPresentation Arena { get; private set; }
        private KartPresentation kartView;
        private KartRace kart;
        private CombatAI cpu, demoAI;
        private readonly PlayerControls input=new PlayerControls();
        private AppPreferences prefs=new AppPreferences();
        private ProfileData profile;
        private ProfileStore store;
        private CampaignRun campaign;
        private GameAudio audioBus;
        private PlayMode mode;
        private int selected=3, opponent=4, stage, skin, selectionPlayer;
        private bool paused, showBoxes, rewardRecorded, replaying, replayEnded, dummyRecording;
        private int dummyMode=1, dummyPlayback, replayIndex;
        private readonly List<InputFrame> dummyTape=new List<InputFrame>();
        private readonly Queue<string> history=new Queue<string>();
        private float accumulator, resultDelay, bannerTime, menuTime;
        private string banner="", error="", catalogHash="", replayStatus="", savePath, settingsPath;
        private RecordedMatch recorded;
        private readonly int[] wins=new int[2];
        private const float TickSeconds=1f/60f;
        private bool smoke;
        private string smokeDirectory;
        private Camera menuCamera;
        private Texture2D[] portraits, stages;
        private Texture2D[] cardImages;
        private Font font, bold;

        private sealed class ProfileCodec : IProfileCodec
        {
            public string Encode(ProfileData value)=>JsonUtility.ToJson(value,true);
            public ProfileData Decode(string json)=>JsonUtility.FromJson<ProfileData>(json);
        }
        void Awake()
        {
            Application.runInBackground=true; Application.targetFrameRate=144; QualitySettings.vSyncCount=1;
            ContentPath=Path.Combine(Application.streamingAssetsPath,"Content");
            savePath=Application.persistentDataPath;
            string[] args=Environment.GetCommandLineArgs();
            smoke=Array.IndexOf(args,"--ufn-smoke")>=0;
            int output=Array.IndexOf(args,"--ufn-output");
            smokeDirectory=output>=0&&output+1<args.Length?args[output+1]:Path.Combine(savePath,"smoke");
            if(smoke){savePath=Path.Combine(smokeDirectory,"test-profile");Directory.CreateDirectory(smokeDirectory);QualitySettings.vSyncCount=0;Application.targetFrameRate=120;}
            Directory.CreateDirectory(savePath); settingsPath=Path.Combine(savePath,"settings.json");
            try
            {
                string json=File.ReadAllText(Path.Combine(ContentPath,"catalog.json"));
                Catalog=JsonUtility.FromJson<GameCatalog>(json);
                using(var hash=SHA256.Create())catalogHash=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-","");
                if(Catalog==null||Catalog.Characters.Length!=16)throw new InvalidDataException("Catálogo de personagens incompleto.");
                store=new ProfileStore(Path.Combine(savePath,"profile.json"),new ProfileCodec());profile=store.Load();
                selected=Mathf.Clamp(profile.SelectedCharacter,0,13);skin=Mathf.Clamp(profile.SelectedSkin,0,1);
                if(File.Exists(settingsPath))prefs=JsonUtility.FromJson<AppPreferences>(File.ReadAllText(settingsPath))??new AppPreferences();
                input.Configure(prefs);LoadProgress();
                LoadInterfaceArt();
                var camObject=new GameObject("Menu Camera");menuCamera=camObject.AddComponent<Camera>();menuCamera.clearFlags=CameraClearFlags.SolidColor;menuCamera.backgroundColor=new Color(.025f,.038f,.065f);menuCamera.orthographic=true;
                camObject.AddComponent<AudioListener>();
                Arena=new GameObject("Combat Presentation").AddComponent<ArenaPresentation>();Arena.Initialize(Catalog,ContentPath);Arena.gameObject.SetActive(false);
                audioBus=gameObject.AddComponent<GameAudio>();audioBus.Initialize(Path.Combine(ContentPath,"audio"));audioBus.Subtitle+=ShowSubtitle;ApplyPreferences();audioBus.Music();
                if(!string.IsNullOrEmpty(store.RecoveryMessage))Notify(store.RecoveryMessage,8);
            }
            catch(Exception ex){error="Não foi possível iniciar: "+ex.Message;Debug.LogException(ex);}
        }
        private void LoadInterfaceArt()
        {
            font=Resources.Load<Font>("Fonts/Rajdhani-Medium");bold=Resources.Load<Font>("Fonts/Rajdhani-Bold");
            portraits=new Texture2D[16];cardImages=new Texture2D[16];
            for(int i=0;i<16;i++){portraits[i]=TextureFile(Path.Combine(ContentPath,"sprites",Catalog.Character(i).Key,"portrait.png"));cardImages[i]=portraits[i];}
            stages=new Texture2D[8];for(int i=0;i<8;i++)stages[i]=TextureFile(Path.Combine(ContentPath,"arenas",StageKeys[i]+".png"));
        }
        private static Texture2D TextureFile(string path)
        {
            if(!File.Exists(path))return null;
            var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);tex.LoadImage(File.ReadAllBytes(path));tex.filterMode=FilterMode.Bilinear;return tex;
        }
        private void ApplyPreferences()
        {
            prefs.Music=Mathf.Clamp01(prefs.Music);prefs.Effects=Mathf.Clamp01(prefs.Effects);prefs.CameraShake=Mathf.Clamp01(prefs.CameraShake);prefs.Particles=Mathf.Clamp01(prefs.Particles);
            audioBus.EffectsVolume=prefs.Effects;audioBus.MusicVolume=prefs.Music;audioBus.VoiceVolume=prefs.Voice;AudioListener.volume=prefs.Master;input.NoNumpad=prefs.NoNumpad;
            if(Arena!=null){Arena.CameraShake=prefs.CameraShake;Arena.ParticleIntensity=prefs.Particles;Arena.FlashIntensity=prefs.Flashes;Arena.ShowHitboxes=showBoxes;}
            prefs.Master=Mathf.Clamp01(prefs.Master);prefs.Voice=Mathf.Clamp01(prefs.Voice);prefs.Difficulty=Mathf.Clamp(prefs.Difficulty,0,3);
            QualitySettings.antiAliasing=prefs.Quality==0?0:prefs.Quality==1?2:4;QualitySettings.shadows=prefs.Quality==0?ShadowQuality.Disable:ShadowQuality.All;
            QualitySettings.shadowDistance=prefs.Quality==2?100:60;QualitySettings.lodBias=prefs.Quality==0?.65f:prefs.Quality==1?1:1.5f;
            if(!smoke){int index=Mathf.Clamp(prefs.Resolution,0,2);int width=new[]{1280,1600,1920}[index],height=new[]{720,900,1080}[index];if(Screen.width!=width||Screen.height!=height||Screen.fullScreen!=prefs.Fullscreen)Screen.SetResolution(width,height,prefs.Fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);}
        }
        private void SaveProfile()
        {
            if(profile==null)return;
            try{profile.SelectedCharacter=selected;profile.SelectedSkin=skin;if(campaign!=null)profile.Campaign=campaign.Export();store.Save(profile);SaveProgress();}
            catch(Exception ex){Notify("Não foi possível salvar o progresso: "+ex.Message,8);Debug.LogWarning(ex);}
        }
        private void SaveSettings()
        {
            try{File.WriteAllText(settingsPath,JsonUtility.ToJson(prefs,true));ApplyPreferences();}
            catch(Exception ex){Notify("Não foi possível salvar opções: "+ex.Message,8);}
        }
        private void Notify(string text,float seconds=3){banner=text;bannerTime=seconds;}
        void Update()
        {
            if(Catalog==null)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);menuTime+=dt;bannerTime-=dt;
            if(input.Rebinding){if(input.PollRebind())SaveSettings();return;}
            if(MenuInput.Press(Key.F11)){prefs.Fullscreen=!Screen.fullScreen;SaveSettings();}
            if(MenuInput.Pause)
            {
                if(Page==ScreenPage.Fight||Page==ScreenPage.Kart){paused=!paused;accumulator=0;input.Clear();}
                else if(Page!=ScreenPage.Home)GoHome();
            }
            else if(MenuInput.Back&&Page!=ScreenPage.Fight&&Page!=ScreenPage.Kart&&Page!=ScreenPage.Home)GoHome();
            var menuPage=Page;
            UpdateMenus(dt);
            if(Page==menuPage&&menuPage==ScreenPage.Select)SelectionKeys();
            if(Page==menuPage&&menuPage==ScreenPage.ArenaSelect)
            {
                if(mode==PlayMode.Kart)kartTrack=(kartTrack+MenuInput.Horizontal+MenuInput.Vertical*3+10)%5;else stage=(stage+MenuInput.Horizontal+MenuInput.Vertical*4+16)%8;
                if(MenuInput.Confirm)StartChosenGame();
            }
            if(Page==ScreenPage.Fight&&!paused)UpdateFight(dt);
            if(Page==ScreenPage.Kart&&!paused)UpdateKart(dt);
            if(Page==ScreenPage.Fight&&Arena!=null)Arena.Render(Mathf.Clamp01(accumulator/TickSeconds),paused?0:dt);
        }
        private void UpdateFight(float dt)
        {
            if(World==null)return;
            // A recorded KO also sets Winner. Finish playback before the normal
            // round-result branch, otherwise it waits for a new match confirmation.
            if(replaying&&recorded!=null&&replayIndex>=recorded.Frames.Count)
            {
                replayEnded=true;paused=true;Notify("Fim do replay • "+replayStatus,8);return;
            }
            if(IsTraining&&!replaying)
            {
                if(MenuInput.Press(Key.F5))StartFight();
                if(MenuInput.Press(Key.F1)){World.Training.InfiniteMeter=!World.Training.InfiniteMeter;Notify("Energia infinita: "+(World.Training.InfiniteMeter?"SIM":"NÃO"));}
                if(MenuInput.Press(Key.F2)){showBoxes=!showBoxes;Arena.ShowHitboxes=showBoxes;}
                if(MenuInput.Press(Key.F3)){dummyMode=(dummyMode+1)%6;Notify("Oponente: "+DummyLabel());}
                if(MenuInput.Press(Key.F4)){dummyRecording=!dummyRecording;if(dummyRecording){dummyTape.Clear();Notify("Gravando seus comandos para o dummy; F4 encerra",5);}else{dummyPlayback=0;dummyMode=3;Notify("Dummy repetindo "+dummyTape.Count+" comandos");}}
                if(MenuInput.Press(Key.Tab)){Page=ScreenPage.TrainingOptions;return;}
            }
            if(World.Winner>=0)
            {
                resultDelay+=dt;
                if(resultDelay>1&&(MenuInput.Confirm||mode==PlayMode.Attract))FinishFight();
                return;
            }
            input.Sample(); accumulator+=dt;int steps=0;
            while(accumulator>=TickSeconds&&steps++<8)
            {
                InputFrame p1,p2;
                if(replaying)
                {
                    if(recorded==null||replayIndex>=recorded.Frames.Count){replayEnded=true;paused=true;Notify("Fim do replay • "+replayStatus,8);break;}
                    var f=recorded.Frames[replayIndex];p1=PlayerControls.Unpack(f.P1);p2=PlayerControls.Unpack(f.P2);
                }
                else
                {
                    p1=smoke||mode==PlayMode.Attract||TournamentCpu(0)?demoAI.Decide(World,0):input.Consume(0);
                    p2=mode==PlayMode.VersusLocal||mode==PlayMode.Teams||(mode==PlayMode.Tournament&&!TournamentCpu(1))?input.Consume(1):IsTraining?DummyInput():cpu.Decide(World,1);
                    if(dummyRecording&&dummyTape.Count<1800)dummyTape.Add(p1);
                }
                World.Step(p1,p2);Arena.CaptureSimulationStep();audioBus.Consume(World.Events);
                ConsumeProgressEvents(p1);
                if(replaying)
                {
                    var frame=recorded.Frames[replayIndex++];
                    if(!string.IsNullOrEmpty(frame.Hash)&&World.StateHash().ToString("X16")!=frame.Hash)
                    {replayStatus="DIVERGÊNCIA NO PASSO "+replayIndex;Notify(replayStatus,20);paused=true;break;}
                }
                else if(recorded!=null&&recorded.Frames.Count<72000)
                {
                    recorded.Frames.Add(new RecordedFrame {P1=PlayerControls.Pack(p1),P2=PlayerControls.Pack(p2),Hash=World.Tick%60==0?World.StateHash().ToString("X16"):""});
                }
                if(World.Events.Count>0)foreach(var e in World.Events)if(e.Type=="attack"||e.Type=="hit"||e.Type=="parry"||e.Type=="super_hit")
                {if(history.Count>=6)history.Dequeue();history.Enqueue((e.Player==0?"J1 ":"J2 ")+(string.IsNullOrEmpty(e.Text)?e.Type:e.Text)+(e.Damage>0?" · "+Mathf.RoundToInt(e.Damage):""));}
                accumulator-=TickSeconds;
                if(World.Winner>=0)break;
            }
            if(accumulator>TickSeconds*8)accumulator=TickSeconds*8;
        }
        private InputFrame DummyInput()
        {
            if(dummyMode==1)return new InputFrame{Block=true};
            if(dummyMode==2)return cpu.Decide(World,1);
            if(dummyMode==3&&dummyTape.Count>0)return dummyTape[dummyPlayback++%dummyTape.Count];
            if(dummyMode==4)return new InputFrame{Block=true,Down=true};
            if(dummyMode==5)return input.Consume(1);
            return mode==PlayMode.Tutorial?TutorialDummy():default;
        }
        private string DummyLabel()=>new[]{"Parado","Defendendo","CPU","Comandos gravados","Defesa baixa","Jogador 2"}[dummyMode];
        private void UpdateKart(float dt)
        {
            input.Sample(true);
            if(input.ConsumeKartRecover(0)||MenuInput.Press(Key.F2))kart.Recover(0);
            if(kart.HumanCount==2&&(input.ConsumeKartRecover(1)||MenuInput.Press(Key.Backspace)))kart.Recover(1);
            accumulator+=dt;
            while(accumulator>=TickSeconds)
            {
                var command=smoke?kart.GetAIInput(0):input.ConsumeKart(0,prefs.AutoAccelerate);
                kart.Step(command,smoke&&kart.HumanCount==2?kart.GetAIInput(1):input.ConsumeKart(1,prefs.AutoAccelerate));accumulator-=TickSeconds;
            }
            kartView.UpdateView();
            if(kart.Completed&&!rewardRecorded){rewardRecorded=true;profile.RecordKartResult(kart);RecordKartProgress();SaveProfile();}
        }
        private void ChooseMode(PlayMode value)
        {
            mode=value;selectionPlayer=0;campaign=null;classic=null;bracket=null;wins[0]=wins[1]=0;replaying=false;paused=false;survivalHealth=1000;survivalWave=survivalScore=0;Page=(value==PlayMode.Tower||value==PlayMode.Kart||value==PlayMode.Tournament)?ScreenPage.Setup:ScreenPage.Select;audioBus.Play("confirm");
            if(value==PlayMode.Tutorial){selected=3;opponent=0;dummyMode=0;tutorialStep=tutorialHits=0;StartFight();}
        }
        private void SelectionKeys()
        {
            SetSelectionId((SelectionId+MenuInput.Horizontal+MenuInput.Vertical*7+28)%14);
            if(MenuInput.Press(Key.Tab))ToggleSelectionSkin();
            if(MenuInput.Confirm)ConfirmCharacter();
        }
        private void ConfirmCharacter()
        {
            audioBus.Play("confirm");
            if(mode==PlayMode.Teams&&selectionPlayer>=2&&SelectionId==(selectionPlayer==2?selected:opponent)){Notify("Escolha um parceiro diferente.");return;}
            int count=mode==PlayMode.Teams?4:(mode==PlayMode.VersusLocal||mode==PlayMode.Training||mode==PlayMode.VersusCPU||(mode==PlayMode.Kart&&kartPlayers==2)?2:1);
            if(++selectionPlayer<count)return;
            Page=ScreenPage.ArenaSelect;
        }
        private void StartChosenGame()
        {
            if(mode==PlayMode.Kart){StartKart();return;}
            if(mode==PlayMode.Tower||mode==PlayMode.Arcade){StartClassic();return;}
            if(mode==PlayMode.Evolution){campaign=CampaignRun.Create(CampaignMode.Tower,selected,unchecked((int)NewSeed()));opponent=campaign.OpponentId;}
            if(mode==PlayMode.Survival)opponent=(selected+1)%14;
            wins[0]=wins[1]=0;StartFight();SaveProfile();
        }
        private static uint NewSeed()=>unchecked((uint)DateTime.UtcNow.Ticks)|1u;
        private void StartFight()
        {
            if(kartView!=null){Destroy(kartView.gameObject);kartView=null;}
            uint seed=smoke?7331u:NewSeed();
            int activeDifficulty=Mathf.Clamp(classic!=null?classic.Difficulty:campaign==null?prefs.Difficulty:campaign.Difficulty,0,3);
            World=new CombatWorld(Catalog,selected,opponent,seed,activeDifficulty);
            if(mode==PlayMode.Teams||mode==PlayMode.Tutorial){World.SetupTeams(reserves[0],reserves[1]);World.Reserve(0).Skin=reserveSkins[0];World.Reserve(1).Skin=reserveSkins[1];}
            World.Fighters[0].Skin=selected==3?skin:0;World.Fighters[1].Skin=opponent==3?opponentSkin:0;
            World.Training.Enabled=IsTraining;World.Training.InfiniteHealth=IsTraining&&trainingHealth;World.Training.InfiniteMeter=IsTraining&&trainingMeter;
            if(mode==PlayMode.Survival)World.Fighters[0].Hp=survivalHealth;
            if(campaign!=null){World.Fighters[0].MaxHp*=campaign.HealthMultiplier;World.Fighters[0].Hp=World.Fighters[0].MaxHp;World.Fighters[0].DamageMultiplier=campaign.DamageMultiplier;World.Fighters[0].Meter=campaign.MeterBonus;}
            cpu=new CombatAI(seed^49291u,Mathf.Clamp(classic!=null?classic.Difficulty:campaign==null?prefs.Difficulty:campaign.Difficulty,0,3));demoAI=new CombatAI(seed^120u,prefs.Difficulty);
            recorded=new RecordedMatch{Player1=selected,Player2=opponent,Stage=stage,Skin=skin,Seed=seed,Difficulty=activeDifficulty,CatalogHash=catalogHash,
                HealthMultiplier=campaign?.HealthMultiplier??1,DamageMultiplier=campaign?.DamageMultiplier??1,StartingMeter=campaign?.MeterBonus??0,StartingHp=World.Fighters[0].Hp,SecondSkin=opponentSkin,Teams=mode==PlayMode.Teams,Reserve1=reserves[0],Reserve2=reserves[1],ReserveSkin1=reserveSkins[0],ReserveSkin2=reserveSkins[1]};
            replaying=false;replayEnded=false;accumulator=0;resultDelay=0;paused=false;rewardRecorded=false;history.Clear();input.Clear();
            Arena.gameObject.SetActive(true);Arena.SetWorld(World,stage);menuCamera.enabled=false;Page=ScreenPage.Fight;audioBus.Music(stage);tutorialOrigin=World.Fighters[0].X;ApplyPreferences();if(!IsTraining)audioBus.Play(wins[0]+wins[1]==0?"voice_round_one":wins[0]==1&&wins[1]==1?"voice_final_round":"voice_round_two");
        }
        private void StartKart()
        {
            Arena.gameObject.SetActive(false);if(kartView!=null)Destroy(kartView.gameObject);
            kart=new KartRace(smoke?7331u:NewSeed(),selected,kartMode==KartRaceMode.Championship?0:kartTrack,kartMode,kartPlayers==2?opponent:-1,skin,opponentSkin,prefs.Difficulty);
            kartView=new GameObject("Kart Presentation").AddComponent<KartPresentation>();kartView.CharacterPortraits=portraits;kartView.HorizontalSplit=prefs.HorizontalSplit;kartView.Bind(kart);
            menuCamera.enabled=false;Page=ScreenPage.Kart;paused=false;rewardRecorded=false;accumulator=0;audioBus.Music(4);
        }
        private void FinishFight()
        {
            if(World==null||World.Winner<0)return;
            if(replaying){GoHome();return;}
            if(IsTraining){StartFight();return;}
            int winner=World.Winner;
            if(winner==2){SaveReplay();StartFight();Notify("EMPATE · ROUND REINICIADO");return;}
            if(winner<2)wins[winner]++;
            SaveReplay();
            bool single=mode==PlayMode.Survival;
            if(!single&&wins[0]<2&&wins[1]<2){StartFight();return;}
            if(mode==PlayMode.Attract){selected=(selected+5)%14;opponent=(opponent+3)%14;wins[0]=wins[1]=0;StartFight();return;}
            if(mode!=PlayMode.Evolution){FinishClassicMatch(winner);return;}
            if(!rewardRecorded){rewardRecorded=true;bool won=winner==0;if(campaign!=null){if(won)campaign.RecordVictory();else campaign.RecordDefeat();profile.RecordCombatResult(campaign,won);}else{if(won)profile.TotalWins++;else profile.TotalLosses++;}SaveProfile();}
            if(campaign!=null&&campaign.AwaitingBenefit){Page=ScreenPage.Rewards;Arena.gameObject.SetActive(false);menuCamera.enabled=true;}
            else if(campaign!=null&&!campaign.Complete){opponent=campaign.OpponentId;stage=(stage+1)%8;wins[0]=wins[1]=0;StartFight();}
            else{Page=ScreenPage.RunFinished;Arena.gameObject.SetActive(false);menuCamera.enabled=true;}
        }
        private void ChooseReward(int index)
        {
            campaign.ChooseBenefit(index);SaveProfile();opponent=campaign.OpponentId;stage=(stage+1)%8;wins[0]=wins[1]=0;StartFight();
        }
        private void ResumeCampaign()
        {
            try{campaign=CampaignRun.Restore(profile.Campaign);classic=null;bracket=null;wins[0]=wins[1]=0;selected=campaign.CharacterId;mode=PlayMode.Evolution;
                if(campaign.AwaitingBenefit){Page=ScreenPage.Rewards;return;}if(campaign.Complete){Page=ScreenPage.RunFinished;return;}opponent=campaign.OpponentId;stage=campaign.Stage%8;StartFight();}
            catch(Exception ex){Notify("Campanha não pôde ser retomada: "+ex.Message,7);}
        }
        private void SaveReplay()
        {
            if(replaying||recorded==null||recorded.Frames.Count==0||IsTraining||mode==PlayMode.Attract)return;
            try{File.WriteAllText(Path.Combine(savePath,"last-match.json"),JsonUtility.ToJson(recorded));}
            catch(Exception ex){Notify("Replay não foi salvo: "+ex.Message,5);}
        }
        private void LoadReplay()
        {
            try
            {
                var tape=JsonUtility.FromJson<RecordedMatch>(File.ReadAllText(Path.Combine(savePath,"last-match.json")));
                if(tape==null||tape.Version!=1||tape.CatalogHash!=catalogHash||tape.Frames==null||tape.Frames.Count==0||tape.Frames.Count>72000)throw new InvalidDataException("Replay de versão incompatível.");
                Catalog.Character(tape.Player1);Catalog.Character(tape.Player2);
                if(tape.Teams){Catalog.Character(tape.Reserve1);Catalog.Character(tape.Reserve2);}
                if(tape.Stage<0||tape.Stage>=StageKeys.Length||tape.Difficulty<0||tape.Difficulty>3||
                    tape.Frames.Exists(f=>f==null||f.P1<0||f.P1>2047||f.P2<0||f.P2>2047)||
                    float.IsNaN(tape.HealthMultiplier)||float.IsInfinity(tape.HealthMultiplier)||tape.HealthMultiplier<=0||
                    float.IsNaN(tape.DamageMultiplier)||float.IsInfinity(tape.DamageMultiplier)||tape.DamageMultiplier<=0||
                    float.IsNaN(tape.StartingHp)||float.IsInfinity(tape.StartingHp)||tape.StartingHp<=0||
                    float.IsNaN(tape.StartingMeter)||float.IsInfinity(tape.StartingMeter)||tape.StartingMeter<0||tape.StartingMeter>100)
                    throw new InvalidDataException("Replay contém um estado inicial inválido.");
                selected=tape.Player1;opponent=tape.Player2;stage=tape.Stage;campaign=null;classic=null;bracket=null;mode=PlayMode.VersusCPU;StartFight();recorded=tape;
                World=new CombatWorld(Catalog,tape.Player1,tape.Player2,tape.Seed,tape.Difficulty);if(tape.Teams){World.SetupTeams(tape.Reserve1,tape.Reserve2);World.Reserve(0).Skin=tape.ReserveSkin1;World.Reserve(1).Skin=tape.ReserveSkin2;}World.Fighters[0].Skin=tape.Player1==3?tape.Skin:0;World.Fighters[1].Skin=tape.Player2==3?tape.SecondSkin:0;
                World.Training.Enabled=false;World.Training.InfiniteHealth=false;World.Training.InfiniteMeter=false;
                World.Fighters[0].MaxHp*=tape.HealthMultiplier;World.Fighters[0].Hp=tape.StartingHp;World.Fighters[0].DamageMultiplier=tape.DamageMultiplier;World.Fighters[0].Meter=tape.StartingMeter;
                Arena.SetWorld(World,stage);replayIndex=0;replaying=true;replayStatus="verificação sincronizada";
            }
            catch(Exception ex){Notify("Não foi possível abrir o replay: "+ex.Message,8);}
        }
        private void GoHome()
        {
            if(Page==ScreenPage.Fight&&!replaying)SaveReplay();SaveProfile();Page=ScreenPage.Home;paused=false;replaying=false;accumulator=0;
            if(Arena!=null)Arena.gameObject.SetActive(false);if(kartView!=null){Destroy(kartView.gameObject);kartView=null;}
            menuCamera.enabled=true;audioBus.Music();input.Clear();
        }
        void OnApplicationFocus(bool focus){if(!focus&&!smoke&&(Page==ScreenPage.Fight||Page==ScreenPage.Kart)){paused=true;accumulator=0;input.Clear();}}
        void OnApplicationQuit(){if(!smoke){SaveProfile();SaveSettings();}}
        void OnDestroy(){DisposeHud();if(portraits!=null)foreach(var p in portraits)if(p!=null)Destroy(p);if(stages!=null)foreach(var p in stages)if(p!=null)Destroy(p);}
    }
}
