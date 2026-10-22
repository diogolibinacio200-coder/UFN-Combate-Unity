using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UFN.Core;

namespace UFN.App
{
    public sealed partial class GameApp
    {
        // The Godot profile is read only. A complete original copy is retained in the new profile.
        private void ImportLegacyProfile()
        {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Godot","app_userdata","UFN COMBATE","profile.json");
            if(!File.Exists(path))return;
            try
            {
                string json=File.ReadAllText(path);if(json.Length>1024*1024)return;
                var old=LegacyJson.Parse(json) as Dictionary<string,object>;if(old==null)return;
                var stats=Section(old,"stats");var options=Section(old,"settings");var saved=Section(old,"progress");var records=Section(saved,"records");
                progress.MigratedOriginalJson=json;progress.Matches=Whole(stats,"matches");progress.P1Wins=Whole(stats,"p1_wins");progress.P2Wins=Whole(stats,"p2_wins");
                progress.BiggestCombo=Whole(stats,"biggest_combo");progress.PerfectBlocks=Whole(stats,"perfect_blocks");progress.GuardBreaks=Whole(stats,"guard_breaks");progress.Supers=Whole(stats,"supers");progress.SurvivalRecord=Whole(records,"survival");
                for(int track=0;track<5;track++)if(records.TryGetValue("tempo_"+track,out var time)&&time is double seconds&&seconds>0&&!double.IsNaN(seconds)&&!double.IsInfinity(seconds))
                {
                    int milliseconds=(int)Math.Min(int.MaxValue,Math.Round(seconds*1000));progress.TrackRecords[track]=milliseconds;
                    if(profile.BestKartMilliseconds==0||milliseconds<profile.BestKartMilliseconds)profile.BestKartMilliseconds=milliseconds;
                }
                var championship=Section(saved,"championship");
                if(championship.TryGetValue("choices",out var choicesValue)&&choicesValue is List<object> choices&&choices.Count==2)
                {
                    try
                    {
                        var snapshot=new KartChampionshipSnapshot{Seed=606,Track=Whole(championship,"track"),Player=Convert.ToInt32(choices[0]),SecondPlayer=Whole(championship,"humans")==2?Convert.ToInt32(choices[1]):-1,Difficulty=Mathf.Clamp(Whole(options,"difficulty"),0,2)};
                        if(championship.TryGetValue("skins",out var skinsValue)&&skinsValue is List<object> skins&&skins.Count==2){snapshot.PlayerSkin=Convert.ToInt32(skins[0]);snapshot.SecondSkin=Convert.ToInt32(skins[1]);}
                        var points=Section(championship,"points");for(int p=0;p<8;p++)snapshot.Points[p]=Whole(points,p.ToString());
                        progress.Championship=KartRace.RestoreChampionship(snapshot).ExportChampionship();
                    }
                    catch(Exception e){Debug.LogWarning("Campeonato antigo preservado, mas inválido para retomar: "+e.Message);}
                }
                var most=Section(stats,"most_played");for(int i=0;i<16;i++)progress.MostPlayed[i]=Whole(most,i.ToString());
                profile.TotalWins=Math.Max(profile.TotalWins,progress.P1Wins);profile.TotalLosses=Math.Max(profile.TotalLosses,progress.P2Wins);
                if(saved.TryGetValue("achievements",out var achievements)&&achievements is List<object> items){var names=new List<string>();foreach(var value in items)if(value is string name)names.Add(name);progress.Achievements=names.ToArray();}
                var tower=Section(saved,"tower");
                if(tower.TryGetValue("order",out var orderValue)&&orderValue is List<object> order)
                {
                    var ids=new int[order.Count];for(int i=0;i<ids.Length;i++)ids[i]=Convert.ToInt32(order[i]);
                    try{progress.Tower=ClassicRun.Restore(new ClassicSnapshot{Tower=true,Character=Whole(tower,"character"),Skin=Whole(tower,"skin"),Difficulty=Whole(tower,"difficulty"),Count=ids.Length-2,Index=Whole(tower,"step"),Order=ids}).ExportSnapshot();}catch(Exception e){Debug.LogWarning("Torre antiga preservada, mas inválida para retomar: "+e.Message);}
                }
                if(!File.Exists(settingsPath))
                {
                    prefs.Master=Fraction(options,"master",prefs.Master);prefs.Music=Fraction(options,"music",prefs.Music);prefs.Effects=Fraction(options,"sfx",prefs.Effects);prefs.Voice=Fraction(options,"voice",prefs.Voice);
                    prefs.CameraShake=Fraction(options,"shake",prefs.CameraShake);prefs.Flashes=Fraction(options,"flashes",prefs.Flashes);prefs.Particles=Fraction(options,"particles",prefs.Particles);
                    prefs.Difficulty=Mathf.Clamp(Whole(options,"difficulty"),0,3);prefs.Resolution=Mathf.Clamp(Whole(options,"resolution"),0,2);prefs.Quality=Mathf.Clamp(Whole(options,"quality"),0,2);prefs.TextSize=Mathf.Clamp(Whole(options,"text_size"),0,2);
                    prefs.Fullscreen=Flag(options,"fullscreen",true);prefs.Subtitles=Flag(options,"subtitles",true);prefs.AutoAccelerate=Flag(options,"auto_accelerate",false);prefs.HorizontalSplit=Flag(options,"split_horizontal",false);
                    ImportLegacyBindings(Section(old,"bindings"));
                    File.WriteAllText(settingsPath,JsonUtility.ToJson(prefs,true));
                }
                SaveProgress();Notify("Progresso da versão 0.6 importado. O arquivo original foi preservado.",9);
            }
            catch(Exception e){Debug.LogWarning("Importação do perfil antigo não concluída: "+e.Message);}
        }
        private static Dictionary<string,object> Section(Dictionary<string,object> source,string key)=>source.TryGetValue(key,out var value)&&value is Dictionary<string,object> section?section:new Dictionary<string,object>();
        private static int Whole(Dictionary<string,object> source,string key)=>source.TryGetValue(key,out var value)&&value is double number?Math.Max(0,(int)Math.Min(int.MaxValue,number)):0;
        private static float Fraction(Dictionary<string,object> source,string key,float fallback)=>source.TryGetValue(key,out var value)&&value is double number?Mathf.Clamp01((float)number):fallback;
        private static bool Flag(Dictionary<string,object> source,string key,bool fallback)=>source.TryGetValue(key,out var value)&&value is bool flag?flag:fallback;
        private void ImportLegacyBindings(Dictionary<string,object> bindings)
        {
            string[] actions={"left","right","up","down","attack","heavy","kick","block","dodge"};
            var pads=Section(bindings,"gamepads");
            var kartBindings=Section(bindings,"kart");
            var kartKeys=kartBindings.TryGetValue("keys",out var savedKeys)?savedKeys as List<object>:null;
            var kartButtons=kartBindings.TryGetValue("buttons",out var savedButtons)?savedButtons as List<object>:null;
            string[] kartActions={"left","right","attack","heavy","kick","block","down"};
            string[] buttonNames={"buttonSouth","buttonEast","buttonWest","buttonNorth","select","","start","leftStickPress","rightStickPress","leftShoulder","rightShoulder","dpad/up","dpad/down","dpad/left","dpad/right"};
            for(int p=0;p<2;p++)
            {
                var map=Section(bindings,p.ToString());var pad=Section(pads,p.ToString());var buttons=Section(pad,"buttons");
                for(int a=0;a<actions.Length;a++)
                {
                    string key=LegacyKey(Whole(map,actions[a]));if(key!=null)prefs.Controls[p].Keys[a]=key;
                    if(buttons.ContainsKey(actions[a])){int n=Whole(buttons,actions[a]);if(n<buttonNames.Length&&buttonNames[n]!="")prefs.Controls[p].Buttons[a]=buttonNames[n];}
                }
                if(pad.ContainsKey("device"))prefs.Controls[p].PadIndex=Whole(pad,"device");
                prefs.Controls[p].Deadzone=Mathf.Clamp(Fraction(pad,"deadzone",prefs.Controls[p].Deadzone),.1f,.9f);
                prefs.Controls[p].InvertHorizontal=Flag(pad,"invert_horizontal",false);prefs.Controls[p].InvertVertical=Flag(pad,"invert_vertical",false);
                if(pad.ContainsKey("horizontal"))prefs.Controls[p].HorizontalAxis=Mathf.Clamp(Whole(pad,"horizontal"),0,5);
                if(pad.ContainsKey("vertical"))prefs.Controls[p].VerticalAxis=Mathf.Clamp(Whole(pad,"vertical"),0,5);
                var kartKeyMap=kartKeys!=null&&kartKeys.Count>p?kartKeys[p] as Dictionary<string,object>:null;
                var kartButtonMap=kartButtons!=null&&kartButtons.Count>p?kartButtons[p] as Dictionary<string,object>:null;
                for(int action=0;action<kartActions.Length;action++)
                {
                    if(kartKeyMap!=null){string key=LegacyKey(Whole(kartKeyMap,kartActions[action]));if(key!=null)prefs.Controls[p].KartKeys[action]=key;}
                    if(kartButtonMap!=null&&kartButtonMap.ContainsKey(kartActions[action])){int n=Whole(kartButtonMap,kartActions[action]);if(n<buttonNames.Length&&buttonNames[n]!="")prefs.Controls[p].KartButtons[action]=buttonNames[n];}
                }
                if(bindings.TryGetValue("devices",out var deviceValue)&&deviceValue is List<object> devices&&devices.Count>p)prefs.Controls[p].DeviceMode=(string)devices[p]=="keyboard"?1:(string)devices[p]=="pad"?2:0;
            }
            input.Configure(prefs);
        }
        private static string LegacyKey(int code)
        {
            if(code>=65&&code<=90)return ((char)code).ToString();if(code>=48&&code<=57)return "Digit"+(char)code;
            // Godot special keys use the 1<<22 mask; ordinary physical letters use Unicode values.
            switch(code-(1<<22)){case 15:return "LeftArrow";case 16:return "UpArrow";case 17:return "RightArrow";case 18:return "DownArrow";case 21:return "LeftShift";case 22:return "LeftCtrl";case 24:return "LeftAlt";}
            if(code==32)return "Space";
            if(code>=(1<<22)+134&&code<=(1<<22)+143)return "Numpad"+(code-((1<<22)+134));
            return null;
        }
    }
}
