using System;

namespace UFN.Core
{
    [Serializable]
    public sealed class KartChampionshipSnapshot
    {
        public int Version=1,Seed,Track,Player,SecondPlayer=-1,PlayerSkin,SecondSkin,Difficulty=1;
        public int[] Points=new int[8];
    }
    public sealed partial class KartRace
    {
        /// <summary>Stores the current stage's starting grid, or the next stage after
        /// its result. Reloading a mid-race save restarts that stage, as in 0.6.</summary>
        public KartChampionshipSnapshot ExportChampionship()
        {
            if(Mode!=KartRaceMode.Championship)throw new InvalidOperationException("Esta corrida não faz parte de um campeonato.");
            if(ChampionshipComplete)return null;
            return new KartChampionshipSnapshot{Seed=initialSeed,Track=Track.Id+(Completed?1:0),Player=Racers[0].CharacterId,SecondPlayer=HumanCount==2?Racers[1].CharacterId:-1,PlayerSkin=Racers[0].Skin,SecondSkin=HumanCount==2?Racers[1].Skin:0,Difficulty=Difficulty,Points=(int[])ChampionshipPoints.Clone()};
        }
        public static KartRace RestoreChampionship(KartChampionshipSnapshot snapshot)
        {
            if(snapshot==null||snapshot.Version!=1||snapshot.Track<0||snapshot.Track>3||snapshot.Player<0||snapshot.Player>13||snapshot.SecondPlayer< -1||snapshot.SecondPlayer>13||snapshot.PlayerSkin<0||snapshot.PlayerSkin>1||snapshot.SecondSkin<0||snapshot.SecondSkin>1||snapshot.Difficulty<0||snapshot.Difficulty>2||snapshot.Points==null||snapshot.Points.Length!=8)throw new ArgumentException("Campeonato salvo inválido.");
            int sum=0;foreach(int points in snapshot.Points){if(points<0||points>15*snapshot.Track)throw new ArgumentException("Pontuação de campeonato inválida.");sum+=points;}
            if(sum!=58*snapshot.Track)throw new ArgumentException("Pontuação incompatível com a etapa salva.");
            var race=new KartRace(snapshot.Seed,snapshot.Player,snapshot.Track,KartRaceMode.Championship,snapshot.SecondPlayer,snapshot.Player==3?snapshot.PlayerSkin:0,snapshot.SecondPlayer==3?snapshot.SecondSkin:0,snapshot.Difficulty);
            race.ChampionshipPoints=(int[])snapshot.Points.Clone();return race;
        }
    }
}
