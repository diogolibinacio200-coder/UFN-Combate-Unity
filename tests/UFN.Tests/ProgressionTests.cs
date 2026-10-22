using System;
using System.Collections.Generic;
using System.Text.Json;
using UFN.Core;

namespace UFN.Tests
{
    public static class ProgressionTests
    {
        public static int Checks { get; private set; }
        private static readonly List<string> failures = new List<string>();
        private static readonly JsonSerializerOptions json = new JsonSerializerOptions { IncludeFields = true };
        private static void Check(bool pass, string message) { Checks++; if (!pass) failures.Add(message); }
        private static void Reject(Action action, string message)
        {
            try { action(); Check(false, message); }
            catch (ArgumentException) { Check(true, message); }
            catch (InvalidOperationException) { Check(true, message); }
        }
        private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, json), json);
        private static bool Equal(int[] a, int[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

        public static List<string> Run()
        {
            Checks = 0; failures.Clear(); Routes(); Brackets(); Survival(); return new List<string>(failures);
        }

        private static void Routes()
        {
            for (int id = 0; id < 14; id++) foreach (int count in new[] { 3, 5, 8, 10 }) for (int difficulty = 0; difficulty < 4; difficulty++)
            {
                var run = ClassicRun.Create(true, id, id % 2, count, difficulty, 412u);
                var order = run.Order; var used = new HashSet<int>();
                bool regular = true;
                for (int i = 0; i < count; i++) regular &= order[i] != id && order[i] >= 0 && order[i] < 14 && used.Add(order[i]);
                Check(regular && order.Length == count + 2 && order[count] == 14 && order[count + 1] == 15, "Tower excludes player, has unique rivals and both final bosses");
                Check(Equal(order, ClassicRun.Create(true, id, id % 2, count, difficulty, 412u).Order), "Seed deterministically preserves tower route");
                for (int step = 0; step < order.Length; step++)
                {
                    var restored = ClassicRun.Restore(RoundTrip(run.ExportSnapshot()));
                    Check(restored.Character == id && restored.Skin == id % 2 && restored.Difficulty == difficulty && restored.Count == count && restored.Index == step && restored.Opponent == order[step] && !restored.Complete, "Resume preserves every next opponent and selection");
                    Check(run.Index == step && run.Opponent == order[step], "Saving/retrying never advances without a victory");
                    run.AdvanceVictory();
                }
                Check(run.Complete && run.Opponent == -1 && ClassicRun.Restore(RoundTrip(run.ExportSnapshot())).Complete, "Final boss victory completes and persists route");
                Reject(run.AdvanceVictory, "Cannot advance beyond final boss");
            }
            for (int id = 0; id < 14; id++)
            {
                var arcade = ClassicRun.Create(false, id, 0, 8, 1, (uint)id);
                Check(arcade.Order.Length == 10 && !arcade.Tower && arcade.Index == 0, "Arcade always has eight rivals and two bosses");
            }
            var a = ClassicRun.Create(true, 3, 1, 5, 2, 99); var saved = a.ExportSnapshot(); var external = a.Order;
            saved.Order[0] = 99; external[1] = 99;
            Check(a.Order[0] != 99 && a.Order[1] != 99, "Exported routes cannot mutate live state");
            var s = a.ExportSnapshot(); var b = ClassicRun.Restore(s); s.Order[0] = 99;
            Check(b.Order[0] != 99, "Restore defensively copies route");
            Reject(() => ClassicRun.Create(false, 1, 0, 3, 1, 1), "Reject nonstandard arcade size");
            Reject(() => ClassicRun.Create(true, 1, 0, 4, 1, 1), "Reject unsupported tower size");
            Reject(() => ClassicRun.Create(true, 14, 0, 3, 1, 1), "Reject boss as classic player");
            Reject(() => ClassicRun.Create(true, 1, 2, 3, 1, 1), "Reject invalid classic skin");
            Reject(() => ClassicRun.Create(true, 1, 0, 3, 4, 1), "Reject invalid classic difficulty");
            Reject(() => ClassicRun.Restore(null), "Reject absent classic save");
            foreach (Action<ClassicSnapshot> corrupt in new Action<ClassicSnapshot>[] {
                x => x.Version = 2, x => x.Order = null, x => x.Order = new[] { 0 }, x => x.Index = -1, x => x.Index = 99,
                x => x.Order[0] = x.Character, x => x.Order[0] = x.Order[1], x => x.Order[0] = 14, x => x.Order[5] = 15, x => x.Order[6] = 14,
                x => x.Character = -1, x => x.Skin = -1, x => x.Difficulty = -1, x => x.Count = 99 })
            { var broken = a.ExportSnapshot(); corrupt(broken); Reject(() => ClassicRun.Restore(broken), "Reject malformed classic snapshot"); }
        }

        private static void Brackets()
        {
            foreach (int size in new[] { 4, 8 }) foreach (int cpuPolicy in new[] { 0, 1, 2 })
            {
                var ids = new int[size]; var skins = new int[size]; var cpu = new bool[size];
                for (int i = 0; i < size; i++) { ids[i] = i == 1 ? 0 : i; skins[i] = i % 2; cpu[i] = cpuPolicy == 1 || cpuPolicy == 2 && i % 2 == 1; }
                var bracket = LocalBracket.Create(ids, skins, cpu); var winCounts = new int[size]; var remaining = new List<int>();
                for (int i = 0; i < size; i++) remaining.Add(i);
                int rounds = size == 4 ? 2 : 3, match = 0;
                for (int round = 1; round <= rounds; round++)
                {
                    var advancing = new List<int>();
                    for (int pair = 0; pair < remaining.Count / 2; pair++)
                    {
                        int left = remaining[pair * 2], right = remaining[pair * 2 + 1];
                        Check(bracket.PairA == left && bracket.PairB == right && bracket.Round == round && bracket.MatchIndex == pair, "Real elimination schedules advancing slots in order");
                        for (int slot = 0; slot < size; slot++) Check(bracket.Character(slot) == ids[slot] && bracket.Skin(slot) == skins[slot] && bracket.IsCpu(slot) == cpu[slot], "Each participant retains character, skin and human/CPU control");
                        var restored = LocalBracket.Restore(RoundTrip(bracket.ExportSnapshot()));
                        Check(restored.PairA == left && restored.PairB == right && restored.Round == round && restored.MatchIndex == pair && restored.MatchesPlayed == match, "Mid-round restore reconstructs pending pair");
                        int side = match % 2, winner = side == 0 ? left : right;
                        bracket.RecordWinner(side); restored.RecordWinner(side); winCounts[winner]++; advancing.Add(winner); match++;
                        Check(bracket.PairA == restored.PairA && bracket.PairB == restored.PairB, "Restored bracket follows same next pair");
                        for (int slot = 0; slot < size; slot++) Check(bracket.Wins(slot) == winCounts[slot] && restored.Wins(slot) == winCounts[slot], "Wins attach to participant slots, including mirrors");
                    }
                    remaining = advancing;
                }
                Check(bracket.Complete && bracket.Champion == remaining[0] && bracket.PairA == -1 && bracket.PairB == -1 && match == size - 1 && bracket.Wins(bracket.Champion) == rounds, "Tournament finishes after size-minus-one real matches");
                var final = LocalBracket.Restore(RoundTrip(bracket.ExportSnapshot()));
                Check(final.Complete && final.Champion == bracket.Champion && final.MatchesPlayed == size - 1, "Champion persists after final match");
                Reject(() => bracket.RecordWinner(0), "Reject duplicate result after tournament final");
            }
            var chars = new[] { 3, 3, 5, 6 }; var skin = new[] { 0, 1, 0, 0 }; var bots = new[] { false, false, true, true };
            var b = LocalBracket.Create(chars, skin, bots); chars[0] = 10; skin[0] = 1; bots[0] = true;
            Check(b.Character(0) == 3 && b.Skin(0) == 0 && !b.IsCpu(0), "Bracket takes independent copy of slot configuration");
            var s = b.ExportSnapshot(); s.Characters[0] = 9; s.Skins[0] = 1; s.Cpu[0] = true;
            Check(b.Character(0) == 3 && b.Skin(0) == 0 && !b.IsCpu(0), "Bracket snapshot cannot mutate live configuration");
            Reject(() => b.RecordWinner(-1), "Reject negative winner side"); Reject(() => b.RecordWinner(2), "Tie cannot eliminate a tournament participant");
            Reject(() => b.Character(4), "Reject out-of-bounds slot");
            Reject(() => LocalBracket.Create(new[] { 0, 1 }, new[] { 0, 0 }, new[] { true, false }), "Reject unsupported tournament size");
            Reject(() => LocalBracket.Create(chars, new[] { 0 }, bots), "Reject incomplete participant settings");
            Reject(() => LocalBracket.Restore(null), "Reject absent bracket snapshot");
            foreach (Action<LocalBracketSnapshot> corrupt in new Action<LocalBracketSnapshot>[] {
                x => x.Version = 2, x => x.Characters = null, x => x.Cpu = null, x => x.Skins[0] = 2, x => x.Characters[0] = 14,
                x => x.Winners = null, x => x.Winners = new[] { 2 }, x => x.Winners = new[] { 0, 0 }, x => x.Winners = new[] { 0, 2, 1 },
                x => x.Winners = new[] { 0, 2, 0, 0 } })
            { var broken = b.ExportSnapshot(); corrupt(broken); Reject(() => LocalBracket.Restore(broken), "Reject impossible saved tournament history"); }
        }

        private static void Survival()
        {
            var run = SurvivalRun.Create(3, 1, 0, 2);
            Check(run.Health == 1000 && run.Score == 0 && run.Wave == 0 && run.Opponent == 0, "Classic survival begins at full health with selected rival");
            run.AdvanceVictory(700.9f, 61);
            Check(Math.Abs(run.Health - 880.9f) < .001f && run.Score == 1720 && run.Wave == 1 && run.Opponent == 4, "Survival truncates HP score, rounds timer up, heals 180 and skips own character");
            var restored = SurvivalRun.Restore(RoundTrip(run.ExportSnapshot()));
            restored.AdvanceVictory(900.6f, 0); run.AdvanceVictory(900.6f, 0);
            Check(run.Health == 1000 && run.Score == 3620 && run.Opponent == 7 && run.Wave == 2 && restored.Score == run.Score && restored.Health == run.Health, "Survival health is capped and restored continuation is equivalent");
            for (int wave = 0; wave < 120; wave++)
            {
                int expected = (run.Opponent + 3) % 14; if (expected == run.Character) expected = (expected + 1) % 14;
                run.AdvanceVictory(1, 60);
                Check(run.Opponent == expected && run.Opponent != 3 && run.Health == 181 && !run.Complete, "Survival keeps original endless rival order and health carryover");
            }
            var copy = run.ExportSnapshot(); copy.Health = 1000; Check(run.Health == 181, "Survival export cannot mutate health");
            var b = SurvivalRun.Restore(run.ExportSnapshot()); b.RecordDefeat();
            Check(b.Complete && SurvivalRun.Restore(RoundTrip(b.ExportSnapshot())).Complete, "Defeat ends survival and persists score");
            Reject(() => b.AdvanceVictory(100, 0), "Ended survival cannot advance");
            Reject(() => run.AdvanceVictory(float.NaN, 60), "Reject NaN survival health");
            Reject(() => run.AdvanceVictory(float.PositiveInfinity, 60), "Reject infinite survival health");
            Reject(() => run.AdvanceVictory(0, 60), "Dead fighter cannot win survival");
            Reject(() => run.AdvanceVictory(1001, 60), "Reject health above classic cap");
            Reject(() => run.AdvanceVictory(100, -1), "Reject negative survival timer");
            Reject(() => SurvivalRun.Create(3, 0, 3), "Survival opponent cannot equal player");
            Reject(() => SurvivalRun.Restore(null), "Reject absent survival snapshot");
            foreach (Action<SurvivalSnapshot> corrupt in new Action<SurvivalSnapshot>[] { x => x.Version = 0, x => x.Opponent = 14, x => x.Opponent = 3, x => x.Health = float.NaN, x => x.Health = 0, x => x.Wave = -1, x => x.Score = -1 })
            { var broken = run.ExportSnapshot(); corrupt(broken); Reject(() => SurvivalRun.Restore(broken), "Reject malformed survival snapshot"); }
        }
    }
}
