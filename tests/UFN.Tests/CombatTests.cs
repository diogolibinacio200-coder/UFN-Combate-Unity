using System;
using System.Collections.Generic;
using UFN.Core;

namespace UFN.Tests
{
    public static class CombatTests
    {
        public static int Checks { get; private set; }
        private static readonly List<string> Failures = new List<string>();
        private static void Check(bool pass, string label) { Checks++; if (!pass) Failures.Add(label); }
        private static bool Near(float a, float b) => Math.Abs(a - b) < .001f;
        private static void Step(CombatWorld w, int frames, InputFrame a = default, InputFrame b = default) { for (int i = 0; i < frames; i++) w.Step(a, b); }
        private static CombatWorld Fresh(GameCatalog catalog, int id = 1, int other = 0, int side = 0, int face = 1, float distance = 100)
        {
            var w = new CombatWorld(catalog, side == 0 ? id : other, side == 1 ? id : other);
            var a = w.Fighters[side]; var d = w.Fighters[1 - side];
            a.X = face == 1 ? 90 : 1190; a.Face = face; d.X = a.X + face * distance; d.Face = -face; return w;
        }
        private static void Activate(CombatWorld w, int player = 0)
        {
            w.Fighters[player].Meter = 100; var input = new InputFrame { Power = true };
            w.Step(player == 0 ? input : default, player == 1 ? input : default);
        }
        private static int Finish(CombatWorld w, int limit = 440)
        {
            int hits = 0; bool started = w.Cinematic.Active;
            for (int frame = 0; frame < limit; frame++)
            {
                w.Step(default, default);
                foreach (var e in w.Events) if (e.Type == "super_hit") hits++;
                if (w.Cinematic.Active) started = true;
                else if (started) break;
            }
            return hits;
        }

        public static List<string> Run(GameCatalog catalog)
        {
            Checks = 0; Failures.Clear();
            CatalogContracts(catalog); InputAndDefense(catalog); PursuitMatrix(catalog); RobotsAndBuffs(catalog);
            SpecialsAndResources(catalog); ResourceFollowups(catalog); CompleteUltimateRoster(catalog); TeamsAndSwaps(catalog); CombosAndTransitions(catalog); ReplayAndSnapshots(catalog); AtomicCheckpointsAndDifficulty(catalog); AiAndSoak(catalog);
            return new List<string>(Failures);
        }

        private static void CatalogContracts(GameCatalog catalog)
        {
            Check(catalog.Characters.Length == 16 && catalog.Normals.Length == 9 && catalog.Chains.Length == 2 && Array.Exists(catalog.Chains, m => m.ChainStage == 1) && Array.Exists(catalog.Chains, m => m.ChainStage == 2), "Complete original roster/normal/chain catalog");
            foreach (var c in catalog.Characters)
            {
                Check(c.Specials.Length == 3 && c.Ultimate != null && c.Defense != null && c.Offensive != null, "Complete character move set " + c.Id);
                Check(catalog.Character(c.Id) == c, "Stable character identity " + c.Id);
            }
            for (ushort i = 0; i < 2048; i++) Check(InputFrame.FromBits(i).Bits == i, "Lossless input serialization " + i);
        }

        private static void InputAndDefense(GameCatalog catalog)
        {
            var w = Fresh(catalog, 0); float hp = w.Fighters[1].Hp;
            Step(w, 45, new InputFrame { Attack = true });
            Check(w.Fighters[1].Hp < hp && w.Fighters[0].Meter > 0, "A real normal hit damages and earns meter");
            Check(w.Fighters[0].DamageDealt == hp - w.Fighters[1].Hp, "A held attack produces only one move, no auto-repeat");
            w = Fresh(catalog, 0); Step(w, 12, default, new InputFrame { Block = true });
            Step(w, 45, new InputFrame { Attack = true }, new InputFrame { Block = true });
            Check(w.Fighters[1].Hp > 990 && w.Fighters[1].Guard < 100, "Held guard mitigates normal and spends guard");
            w = Fresh(catalog, 0); w.Step(new InputFrame { Attack = true }, default); Step(w, catalog.Normals[0].Startup - 1);
            w.Step(default, new InputFrame { Block = true });
            Check(Near(w.Fighters[1].Hp, 1000) && w.Events.Exists(e => e.Type == "perfect_block"), "Late guard creates perfect block");
            w = Fresh(catalog, 0); w.Step(new InputFrame { Attack = true }, default); Step(w, catalog.Normals[0].Startup - 1);
            w.Step(default, new InputFrame { Block = true, Left = true });
            Check(w.Events.Exists(e => e.Type == "parry") && w.Fighters[0].State == "hitstun", "Forward timed guard parries and recovers meter");
            w = Fresh(catalog, 0); w.Step(new InputFrame { Attack = true }, default); Step(w, 3);
            w.Step(default, new InputFrame { Dodge = true }); Step(w, 45);
            Check(Near(w.Fighters[1].Hp, 1000) && w.Fighters[1].DodgeCooldown > 0, "Dodge avoids contact and carries cooldown");
            w = Fresh(catalog, 0, 3, 0, 1, 75); w.Step(new InputFrame { Block = true, Attack = true }, default); Step(w, 30);
            Check(Near(w.Fighters[1].Hp, 895), "Throw applies its exact damage");
            w = Fresh(catalog, 0, 3, 0, 1, 75); w.Step(new InputFrame { Block = true, Attack = true }, new InputFrame { Block = true, Attack = true }); Step(w, 30);
            Check(Near(w.Fighters[0].Hp, 1000) && Near(w.Fighters[1].Hp, 1000), "Simultaneous throws tech without player priority");
            w = Fresh(catalog, 0); w.Step(new InputFrame { Attack = true }, new InputFrame { Attack = true }); Step(w, 45);
            Check(Near(w.Fighters[0].Hp, w.Fighters[1].Hp) && w.Fighters[0].Hp < 1000, "Simultaneous strikes trade symmetrically");
            w = Fresh(catalog, 0); w.Step(new InputFrame { Attack = true }, default);
            for (int i = 0; i < 25 && w.Hitstop == 0; i++) w.Step(default, default);
            int clock = w.TimerFrames; float x = w.Fighters[0].X; w.Step(new InputFrame { Right = true }, default);
            Check(w.TimerFrames == clock && Near(x, w.Fighters[0].X), "Hitstop freezes movement and match clock");
            w = Fresh(catalog, 0); w.Fighters[0].Meter = 0; w.Step(new InputFrame { Power = true }, default);
            Check(w.Fighters[0].Move == null && w.Events.Exists(e => e.Type == "no_meter"), "Unaffordable ultimate rejected without state mutation");
            w = Fresh(catalog, 0); w.TimerFrames = 1; w.Step(default, default);
            Check(w.Winner == 2 && w.Phase == "result", "Exact tied timeout is a draw");
        }

        private static void PursuitMatrix(GameCatalog catalog)
        {
            foreach (var target in catalog.Characters)
            for (int side = 0; side < 2; side++)
            foreach (int face in new[] { -1, 1 })
            foreach (float distance in new[] { 95f, 540f, 1100f })
            {
                var w = Fresh(catalog, 1, target.Id, side, face, distance); var a = w.Fighters[side]; var d = w.Fighters[1 - side]; Activate(w, side);
                Check(a.UltimateTint && !d.UltimateTint && a.Move.Kind == "rush_red" && a.Meter == 0, "Vitor proactively transforms himself " + target.Id + "/" + side + "/" + face + "/" + distance);
                int hits = Finish(w);
                Check(hits == 15 && Near(d.Hp, d.MaxHp - 280), "Fifteen contacts and280 budget at every distance/side");
                Check(!w.Cinematic.Active && !a.UltimateTint && !d.UltimateTint && a.State == "idle", "Cinematic releases and clears tint");
                Check(a.X >= 90 && d.X >= 90 && a.X <= 1190 && d.X <= 1190 && Math.Abs(a.X - d.X) >= 66, "Corner-safe world positions");
                Step(w, 80); Check(CombatWorld.IsFree(a) && CombatWorld.IsFree(d), "Both actors recover after ultimate");
            }
            for (int side = 0; side < 2; side++)
            {
                var w = Fresh(catalog, 1, 0, side, 1, 600); Activate(w, side); Step(w, 44);
                var a = w.Fighters[side]; var d = w.Fighters[1 - side]; d.X = a.X - 90;
                Check(Finish(w) == 15 && a.Face == -1, "Pursuit reacquires crossing opponent");
                w = Fresh(catalog, 1, 0, side); Activate(w, side); w.Fighters[1 - side].Invuln = 40;
                Check(Finish(w) == 15, "Temporary invulnerability does not consume pending hit");
                w = Fresh(catalog, 1, 0, side); Activate(w, side); w.Fighters[1 - side].Invuln = 220; Step(w, 260);
                Check(Near(w.Fighters[1 - side].Hp, 1000) && !w.Fighters[side].UltimateTint, "Long invulnerability whiffs cleanly");
                w = Fresh(catalog, 1, 0, side); Activate(w, side);
                for (int i = 0; i < 100 && !w.Cinematic.Active; i++) w.Step(default, default);
                Step(w, 40); float hp = w.Fighters[1 - side].Hp; w.CancelCinematic(); Step(w, 180);
                Check(Near(w.Fighters[1 - side].Hp, hp) && CombatWorld.IsFree(w.Fighters[side]), "Cancellation prevents orphan cinematic damage");
            }
        }

        private static void RobotsAndBuffs(GameCatalog catalog)
        {
            for (int side = 0; side < 2; side++) foreach (int face in new[] { -1, 1 })
            {
                var w = Fresh(catalog, 4, 0, side, face, 300); var a = w.Fighters[side]; var d = w.Fighters[1 - side]; Activate(w, side);
                bool robots = false, earlyArmor = false; int hits = 0;
                for (int i = 0; i < 300; i++)
                {
                    w.Step(default, default); robots |= w.Projectiles.Exists(p => p.Kind == "robot_strike");
                    if (w.Cinematic.Active) earlyArmor |= a.TransformFrames > 0;
                    foreach (var e in w.Events) if (e.Type == "super_hit") hits++;
                    if (a.TransformFrames > 0) break;
                }
                Check(robots && hits == 6 && Near(d.Hp, 800), "Real robot objects confirm six hits");
                Check(!earlyArmor && a.TransformFrames == 480 && w.Projectiles.Count == 0, "Armor begins only after robots/assembly");
                Step(w, 479); Check(a.TransformFrames == 1, "Armor persists exactly479 following steps");
                Step(w, 1); Check(a.TransformFrames == 0 && !a.ArmorActive, "Armor ends at480, identity unchanged");
            }
            var blocked = Fresh(catalog, 4); Activate(blocked); Step(blocked, 180, default, new InputFrame { Block = true });
            Check(Near(blocked.Fighters[1].Hp, 992) && blocked.Fighters[0].TransformFrames > 0, "Blocked robot pair deduplicates chip; uninterrupted armor still granted");
            var whiff = Fresh(catalog, 4, 0, 0, 1, 600); Activate(whiff); Step(whiff, 180);
            Check(Near(whiff.Fighters[1].Hp, 1000) && whiff.Fighters[0].TransformFrames > 0, "Robot range finite, no invisible distant hit");
            var buff = Fresh(catalog, 4); buff.Fighters[0].TransformFrames = 100; float baseline = catalog.Normals[0].Damage;
            buff.Step(new InputFrame { Attack = true }, default);
            Check(Near(buff.Fighters[0].Move.Damage, baseline * 1.2f) && Near(catalog.Normals[0].Damage, baseline), "Armor buff copies move and preserves catalog");
            var perk = Fresh(catalog, 1); perk.Fighters[0].DamageMultiplier = 1.2f; Activate(perk); Finish(perk);
            Check(Near(perk.Fighters[1].Hp, 664), "Campaign damage multiplier affects exact ultimate budget");
        }

        private static void SpecialsAndResources(GameCatalog catalog)
        {
            foreach (var character in catalog.Characters) for (int slot = 0; slot < 4; slot++)
            {
                var w = Fresh(catalog, character.Id, 0, 0, 1, 220); w.Fighters[0].Meter = 100;
                InputFrame command = slot == 0 ? new InputFrame { Left = true, Attack = true } : slot == 1 ? new InputFrame { Left = true, Down = true, Attack = true } : slot == 2 ? new InputFrame { Right = true, Down = true, Kick = true } : new InputFrame { Right = true, Down = true, Heavy = true };
                w.Step(command, default);
                var expected = slot == 3 ? character.Offensive : character.Specials[slot];
                Check(w.Fighters[0].Move != null && w.Fighters[0].Move.Name == expected.Name, "All roster special commands routed " + character.Id + "/" + slot);
                Step(w, 220);
                Check(w.Fighters[0].State != "attack" || w.Winner >= 0, "Special recovers without permanent lock " + character.Id + "/" + slot);
                Check(w.Projectiles.Count <= 8, "Devices remain bounded " + character.Id + "/" + slot);
            }
            var ammo = Fresh(catalog, 2, 0, 0, 1, 600); ammo.Fighters[0].Ammo = 0;
            ammo.Step(new InputFrame { Left = true, Attack = true }, default);
            Check(ammo.Fighters[0].Move == null && ammo.Events.Exists(e => e.Type == "no_meter"), "Maria cannot shoot with empty ammunition");
            var training = Fresh(catalog); training.Training.Enabled = true; Activate(training); Finish(training); Step(training, 2);
            Check(training.Winner == -1 && training.Fighters[1].Hp == training.Fighters[1].MaxHp && training.Fighters[0].Meter == 100, "Training restores enabled health and meter");
        }

        private static void ReplayAndSnapshots(GameCatalog catalog)
        {
            var a = Fresh(catalog, 1, 4, 0, 1, 600); a.Training.Enabled = true; a.Training.InfiniteHealth = false; a.Training.InfiniteMeter = false;
            var b = Fresh(catalog, 1, 4, 0, 1, 600); b.Training.Enabled = true; b.Training.InfiniteHealth = false; b.Training.InfiniteMeter = false;
            a.Fighters[0].Meter = b.Fighters[0].Meter = 100;
            var tape = new ReplayTape(); var hashes = new List<ulong>();
            var ai = new CombatAI(783, 4);
            for (int i = 0; i < 1200; i++)
            {
                var p1 = i == 0 ? new InputFrame { Power = true } : ai.Decide(a, 0);
                var p2 = i % 40 == 0 ? new InputFrame { Attack = true } : new InputFrame { Left = i % 80 < 30 };
                tape.Record(p1, p2); a.Step(p1, p2); hashes.Add(a.StateHash());
            }
            var playback = tape.Play(b); bool equal = playback.Length == hashes.Count;
            for (int i = 0; i < playback.Length; i++) equal &= playback[i] == hashes[i];
            Check(equal, "Recorded held inputs reproduce all1200 exact state hashes");
            var w = Fresh(catalog, 4, 1, 0, 1, 300); Activate(w); Step(w, 44);
            Check(w.Projectiles.Count == 2, "Snapshot scenario contains both active robots");
            var snapshot = w.CaptureSnapshot(); ulong checkpoint = w.StateHash(); Step(w, 160); ulong result = w.StateHash();
            w.RestoreSnapshot(snapshot); Check(w.StateHash() == checkpoint, "Snapshot restores projectile/input/clock state exactly");
            Step(w, 160); Check(w.StateHash() == result, "Snapshot continuation matches original cinematic progression");
            w.RestoreSnapshot(snapshot); w.Fighters[0].X += 20;
            Check(!Near(snapshot.Fighters[0].X, w.Fighters[0].X), "Snapshots own deep copies");
            w.RestoreSnapshot(snapshot); w.Fighters[0].DamageMultiplier += .1f;
            Check(w.StateHash() != checkpoint, "Campaign multiplier participates in hash");
        }

        private static void ResourceFollowups(GameCatalog catalog)
        {
            var maria = Fresh(catalog, 2, 0, 0, 1, 700); maria.Fighters[0].Ammo = 1;
            maria.Step(new InputFrame { Left = true, Attack = true }, default); Step(maria, 120);
            Check(maria.Fighters[0].Ammo == 0, "Maria spends exactly one round per accepted shot");
            maria.Step(new InputFrame { Right = true, Down = true, Kick = true }, default);
            Check(maria.Fighters[0].Ammo == 0, "Reload has a real completion window instead of instant ammunition");
            Step(maria, 120); Check(maria.Fighters[0].Ammo == 6, "Completed reload replenishes all six rounds");

            var leo = Fresh(catalog, 11, 0, 0, 1, 700);
            leo.Step(new InputFrame { Right = true, Down = true, Kick = true }, default); Step(leo, 120);
            Check(leo.Fighters[0].Embalo == 1, "Leonardo earns Embalo after completing the drink");
            leo.Step(new InputFrame { Attack = true }, default);
            Check(leo.Fighters[0].Embalo == 0 && Near(leo.Fighters[0].Move.Damage, catalog.Normals[0].Damage * 1.12f), "Embalo empowers and is spent by one attack only");

            var felipe = Fresh(catalog, 5, 0, 0, 1, 700); felipe.Fighters[0].Meter = 100;
            felipe.Step(new InputFrame { Right = true, Down = true, Kick = true }, default); Step(felipe, 80);
            Check(felipe.Fighters[0].PreparedFrames > 0, "Felipe setup remains available after recovery");
            felipe.Step(new InputFrame { Kick = true }, default);
            Check(felipe.Fighters[0].PreparedFrames == 0 && felipe.Fighters[0].Move.Name == "VOLEIO PREPARADO" && felipe.Fighters[0].Move.Damage == 110, "Prepared volley consumes setup and keeps its authored move");

            var murilo = Fresh(catalog, 6, 0, 0, 1, 700); murilo.Fighters[0].Meter = 100;
            murilo.Step(new InputFrame { Right = true, Down = true, Kick = true }, default); Step(murilo, 70);
            Check(murilo.Fighters[0].FeintFrames > 0, "Murilo feint opens a follow-up window");
            murilo.Step(new InputFrame { Attack = true }, default);
            Check(murilo.Fighters[0].FeintFrames == 0 && murilo.Fighters[0].Move.Name == "FINALIZAÇÃO DE QUADRA", "Handball follow-up consumes feint instead of becoming a normal jab");
        }

        private static void AtomicCheckpointsAndDifficulty(GameCatalog catalog)
        {
            var world = Fresh(catalog, 3, 4); world.SetupTeams(0, 1); world.Step(new InputFrame { Tag = true }, default);
            ulong before = world.StateHash();
            foreach (Action<CombatSnapshot> corrupt in new Action<CombatSnapshot>[] {
                s => s.Teams = null, s => s.TeamActive = new[] { 0 }, s => s.TeamActive[0] = 4,
                s => s.Teams[1][0] = null, s => s.Teams[0][0].Hp = float.NaN,
                s => s.Teams[0][0].Id = 99, s => s.Projectiles = new ProjectileState[] { null },
                s => s.Cinematic.Active = true, s => s.TimerFrames = -1 })
            {
                var broken = world.CaptureSnapshot(); corrupt(broken); bool rejected = false;
                try { world.RestoreSnapshot(broken); } catch (ArgumentException) { rejected = true; }
                Check(rejected && world.StateHash() == before, "Malformed checkpoint is rejected without mutating a live team match");
            }
            bool failed = false;
            try { world.StartRound(99, 0); } catch (ArgumentException) { failed = true; }
            Check(failed && world.StateHash() == before, "Invalid singles selection leaves active team round unchanged");
            failed = false;
            try { world.ConfigureTeams(4, 99, 5, 6); } catch (ArgumentException) { failed = true; }
            Check(failed && world.StateHash() == before, "Invalid reserve selection cannot partially replace leaders");
            float[] andre = {1100, 1150, 1250, 1300}, kelvin = {1150, 1250, 1300, 1400};
            for (int level = 0; level < 4; level++)
            {
                var bosses = new CombatWorld(catalog, 14, 15, 19, level);
                Check(bosses.Fighters[0].MaxHp == andre[level] && bosses.Fighters[1].MaxHp == kelvin[level], "Original boss health follows selected difficulty " + level);
                var snapshot = bosses.CaptureSnapshot(); bosses.Difficulty = 0; bosses.ResetRound(); bosses.RestoreSnapshot(snapshot); bosses.ResetRound();
                Check(bosses.Difficulty == level && bosses.Fighters[0].Hp == andre[level] && bosses.Fighters[1].Hp == kelvin[level], "Checkpoint restores difficulty for the next round " + level);
            }
        }

        private static void CompleteUltimateRoster(GameCatalog catalog)
        {
            foreach (var character in catalog.Characters) for (int side = 0; side < 2; side++)
            {
                float distance = character.Id == 0 ? 85 : character.Id == 3 || character.Id == 8 || character.Id == 12 ? 265 : 140;
                var w = Fresh(catalog, character.Id, character.Id == 0 ? 3 : 0, side, side == 0 ? 1 : -1, distance);
                if (character.Id == 14) { w.Fighters[side].X = side == 0 ? 90 : 1190; w.Fighters[1 - side].X = 340; }
                Activate(w, side); bool captured = false; int hits = 0, finalHits = 0; int movieSteps = 0; var impactFrames = new List<int>();
                for (int frame = 0; frame < 450; frame++)
                {
                    bool wasActive = w.Cinematic.Active; w.Step(default, default);
                    if (wasActive) movieSteps++;
                    captured |= w.Cinematic.Active;
                    foreach (var e in w.Events) if (e.Type == "super_hit") { hits++; finalHits += e.Final ? 1 : 0; impactFrames.Add(w.Cinematic.Frame); }
                }
                var attacker = w.Fighters[side]; var defender = w.Fighters[1 - side];
                if (character.Id == 14)
                {
                    Check(!captured && defender.Hp < 1000 && defender.Hp > 700 && w.Projectiles.Count == 0, "André preserves three finite gas waves rather than generic capture");
                }
                else
                {
                    int count = character.Id == 1 || character.Id == 9 ? 15 : 6;
                    Check(captured && hits == count && finalHits == 1, "Authored ultimate confirms expected impacts " + character.Id + "/" + side);
                    Check(Near(defender.Hp, character.Id == 4 ? 800 : 720), "Per-character exact ultimate damage " + character.Id + "/" + side);
                    if (character.Id != 1 && character.Id != 4 && character.Id != 9) Check(movieSteps == 120, "Original120-frame authored ultimate timing " + character.Id);
                }
                Check(w.Winner == -1 && CombatWorld.IsFree(attacker) && CombatWorld.IsFree(defender), "Ultimate restores both actor controls " + character.Id + "/" + side);
            }
        }

        private static void TeamsAndSwaps(GameCatalog catalog)
        {
            var w = new CombatWorld(catalog, 1, 4); w.SetupTeams(3, 9);
            Check(w.TeamMode && w.TimerFrames == 150 * 60 && w.Teams[0].Length == 2 && w.Teams[1].Length == 2, "Two-by-two team round has original150-second clock");
            var leader = w.Fighters[0]; var reserve = w.Reserve(0); leader.Hp = 611; leader.Meter = 71; reserve.Hp = 777; reserve.Meter = 29; reserve.Skin = 1;
            float x = leader.X;
            w.Step(new InputFrame { Block = true, Kick = true }, default);
            Check(w.Fighters[0] == reserve && w.TeamActive[0] == 1 && w.TagCooldowns[0] == 300 && Near(reserve.X, x), "Defense+kick swaps active member and starts300-tick cooldown");
            Check(reserve.Hp == 777 && reserve.Meter == 29 && reserve.Skin == 1 && w.Reserve(0) == leader && leader.Hp == 611 && leader.Meter == 71, "Team members preserve individual health/meter/skin");
            Check(!w.TryTag(0), "Manual tag cannot bypass cooldown");
            Step(w, 299); Check(!w.TryTag(0), "Cooldown remains active through299 ticks");
            Step(w, 1); Check(w.TryTag(0) && w.Fighters[0] == leader, "Tag becomes available on exactly300th tick");
            leader.Hp = 0; w.Step(default, default);
            Check(w.Fighters[0] == reserve && w.Winner == -1 && reserve.Invuln == 40, "KO automatically tags healthy reserve despite cooldown");
            reserve.Hp = 0; w.Step(default, default); Check(w.Winner == 1, "Team loses only after both members are defeated");
            w.ResetRound(); Check(w.TeamMode && w.TeamActive[0] == 0 && w.TagCooldowns[0] == 0 && w.TeamHealth(0) == 2000, "Round reset recreates both healthy teams");
            w.Teams[0][0].Hp = 200; w.Teams[0][1].Hp = 1000; w.Teams[1][0].Hp = 800; w.Teams[1][1].Hp = 200; w.TimerFrames = 1; w.Step(default, default);
            Check(w.Winner == 0, "Team timeout uses total health fractions including reserve");
            w.ConfigureTeams(4, 3, 0, 9); w.Fighters[0].X = 90; w.Fighters[1].X = 690; w.Fighters[0].Meter = 100;
            w.Step(new InputFrame { Left = true, Attack = true }, default); Step(w, 60);
            Check(w.Projectiles.Count > 0 && w.TryTag(0) && w.Projectiles.Count == 0, "Outgoing fighter devices retire on tag");
            var state = w.CaptureSnapshot(); ulong hash = w.StateHash(); Step(w, 330); w.TryTag(0); Step(w, 12); ulong future = w.StateHash();
            w.RestoreSnapshot(state); Check(w.StateHash() == hash && w.Fighters[0] == w.Teams[0][w.TeamActive[0]], "Team snapshot preserves shared active-member identity");
            Step(w, 330); w.TryTag(0); Step(w, 12); Check(w.StateHash() == future, "Team snapshot deterministic continuation across tag");
            w.StartRound(0, 3); Check(!w.TeamMode && w.TimerFrames == 99 * 60, "Starting a singles round disables prior team rules");
        }

        private static void CombosAndTransitions(GameCatalog catalog)
        {
            var w = Fresh(catalog, 0); w.Step(new InputFrame { Attack = true }, default);
            for (int i = 0; i < 30 && w.Fighters[0].Combo == 0; i++) w.Step(default, default);
            w.Step(new InputFrame { Attack = true }, default);
            for (int i = 0; i < 40 && w.Fighters[0].Move?.ChainStage != 1; i++) w.Step(default, default);
            Check(w.Fighters[0].Move?.ChainStage == 1 && w.Fighters[0].Move.Damage == 44, "Confirmed jab cancels into authored44-damage first chain link");
            for (int i = 0; i < 40 && w.Fighters[0].Combo < 2; i++) w.Step(default, default);
            w.Step(new InputFrame { Attack = true }, default);
            for (int i = 0; i < 40 && w.Fighters[0].Move?.ChainStage != 2; i++) w.Step(default, default);
            Check(w.Fighters[0].Move?.ChainStage == 2 && w.Fighters[0].Move.Damage == 58, "Second confirmed chain links to authored58-damage finisher");
            Step(w, 80); Check(w.Fighters[0].MaxCombo == 3 && Near(w.Fighters[1].Hp, 875), "Three-hit chain applies42+39+44 combo scaling");
            w = Fresh(catalog, 1); var defender = w.Fighters[1]; defender.Guard = 1; Step(w, 10, default, new InputFrame { Block = true });
            w.Step(new InputFrame { Heavy = true }, new InputFrame { Block = true }); bool broke = false;
            for (int i = 0; i < 70; i++) { w.Step(default, new InputFrame { Block = true }); broke |= w.Events.Exists(e => e.Type == "guard_break"); }
            Check(broke && defender.Hp > 990, "Guard break opens defender without full unblockable damage");
            w = Fresh(catalog, 1, 0, 0, 1, 600); Activate(w); Step(w, 25); w.Fighters[1].X = w.Fighters[0].X + 100;
            w.Step(default, new InputFrame { Attack = true }); Step(w, 380);
            Check(!w.Cinematic.Active && !w.Fighters[0].UltimateTint && w.Fighters[0].State != "super", "Startup/pursuit contention never leaves a locked red actor");
        }

        private static void AiAndSoak(GameCatalog catalog)
        {
            for (int id = 0; id < catalog.Characters.Length; id++)
            {
                var w = new CombatWorld(catalog, id, (id + 3) % catalog.Characters.Length, 17); w.Training.Enabled = true;
                var cpu1 = new CombatAI((uint)(id + 10), 3); var cpu2 = new CombatAI((uint)(id + 30), 4);
                bool finite = true, bounded = true, damaged = false;
                for (int frame = 0; frame < 3600; frame++)
                {
                    w.Step(cpu1.Decide(w, 0), cpu2.Decide(w, 1));
                    foreach (var f in w.Fighters) { finite &= !float.IsNaN(f.X) && !float.IsInfinity(f.Y) && f.Hp >= 0; bounded &= f.X >= 90 && f.X <= 1190 && f.Meter >= 0 && f.Meter <= 100; damaged |= f.DamageReceived > 0; }
                    bounded &= w.Projectiles.Count < 32;
                }
                Check(finite && bounded, "Seeded roster soak has finite bounded state " + id);
                Check(damaged, "CPU produces combat contacts for roster " + id);
                w.ResetRound(); Check(w.Projectiles.Count == 0 && !w.Cinematic.Active && !w.Fighters[0].UltimateTint, "Reset removes all owned actors/state " + id);
            }
            var left = Fresh(catalog, 0, 3, 0, 1, 600); var right = Fresh(catalog, 0, 3, 0, 1, 600);
            var first = new CombatAI(91, 3); var second = new CombatAI(91, 3); bool same = true;
            for (int i = 0; i < 180; i++) { var p = first.Decide(left, 0); var q = second.Decide(right, 0); same &= p.Bits == q.Bits; left.Step(p, default); right.Step(q, default); }
            Check(same, "CPU choices deterministic from current visible state and seed");
        }
    }
}
