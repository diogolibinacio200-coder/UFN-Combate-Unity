using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UFN.Core;
using UFN.Presentation;

namespace UFN.App
{
    public sealed partial class GameApp
    {
        private int settingsTab,galleryIndex=3,gallerySkin,galleryPose,galleryDetail,moveTab,movePage,statisticsTab,rebindSeat,rebindAction,controlsTab;
        private AtlasLibrary galleryLibrary;
        private static readonly string[] GalleryPoses={"idle","walk","guard","light","kick","jump","ultimate","victory"};
        private static readonly string[] GalleryPoseNames={"POSTURA","CAMINHADA","DEFESA","SOCO","CHUTE","SALTO","ULTIMATE","VITÓRIA"};
        private static readonly string[] DeviceNames={"TECLADO + CONTROLE","SÓ TECLADO","SÓ CONTROLE"};

        private void ExtendedSettingsUI()
        {
            Header("AJUSTE A EXPERIÊNCIA","CONFIGURAÇÕES","Preferências salvas neste computador. Os ajustes de volume e efeitos são aplicados ao sair.");
            string[] tabs={"ÁUDIO","VÍDEO E EFEITOS","ACESSIBILIDADE"};
            for(int i=0;i<tabs.Length;i++)if(Button(new Rect(44+i*398,170,383,46),tabs[i],settingsTab==i))settingsTab=i;
            RectFill(new Rect(44,231,1186,366),panel);
            if(settingsTab==0)
            {
                SliderSetting(72,250,"VOLUME GERAL",ref prefs.Master);SliderSetting(72,330,"MÚSICA",ref prefs.Music);
                SliderSetting(72,410,"EFEITOS SONOROS",ref prefs.Effects);SliderSetting(72,490,"VOZES",ref prefs.Voice);
                Label(new Rect(678,258,495,46),"O SOM DA ARENA",29,cyan,true);
                Label(new Rect(680,329,489,170),"Equilibre a trilha, os impactos e as vozes dos personagens. O volume geral afeta todas as categorias.",26,muted);
                if(Button(new Rect(680,512,496,48),prefs.Subtitles?"LEGENDAS: ATIVADAS":"LEGENDAS: DESATIVADAS",false)){prefs.Subtitles=!prefs.Subtitles;SaveSettings();}
            }
            else if(settingsTab==1)
            {
                SliderSetting(72,250,"MOVIMENTO DA CÂMERA",ref prefs.CameraShake);SliderSetting(72,350,"PARTÍCULAS",ref prefs.Particles);SliderSetting(72,450,"BRILHO DOS IMPACTOS",ref prefs.Flashes);
                if(Button(new Rect(680,255,496,52),prefs.Fullscreen?"TELA CHEIA: SIM":"TELA CHEIA: NÃO",false)){prefs.Fullscreen=!prefs.Fullscreen;SaveSettings();}
                string[] resolutions={"1280 × 720","1600 × 900","1920 × 1080"};
                if(Button(new Rect(680,329,496,52),"RESOLUÇÃO: "+resolutions[Mathf.Clamp(prefs.Resolution,0,2)],false)){prefs.Resolution=(prefs.Resolution+1)%3;SaveSettings();}
                string[] quality={"BAIXA","MÉDIA","ALTA"};
                if(Button(new Rect(680,403,496,52),"QUALIDADE: "+quality[Mathf.Clamp(prefs.Quality,0,2)],false)){prefs.Quality=(prefs.Quality+1)%3;SaveSettings();}
                Label(new Rect(680,484,496,74),"A imagem completa dos personagens é preservada em todas as qualidades.",23,muted);
            }
            else
            {
                string[] sizes={"PADRÃO","AMPLIADO","GRANDE"};
                if(Button(new Rect(72,256,541,52),"TEXTO: "+sizes[Mathf.Clamp(prefs.TextSize,0,2)],false)){prefs.TextSize=(prefs.TextSize+1)%3;SaveSettings();}
                if(Button(new Rect(72,331,541,52),prefs.Subtitles?"LEGENDAS: ATIVADAS":"LEGENDAS: DESATIVADAS",false)){prefs.Subtitles=!prefs.Subtitles;SaveSettings();}
                if(Button(new Rect(72,406,541,52),prefs.ShowFrameData?"HISTÓRICO DE TREINO: SIM":"HISTÓRICO DE TREINO: NÃO",false)){prefs.ShowFrameData=!prefs.ShowFrameData;SaveSettings();}
                if(Button(new Rect(72,481,541,52),prefs.NoNumpad?"TECLADO: SEM NUMÉRICO":"TECLADO: COM NUMÉRICO",false)){prefs.NoNumpad=!prefs.NoNumpad;SaveSettings();}
                if(Button(new Rect(680,256,496,52),prefs.AutoAccelerate?"KART: ACELERAÇÃO AUTOMÁTICA":"KART: ACELERAÇÃO MANUAL",false)){prefs.AutoAccelerate=!prefs.AutoAccelerate;SaveSettings();}
                if(Button(new Rect(680,331,496,52),prefs.HorizontalSplit?"KART: DIVISÃO HORIZONTAL":"KART: DIVISÃO VERTICAL",false)){prefs.HorizontalSplit=!prefs.HorizontalSplit;SaveSettings();}
                Label(new Rect(680,414,496,120),"Você pode reduzir os movimentos de câmera, partículas e flashes na aba Vídeo e efeitos.",24,muted);
            }
            if(Button(new Rect(44,620,382,48),"PERSONALIZAR CONTROLES",false)){SaveSettings();Page=ScreenPage.Controls;}
            if(Button(new Rect(849,620,382,48),"SALVAR E VOLTAR")){SaveSettings();GoHome();}
        }

        private void ControlsUI()
        {
            Header("TECLADO E CONTROLES","SEU JEITO DE JOGAR","Escolha uma ação e pressione uma tecla ou botão. Esc cancela a alteração.");
            string[] tabs={"LUTA","KART","DISPOSITIVOS"};
            for(int i=0;i<3;i++)if(Button(new Rect(44+i*398,157,383,38),tabs[i],controlsTab==i))controlsTab=i;
            bool kartMap=controlsTab==1;string[] actions=kartMap?PlayerControls.KartActions:PlayerControls.Actions;
            for(int p=0;p<2;p++)
            {
                float x=44+p*600;var binding=input.Bindings[p];
                RectFill(new Rect(x,206,586,399),panel);Label(new Rect(x+18,216,258,33),"JOGADOR "+(p+1),25,p==0?cyan:gold,true);
                if(Button(new Rect(x+284,215,285,36),DeviceNames[binding.DeviceMode],false)){binding.DeviceMode=(binding.DeviceMode+1)%3;SaveSettings();}
                string pad=input.Pad(p)?.displayName??"NENHUM CONECTADO";
                Label(new Rect(x+18,258,383,32),"Controle "+(binding.PadIndex+1)+": "+pad,16,muted);
                if(Button(new Rect(x+415,256,154,32),"TROCAR PAD",false)){input.CyclePad(p);SaveSettings();}
                if(controlsTab==2)
                {
                    Label(new Rect(x+18,317,354,32),"ZONA MORTA: "+Mathf.RoundToInt(binding.Deadzone*100)+"%",23,white,true);
                    if(Button(new Rect(x+414,312,69,39),"−",false)){binding.Deadzone=Mathf.Max(.1f,binding.Deadzone-.05f);SaveSettings();}
                    if(Button(new Rect(x+500,312,69,39),"+",false)){binding.Deadzone=Mathf.Min(.9f,binding.Deadzone+.05f);SaveSettings();}
                    string[] axes={"ANALÓGICO E · X","ANALÓGICO E · Y","ANALÓGICO D · X","ANALÓGICO D · Y","GATILHO E","GATILHO D"};
                    if(Button(new Rect(x+18,372,370,42),"HORIZONTAL: "+axes[binding.HorizontalAxis],false)){binding.HorizontalAxis=(binding.HorizontalAxis+1)%6;SaveSettings();}
                    if(Button(new Rect(x+402,372,167,42),binding.InvertHorizontal?"INVERTIDO":"NORMAL",binding.InvertHorizontal)){binding.InvertHorizontal=!binding.InvertHorizontal;SaveSettings();}
                    if(Button(new Rect(x+18,434,370,42),"VERTICAL: "+axes[binding.VerticalAxis],false)){binding.VerticalAxis=(binding.VerticalAxis+1)%6;SaveSettings();}
                    if(Button(new Rect(x+402,434,167,42),binding.InvertVertical?"INVERTIDO":"NORMAL",binding.InvertVertical)){binding.InvertVertical=!binding.InvertVertical;SaveSettings();}
                    Label(new Rect(x+18,508,551,74),"A zona morta filtra movimentos involuntários. A direção do kart responde à inclinação do analógico.",22,muted);
                }
                else for(int a=0;a<actions.Length;a++)
                {
                    float y=296+a*26;Label(new Rect(x+18,y+1,243,25),actions[a],16,white,true);
                    Rect actionRect=new Rect(x+258,y,311,25);RectFill(actionRect,new Color(.05f,.105f,.15f));
                    Label(new Rect(actionRect.x+7,y,297,25),FriendlyBinding(input.GetLabel(p,a,kartMap)),15,cyan,false,TextAnchor.MiddleCenter);
                    if(HitButton(actionRect)&&!input.Rebinding){rebindSeat=p;rebindAction=a;input.BeginRebind(p,a,kartMap);}
                }
                if(kartMap)Label(new Rect(x+18,529,551,56),"Alterar o kart preserva os comandos de luta.\nF2 / Backspace também recuperam os pilotos.",19,muted);
            }
            if(controlsTab==2)
            {
                if(Button(new Rect(44,620,369,48),"RESTAURAR EIXOS",false)){foreach(var b in input.Bindings){b.Deadzone=.32f;b.HorizontalAxis=0;b.VerticalAxis=1;b.InvertHorizontal=b.InvertVertical=false;}SaveSettings();}
                if(Button(new Rect(451,620,369,48),"RESTAURAR DISPOSITIVOS",false)){for(int p=0;p<2;p++){input.Bindings[p].PadIndex=p;input.Bindings[p].DeviceMode=0;}SaveSettings();}
            }
            else
            {
                if(Button(new Rect(44,620,369,48),"RESTAURAR TECLAS",false)){input.ResetKeys(kartMap);SaveSettings();}
                if(Button(new Rect(451,620,369,48),"RESTAURAR BOTÕES",false)){input.ResetButtons(kartMap);SaveSettings();}
            }
            if(Button(new Rect(859,620,371,48),"SALVAR E VOLTAR")){SaveSettings();GoHome();}
            if(input.Rebinding)
            {
                hudControls.Clear();ModalBlocker(new Rect(0,0,1280,720),new Color(.015f,.025f,.045f,.94f));
                Label(new Rect(190,235,900,60),"JOGADOR "+(rebindSeat+1)+" · "+actions[rebindAction],36,cyan,true,TextAnchor.MiddleCenter);
                Label(new Rect(190,334,900,100),"Pressione a nova tecla ou botão\nEsc cancela",28,white,false,TextAnchor.MiddleCenter);
                Label(new Rect(190,468,900,69),input.RebindMessage,23,gold,false,TextAnchor.MiddleCenter);
            }
        }
        private static string FriendlyBinding(string text)=>text.Replace("Numpad","Num ").Replace("LeftArrow","←").Replace("RightArrow","→").Replace("UpArrow","↑").Replace("DownArrow","↓").Replace("LeftShift","Shift E").Replace("RightShift","Shift D").Replace("buttonWest","X / □").Replace("buttonNorth","Y / △").Replace("buttonSouth","A / ×").Replace("buttonEast","B / ○").Replace("leftShoulder","LB / L1").Replace("rightShoulder","RB / R1").Replace("dpad/left","←").Replace("dpad/right","→").Replace("dpad/up","↑").Replace("dpad/down","↓").Replace("select","Select");

        private void GalleryUI()
        {
            Header("PERSONAGENS E HISTÓRIAS","GALERIA DO CAMPUS","Explore os 16 personagens, suas poses, aparências e finais.");
            for(int i=0;i<Catalog.Characters.Length;i++)
            {
                float x=44+(i%4)*100,y=172+(i/4)*103;Rect r=new Rect(x,y,91,95);RectFill(r,panel);
                if(portraits[i]!=null)Image(new Rect(x+3,y+2,85,52),portraits[i]);
                Label(new Rect(x+2,y+54,87,39),Catalog.Character(i).Name,13,i==galleryIndex?cyan:muted,true,TextAnchor.MiddleCenter);
                if(i==galleryIndex)Border(r,cyan,2);if(HitButton(r)){galleryIndex=i;gallerySkin=0;}
            }
            var character=Catalog.Character(galleryIndex);Color accent=cyan;if(ColorUtility.TryParseHtmlString("#"+character.ColorHex,out var parsed))accent=parsed;
            RectFill(new Rect(458,172,336,426),panel);RectFill(new Rect(458,172,336,3),accent);
            galleryLibrary=galleryLibrary??new AtlasLibrary(ContentPath);
            string key=gallerySkin>0&&galleryIndex==3?"diogo_alt":gallerySkin>0&&galleryIndex==4?"mirkos_armor":character.Key;
            var atlas=galleryLibrary.Get(key);string pose=GalleryPoses[galleryPose];float phase=Mathf.Repeat(menuTime*.75f,1);
            SpriteImage(new Rect(462,177,328,328),atlas.Sample(pose,phase));
            Label(new Rect(474,510,303,28),GalleryPoseNames[galleryPose],20,accent,true,TextAnchor.MiddleCenter);
            if(Button(new Rect(475,549,66,33),"←",false))galleryPose=(galleryPose+GalleryPoses.Length-1)%GalleryPoses.Length;
            Label(new Rect(546,552,158,25),(galleryPose+1)+" / "+GalleryPoses.Length,17,muted,false,TextAnchor.MiddleCenter);
            if(Button(new Rect(710,549,66,33),"→",false))galleryPose=(galleryPose+1)%GalleryPoses.Length;
            RectFill(new Rect(814,172,417,426),panel);Label(new Rect(835,188,375,45),character.Name,34,white,true);
            Label(new Rect(837,239,371,64),character.Title,21,accent,true);
            if(Button(new Rect(835,304,176,36),"IDENTIDADE",galleryDetail==0))galleryDetail=0;
            if(Button(new Rect(1027,304,182,36),"FINAL",galleryDetail==1))galleryDetail=1;
            if(galleryDetail==0)
            {
                Label(new Rect(837,361,369,67),"“"+character.Quote+"”",24,white);
                Label(new Rect(837,445,369,27),"ULTIMATE",17,gold,true);Label(new Rect(837,474,369,76),character.UltimateName,26,white,true);
                Label(new Rect(837,558,369,34),"Velocidade: "+character.WalkSpeed.ToString("0.0")+" por quadro",18,muted);
            }
            else Label(new Rect(837,358,369,224),character.Ending,23,white);
            if(galleryIndex==3||galleryIndex==4)
            {
                string variant=gallerySkin==0?"ORIGINAL":galleryIndex==3?"DIOGO CARECA":"ARMADURA DE MIRKOS";
                if(Button(new Rect(44,600,391,64),"APARÊNCIA: "+variant,false))gallerySkin=1-gallerySkin;
            }
            if(Button(new Rect(814,620,417,48),"VER GOLPES DE "+character.Name)){moveTab=movePage=0;Page=ScreenPage.Guide;}
        }
        private void SpriteImage(Rect r,Sprite sprite)
        {
            var item=HudElement<UnityEngine.UIElements.Image>("sprite",r);var image=(UnityEngine.UIElements.Image)item.Element;
            if(image.sprite!=sprite)image.sprite=sprite;image.scaleMode=ScaleMode.ScaleToFit;image.tintColor=Color.white;
        }

        private void StatisticsUI()
        {
            Header("SUA HISTÓRIA NA UFN","ESTATÍSTICAS","Resultados deste perfil, recordes de corrida e conquistas.");
            string[] tabs={"COMBATE","KART","CONQUISTAS"};for(int i=0;i<3;i++)if(Button(new Rect(44+i*398,170,383,46),tabs[i],statisticsTab==i))statisticsTab=i;
            if(statisticsTab==0)
            {
                string[] labels={"PARTIDAS","VITÓRIAS J1","VITÓRIAS J2","MAIOR COMBO","DEFESAS PERFEITAS","QUEBRAS DE GUARDA","ULTIMATES","SOBREVIVÊNCIA"};
                int[] values={progress.Matches,progress.P1Wins,progress.P2Wins,progress.BiggestCombo,progress.PerfectBlocks,progress.GuardBreaks,progress.Supers,progress.SurvivalRecord};
                for(int i=0;i<8;i++)
                {
                    float x=44+(i%4)*298,y=238+(i/4)*139;RectFill(new Rect(x,y,283,121),panel);
                    Label(new Rect(x+17,y+12,249,27),labels[i],18,muted,true);Label(new Rect(x+17,y+45,249,61),values[i].ToString("N0"),41,i%2==0?cyan:gold,true);
                }
                int favorite=0;for(int i=1;i<progress.MostPlayed.Length;i++)if(progress.MostPlayed[i]>progress.MostPlayed[favorite])favorite=i;
                RectFill(new Rect(44,530,1180,112),panel);
                if(progress.MostPlayed[favorite]>0)
                {
                    Image(new Rect(54,531,111,108),portraits[favorite]);Label(new Rect(186,547,1001,28),"PERSONAGEM MAIS USADO",18,muted,true);
                    Label(new Rect(183,581,1004,43),Catalog.Character(favorite).Name+" · "+progress.MostPlayed[favorite]+" participações",30,white,true);
                }
                else Label(new Rect(64,560,1120,48),"Jogue seu primeiro confronto para iniciar o histórico.",27,muted);
            }
            else if(statisticsTab==1)
            {
                RectFill(new Rect(44,238,1186,398),panel);Label(new Rect(66,255,650,35),"CIRCUITO",21,cyan,true);Label(new Rect(767,255,200,35),"CORRIDA",21,cyan,true);Label(new Rect(1010,255,200,35),"MELHOR VOLTA",21,cyan,true);
                for(int i=0;i<5;i++)
                {
                    float y=309+i*62;Label(new Rect(66,y,647,44),KartContent.TrackNames[i],25,white,true);Label(new Rect(767,y,213,44),RecordTime(progress.TrackRecords[i]),25,gold);Label(new Rect(1010,y,209,44),RecordTime(progress.LapRecords[i]),25,gold);
                    RectFill(new Rect(65,y+47,1145,1),new Color(.15f,.22f,.28f));
                }
            }
            else
            {
                var achievements=progress.Achievements??Array.Empty<string>();
                if(achievements.Length==0){RectFill(new Rect(44,238,1186,352),panel);Label(new Rect(82,311,1100,170),"Sua coleção começa com o primeiro confronto.\nVença, forme combos e conquiste o campus.",30,muted,false,TextAnchor.MiddleCenter);}
                for(int i=0;i<Mathf.Min(achievements.Length,12);i++)
                {
                    float x=44+(i%3)*399,y=238+(i/3)*99;RectFill(new Rect(x,y,382,83),panel);Label(new Rect(x+18,y+15,346,51),"★  "+achievements[i],24,gold,true,TextAnchor.MiddleLeft);
                }
            }
        }
        private static string RecordTime(int milliseconds)=>milliseconds<=0?"—":(milliseconds/60000).ToString("00")+":"+((milliseconds/1000)%60).ToString("00")+"."+(milliseconds%1000).ToString("000");

        private void MoveListUI()
        {
            var c=Catalog.Character(galleryIndex);Header("APRENDA SEU PERSONAGEM","GOLPES DE "+c.Name,"Frente e trás acompanham o adversário. Os tempos abaixo usam quadros de 1/60 de segundo.");
            if(Button(new Rect(44,170,62,43),"←",false)){galleryIndex=(galleryIndex+Catalog.Characters.Length-1)%Catalog.Characters.Length;movePage=0;c=Catalog.Character(galleryIndex);}
            Label(new Rect(119,176,276,35),c.Name,25,cyan,true,TextAnchor.MiddleCenter);
            if(Button(new Rect(408,170,62,43),"→",false)){galleryIndex=(galleryIndex+1)%Catalog.Characters.Length;movePage=0;c=Catalog.Character(galleryIndex);}
            string[] tabs={"ESPECIAIS","NORMAIS","COMBOS"};for(int i=0;i<3;i++)if(Button(new Rect(498+i*247,170,231,43),tabs[i],moveTab==i)){moveTab=i;movePage=0;}
            MoveDefinition[] moves;string[] commands;
            if(moveTab==0){moves=new[]{c.Specials[0],c.Specials[1],c.Specials[2],c.Offensive,c.Defense,c.Ultimate};commands=new[]{"Trás + leve / baixo → frente + leve","Trás + baixo + leve","Frente + baixo + chute","Frente + baixo + forte","Trás + forte","Defesa + forte / Ultimate"};}
            else if(moveTab==1){moves=Catalog.Normals;commands=new[]{"Leve","Frente + leve","Recuo contextual","Baixo + leve / chute","No ar + leve","No ar + direção + leve / chute","Forte","Cima + leve / forte","Chute"};}
            else {moves=Catalog.Chains;commands=new[]{"Leve → leve (após acertar)","Leve → leve → leve (após acertar)"};}
            int pages=Mathf.Max(1,(moves.Length+5)/6);movePage=Mathf.Clamp(movePage,0,pages-1);
            RectFill(new Rect(44,234,1186,389),panel);
            string[] columns={"GOLPE / COMANDO","DANO","ALCANCE","INÍCIO","ATIVO","RECUP.","CUSTO"};float[] offsets={20,514,615,734,841,945,1069};float[] widths={489,90,109,100,100,115,98};
            for(int col=0;col<columns.Length;col++)Label(new Rect(44+offsets[col],246,widths[col],26),columns[col],17,cyan,true,col==0?TextAnchor.UpperLeft:TextAnchor.UpperCenter);
            for(int row=0;row<6;row++)
            {
                int i=movePage*6+row;if(i>=moves.Length)break;var m=moves[i];float y=286+row*55;
                Label(new Rect(64,y,489,26),m.Name,21,white,true);
                string command=i<commands.Length?commands[i]:"Etapa "+Mathf.Max(1,m.ChainStage);Label(new Rect(65,y+25,487,23),command+" · "+MoveLevel(m.Level),15,muted);
                string[] values={m.Damage.ToString("0.#"),m.Range.ToString("0.#"),m.Startup.ToString(),m.Active.ToString(),m.Recovery.ToString(),m.Cost.ToString("0.#")};
                for(int col=0;col<6;col++)Label(new Rect(44+offsets[col+1],y+7,widths[col+1],34),values[col],23,col==0?gold:white,false,TextAnchor.MiddleCenter);
                RectFill(new Rect(64,y+50,1147,1),new Color(.13f,.2f,.27f));
            }
            if(moveTab==2)Label(new Rect(44,636,824,34),"As sequências exigem contato confirmado; veja o custo e a recuperação de cada continuação.",18,muted);
            else Label(new Rect(44,636,824,34),"Dano base do catálogo · alcance em unidades da arena · defesa e estados alteram o resultado.",17,muted);
            if(pages>1)
            {
                if(Button(new Rect(913,635,58,34),"←",false))movePage=(movePage+pages-1)%pages;Label(new Rect(979,638,182,28),(movePage+1)+" / "+pages,17,muted,false,TextAnchor.MiddleCenter);if(Button(new Rect(1171,635,58,34),"→",false))movePage=(movePage+1)%pages;
            }
        }
        private static string MoveLevel(string level)=>level=="low"?"baixo":level=="high"?"alto":level=="overhead"?"sobre a guarda":level=="throw"?"agarrão":"médio";
    }
}
