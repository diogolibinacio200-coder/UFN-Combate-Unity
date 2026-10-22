using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UFN.Core;

namespace UFN.App
{
    public sealed partial class GameApp
    {
        private static readonly string[] StageKeys={"patio","container","rua","entrada","quadra","laboratorio","corredor","vilabelga"};
        private static readonly string[] StageNames={"PÁTIO DA UFN","ESPAÇO CONTAINER","RUA DE SANTA MARIA","ENTRADA DA UFN","QUADRA UNIVERSITÁRIA","LABORATÓRIO TECNOLÓGICO","CORREDOR UNIVERSITÁRIO","VILA BELGA"};
        private readonly Color ink=new Color(.027f,.041f,.072f), panel=new Color(.035f,.061f,.105f,.96f), muted=new Color(.57f,.66f,.76f);
        private readonly Color cyan=new Color(.29f,.9f,.94f), gold=new Color(1,.7f,.34f), white=new Color(.93f,.96f,1);
        private int menuIndex;
        private UIDocument hudDocument;
        private PanelSettings hudPanel;
        private VisualElement hudRoot;
        private readonly Dictionary<string,HudNode> hudNodes=new Dictionary<string,HudNode>();
        private readonly List<VisualElement> hudOrder=new List<VisualElement>();
        private readonly List<VisualElement> previousHudOrder=new List<VisualElement>();
        private readonly HashSet<string> clickedControls=new HashSet<string>();
        private int hudCursor,hudFrame;
        private ScreenPage hudPage;
        private readonly List<HudControl> hudControls=new List<HudControl>();
        private string focusedControl;
        private bool showMenuFocus;
        private string subtitleText="";
        private float subtitleUntil;
        private sealed class HudControl
        {
            public string Key;
            public Rect Rect;
            public Slider Slider;
        }
        private sealed class HudNode
        {
            public VisualElement Element;
            public Rect Rect;
            public Color Color;
            public string Text;
            public int Size,Seen;
            public bool Visible,Strong,Changed;
            public float Value;
            public TextAnchor Anchor;
        }
        private void EnsureHud()
        {
            if(hudDocument!=null)return;
            hudPanel=ScriptableObject.CreateInstance<PanelSettings>();hudPanel.name="UFN retained UI panel";
            hudPanel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("UFNTheme");
            hudPanel.scaleMode=PanelScaleMode.ConstantPixelSize;hudPanel.sortingOrder=100;
            hudDocument=gameObject.AddComponent<UIDocument>();hudDocument.panelSettings=hudPanel;hudDocument.sortingOrder=100;
            hudRoot=new VisualElement{name="UFN-1280x720"};hudRoot.pickingMode=PickingMode.Ignore;
            hudRoot.style.position=Position.Absolute;hudRoot.style.width=1280;hudRoot.style.height=720;
            hudRoot.style.transformOrigin=new TransformOrigin(Length.Percent(0),Length.Percent(0),0);
            hudDocument.rootVisualElement.pickingMode=PickingMode.Ignore;hudDocument.rootVisualElement.Add(hudRoot);
            hudPage=Page;
        }
        private void LateUpdate()
        {
            EnsureHud();
            if(hudPage!=Page){hudPage=Page;clickedControls.Clear();focusedControl=null;hudControls.Clear();}
            NavigateHud();
            hudCursor=0;hudFrame++;hudOrder.Clear();hudControls.Clear();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            hudRoot.style.left=(Screen.width-1280*scale)*.5f;hudRoot.style.top=(Screen.height-720*scale)*.5f;hudRoot.style.scale=new Scale(new Vector3(scale,scale,1));
            if(!string.IsNullOrEmpty(error))
            {
                RectFill(new Rect(0,0,1280,720),ink);Label(new Rect(80,100,1100,60),"UFN COMBATE",48,cyan,true);Label(new Rect(80,210,1100,260),error,24,white);if(Button(new Rect(80,560,230,55),"FECHAR"))Application.Quit();EndHudFrame();return;
            }
            if(Catalog==null){EndHudFrame();return;}
            if(Page!=ScreenPage.Fight&&Page!=ScreenPage.Kart)Background();
            if(!ExtraUI())switch(Page)
            {
                case ScreenPage.Home:HomeUI();break;
                case ScreenPage.Select:SelectionUI();break;
                case ScreenPage.ArenaSelect:ArenaSelectUI();break;
                case ScreenPage.Fight:FightUI();break;
                case ScreenPage.Rewards:RewardsUI();break;
                case ScreenPage.RunFinished:FinishUI();break;
                case ScreenPage.Settings:SettingsUI();break;
                case ScreenPage.Guide:GuideUI();break;
                case ScreenPage.Credits:CreditsUI();break;
                case ScreenPage.Kart:KartUI();break;
            }
            if(paused&&(Page==ScreenPage.Fight||Page==ScreenPage.Kart)){hudControls.Clear();PauseUI();}
            if(prefs.Subtitles&&!paused&&Time.unscaledTime<subtitleUntil&&(Page==ScreenPage.Fight||Page==ScreenPage.ClassicResult||Page==ScreenPage.RunFinished||Page==ScreenPage.Ending))
            {
                float y=Page==ScreenPage.Fight?(IsTraining?576:634):650;
                RectFill(new Rect(360,y,560,39),new Color(.015f,.025f,.045f,.94f));Label(new Rect(372,y+4,536,31),subtitleText,21,white,true,TextAnchor.MiddleCenter);
            }
            if(bannerTime>0){RectFill(new Rect(300,657,680,43),new Color(.04f,.1f,.15f,.97f));Label(new Rect(316,663,648,30),banner,19,cyan,false,TextAnchor.MiddleCenter);}
            EndHudFrame();
        }
        private void EndHudFrame()
        {
            if(hudControls.Count>0&&!hudControls.Exists(c=>c.Key==focusedControl))focusedControl=(hudControls.Find(c=>c.Rect.y>150)??hudControls[0]).Key;
            if(showMenuFocus&&UsesHudNavigation())
            {
                var focus=hudControls.Find(c=>c.Key==focusedControl);
                if(focus!=null)Border(new Rect(focus.Rect.x-3,focus.Rect.y-3,focus.Rect.width+6,focus.Rect.height+6),gold,2);
            }
            foreach(var item in hudNodes.Values){bool visible=item.Seen==hudFrame;if(visible!=item.Visible){item.Element.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;item.Visible=visible;}}
            bool reordered=hudOrder.Count!=previousHudOrder.Count;
            if(!reordered)for(int i=0;i<hudOrder.Count;i++)if(hudOrder[i]!=previousHudOrder[i]){reordered=true;break;}
            if(reordered){foreach(var element in hudOrder)element.BringToFront();previousHudOrder.Clear();previousHudOrder.AddRange(hudOrder);}
            clickedControls.Clear();
        }
        private HudNode HudElement<T>(string kind,Rect rect) where T:VisualElement,new()
        {
            string key=kind+":"+hudCursor++;
            if(!hudNodes.TryGetValue(key,out var item))
            {
                var element=new T{name=key};element.style.position=Position.Absolute;element.pickingMode=PickingMode.Ignore;element.focusable=false;
                element.style.marginLeft=element.style.marginRight=element.style.marginTop=element.style.marginBottom=0;
                element.style.paddingLeft=element.style.paddingRight=element.style.paddingTop=element.style.paddingBottom=0;
                item=new HudNode{Element=element,Rect=new Rect(-10000,-10000,0,0)};hudNodes.Add(key,item);hudRoot.Add(element);
            }
            item.Seen=hudFrame;hudOrder.Add(item.Element);
            if(item.Rect!=rect){item.Rect=rect;item.Element.style.left=rect.x;item.Element.style.top=rect.y;item.Element.style.width=rect.width;item.Element.style.height=rect.height;}
            return item;
        }
        private bool UsesHudNavigation()=>!input.Rebinding&&((Page!=ScreenPage.Home&&Page!=ScreenPage.Select&&Page!=ScreenPage.ArenaSelect&&Page!=ScreenPage.Fight&&Page!=ScreenPage.Kart)||paused||(Page==ScreenPage.Kart&&kart!=null&&kart.Completed));
        private void NavigateHud()
        {
            if(Mouse.current?.delta.ReadValue().sqrMagnitude>1)showMenuFocus=false;
            if(!UsesHudNavigation()||hudControls.Count==0)return;
            int current=hudControls.FindIndex(c=>c.Key==focusedControl);if(current<0)current=0;
            int horizontal=MenuInput.Horizontal,vertical=MenuInput.Vertical;
            if(MenuInput.Press(Key.Tab))
            {
                current=(current+1)%hudControls.Count;focusedControl=hudControls[current].Key;showMenuFocus=true;
            }
            else if(horizontal!=0||vertical!=0)
            {
                var active=hudControls[current];
                if(active.Slider!=null&&horizontal!=0)active.Slider.value=Mathf.Clamp01(active.Slider.value+horizontal*.05f);
                else
                {
                    Vector2 direction=new Vector2(horizontal,vertical),origin=active.Rect.center;float score=float.MaxValue;int candidate=current;
                    for(int i=0;i<hudControls.Count;i++)
                    {
                        if(i==current)continue;Vector2 delta=hudControls[i].Rect.center-origin;float along=Vector2.Dot(delta,direction);if(along<=1)continue;
                        float across=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);float distance=along+across*3.5f;
                        if(distance<score){score=distance;candidate=i;}
                    }
                    current=candidate;
                }
                focusedControl=hudControls[current].Key;showMenuFocus=true;
            }
            if(MenuInput.Confirm&&hudControls[current].Slider==null){clickedControls.Add(hudControls[current].Key);showMenuFocus=true;}
        }
        private void RegisterControl(string key,Rect rect,Slider slider=null)
        {
            hudControls.Add(new HudControl{Key=key,Rect=rect,Slider=slider});
        }
        private void ModalBlocker(Rect rect,Color color)
        {
            var item=HudElement<VisualElement>("modal",rect);item.Element.pickingMode=PickingMode.Position;item.Element.style.backgroundColor=color;
        }
        private void DisposeHud(){galleryLibrary?.Dispose();galleryLibrary=null;if(hudPanel!=null)Destroy(hudPanel);}
        private void ShowSubtitle(string text,float seconds){subtitleText=text;subtitleUntil=Time.unscaledTime+Mathf.Clamp(seconds,1.2f,5);}
        public string[] UiLayoutIssues()
        {
            var issues=new List<string>();
            if(hudPanel==null||hudPanel.themeStyleSheet==null)issues.Add("Missing runtime UI theme");
            foreach(var node in hudNodes.Values)
            {
                if(!node.Visible||!(node.Element is TextElement text)||string.IsNullOrWhiteSpace(text.text))continue;
                Vector2 measured=text.MeasureTextSize(text.text,node.Rect.width,VisualElement.MeasureMode.AtMost,0,VisualElement.MeasureMode.Undefined);
                if(measured.y>node.Rect.height+3)issues.Add(node.Element.name+": "+text.text.Replace('\n',' ')+" needs "+measured.y.ToString("0")+"px; has "+node.Rect.height.ToString("0"));
            }
            return issues.ToArray();
        }
        private void Background()
        {
            RectFill(new Rect(0,0,1280,720),ink);
            if(stages!=null&&stages[stage]!=null){Image(new Rect(290+Mathf.Sin(menuTime*.08f)*12,-12,1120,746),stages[stage],ScaleMode.ScaleAndCrop,new Color(.45f,.61f,.78f,.36f));}
            for(int i=0;i<36;i++)RectFill(new Rect(i*36,0,38,720),new Color(.015f,.03f,.057f,Mathf.Lerp(.98f,.38f,i/35f)));
            RectFill(new Rect(0,0,1280,7),cyan);RectFill(new Rect(0,689,1280,31),new Color(.017f,.03f,.05f,.95f));
            Label(new Rect(35,692,880,24),"UFN COMBATE   /   NEXT 0.7   /   UNITY + C#",16,muted);
            Label(new Rect(950,692,292,24),"DIOGO LIBERALESSO INÁCIO",16,muted,false,TextAnchor.MiddleRight);
        }
        private void Header(string kicker,string title,string subtitle="")
        {
            Label(new Rect(42,30,1000,28),kicker,18,cyan,true);Label(new Rect(39,56,1100,64),title,46,white,true);
            if(!string.IsNullOrEmpty(subtitle))Label(new Rect(43,117,1110,35),subtitle,20,muted);
            if(Page!=ScreenPage.Home&&Button(new Rect(1090,39,143,42),"← VOLTAR",false))GoHome();
        }
        private void HomeUI(){ClassicHomeUI();}
        private void SelectionUI()
        {
            Header(ModeTitle(),SelectionTitle(),"14 lutadores · estilos próprios · especiais e ultimate exclusivos");
            int current=SelectionId;
            for(int i=0;i<14;i++)
            {
                float x=44+(i%7)*127, y=184+(i/7)*186;Rect r=new Rect(x,y,116,172);bool chosen=current==i;
                RectFill(r,chosen?new Color(.075f,.25f,.30f):panel);if(chosen)Border(r,cyan,3);
                if(portraits[i]!=null)Image(new Rect(x+3,y+1,110,128),portraits[i],ScaleMode.ScaleToFit);
                Label(new Rect(x+6,y+129,104,40),Catalog.Character(i).Name,17,chosen?white:muted,true,TextAnchor.MiddleCenter);
                if(HitButton(r)){SetSelectionId(i);audioBus.Play("confirm",1.05f);}
            }
            RectFill(new Rect(955,184,276,359),panel);var c=Catalog.Character(current);
            Label(new Rect(976,203,235,65),c.Name,30,white,true);Label(new Rect(978,279,230,70),c.Title,20,cyan);
            Label(new Rect(978,363,230,24),"ULTIMATE",16,gold,true);Label(new Rect(978,393,230,68),c.UltimateName,25,white,true);
            Label(new Rect(978,478,230,46),"“"+c.Quote+"”",18,muted);
            if(current==3){if(Button(new Rect(44,580,390,47),SelectionSkin==0?"APARÊNCIA: ORIGINAL":"APARÊNCIA: DIOGO CARECA",false))ToggleSelectionSkin();}
            else Label(new Rect(44,585,530,30),"Setas ou WASD para escolher · Enter para confirmar",19,muted);
            if(Button(new Rect(870,582,360,56),selectionPlayer==0?"CONFIRMAR LUTADOR  →":"CONFIRMAR ADVERSÁRIO  →"))ConfirmCharacter();
        }
        private void ArenaSelectUI()
        {
            Header(ModeTitle(),mode==PlayMode.Kart?"PRONTO PARA A LARGADA?":"ONDE SERÁ O CONFRONTO?",mode==PlayMode.Kart?"Circuito Campus · três voltas · oito corredores":"Oito arenas da UFN e de Santa Maria");
            if(mode==PlayMode.Kart)
            {
                RectFill(new Rect(44,180,1186,330),panel);Label(new Rect(83,215,650,65),"CIRCUITO CAMPUS",46,white,true);
                Label(new Rect(86,307,650,140),"Encontre a linha ideal. Segure o drift nas curvas para carregar turbo e use os itens para abrir caminho.",28,muted);
                Image(new Rect(852,190,299,300),portraits[selected],ScaleMode.ScaleToFit);
            }
            else for(int i=0;i<8;i++)
            {
                Rect r=new Rect(44+(i%4)*298,183+(i/4)*174,284,157);RectFill(r,panel);
                if(stages[i]!=null){Image(new Rect(r.x,r.y,r.width,125),stages[i],ScaleMode.ScaleAndCrop);}
                Label(new Rect(r.x+10,r.y+128,r.width-20,26),StageNames[i],18,white,true);
                if(i==stage)Border(r,cyan,3);if(HitButton(r))stage=i;
            }
            if(mode!=PlayMode.VersusLocal&&mode!=PlayMode.Kart)
            {Label(new Rect(44,567,210,25),"NÍVEL DA CPU",17,muted,true);string[] levels={"FÁCIL","NORMAL","DIFÍCIL"};if(Button(new Rect(44,596,290,45),levels[Mathf.Clamp(prefs.Difficulty,0,2)],false)){prefs.Difficulty=(prefs.Difficulty+1)%3;SaveSettings();}}
            if(mode==PlayMode.Evolution)Label(new Rect(390,567,425,76),"Entre vitórias, escolha melhorias. André CSTH e Kelvin aguardam no topo.",22,muted);
            if(Button(new Rect(887,580,343,60),mode==PlayMode.Kart?"INICIAR CORRIDA  →":"ENTRAR NA ARENA  →"))StartChosenGame();
        }
        private string ModeTitle()=>mode==PlayMode.Evolution?"TORRE EVOLUÇÃO":mode==PlayMode.Tower?"TORRE UFN":mode==PlayMode.Arcade?"ARCADE":mode==PlayMode.Teams?"DUPLAS 2 × 2":mode==PlayMode.Tutorial?"TUTORIAL":mode==PlayMode.Training?"LABORATÓRIO DE COMBATE":mode==PlayMode.Survival?"SOBREVIVÊNCIA":mode==PlayMode.Tournament?"TORNEIO DO CAMPUS":mode==PlayMode.VersusLocal?"VERSUS LOCAL":mode==PlayMode.Kart?"KART UFN":"VERSUS CPU";
        private void FightUI()
        {
            if(World==null)return;
            for(int p=0;p<2;p++)
            {
                var f=World.Fighters[p];float x=p==0?44:750;Color accent=p==0?cyan:gold;
                RectFill(new Rect(x-8,24,494,116),new Color(.025f,.04f,.065f,.93f));
                Label(new Rect(x,30,340,29),Catalog.Character(f.Id).Name,25,white,true,p==0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);
                Label(new Rect(x+352,35,114,21),FightSeatName(p),15,accent,true,TextAnchor.MiddleRight);
                Bar(new Rect(x,68,468,23),f.Hp/f.MaxHp,accent,p==1);
                Bar(new Rect(x,96,468,5),f.Guard/100,new Color(.64f,.76f,.91f),p==1);
                Bar(new Rect(x,109,344,13),f.Meter/100,f.Meter>=100?gold:accent,p==1);
                Label(new Rect(x+349,102,123,27),f.Meter>=100?"ULT PRONTA":Mathf.RoundToInt(f.Meter)+" / 100",16,f.Meter>=100?gold:muted,true,TextAnchor.MiddleRight);
                for(int k=0;k<2;k++)RectFill(new Rect(p==0?x+k*17:x+449-k*17,146,11,6),wins[p]>k?accent:new Color(.13f,.2f,.26f));
                if(f.Combo>1) {float cx=p==0?44:956;Label(new Rect(cx,197,280,58),f.Combo+" ACERTOS",38,accent,true,p==0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);Label(new Rect(cx,247,280,28),Mathf.RoundToInt(f.ComboDamage)+" DE DANO",18,white,false,p==0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);}
            }
            RectFill(new Rect(565,24,150,93),new Color(.025f,.04f,.065f,.94f));Label(new Rect(568,32,144,60),IsTraining?"∞":Mathf.CeilToInt(World.TimerFrames/60f).ToString("00"),49,white,true,TextAnchor.MiddleCenter);
            Label(new Rect(564,94,152,25),replaying?"REPLAY":mode==PlayMode.Tutorial?"TUTORIAL":mode==PlayMode.Training?"TREINAMENTO":"ROUND "+(wins[0]+wins[1]+1),16,cyan,true,TextAnchor.MiddleCenter);
            if(campaign!=null)Label(new Rect(440,150,400,28),campaign.StageLabel,18,white,true,TextAnchor.MiddleCenter);
            if(World.Cinematic.Active) {RectFill(new Rect(0,564,1280,66),new Color(.02f,.025f,.06f,.85f));Label(new Rect(40,574,1200,46),Catalog.Character(World.Cinematic.Character).UltimateName,33,World.Cinematic.Character==1?new Color(1,.3f,.25f):cyan,true,TextAnchor.MiddleCenter);}
            RectFill(new Rect(0,680,1280,40),new Color(.015f,.03f,.055f,.93f));Label(new Rect(30,686,1040,25),replaying?"REPLAY  /  "+replayStatus+"  /  "+replayIndex+" passos":"WASD + J / K / L / U   ·   SHIFT esquiva   ·   U + K ultimate   ·   ESC pausa",17,muted);
            if(Button(new Rect(1150,685,95,27),"PAUSA",false))paused=true;
            if(mode==PlayMode.Training&&!replaying)TrainingUI();
            ExtraFightUI();
            if(World.Winner>=0&&!paused)ResultOverlay();
        }
        private string FightSeatName(int seat)
        {
            if(mode==PlayMode.Attract||(mode==PlayMode.Tournament&&TournamentCpu(seat)))return "CPU";
            if(seat==0)return "JOGADOR 1";
            if(IsTraining)return "ALVO";
            return mode==PlayMode.Teams||mode==PlayMode.VersusLocal||mode==PlayMode.Tournament?"JOGADOR 2":"CPU";
        }
        private void TrainingUI()
        {
            RectFill(new Rect(30,335,268,224),new Color(.025f,.04f,.065f,.89f));Label(new Rect(44,347,240,24),"DADOS DO COMBATE",19,cyan,true);
            var f=World.Fighters[0];var m=f.Move;
            Label(new Rect(44,382,240,44),m==null?"Pronto para agir":m.Name,20,white,true);
            Label(new Rect(44,428,240,63),m==null?"Estado: "+f.State:"Início "+m.Startup+"  ·  Ativo "+m.Active+"  ·  Recup. "+m.Recovery+"\nQuadro atual: "+f.Frame,17,muted);
            Label(new Rect(44,504,240,39),"Dummy: "+DummyLabel()+(dummyRecording?"  ● GRAVANDO":""),16,gold);
            RectFill(new Rect(340,626,886,44),new Color(.025f,.04f,.065f,.89f));Label(new Rect(354,636,861,26),"F5 reiniciar  ·  F1 energia  ·  F2 colisões  ·  F3 dummy  ·  F4 gravar/repetir",18,cyan);
            if(prefs.ShowFrameData){int i=0;foreach(string line in history){Label(new Rect(925,345+i*29,302,28),line,17,white,false,TextAnchor.MiddleRight);i++;}}
        }
        private void ResultOverlay()
        {
            RectFill(new Rect(0,230,1280,219),new Color(.02f,.03f,.055f,.91f));
            string title=World.Winner==2?"EMPATE":Catalog.Character(World.Fighters[World.Winner].Id).Name+" VENCE";
            Label(new Rect(90,258,1100,67),title,49,white,true,TextAnchor.MiddleCenter);
            Label(new Rect(180,338,920,39),replaying?"Partida reproduzida":"A próxima decisão começa agora.",24,muted,false,TextAnchor.MiddleCenter);
            if(resultDelay>1&&Button(new Rect(440,386,400,46),replaying?"VOLTAR AO MENU":"CONTINUAR  [ENTER]"))FinishFight();
        }
        private void RewardsUI()
        {
            Header("TORRE EVOLUÇÃO  /  "+campaign.StageLabel,"SUA VITÓRIA, SUA ESCOLHA","Escolha uma melhoria para o próximo confronto. Os efeitos permanecem nesta campanha.");
            var choices=campaign.BenefitChoices;
            for(int i=0;i<choices.Length;i++)
            {
                Rect r=new Rect(48+i*397,218,380,324);RectFill(r,panel);RectFill(new Rect(r.x,r.y,r.width,4),i==0?cyan:i==1?gold:new Color(.75f,.57f,1));
                Label(new Rect(r.x+24,r.y+26,320,28),"MELHORIA 0"+(i+1),17,muted,true);
                Label(new Rect(r.x+24,r.y+83,326,73),CampaignRun.BenefitLabel(choices[i]),35,white,true);
                Label(new Rect(r.x+24,r.y+169,326,90),CampaignRun.BenefitDescription(choices[i]),23,muted);
                if(Button(new Rect(r.x+24,r.y+257,332,46),"ESCOLHER  →"))ChooseReward(i);
            }
            Label(new Rect(48,590,1130,44),"Próximo rival: "+Catalog.Character(campaign.OpponentId).Name,25,cyan,true);
        }
        private void FinishUI()
        {
            bool won=campaign!=null?campaign.Won:wins[0]>=2;
            Header(ModeTitle(),won?"O CAMPUS TEM UM CAMPEÃO":"CADA ROUND ENSINA",won?"Você fez sua história na arena.":"Reveja a luta, ajuste a estratégia e volte mais forte.");
            if(portraits[selected]!=null)Image(new Rect(792,170,410,420),portraits[selected],ScaleMode.ScaleToFit);
            RectFill(new Rect(45,201,682,340),panel);Label(new Rect(76,235,620,65),Catalog.Character(selected).Name,46,white,true);
            Label(new Rect(78,317,610,160),won?Catalog.Character(selected).Ending:"O caminho continua. O treino permite testar defesa, combos e os especiais do seu personagem.",27,muted);
            if(Button(new Rect(45,583,325,60),"NOVA CAMPANHA"))ChooseMode(mode);
            if(Button(new Rect(389,583,325,60),"VER REPLAY",false))LoadReplay();
            if(Button(new Rect(908,583,325,60),"MENU PRINCIPAL",false))GoHome();
        }
        private void SettingsUI()
        {
            Header("AJUSTE A EXPERIÊNCIA","DO SEU JEITO","As configurações ficam salvas neste computador.");
            RectFill(new Rect(44,180,1188,438),panel);
            SliderSetting(82,213,"MÚSICA",ref prefs.Music);SliderSetting(82,288,"EFEITOS SONOROS",ref prefs.Effects);
            SliderSetting(82,363,"INTENSIDADE DA CÂMERA",ref prefs.CameraShake);SliderSetting(82,438,"PARTÍCULAS",ref prefs.Particles);
            if(Button(new Rect(760,217,410,54),prefs.Fullscreen?"TELA CHEIA: SIM":"TELA CHEIA: NÃO",false)){prefs.Fullscreen=!prefs.Fullscreen;SaveSettings();}
            if(Button(new Rect(760,298,410,54),prefs.NoNumpad?"TECLADO: SEM NUMÉRICO":"TECLADO: PADRÃO",false)){prefs.NoNumpad=!prefs.NoNumpad;SaveSettings();}
            if(Button(new Rect(760,379,410,54),prefs.ShowFrameData?"HISTÓRICO DE TREINO: SIM":"HISTÓRICO DE TREINO: NÃO",false))prefs.ShowFrameData=!prefs.ShowFrameData;
            Label(new Rect(760,471,410,102),"Dois controles USB são detectados separadamente. Os controles físicos devem ser testados no seu computador.",21,muted);
            if(Button(new Rect(865,633,367,45),"SALVAR E VOLTAR")){SaveSettings();GoHome();}
        }
        private void SliderSetting(float x,float y,string title,ref float value)
        {
            Label(new Rect(x,y,410,26),title,19,white,true);Label(new Rect(x+440,y,98,26),Mathf.RoundToInt(value*100)+"%",19,cyan,true,TextAnchor.MiddleRight);
            var item=HudElement<Slider>("slider",new Rect(x,y+34,538,28));var slider=(Slider)item.Element;
            if(slider.userData==null){slider.userData=item;slider.lowValue=0;slider.highValue=1;slider.pickingMode=PickingMode.Position;slider.RegisterValueChangedCallback(e=>{item.Value=e.newValue;item.Changed=true;});}
            if(item.Changed){value=item.Value;item.Changed=false;}else if(!Mathf.Approximately(slider.value,value))slider.SetValueWithoutNotify(value);
            RegisterControl("slider:"+x+":"+y,new Rect(x,y+34,538,28),slider);
        }
        private void GuideUI()
        {
            Header("DOMINE AS FERRAMENTAS","COMANDOS E TÉCNICAS","Frente e trás acompanham o lado do adversário.");
            RectFill(new Rect(44,182,576,450),panel);RectFill(new Rect(640,182,592,450),panel);
            Label(new Rect(70,205,520,37),"CONTROLES",29,cyan,true);
            Label(new Rect(70,260,520,340),"JOGADOR 1\nWASD movimento · J leve · K forte · L chute\nU defesa · Shift esquiva\n\nJOGADOR 2\nSetas · Num1 leve · Num2 forte · Num3 chute\nNum4 defesa · Num0 esquiva\n\nCONTROLE (PADRÃO XBOX)\nAnalógico · X leve · Y forte · A chute\nLB defesa · B esquiva · RB ultimate",23,white);
            Label(new Rect(668,205,530,37),"ESPECIAIS E COMBOS",29,gold,true);
            Label(new Rect(668,259,530,340),"Defesa + leve → agarrão\nDefesa + forte → ultimate (100 de energia)\nTrás + leve → especial A\nTrás + baixo + leve → especial B\nFrente + baixo + chute → especial C\nFrente + baixo + forte → especial ofensivo\n\nConfirme ataques leves para continuar o combo.\nDefenda no momento do contato para um aparo.\nUse o treino para consultar os quadros dos golpes.",23,white);
        }
        private void CreditsUI()
        {
            Header("FEITO NO CAMPUS","UFN COMBATE NEXT","Criado por Diogo Liberalesso Inácio.");
            RectFill(new Rect(44,183,1188,394),panel);
            Label(new Rect(82,224,1100,80),"Uma nova base em Unity e C#",38,white,true);
            Label(new Rect(84,309,1080,258),"Personagens, cenários, referências e áudio da versão 0.6 foram preservados nesta migração.\n\nA nova simulação roda em 60 passos por segundo, separada da apresentação. A câmera e os efeitos acompanham o ritmo do combate, enquanto campanha e replays usam módulos independentes.\n\nConsulte LICENSES-ORIGINAIS.md e a documentação distribuída com o código.",25,muted);
            if(Button(new Rect(44,606,390,50),"VOLTAR AO CAMPUS"))GoHome();
        }
        private void KartUI()
        {
            if(Button(new Rect(22,18,157,42),"PAUSA  [ESC]",false))paused=true;
            if(kart!=null&&kart.Finished){RectFill(new Rect(330,235,620,244),panel);Label(new Rect(359,264,562,67),kart.PlayerPlace+"º LUGAR",53,white,true,TextAnchor.MiddleCenter);Label(new Rect(361,340,558,43),"Corrida concluída • recorde atualizado quando superado",21,muted,false,TextAnchor.MiddleCenter);if(Button(new Rect(362,412,250,46),"CORRER DE NOVO"))StartKart();if(Button(new Rect(633,412,280,46),"VOLTAR AO MENU",false))GoHome();}
        }
        private void PauseUI()
        {
            ModalBlocker(new Rect(0,0,1280,720),new Color(.015f,.025f,.045f,.80f));RectFill(new Rect(422,150,436,423),panel);
            Label(new Rect(448,179,384,68),replayEnded?"REPLAY CONCLUÍDO":"PAUSA",44,white,true,TextAnchor.MiddleCenter);
            if(Button(new Rect(455,286,370,53),replayEnded?"VOLTAR AO MENU":"CONTINUAR")){if(replayEnded)GoHome();else{paused=false;input.Clear();}}
            if(Button(new Rect(455,353,370,53),"REINICIAR",false)){if(Page==ScreenPage.Kart)RestartKartRace();else if(replaying)LoadReplay();else StartFight();}
            if(Button(new Rect(455,420,370,53),"MENU PRINCIPAL",false))GoHome();
            Label(new Rect(455,501,370,46),"ESC retorna ao jogo",19,muted,false,TextAnchor.MiddleCenter);
        }
        private void Label(Rect rect,string text,int size,Color color,bool strong=false,TextAnchor anchor=TextAnchor.UpperLeft)
        {
            size=AccessibleFontSize(size,rect);
            var item=HudElement<UnityEngine.UIElements.Label>("label",rect);var label=(UnityEngine.UIElements.Label)item.Element;
            if(item.Text!=text){item.Text=text;label.text=text??"";}
            if(item.Size!=size){item.Size=size;label.style.fontSize=size;}
            if(item.Color!=color){item.Color=color;label.style.color=color;}
            label.style.unityFont=strong&&bold!=null?bold:font;label.style.unityFontStyleAndWeight=strong?FontStyle.Bold:FontStyle.Normal;
            label.style.unityTextAlign=anchor;label.style.whiteSpace=WhiteSpace.Normal;label.enableRichText=true;
        }
        private bool Button(Rect r,string title,bool primary=true)
        {
            var item=HudElement<UnityEngine.UIElements.Button>("button",r);var button=(UnityEngine.UIElements.Button)item.Element;
            string control="button:"+r.x+":"+r.y+":"+r.width+":"+r.height;
            if(button.userData==null)
            {
                button.pickingMode=PickingMode.Position;button.clicked+=()=>clickedControls.Add((string)button.userData);
                button.RegisterCallback<PointerEnterEvent>(e=>{button.style.opacity=.85f;focusedControl=(string)button.userData;});button.RegisterCallback<PointerLeaveEvent>(e=>button.style.opacity=1);
            }
            button.userData=control;if(item.Text!=title){item.Text=title;button.text=title;}
            RegisterControl(control,r);
            button.style.backgroundColor=primary?cyan:new Color(.04f,.08f,.12f,.96f);button.style.color=primary?ink:white;
            button.style.fontSize=AccessibleFontSize(Mathf.Min(20,Mathf.FloorToInt(r.height*.63f)),r);button.style.unityFont=bold!=null?bold:font;button.style.unityFontStyleAndWeight=FontStyle.Bold;button.style.unityTextAlign=TextAnchor.MiddleCenter;button.style.whiteSpace=WhiteSpace.Normal;
            button.style.borderTopLeftRadius=button.style.borderTopRightRadius=button.style.borderBottomLeftRadius=button.style.borderBottomRightRadius=0;
            button.style.borderLeftWidth=button.style.borderRightWidth=button.style.borderTopWidth=button.style.borderBottomWidth=primary?0:1;
            button.style.borderLeftColor=button.style.borderRightColor=button.style.borderTopColor=button.style.borderBottomColor=new Color(.2f,.34f,.43f);
            return clickedControls.Remove(control);
        }
        private bool HitButton(Rect r)
        {
            var item=HudElement<UnityEngine.UIElements.Button>("hit",r);var button=(UnityEngine.UIElements.Button)item.Element;
            string control="hit:"+r.x+":"+r.y+":"+r.width+":"+r.height;
            if(button.userData==null){button.pickingMode=PickingMode.Position;button.clicked+=()=>clickedControls.Add((string)button.userData);}
            button.userData=control;button.text="";button.style.backgroundColor=Color.clear;button.style.backgroundImage=StyleKeyword.None;
            RegisterControl(control,r);
            button.style.borderLeftWidth=button.style.borderRightWidth=button.style.borderTopWidth=button.style.borderBottomWidth=0;
            return clickedControls.Remove(control);
        }
        private void Image(Rect r,Texture texture,ScaleMode mode=ScaleMode.ScaleToFit,Color? tint=null)
        {
            var item=HudElement<UnityEngine.UIElements.Image>("image",r);var image=(UnityEngine.UIElements.Image)item.Element;
            if(image.image!=texture)image.image=texture;image.scaleMode=mode;image.tintColor=tint??Color.white;
        }
        private int AccessibleFontSize(int size,Rect rect)
        {
            int growth=prefs==null?0:Mathf.Clamp(prefs.TextSize,0,2)*2;
            return Mathf.Max(size,Mathf.Min(size+growth,Mathf.FloorToInt(rect.height*.78f)));
        }
        private void RectFill(Rect r,Color color)
        {
            var item=HudElement<VisualElement>("rect",r);if(item.Color!=color){item.Color=color;item.Element.style.backgroundColor=color;}
        }
        private void Border(Rect r,Color color,float w){RectFill(new Rect(r.x,r.y,r.width,w),color);RectFill(new Rect(r.x,r.yMax-w,r.width,w),color);RectFill(new Rect(r.x,r.y,w,r.height),color);RectFill(new Rect(r.xMax-w,r.y,w,r.height),color);}
        private void Bar(Rect r,float ratio,Color color,bool mirrored=false){ratio=Mathf.Clamp01(ratio);RectFill(r,new Color(.095f,.13f,.18f));RectFill(new Rect(mirrored?r.xMax-r.width*ratio:r.x,r.y,r.width*ratio,r.height),color);RectFill(new Rect(r.x,r.y,r.width,2),new Color(1,1,1,.12f));}
    }
}
