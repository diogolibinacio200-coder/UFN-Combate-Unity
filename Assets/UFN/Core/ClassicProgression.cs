using System;
using System.Collections.Generic;

namespace UFN.Core
{
    [Serializable]
    public sealed class ClassicSnapshot
    {
        public int Version = 1;
        public bool Tower;
        public int Character, Skin, Count, Difficulty, Index;
        public uint Seed;
        public int[] Order = Array.Empty<int>();
    }

    /// <summary>Original arcade/tower route. The host records a victory after the best-of-three match.</summary>
    public sealed class ClassicRun
    {
        private readonly ClassicSnapshot state;
        private ClassicRun(ClassicSnapshot snapshot) { state = snapshot; }
        public bool Tower => state.Tower;
        public int Character => state.Character;
        public int Skin => state.Skin;
        public int Difficulty => state.Difficulty;
        public int Count => state.Count;
        public int Index => state.Index;
        public int[] Order => (int[])state.Order.Clone();
        public bool Complete => Index == state.Order.Length;
        public int Opponent => Complete ? -1 : state.Order[Index];

        public static ClassicRun Create(bool tower, int character, int skin, int count, int difficulty, uint seed)
        {
            ProgressionValidation.Character(character); ProgressionValidation.Skin(skin); ProgressionValidation.Difficulty(difficulty);
            ValidateCount(tower, count);
            var available = new List<int>(13);
            for (int id = 0; id < 14; id++) if (id != character) available.Add(id);
            uint random = seed == 0 ? 1u : seed;
            for (int i = available.Count - 1; i > 0; i--)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                int j = (int)(random % (uint)(i + 1)); int swap = available[i]; available[i] = available[j]; available[j] = swap;
            }
            var order = new int[count + 2]; available.CopyTo(0, order, 0, count); order[count] = 14; order[count + 1] = 15;
            return new ClassicRun(new ClassicSnapshot { Tower = tower, Character = character, Skin = skin, Count = count, Difficulty = difficulty, Seed = seed, Order = order });
        }

        public void AdvanceVictory()
        {
            if (Complete) throw new InvalidOperationException("The route is already complete.");
            state.Index++;
        }

        public ClassicSnapshot ExportSnapshot() => new ClassicSnapshot { Tower = Tower, Character = Character, Skin = Skin, Count = Count, Difficulty = Difficulty, Seed = state.Seed, Index = Index, Order = Order };

        public static ClassicRun Restore(ClassicSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Version != 1) throw new ArgumentException("Unsupported classic progression version.");
            ProgressionValidation.Character(snapshot.Character); ProgressionValidation.Skin(snapshot.Skin); ProgressionValidation.Difficulty(snapshot.Difficulty);
            ValidateCount(snapshot.Tower, snapshot.Count);
            if (snapshot.Order == null || snapshot.Order.Length != snapshot.Count + 2 || snapshot.Index < 0 || snapshot.Index > snapshot.Order.Length)
                throw new ArgumentException("Invalid route length or progress.");
            var ids = new HashSet<int>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                int id = snapshot.Order[i]; ProgressionValidation.Character(id);
                if (id == snapshot.Character || !ids.Add(id)) throw new ArgumentException("Regular opponents must be unique and exclude the player.");
            }
            if (snapshot.Order[snapshot.Count] != 14 || snapshot.Order[snapshot.Count + 1] != 15) throw new ArgumentException("The final opponents must be André and Kelvin.");
            return new ClassicRun(new ClassicSnapshot { Tower = snapshot.Tower, Character = snapshot.Character, Skin = snapshot.Skin, Count = snapshot.Count, Difficulty = snapshot.Difficulty, Seed = snapshot.Seed, Index = snapshot.Index, Order = (int[])snapshot.Order.Clone() });
        }

        private static void ValidateCount(bool tower, int count)
        {
            if (tower ? count != 3 && count != 5 && count != 8 && count != 10 : count != 8)
                throw new ArgumentOutOfRangeException(nameof(count), "Arcade has eight regular opponents; towers offer three, five, eight or ten.");
        }
    }

    [Serializable]
    public sealed class LocalBracketSnapshot
    {
        public int Version = 1;
        public int[] Characters = Array.Empty<int>();
        public int[] Skins = Array.Empty<int>();
        public bool[] Cpu = Array.Empty<bool>();
        // Winners are participant slots, in played-match order. Character IDs may be duplicated.
        public int[] Winners = Array.Empty<int>();
    }

    /// <summary>Local single elimination. Every participant has its own identity, even for mirror matches.</summary>
    public sealed class LocalBracket
    {
        private readonly int[] characters, skins, wins;
        private readonly bool[] cpu;
        private int[] current;
        private readonly List<int> next = new List<int>();
        private readonly List<int> winners = new List<int>();
        public int Size => characters.Length;
        public int Round { get; private set; } = 1;
        public int MatchIndex { get; private set; }
        public int MatchesPlayed => winners.Count;
        public bool Complete => current.Length == 1;
        public int Champion => Complete ? current[0] : -1;
        public int PairA => Complete ? -1 : current[MatchIndex * 2];
        public int PairB => Complete ? -1 : current[MatchIndex * 2 + 1];
        public int[] CurrentSlots => (int[])current.Clone();
        public int[] WinnerSlots => winners.ToArray();

        private LocalBracket(int[] ids, int[] selectedSkins, bool[] bots)
        {
            characters = (int[])ids.Clone(); skins = (int[])selectedSkins.Clone(); cpu = (bool[])bots.Clone(); wins = new int[ids.Length];
            current = new int[ids.Length]; for (int i = 0; i < ids.Length; i++) current[i] = i;
        }

        public static LocalBracket Create(int[] ids, int[] skins, bool[] cpu)
        {
            if (ids == null || skins == null || cpu == null) throw new ArgumentNullException("All participant arrays are required.");
            if ((ids.Length != 4 && ids.Length != 8) || skins.Length != ids.Length || cpu.Length != ids.Length) throw new ArgumentException("A local tournament requires four or eight complete participant slots.");
            for (int i = 0; i < ids.Length; i++) { ProgressionValidation.Character(ids[i]); ProgressionValidation.Skin(skins[i]); }
            return new LocalBracket(ids, skins, cpu);
        }

        public int Character(int slot) { ValidateSlot(slot); return characters[slot]; }
        public int Skin(int slot) { ValidateSlot(slot); return skins[slot]; }
        public bool IsCpu(int slot) { ValidateSlot(slot); return cpu[slot]; }
        public int Wins(int slot) { ValidateSlot(slot); return wins[slot]; }

        public void RecordWinner(int side)
        {
            if (Complete) throw new InvalidOperationException("The tournament is already complete.");
            if (side < 0 || side > 1) throw new ArgumentOutOfRangeException(nameof(side));
            int slot = side == 0 ? PairA : PairB;
            winners.Add(slot); wins[slot]++; next.Add(slot); MatchIndex++;
            if (MatchIndex * 2 < current.Length) return;
            current = next.ToArray(); next.Clear(); MatchIndex = 0;
            if (!Complete) Round++;
        }

        public LocalBracketSnapshot ExportSnapshot() => new LocalBracketSnapshot { Characters = (int[])characters.Clone(), Skins = (int[])skins.Clone(), Cpu = (bool[])cpu.Clone(), Winners = winners.ToArray() };

        public static LocalBracket Restore(LocalBracketSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Version != 1 || snapshot.Winners == null) throw new ArgumentException("Invalid tournament snapshot.");
            var result = Create(snapshot.Characters, snapshot.Skins, snapshot.Cpu);
            if (snapshot.Winners.Length >= result.Size) throw new ArgumentException("Too many tournament results.");
            foreach (int slot in snapshot.Winners)
            {
                if (slot != result.PairA && slot != result.PairB) throw new ArgumentException("A winner is not in the scheduled pair.");
                result.RecordWinner(slot == result.PairA ? 0 : 1);
            }
            return result;
        }

        private void ValidateSlot(int slot) { if (slot < 0 || slot >= Size) throw new ArgumentOutOfRangeException(nameof(slot)); }
    }

    [Serializable]
    public sealed class SurvivalSnapshot
    {
        public int Version = 1;
        public int Character, Skin, Difficulty, Opponent, Wave;
        public float Health = 1000;
        public long Score;
        public bool Complete;
    }

    public sealed class SurvivalRun
    {
        private readonly SurvivalSnapshot state;
        private SurvivalRun(SurvivalSnapshot snapshot) { state = snapshot; }
        public int Character => state.Character;
        public int Skin => state.Skin;
        public int Difficulty => state.Difficulty;
        public int Opponent => state.Opponent;
        public int Wave => state.Wave;
        public float Health => state.Health;
        public long Score => state.Score;
        public bool Complete => state.Complete;

        public static SurvivalRun Create(int character, int skin, int opponent = -1, int difficulty = 1)
        {
            if (opponent == -1) opponent = (character + 1) % 14;
            return Restore(new SurvivalSnapshot { Character = character, Skin = skin, Difficulty = difficulty, Opponent = opponent });
        }

        public void AdvanceVictory(float remainingHp, int timerFrames)
        {
            if (Complete) throw new InvalidOperationException("Survival has ended.");
            if (float.IsNaN(remainingHp) || float.IsInfinity(remainingHp) || remainingHp <= 0 || remainingHp > 1000) throw new ArgumentOutOfRangeException(nameof(remainingHp));
            if (timerFrames < 0) throw new ArgumentOutOfRangeException(nameof(timerFrames));
            long reward = 1000L + (int)remainingHp + ((timerFrames + 59L) / 60L) * 10L;
            if (state.Score > long.MaxValue - reward || state.Wave == int.MaxValue) throw new InvalidOperationException("Survival progress exceeds the save format.");
            state.Health = Math.Min(1000, remainingHp + 180); state.Score += reward; state.Wave++;
            state.Opponent = (state.Opponent + 3) % 14;
            if (state.Opponent == Character) state.Opponent = (state.Opponent + 1) % 14;
        }

        public void RecordDefeat() { if (Complete) throw new InvalidOperationException("Survival has ended."); state.Complete = true; }
        public SurvivalSnapshot ExportSnapshot() => new SurvivalSnapshot { Character = Character, Skin = Skin, Difficulty = Difficulty, Opponent = Opponent, Wave = Wave, Health = Health, Score = Score, Complete = Complete };
        public static SurvivalRun Restore(SurvivalSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Version != 1) throw new ArgumentException("Unsupported survival version.");
            ProgressionValidation.Character(snapshot.Character); ProgressionValidation.Character(snapshot.Opponent); ProgressionValidation.Skin(snapshot.Skin); ProgressionValidation.Difficulty(snapshot.Difficulty);
            if (snapshot.Character == snapshot.Opponent || snapshot.Wave < 0 || snapshot.Score < 0 || float.IsNaN(snapshot.Health) || float.IsInfinity(snapshot.Health) || snapshot.Health <= 0 || snapshot.Health > 1000)
                throw new ArgumentException("Invalid survival progress.");
            return new SurvivalRun(new SurvivalSnapshot { Character = snapshot.Character, Skin = snapshot.Skin, Difficulty = snapshot.Difficulty, Opponent = snapshot.Opponent, Wave = snapshot.Wave, Health = snapshot.Health, Score = snapshot.Score, Complete = snapshot.Complete });
        }
    }

    internal static class ProgressionValidation
    {
        internal static void Character(int id) { if (id < 0 || id >= 14) throw new ArgumentOutOfRangeException(nameof(id)); }
        internal static void Skin(int skin) { if (skin < 0 || skin > 1) throw new ArgumentOutOfRangeException(nameof(skin)); }
        internal static void Difficulty(int difficulty) { if (difficulty < 0 || difficulty > 3) throw new ArgumentOutOfRangeException(nameof(difficulty)); }
    }
}
