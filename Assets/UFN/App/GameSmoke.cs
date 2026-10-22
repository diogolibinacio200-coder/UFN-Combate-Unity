using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UFN.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UFN.App
{
    public sealed partial class GameApp
    {
        [Serializable] private sealed class RuntimeEvidence
        {
            public string Unity,GraphicsDevice;
            public int Frames,CombatTicks,KartTicks,Characters;
            public float Seconds,AverageFps;
            public bool Passed;
            public List<string> Checks=new List<string>(),Errors=new List<string>(),Screenshots=new List<string>();
        }
        private RuntimeEvidence smokeEvidence;
        private float smokeStarted;
        private void Start()
        {
            if(!smoke)return;
            smokeEvidence=new RuntimeEvidence{Unity=Application.unityVersion,GraphicsDevice=SystemInfo.graphicsDeviceName,Characters=Catalog?.Characters.Length??0};
            smokeStarted=Time.realtimeSinceStartup;
            Application.logMessageReceived+=SmokeLog;
            StartCoroutine(SmokeWatchdog());StartCoroutine(SmokeRun());
        }
        private void SmokeLog(string text,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert||(type==LogType.Warning&&text.Contains("Exception")))
                if(smokeEvidence.Errors.Count<30)smokeEvidence.Errors.Add(text+"\n"+stack);
        }
        private void SmokeCheck(bool value,string description)
        {
            smokeEvidence.Checks.Add((value?"PASS ":"FAIL ")+description);
            if(!value)smokeEvidence.Errors.Add(description);
        }
        private IEnumerator SmokeShot(string name,float delay=.35f)
        {
            yield return new WaitForSecondsRealtime(delay);
            yield return new WaitForEndOfFrame();
            File.WriteAllLines(Path.Combine(smokeDirectory,name+"-layout.txt"),UiLayoutIssues());
            var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
            if(screenshot==null){smokeEvidence.Errors.Add("Capture failed: "+name);yield break;}
            File.WriteAllBytes(Path.Combine(smokeDirectory,name+".png"),screenshot.EncodeToPNG());
            var pixels=screenshot.GetPixels32();int changed=0;var first=pixels[0];
            for(int i=0;i<pixels.Length;i+=37)if(Math.Abs(pixels[i].r-first.r)+Math.Abs(pixels[i].g-first.g)+Math.Abs(pixels[i].b-first.b)>25)changed++;
            SmokeCheck(changed>pixels.Length/3700,"Rendered image has content: "+name);
            Destroy(screenshot);smokeEvidence.Screenshots.Add(name+".png");
        }
        private IEnumerator SmokeRun()
        {
            if(Catalog==null){SmokeCheck(false,error);FinishSmoke();yield break;}
            yield return SmokeShot("01-menu",2);
            yield return SmokeKey(Key.Enter);
            SmokeCheck(Page==ScreenPage.Select&&selectionPlayer==0,"One Enter opens selection without confirming a character");
            yield return SmokeKey(Key.Enter);
            SmokeCheck(Page==ScreenPage.ArenaSelect,"Second Enter confirms the character without starting the fight");
            GoHome();
            ChooseMode(PlayMode.VersusCPU);yield return SmokeShot("02-personagens");
            // Keep the previously selected Diogo skin in the opponent selector;
            // Mirkos must still record and replay with his actual skin zero.
            selected=3;opponent=4;skin=0;opponentSkin=1;stage=0;StartFight();
            yield return SmokeShot("03-combate",6);
            SmokeCheck(World.Tick>120,"Combat simulation advances in the Windows player");smokeEvidence.CombatTicks=World.Tick;
            SaveReplay();LoadReplay();yield return new WaitForSecondsRealtime(7);
            SmokeCheck(replayEnded&&replayStatus=="verificação sincronizada","Recorded match replays to completion without a hash mismatch");
            GoHome();ChooseMode(PlayMode.Teams);selected=3;skin=1;opponent=9;stage=7;reserves[0]=4;reserves[1]=2;StartFight();
            SmokeCheck(World.TeamMode&&World.Fighters[0].Skin==1,"Teams and Diogo skin are active");
            yield return SmokeShot("04-duplas",2);
            GoHome();ChooseMode(PlayMode.Tutorial);yield return SmokeShot("05-tutorial");
            GoHome();ChooseMode(PlayMode.Training);StartFight();Page=ScreenPage.TrainingOptions;yield return SmokeShot("06-treinamento");
            GoHome();ChooseMode(PlayMode.Tower);selected=3;towerSize=5;StartClassic();yield return SmokeShot("07-torre");
            var savedTower=classic.ExportSnapshot();ChooseMode(PlayMode.Tournament);StartTournament();yield return SmokeShot("08-torneio");
            progress.Tower=savedTower;ResumeTower();SmokeCheck(bracket==null&&classic!=null&&wins[0]==0&&wins[1]==0,"Resuming a tower clears tournament and round state");
            ChooseMode(PlayMode.Evolution);selected=3;StartChosenGame();wins[0]=wins[1]=1;SaveProfile();ResumeCampaign();
            SmokeCheck(classic==null&&bracket==null&&wins[0]==0&&wins[1]==0,"Resuming Evolution clears other modes and rounds");
            GoHome();Page=ScreenPage.Gallery;galleryIndex=3;gallerySkin=1;yield return SmokeShot("09-galeria-careca");
            galleryIndex=4;gallerySkin=1;yield return SmokeShot("10-galeria-armadura");
            Page=ScreenPage.Guide;yield return SmokeShot("11-golpes");
            Page=ScreenPage.Controls;yield return SmokeShot("12-controles");
            Page=ScreenPage.Settings;for(settingsTab=0;settingsTab<3;settingsTab++)yield return SmokeShot("13-configuracoes-"+settingsTab);
            int originalTextSize=prefs.TextSize;prefs.TextSize=2;settingsTab=2;
            yield return SmokeShot("13-configuracoes-texto-grande");
            Page=ScreenPage.Gallery;yield return SmokeShot("13-galeria-texto-grande");
            prefs.TextSize=originalTextSize;
            Page=ScreenPage.Statistics;yield return SmokeShot("14-estatisticas");
            Page=ScreenPage.Credits;yield return SmokeShot("15-creditos");
            for(int track=0;track<5;track++)
            {
                GoHome();ChooseMode(PlayMode.Kart);selected=3;skin=track==0?1:0;kartTrack=track;kartPlayers=1;kartMode=KartRaceMode.Quick;StartKart();
                yield return SmokeShot("16-kart-pista-"+track,track==0?8:1.5f);
                SmokeCheck(kart.Track.Id==track&&kart.ElapsedTicks>30,"Kart track "+track+" advances");smokeEvidence.KartTicks+=kart.ElapsedTicks;
            }
            GoHome();ChooseMode(PlayMode.Kart);kartPlayers=2;opponent=2;prefs.HorizontalSplit=false;StartKart();yield return SmokeShot("17-kart-dupla-vertical",2);
            SmokeCheck(kart.HumanCount==2,"Two player split screen initialized");
            prefs.HorizontalSplit=true;StartKart();yield return SmokeShot("18-kart-dupla-horizontal",2);
            GoHome();SaveSettings();SaveProfile();SmokeCheck(File.Exists(Path.Combine(savePath,"profile.json"))&&File.Exists(Path.Combine(savePath,"settings.json")),"Player profile and preferences persist");
            FinishSmoke();
        }
        private IEnumerator SmokeKey(Key key)
        {
            if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return null;
        }
        private IEnumerator SmokeWatchdog()
        {
            yield return new WaitForSecondsRealtime(150);
            SmokeCheck(false,"Runtime smoke timed out");FinishSmoke();
        }
        private void FinishSmoke()
        {
            smokeEvidence.Frames=Time.frameCount;smokeEvidence.Seconds=Time.realtimeSinceStartup-smokeStarted;
            smokeEvidence.AverageFps=smokeEvidence.Frames/Mathf.Max(1,smokeEvidence.Seconds);smokeEvidence.Passed=smokeEvidence.Errors.Count==0;
            File.WriteAllText(Path.Combine(smokeDirectory,"runtime.json"),JsonUtility.ToJson(smokeEvidence,true));
            Application.logMessageReceived-=SmokeLog;Debug.Log("UFN_SMOKE_COMPLETE passed="+smokeEvidence.Passed);Application.Quit(smokeEvidence.Passed?0:1);
        }
    }
}
