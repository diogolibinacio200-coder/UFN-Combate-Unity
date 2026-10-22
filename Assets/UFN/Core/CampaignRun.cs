using System;
using System.Collections.Generic;

namespace UFN.Core
{
    public enum CampaignMode { Tower, Survival, Tournament }
    public enum BenefitKind { Vitality, Power, Focus, Recovery, Fortune }

    [Serializable]
    public sealed class CampaignSnapshot
    {
        public int Version = 1;
        public CampaignMode Mode;
        public int CharacterId;
        public int Seed;
        public uint RandomState;
        public int Stage;
        public int Wins;
        public int Losses;
        public int[] Opponents = Array.Empty<int>();
        public int[] Bracket = Array.Empty<int>();
        public int[] Benefits = new int[5];
        public BenefitKind[] Choices = Array.Empty<BenefitKind>();
        public bool Complete;
        public bool Won;
    }

    /// <summary>Pure campaign state. A pending perk must be chosen before the next fight.</summary>
    public sealed class CampaignRun
    {
        private CampaignSnapshot state;
        public CampaignMode Mode => state.Mode;
        public int CharacterId => state.CharacterId;
        public int Stage => state.Stage;
        public int Wins => state.Wins;
        public int Losses => state.Losses;
        public bool Complete => state.Complete;
        public bool Won => state.Won;
        public bool AwaitingBenefit => state.Choices.Length > 0 && !Complete;
        public BenefitKind[] BenefitChoices => (BenefitKind[])state.Choices.Clone();
        public int OpponentId => Complete ? -1 : Mode == CampaignMode.Tournament ? TournamentOpponent() : state.Opponents[Math.Min(Stage, state.Opponents.Length - 1)];
        public float HealthMultiplier => 1f + Math.Min(.4f, .07f * state.Benefits[0] + .04f * state.Benefits[3]);
        public float DamageMultiplier => 1f + Math.Min(.25f, .04f * state.Benefits[1]);
        public float MeterBonus => Math.Min(60, 10 * state.Benefits[2] + 5 * state.Benefits[4]);
        public int Difficulty => Math.Min(5, 1 + Stage / (Mode == CampaignMode.Survival ? 3 : 2));
        public string StageLabel => Mode == CampaignMode.Survival ? "SOBREVIVÊNCIA · ONDA " + (Stage + 1) : Mode == CampaignMode.Tournament ? new[] { "OITAVAS", "QUARTAS", "SEMIFINAL", "FINAL" }[Math.Min(3, Stage)] : "TORRE · " + (Math.Min(Stage + 1, state.Opponents.Length)) + "/" + state.Opponents.Length;
        public string Description => Mode == CampaignMode.Tower ? "Suba a torre, escolha melhorias e enfrente André e Kelvin." : Mode == CampaignMode.Survival ? "Uma sequência sem fim. Cada vitória fortalece os próximos adversários." : "Dezesseis participantes. Quatro vitórias para conquistar o torneio.";

        private CampaignRun(CampaignSnapshot snapshot) { state = snapshot; }

        public static CampaignRun Create(CampaignMode mode, int characterId, int seed, int[] towerOrder = null)
        {
            if (characterId < 0 || characterId > 13) throw new ArgumentOutOfRangeException(nameof(characterId));
            if ((int)mode < 0 || (int)mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
            var result = new CampaignRun(new CampaignSnapshot { Mode = mode, CharacterId = characterId, Seed = seed, RandomState = unchecked((uint)seed) ^ 0x9e3779b9u });
            if (result.state.RandomState == 0) result.state.RandomState = 1;
            if (mode == CampaignMode.Tower)
            {
                result.state.Opponents = towerOrder == null ? result.DefaultTower() : (int[])towerOrder.Clone();
                ValidateTower(result.state.Opponents);
            }
            else if (mode == CampaignMode.Survival)
            {
                result.state.Opponents = new[] { result.NextInt(14) };
                if (result.state.Opponents[0] == characterId) result.state.Opponents[0] = (characterId + 1) % 14;
            }
            else
            {
                result.state.Bracket = new int[16];
                for (int i = 0; i < 16; i++) result.state.Bracket[i] = i;
                result.Shuffle(result.state.Bracket);
            }
            return result;
        }

        private int[] DefaultTower()
        {
            var candidates = new List<int>();
            for (int id = 0; id < 14; id++) if (id != CharacterId) candidates.Add(id);
            var shuffled = candidates.ToArray();
            Shuffle(shuffled);
            return new[] { shuffled[0], shuffled[1], shuffled[2], shuffled[3], shuffled[4], 14, 15 };
        }

        private static void ValidateTower(int[] opponents)
        {
            if (opponents == null || opponents.Length < 2 || opponents.Length > 64 || opponents[opponents.Length - 2] != 14 || opponents[opponents.Length - 1] != 15)
                throw new ArgumentException("A torre deve terminar com os chefes André (14) e Kelvin (15).");
            foreach (int id in opponents) if (id < 0 || id > 15) throw new ArgumentException("Personagem inválido na torre.");
        }

        public void RecordVictory()
        {
            EnsureFightReady();
            state.Wins++;
            if (Mode == CampaignMode.Tournament)
            {
                var survivors = new int[state.Bracket.Length / 2];
                for (int i = 0; i < survivors.Length; i++)
                {
                    int a = state.Bracket[i * 2], b = state.Bracket[i * 2 + 1];
                    survivors[i] = a == CharacterId || b == CharacterId ? CharacterId : NextInt(2) == 0 ? a : b;
                }
                state.Bracket = survivors;
                state.Stage++;
                if (survivors.Length == 1) End(true);
            }
            else
            {
                state.Stage++;
                if (Mode == CampaignMode.Tower && Stage >= state.Opponents.Length) End(true);
                if (Mode == CampaignMode.Survival)
                {
                    // Boss encounters mark each fifth wave; ordinary waves avoid an immediate repeat.
                    int previous = state.Opponents[state.Opponents.Length - 1];
                    int opponent = (Stage + 1) % 5 == 0 ? 14 + (Stage / 5) % 2 : NextInt(14);
                    if (opponent == previous && opponent < 14) opponent = (opponent + 1) % 14;
                    // Only the current wave needs storage, so a long run has bounded save size.
                    state.Opponents = new[] { opponent };
                }
            }
            if (!Complete) OfferBenefits();
        }

        public void RecordDefeat() { EnsureFightReady(); state.Losses++; End(false); }

        public void ChooseBenefit(int choice)
        {
            if (!AwaitingBenefit || choice < 0 || choice >= state.Choices.Length) throw new InvalidOperationException("Escolha de benefício inválida.");
            int kind = (int)state.Choices[choice];
            state.Benefits[kind] = Math.Min(100, state.Benefits[kind] + 1);
            state.Choices = Array.Empty<BenefitKind>();
        }

        public static string BenefitLabel(BenefitKind benefit)
        {
            switch (benefit)
            {
                case BenefitKind.Vitality: return "VITALIDADE";
                case BenefitKind.Power: return "POTÊNCIA";
                case BenefitKind.Focus: return "FOCO";
                case BenefitKind.Recovery: return "RECUPERAÇÃO";
                default: return "CONFIANÇA";
            }
        }
        public static string BenefitDescription(BenefitKind benefit)
        {
            switch (benefit)
            {
                case BenefitKind.Vitality: return "+7% de vida máxima nas próximas lutas (limite total +40%).";
                case BenefitKind.Power: return "+4% de dano nas próximas lutas (limite +25%).";
                case BenefitKind.Focus: return "+10 de energia ao iniciar cada luta (limite 60).";
                case BenefitKind.Recovery: return "+4% de vida máxima nas próximas lutas (limite total +40%).";
                default: return "+5 de energia ao iniciar cada luta (limite 60).";
            }
        }

        private void OfferBenefits()
        {
            var pool = new[] { 0, 1, 2, 3, 4 };
            Shuffle(pool);
            state.Choices = new[] { (BenefitKind)pool[0], (BenefitKind)pool[1], (BenefitKind)pool[2] };
        }
        private int TournamentOpponent()
        {
            int index = Array.IndexOf(state.Bracket, CharacterId);
            if (index < 0 || state.Bracket.Length < 2) throw new InvalidOperationException("Chave do torneio inválida.");
            return state.Bracket[index ^ 1];
        }
        private void EnsureFightReady() { if (Complete || AwaitingBenefit) throw new InvalidOperationException("A campanha não aguarda um resultado de luta."); }
        private void End(bool won) { state.Complete = true; state.Won = won; state.Choices = Array.Empty<BenefitKind>(); }
        private int NextInt(int count) { uint x = state.RandomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5; state.RandomState = x; return (int)(x % (uint)count); }
        private void Shuffle(int[] values) { for (int i = values.Length - 1; i > 0; i--) { int j = NextInt(i + 1); int hold = values[i]; values[i] = values[j]; values[j] = hold; } }

        public CampaignSnapshot Export()
        {
            return new CampaignSnapshot { Version = 1, Mode = Mode, CharacterId = CharacterId, Seed = state.Seed, RandomState = state.RandomState, Stage = Stage, Wins = Wins, Losses = Losses, Opponents = (int[])state.Opponents.Clone(), Bracket = (int[])state.Bracket.Clone(), Benefits = (int[])state.Benefits.Clone(), Choices = (BenefitKind[])state.Choices.Clone(), Complete = Complete, Won = Won };
        }

        public static CampaignRun Restore(CampaignSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Version != 1 || snapshot.CharacterId < 0 || snapshot.CharacterId > 13 || (int)snapshot.Mode < 0 || (int)snapshot.Mode > 2 || snapshot.Stage < 0 || snapshot.Stage > 100000 || snapshot.Wins < 0 || snapshot.Losses < 0 || snapshot.RandomState == 0 || snapshot.Benefits == null || snapshot.Benefits.Length != 5 || snapshot.Choices == null || snapshot.Choices.Length > 3 || snapshot.Opponents == null || snapshot.Bracket == null)
                throw new ArgumentException("Progresso da campanha inválido.");
            foreach (int benefit in snapshot.Benefits) if (benefit < 0 || benefit > 100) throw new ArgumentException("Benefício inválido.");
            var seenChoices = new HashSet<BenefitKind>();
            foreach (BenefitKind choice in snapshot.Choices) if ((int)choice < 0 || (int)choice > 4 || !seenChoices.Add(choice)) throw new ArgumentException("Escolhas inválidas.");
            if (snapshot.Mode == CampaignMode.Tower)
            {
                ValidateTower(snapshot.Opponents);
                if (snapshot.Stage > snapshot.Opponents.Length || (!snapshot.Complete && snapshot.Stage >= snapshot.Opponents.Length)) throw new ArgumentException("Andar inválido.");
            }
            if (snapshot.Mode == CampaignMode.Survival && (snapshot.Opponents.Length != 1 || snapshot.Opponents[0] < 0 || snapshot.Opponents[0] > 15)) throw new ArgumentException("Onda inválida.");
            if (snapshot.Mode == CampaignMode.Tournament)
            {
                int expected = 16 >> Math.Min(snapshot.Stage, 4);
                if (snapshot.Stage > 4 || snapshot.Bracket.Length != expected || Array.IndexOf(snapshot.Bracket, snapshot.CharacterId) < 0) throw new ArgumentException("Chave inválida.");
                var seen = new HashSet<int>();
                foreach (int fighter in snapshot.Bracket) if (fighter < 0 || fighter > 15 || !seen.Add(fighter)) throw new ArgumentException("Participante inválido.");
            }
            if (snapshot.Complete && snapshot.Choices.Length > 0) throw new ArgumentException("Campanha concluída com escolhas pendentes.");
            return new CampaignRun(new CampaignRun(snapshot).Export());
        }
    }
}
