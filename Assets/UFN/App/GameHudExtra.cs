using System;
using UnityEngine;
using UFN.Core;

namespace UFN.App
{
    public sealed partial class GameApp
    {
        private static readonly string[] HomeItems={"ARCADE","TORRE UFN","VERSUS LOCAL","DUPLAS 2 × 2","KART UFN","TREINAMENTO","TUTORIAL","TORNEIO LOCAL","SOBREVIVÊNCIA","GALERIA","ESTATÍSTICAS","CONFIGURAÇÕES","CONTROLES","CRÉDITOS","SAIR"};
        private void ClassicHomeUI()
        {
            Label(new Rect(44,30,800,30),"UNITY EDITION  /  O CAMPUS É A SUA ARENA",18,cyan,true);
            Label(new Rect(38,64,850,86),"UFN COMBATE",72,white,true);
            Label(new Rect(45,150,900,38),"Lute pelo topo. Reúna sua dupla. Acelere com a turma.",24,muted);
            for(int i=0;i<HomeItems.Length;i++)
            {
                int column=i/8,row=i%8;var rect=new Rect(44+column*272,220+row*49,258,41);
                if(Button(rect,HomeItems[i],i==menuIndex)){menuIndex=i;ActivateHome(i);}
            }
            RectFill(new Rect(625,221,605,366),panel);Image(new Rect(860,238,330,306),portraits[selected],ScaleMode.ScaleToFit);
            Label(new Rect(650,245,320,30),"SEU PERSONAGEM",17,cyan,true);Label(new Rect(650,286,350,58),Catalog.Character(selected).Name,34,white,true);
            Label(new Rect(650,350,220,64),Catalog.Character(selected).Title,22,muted);
            Label(new Rect(650,440,235,90),"“"+Catalog.Character(selected).Quote+"”",23,white);
            Label(new Rect(650,553,545,28),profile.TotalWins+" VITÓRIAS  ·  "+profile.TowerClears+" TORRES",18,gold,true);
            if(Button(new Rect(625,604,287,43),"VERSUS CPU",false))ChooseMode(PlayMode.VersusCPU);
            if(Button(new Rect(926,604,304,43),"TORRE EVOLUÇÃO",false))ChooseMode(PlayMode.Evolution);
            if(progress.Tower!=null&&Button(new Rect(625,652,195,30),"RETOMAR TORRE",false))ResumeTower();
            if(profile.Campaign!=null&&Button(new Rect(830,652,195,30),"RETOMAR EVOLUÇÃO",false))ResumeCampaign();
            if(Button(new Rect(1035,652,195,30),"ÚLTIMO REPLAY",false))LoadReplay();
        }
        private bool ExtraUI()
        {
            switch(Page)
            {
                case ScreenPage.Setup:SetupUI();return true;
                case ScreenPage.TowerMap:TowerMapUI(false);return true;
                case ScreenPage.Ending:TowerMapUI(true);return true;
                case ScreenPage.Bracket:BracketUI();return true;
                case ScreenPage.ClassicResult:ClassicResultUI();return true;
                case ScreenPage.Gallery:GalleryUI();return true;
                case ScreenPage.Statistics:StatisticsUI();return true;
                case ScreenPage.Settings:ExtendedSettingsUI();return true;
                case ScreenPage.Controls:ControlsUI();return true;
                case ScreenPage.Guide:MoveListUI();return true;
                case ScreenPage.TrainingOptions:TrainingOptionsUI();return true;
                case ScreenPage.Kart:ExpandedKartUI();return true;
                case ScreenPage.ArenaSelect:if(mode==PlayMode.Kart){KartTracksUI();return true;}break;
            }
            return false;
        }
        private void SetupUI()
        {
            Header(ModeTitle(),mode==PlayMode.Tower?"MONTE SUA TORRE":mode==PlayMode.Kart?"PILOTOS NA LINHA DE LARGADA":"ORGANIZE O TORNEIO");
            if(mode==PlayMode.Tower)
            {
                Label(new Rect(45,183,1000,35),"QUANTOS RIVAIS ANTES DOS DOIS CHEFES?",24,white,true);
                int[] sizes={3,5,8,10};for(int i=0;i<4;i++)if(Button(new Rect(44+i*298,244,284,80),sizes[i]+" RIVAIS + 2 CHEFES",towerSize==sizes[i]))towerSize=sizes[i];
                Label(new Rect(45,365,1120,60),"André CSTH e Kelvin encerram todas as torres. Seu progresso fica salvo entre os confrontos.",26,muted);
                string[] levels={"FÁCIL","NORMAL","DIFÍCIL"};for(int i=0;i<3;i++)if(Button(new Rect(44+i*298,458,284,60),levels[i],prefs.Difficulty==i)){prefs.Difficulty=i;SaveSettings();}
                if(progress.Tower!=null&&Button(new Rect(44,596,365,56),"RETOMAR TORRE SALVA",false))ResumeTower();
                if(Button(new Rect(865,596,365,56),"ESCOLHER LUTADOR  →"))Page=ScreenPage.Select;
            }
            else if(mode==PlayMode.Kart)
            {
                string[] names={"CORRIDA RÁPIDA","CAMPEONATO","CONTRA O RELÓGIO"};for(int i=0;i<3;i++)if(Button(new Rect(44+i*400,208,386,70),names[i],(int)kartMode==i)){kartMode=(KartRaceMode)i;if(i==2)kartPlayers=1;}
                Label(new Rect(45,318,1120,75),kartMode==KartRaceMode.Championship?"Quatro etapas, oito pilotos e pontos acumulados até o pódio.":kartMode==KartRaceMode.TimeAttack?"Uma pista livre para superar seu tempo e sua melhor volta.":"Escolha uma das cinco pistas. Três voltas, drift, itens e poderes próprios.",28,muted);
                if(Button(new Rect(44,430,560,58),kartPlayers==1?"1 JOGADOR":"2 JOGADORES · TELA DIVIDIDA",false)&&kartMode!=KartRaceMode.TimeAttack)kartPlayers=3-kartPlayers;
                if(Button(new Rect(634,430,596,58),prefs.HorizontalSplit?"DIVISÃO: HORIZONTAL":"DIVISÃO: VERTICAL",false)){prefs.HorizontalSplit=!prefs.HorizontalSplit;SaveSettings();}
                if(progress.Championship!=null&&Button(new Rect(44,596,460,56),"RETOMAR CAMPEONATO · ETAPA "+(progress.Championship.Track+1),false))ResumeKartChampionship();
                if(Button(new Rect(865,596,365,56),"ESCOLHER PILOTOS  →"))Page=ScreenPage.Select;
            }
            else
            {
                if(Button(new Rect(44,160,300,43),tournamentSize+" PARTICIPANTES",false))tournamentSize=tournamentSize==4?8:4;
                for(int i=0;i<tournamentSize;i++)
                {
                    float x=44+(i/4)*606,y=226+(i%4)*82;
                    if(Button(new Rect(x,y,330,49),(i+1)+" · "+Catalog.Character(entryCharacters[i]).Name,false))entryCharacters[i]=(entryCharacters[i]+1)%14;
                    if(Button(new Rect(x+340,y,110,49),entryCpu[i]?"CPU":"PESSOA",entryCpu[i]))entryCpu[i]=!entryCpu[i];
                    if(entryCharacters[i]==3&&Button(new Rect(x+460,y,120,49),entrySkins[i]==0?"NORMAL":"CARECA",false))entrySkins[i]=1-entrySkins[i];
                }
                if(progress.Bracket!=null&&Button(new Rect(44,601,380,51),"RETOMAR CHAVE SALVA",false)){try{bracket=LocalBracket.Restore(progress.Bracket);Page=ScreenPage.Bracket;}catch(Exception e){Notify(e.Message);}}
                if(Button(new Rect(865,601,365,51),"CRIAR CHAVEAMENTO  →"))StartTournament();
            }
        }
        private void TowerMapUI(bool ending)
        {
            if(classic==null){GoHome();return;}
            Header(ending?"TORRE CONCLUÍDA":"TORRE UFN",ending?"O TOPO É SEU":"UM ANDAR DE CADA VEZ",ending?Catalog.Character(selected).Name+" venceu todos os rivais.":"Vitórias em melhor de três rounds. A derrota permite tentar o mesmo andar novamente.");
            var order=classic.Order;int count=order.Length;float h=Mathf.Min(57,440f/count),baseY=604;
            for(int i=0;i<count;i++)
            {
                float fall=ending?Mathf.Max(0,endingTime-(count-1-i)*.18f):0;
                float x=78+(ending?Mathf.Sin(i*2.7f+fall*3)*Mathf.Min(90,fall*50):0),y=baseY-(i+1)*h+fall*fall*180;
                if(y>735)continue;
                var rect=new Rect(x,y,555,h-3);RectFill(rect,i==classic.Index?new Color(.08f,.29f,.32f):panel);RectFill(new Rect(x,y,5,h-3),i<classic.Index?cyan:i>=count-2?gold:muted);
                Image(new Rect(x+16,y+1,h-5,h-5),portraits[order[i]],ScaleMode.ScaleToFit);
                Label(new Rect(x+70,y+3,400,h-6),(i+1).ToString("00")+"  "+Catalog.Character(order[i]).Name,Mathf.RoundToInt(Mathf.Min(24,h*.55f)),i>=count-2?gold:white,true);
            }
            Image(new Rect(846,190,335,334),portraits[selected],ScaleMode.ScaleToFit);
            if(ending){Label(new Rect(708,390,510,184),Catalog.Character(selected).Ending,26,white);if(Button(new Rect(804,610,425,52),"VOLTAR AO CAMPUS"))GoHome();}
            else{Label(new Rect(721,492,493,42),"PRÓXIMO: "+Catalog.Character(classic.Opponent).Name,25,cyan,true);if(Button(new Rect(804,594,425,62),"ENTRAR NA ARENA  →")){wins[0]=wins[1]=0;StartFight();}}
        }
        private void BracketUI()
        {
            if(bracket==null){GoHome();return;}
            Header("TORNEIO LOCAL",bracket.Complete?"TEMOS UM CAMPEÃO":"CHAVEAMENTO",bracket.Complete?Catalog.Character(bracket.Character(bracket.Champion)).Name:"Cada participante mantém seu personagem e controle ao avançar.");
            int[] slots=new int[bracket.Size];for(int i=0;i<slots.Length;i++)slots[i]=i;
            var results=bracket.WinnerSlots;int consumed=0,column=0;float spacing=bracket.Size==8?54:108;
            while(slots.Length>0)
            {
                float x=44+column*303;
                Label(new Rect(x,164,270,25),slots.Length==1?"CAMPEÃO":slots.Length==2?"FINAL":slots.Length==4?"SEMIFINAIS":"QUARTAS DE FINAL",17,cyan,true);
                for(int i=0;i<slots.Length;i++)
                {
                    int slot=slots[i];float center=209+spacing*(i+.5f);var r=new Rect(x,center-22,270,44);
                    bool current=slot>=0&&!bracket.Complete&&(slot==bracket.PairA||slot==bracket.PairB);
                    RectFill(r,current?new Color(.08f,.24f,.27f):panel);if(current||slots.Length==1)Border(r,slots.Length==1?gold:cyan,2);
                    if(slot>=0)
                    {
                        int id=bracket.Character(slot);Image(new Rect(x+3,center-21,43,42),portraits[id],ScaleMode.ScaleToFit);
                        Label(new Rect(x+50,center-18,213,36),(slot+1)+" · "+Catalog.Character(id).Name,20,white,true);
                    }
                    else Label(new Rect(x+12,center-16,246,32),"Aguardando confronto",18,muted);
                    if(slots.Length>1)
                    {
                        float join=209+spacing*((i/2)*2+1);
                        RectFill(new Rect(x+270,center,16,2),muted);RectFill(new Rect(x+284,Mathf.Min(center,join),2,Mathf.Max(2,Mathf.Abs(center-join))),muted);
                        if(i%2==0)RectFill(new Rect(x+284,join,19,2),muted);
                    }
                }
                if(slots.Length==1)break;
                var next=new int[slots.Length/2];for(int i=0;i<next.Length;i++)next[i]=consumed+i<results.Length?results[consumed+i]:-1;
                consumed+=next.Length;slots=next;column++;spacing*=2;
            }
            Label(new Rect(44,618,680,37),bracket.Complete?"CAMPEÃO DEFINIDO · "+bracket.MatchesPlayed+" CONFRONTOS":"RODADA "+bracket.Round+" · "+bracket.MatchesPlayed+" CONFRONTOS CONCLUÍDOS",20,muted);
            if(!bracket.Complete&&Button(new Rect(765,609,464,54),"JOGAR PRÓXIMO CONFRONTO  →"))StartBracketMatch();
            else if(bracket.Complete&&Button(new Rect(765,609,464,54),"VOLTAR AO MENU"))GoHome();
        }
        private void ClassicResultUI()
        {
            string winner=Catalog.Character(World.Fighters[Mathf.Clamp(matchWinner,0,1)].Id).Name;
            Header(ModeTitle(),winner+" VENCE",wins[0]+" × "+wins[1]+"  ·  "+(mode==PlayMode.Survival?survivalScore+" PONTOS":"PARTIDA CONCLUÍDA"));
            Image(new Rect(835,184,345,360),portraits[World.Fighters[Mathf.Clamp(matchWinner,0,1)].Id],ScaleMode.ScaleToFit);
            Label(new Rect(50,223,725,115),"“"+Catalog.Character(World.Fighters[Mathf.Clamp(matchWinner,0,1)].Id).Quote+"”",39,white,true);
            Label(new Rect(50,384,700,103),mode==PlayMode.Survival?survivalWave+" vitórias consecutivas · próximo confronto com "+Mathf.RoundToInt(survivalHealth)+" de vida.":classic!=null?"Seu progresso está salvo. Continue a escalada até os dois chefes.":"Jogue novamente, consulte os golpes ou reveja a partida.",27,muted);
            string next=bracket!=null?"VER CHAVEAMENTO":classic!=null?(matchWinner==0?"AVANÇAR":"TENTAR NOVAMENTE"):mode==PlayMode.Survival?(matchWinner==0?"PRÓXIMO RIVAL":"NOVA SOBREVIVÊNCIA"):"REVANCHE";
            if(Button(new Rect(44,588,366,60),next))ContinueClassic();if(Button(new Rect(455,588,366,60),"VER REPLAY",false))LoadReplay();if(Button(new Rect(866,588,366,60),"MENU PRINCIPAL",false))GoHome();
        }
        private void ExtraFightUI()
        {
            if(World==null)return;
            if(mode==PlayMode.Attract)Label(new Rect(250,584,780,63),"DEMONSTRAÇÃO · PRESSIONE PARA JOGAR",32,white,true,TextAnchor.MiddleCenter);
            if(classic!=null)Label(new Rect(420,163,440,30),"ANDAR "+(classic.Index+1)+" / "+classic.Order.Length,21,cyan,true,TextAnchor.MiddleCenter);
            if(World.TeamMode)for(int p=0;p<2;p++){var r=World.Reserve(p);float x=p==0?44:750;RectFill(new Rect(x-7,161,479,38),panel);Label(new Rect(x,165,465,30),"RESERVA · "+Catalog.Character(r.Id).Name+"  "+Mathf.CeilToInt(r.Hp)+" HP  ·  "+(World.TagCooldowns[p]>0?Mathf.CeilToInt(World.TagCooldowns[p]/60f)+"s":"TROCA PRONTA"),17,white);}
            if(mode==PlayMode.Tutorial)
            {
                string[] steps={"MOVIMENTE-SE · ande para os lados","ATAQUE · acerte três golpes","DEFESA EM PÉ · bloqueie o ataque","DEFESA BAIXA · baixo + defesa","AGARRÃO · defesa + leve","ESPECIAL · trás + leve","COMBO · conecte dois acertos","ULTIMATE · defesa + forte","DUPLA · pressione a troca","KART · complete uma corrida"};
                RectFill(new Rect(344,196,593,97),panel);Label(new Rect(364,209,551,25),"TUTORIAL  "+(tutorialStep+1)+" / 10",19,cyan,true);Label(new Rect(364,247,551,41),steps[tutorialStep],24,white,true);
                if(tutorialStep==9&&Button(new Rect(444,305,395,48),"COMEÇAR A AULA DE KART")){kartMode=KartRaceMode.Quick;kartPlayers=1;kartTrack=0;StartKart();}
            }
            if(IsTraining&&Button(new Rect(30,574,268,39),"TREINO [TAB]",false))Page=ScreenPage.TrainingOptions;
        }
        private void TrainingOptionsUI()
        {
            Header("LABORATÓRIO","CONTROLE SEU TREINO");
            string[] items={"OPONENTE: "+DummyLabel(),"VIDA INFINITA: "+Yes(trainingHealth),"ENERGIA INFINITA: "+Yes(trainingMeter),"DEFESA INFINITA: "+Yes(trainingGuard),"COLISÕES: "+Yes(showBoxes),"RIVAL: "+Catalog.Character(opponent).Name,"REINICIAR POSIÇÕES","CONTINUAR"};
            for(int i=0;i<items.Length;i++)if(Button(new Rect(44+(i/4)*606,191+(i%4)*107,586,77),items[i],false))
            {switch(i){case 0:dummyMode=(dummyMode+1)%6;break;case 1:trainingHealth=!trainingHealth;World.Training.InfiniteHealth=trainingHealth;break;case 2:trainingMeter=!trainingMeter;World.Training.InfiniteMeter=trainingMeter;break;case 3:trainingGuard=!trainingGuard;break;case 4:showBoxes=!showBoxes;Arena.ShowHitboxes=showBoxes;break;case 5:opponent=(opponent+1)%16;StartFight();Page=ScreenPage.TrainingOptions;break;case 6:StartFight();break;case 7:Page=ScreenPage.Fight;break;}}
        }
        private static string Yes(bool value)=>value?"SIM":"NÃO";
        private void KartTracksUI()
        {
            Header("KART UFN","ESCOLHA O CIRCUITO","Três voltas · oito pilotos · poderes próprios · drift com turbo");
            for(int i=0;i<5;i++)if(Button(new Rect(44+(i%3)*400,202+(i/3)*132,383,109),KartContent.TrackNames[i],kartTrack==i))kartTrack=i;
            Label(new Rect(45,509,740,91),"Acelerar: leve · Frear: forte · Drift: chute · Item: defesa\nBaixo + item lança para trás. Os controles podem ser remapeados.",23,muted);
            if(Button(new Rect(859,596,370,57),"INICIAR CORRIDA"))StartKart();
        }
        private void ExpandedKartUI()
        {
            if(kart==null)return;
            if(Button(new Rect(1080,18,174,42),"PAUSA [ESC]",false))paused=true;
            for(int p=0;p<kart.HumanCount;p++)
            {
                var racer=kart.Racers[p];float x=24+(prefs.HorizontalSplit?0:p*640),y=76+(prefs.HorizontalSplit?p*360:0);
                RectFill(new Rect(x,y,270,143),panel);Label(new Rect(x+13,y+8,245,42),racer.Place+"º  ·  VOLTA "+Mathf.Min(3,racer.Lap+1)+" / 3",27,white,true);
                Label(new Rect(x+14,y+49,240,33),Mathf.RoundToInt(racer.Speed*3.6f)+" km/h   ·   "+KartRace.ItemName(racer),21,cyan,true);
                Bar(new Rect(x+14,y+91,242,8),racer.DriftCharge/100f,gold);Label(new Rect(x+14,y+106,242,27),racer.WrongWay?"SENTIDO CONTRÁRIO":"J"+(p+1)+" · "+Catalog.Character(racer.CharacterId).Name,17,racer.WrongWay?gold:muted);
            }
            RectFill(new Rect(365,9,508,76),panel);
            Label(new Rect(379,12,480,35),kart.Track.Name,24,white,true,TextAnchor.MiddleCenter);
            Label(new Rect(379,47,480,29),TimeSpan.FromSeconds(kart.TimeSeconds).ToString(@"mm\:ss\.ff"),21,cyan,true,TextAnchor.MiddleCenter);
            if(kart.CountdownTicks>0)Label(new Rect(440,264,400,131),Mathf.CeilToInt(kart.CountdownTicks/60f).ToString(),94,gold,true,TextAnchor.MiddleCenter);
            KartMinimap();
            Label(new Rect(30,675,1190,29),"Acelere, segure o drift nas curvas e solte para usar o turbo. Comandos no menu CONTROLES.  |  ESC pausa",17,white);
            if(kart.Completed)
            {
                RectFill(new Rect(327,231,626,309),panel);Label(new Rect(350,246,580,58),kart.ChampionshipComplete?"CAMPEONATO CONCLUÍDO":kart.PlayerPlace+"º LUGAR",39,white,true,TextAnchor.MiddleCenter);
                Label(new Rect(350,314,580,84),kart.Mode==KartRaceMode.Championship?"PONTOS J1: "+kart.ChampionshipPoints[0]+(kart.HumanCount==2?"   ·   J2: "+kart.ChampionshipPoints[1]:""):"Tempo: "+TimeSpan.FromSeconds(kart.TimeSeconds).ToString(@"mm\:ss\.ff")+"\nMelhor volta: "+TimeSpan.FromMilliseconds(kart.Racers[0].BestLapMilliseconds).ToString(@"mm\:ss\.fff"),25,cyan,true,TextAnchor.MiddleCenter);
                if(Button(new Rect(352,433,278,52),kart.Mode==KartRaceMode.Championship&&!kart.ChampionshipComplete?"PRÓXIMA ETAPA":"CORRER NOVAMENTE")){if(kart.Mode==KartRaceMode.Championship&&!kart.ChampionshipComplete)NextKartRace();else StartKart();}
                if(Button(new Rect(646,433,278,52),"MENU PRINCIPAL",false))GoHome();
            }
        }
        private void KartMinimap()
        {
            Rect r=new Rect(1060,506,190,137);RectFill(r,new Color(.025f,.04f,.07f,.8f));
            float minX=float.MaxValue,maxX=float.MinValue,minY=minX,maxY=maxX;foreach(var point in kart.Track.Points){minX=Mathf.Min(minX,point.X);maxX=Mathf.Max(maxX,point.X);minY=Mathf.Min(minY,point.Y);maxY=Mathf.Max(maxY,point.Y);}
            float scale=Mathf.Min(168/(maxX-minX),112/(maxY-minY));
            foreach(var point in kart.Track.Points)RectFill(new Rect(r.x+10+(point.X-minX)*scale,r.y+10+(point.Y-minY)*scale,3,3),muted);
            foreach(var racer in kart.Racers)RectFill(new Rect(r.x+8+(racer.Position.X-minX)*scale,r.y+8+(racer.Position.Y-minY)*scale,7,7),racer.Index<kart.HumanCount?cyan:gold);
        }
    }
}
