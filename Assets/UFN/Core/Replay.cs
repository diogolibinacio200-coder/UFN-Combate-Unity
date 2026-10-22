using System;
using System.Collections.Generic;

namespace UFN.Core
{
    public sealed class CombatSnapshot
    {
        public FighterState[] Fighters;
        public FighterState[][] Teams;
        public int[] TeamActive, TagCooldowns;
        public bool TeamMode;
        public ProjectileState[] Projectiles;
        public CinematicState Cinematic;
        public TrainingSettings Training;
        public int Tick, TimerFrames, Hitstop, Winner, Round, Difficulty;
        internal int Serial;
        internal int[] SelectedIds;
        internal int[] SelectedReserves;
        internal uint RandomState;
    }

    public struct ReplayInput
    {
        public ushort Player1, Player2;
        public ReplayInput(InputFrame p1, InputFrame p2) { Player1 = p1.Bits; Player2 = p2.Bits; }
    }

    public sealed class ReplayTape
    {
        public readonly List<ReplayInput> Frames = new List<ReplayInput>();
        public void Record(InputFrame p1, InputFrame p2) => Frames.Add(new ReplayInput(p1, p2));
        public ulong[] Play(CombatWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var hashes = new ulong[Frames.Count];
            for (int i = 0; i < Frames.Count; i++) { world.Step(InputFrame.FromBits(Frames[i].Player1), InputFrame.FromBits(Frames[i].Player2)); hashes[i] = world.StateHash(); }
            return hashes;
        }
    }

    internal struct StableHash
    {
        private ulong value;
        internal static StableHash Create() => new StableHash { value = 14695981039346656037UL };
        internal ulong Value => value;
        internal void Add(int x) { unchecked { for (int i = 0; i < 4; i++) { value ^= (byte)((uint)x >> (i * 8)); value *= 1099511628211UL; } } }
        internal void Add(float x) => Add(BitConverter.SingleToInt32Bits(x));
        internal void Add(bool x) => Add(x ? 1 : 0);
        internal void Add(string x)
        {
            if (x == null) { Add(-1); return; }
            Add(x.Length); foreach (char c in x) Add((int)c);
        }
        internal void Add(MoveDefinition m)
        {
            Add(m != null); if (m == null) return;
            Add(m.Name); Add(m.Kind); Add(m.Level); Add(m.Family); Add(m.Visual); Add(m.BodyPose);
            Add(m.Damage); Add(m.Range); Add(m.Height); Add(m.OffsetY); Add(m.GuardDamage); Add(m.Cost); Add(m.Push); Add(m.Launch); Add(m.Speed); Add(m.Chip); Add(m.Pull); Add(m.Gravity); Add(m.Vy);
            Add(m.Startup); Add(m.Active); Add(m.Recovery); Add(m.Hitstun); Add(m.Blockstun); Add(m.Duration); Add(m.Invuln); Add(m.Freeze); Add(m.Bounces); Add(m.PulseCount); Add(m.PulseInterval); Add(m.Root); Add(m.SetupTime); Add(m.Slow); Add(m.ChainStage);
            Add(m.Cancel); Add(m.Chargeable); Add(m.Reflect);
        }
    }

    public sealed partial class CombatWorld
    {
        public CombatSnapshot CaptureSnapshot()
        {
            var projectiles = new ProjectileState[Projectiles.Count]; for (int i = 0; i < projectiles.Length; i++) projectiles[i] = Projectiles[i].Clone();
            var teams = new[] { new[] { Teams[0][0].Clone(), Teams[0][1].Clone() }, new[] { Teams[1][0].Clone(), Teams[1][1].Clone() } };
            return new CombatSnapshot { Fighters = new[] { teams[0][TeamActive[0]], teams[1][TeamActive[1]] }, Teams = teams, TeamMode = TeamMode, TeamActive = (int[])TeamActive.Clone(), TagCooldowns = (int[])TagCooldowns.Clone(), SelectedReserves = (int[])selectedReserves.Clone(), Projectiles = projectiles, Cinematic = Cinematic.Clone(), Training = Training.Clone(), Tick = Tick, TimerFrames = TimerFrames, Hitstop = Hitstop, Winner = Winner, Round = Round, Difficulty = Difficulty, Serial = serial, SelectedIds = (int[])selectedIds.Clone(), RandomState = randomState };
        }

        public void RestoreSnapshot(CombatSnapshot snapshot)
        {
            // Validate the whole checkpoint before touching the live match. A malformed
            // save/checkpoint must never leave half of an old team mixed with new state.
            ValidateSnapshot(snapshot);
            TeamMode = snapshot.TeamMode; TeamActive = (int[])snapshot.TeamActive.Clone(); TagCooldowns = (int[])snapshot.TagCooldowns.Clone();
            Teams = new[] { new[] { snapshot.Teams[0][0].Clone(), snapshot.Teams[0][1].Clone() }, new[] { snapshot.Teams[1][0].Clone(), snapshot.Teams[1][1].Clone() } };
            Fighters = new[] { Teams[0][TeamActive[0]], Teams[1][TeamActive[1]] };
            Projectiles.Clear(); foreach (var p in snapshot.Projectiles) Projectiles.Add(p.Clone());
            Cinematic = snapshot.Cinematic.Clone(); Training = snapshot.Training.Clone(); Tick = snapshot.Tick; TimerFrames = snapshot.TimerFrames; Hitstop = snapshot.Hitstop;
            Winner = snapshot.Winner; Round = snapshot.Round; Difficulty = snapshot.Difficulty; serial = snapshot.Serial; randomState = snapshot.RandomState;
            selectedIds[0] = snapshot.SelectedIds[0]; selectedIds[1] = snapshot.SelectedIds[1]; Events.Clear();
            selectedReserves[0] = snapshot.SelectedReserves[0]; selectedReserves[1] = snapshot.SelectedReserves[1];
        }

        private void ValidateSnapshot(CombatSnapshot s)
        {
            if (s == null || s.Fighters == null || s.Fighters.Length != 2 || s.Cinematic == null || s.Training == null ||
                s.Teams == null || s.Teams.Length != 2 || s.TeamActive == null || s.TeamActive.Length != 2 ||
                s.TagCooldowns == null || s.TagCooldowns.Length != 2 || s.SelectedIds == null || s.SelectedIds.Length != 2 ||
                s.SelectedReserves == null || s.SelectedReserves.Length != 2 || s.Projectiles == null ||
                s.Tick < 0 || s.TimerFrames < 0 || s.Hitstop < 0 || s.Winner < -1 || s.Winner > 2 || s.Round < 1 ||
                s.Cinematic.Active && (s.Cinematic.Attacker < 0 || s.Cinematic.Attacker > 1 || s.Cinematic.Defender != 1 - s.Cinematic.Attacker))
                throw new ArgumentException("Invalid combat snapshot", nameof(s));
            for (int player = 0; player < 2; player++)
            {
                if (s.Teams[player] == null || s.Teams[player].Length != 2 || s.TeamActive[player] < 0 || s.TeamActive[player] > 1 || s.TagCooldowns[player] < 0)
                    throw new ArgumentException("Invalid team snapshot", nameof(s));
                Catalog.Character(s.SelectedIds[player]);
                if (s.TeamMode) Catalog.Character(s.SelectedReserves[player]);
                foreach (var fighter in s.Teams[player])
                {
                    if (fighter == null || fighter.Player != player || fighter.Motions == null || !Finite(fighter.X) || !Finite(fighter.Y) ||
                        !Finite(fighter.Hp) || !Finite(fighter.MaxHp) || fighter.MaxHp <= 0 || fighter.Hp < 0 || fighter.Hp > fighter.MaxHp ||
                        !Finite(fighter.Meter) || !Finite(fighter.Guard) || !Finite(fighter.DamageMultiplier) || fighter.DamageMultiplier < 0)
                        throw new ArgumentException("Invalid fighter checkpoint", nameof(s));
                    Catalog.Character(fighter.Id);
                }
            }
            foreach (var projectile in s.Projectiles)
                if (projectile == null || projectile.Move == null || projectile.Owner < 0 || projectile.Owner > 1 || !Finite(projectile.X) || !Finite(projectile.Y))
                    throw new ArgumentException("Invalid projectile checkpoint", nameof(s));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public ulong StateHash()
        {
            var h = StableHash.Create();
            h.Add(3); // Hash schema version; replay consumers must not mix incompatible versions.
            h.Add(Tick); h.Add(TimerFrames); h.Add(Hitstop); h.Add(Winner); h.Add(Round); h.Add(serial); h.Add(unchecked((int)randomState)); h.Add(selectedIds[0]); h.Add(selectedIds[1]);
            h.Add(Difficulty);
            h.Add(Training.Enabled); h.Add(Training.InfiniteHealth); h.Add(Training.InfiniteMeter);
            h.Add(TeamMode); for (int i = 0; i < 2; i++) { h.Add(TeamActive[i]); h.Add(TagCooldowns[i]); h.Add(selectedReserves[i]); }
            foreach (var team in Teams) foreach (var f in team)
            {
                h.Add(f.Id); h.Add(f.Player); h.Add(f.Face); h.Add(f.Frame); h.Add(f.Stun); h.Add(f.Invuln); h.Add(f.HitFlash); h.Add(f.TransformFrames); h.Add(f.Combo); h.Add(f.ComboTimer); h.Add(f.MaxCombo); h.Add(f.Skin);
                h.Add(f.Ammo); h.Add(f.OverclockFrames); h.Add(f.PreparedFrames); h.Add(f.FeintFrames); h.Add(f.StatusFrames); h.Add(f.DodgeFrames); h.Add(f.DodgeCooldown); h.Add(f.BearStun); h.Add(f.Embalo); h.Add(f.EmbaloFrames);
                h.Add(f.X); h.Add(f.Y); h.Add(f.VelocityX); h.Add(f.VelocityY); h.Add(f.Hp); h.Add(f.MaxHp); h.Add(f.Guard); h.Add(f.Meter); h.Add(f.Rage); h.Add(f.ComboDamage); h.Add(f.DamageDealt); h.Add(f.DamageReceived); h.Add(f.DamageMultiplier); h.Add(f.BearX); h.Add(f.BearPosture);
                h.Add(f.State); h.Add(f.Animation); h.Add(f.Status); h.Add(f.UltimateTint); h.Add(f.Move);
                h.Add(f.BufferFrames); h.Add(f.GuardAge); h.Add(f.ParryAge); h.Add(f.GuardDelay); h.Add(f.CancelCount); h.Add(f.Juggle); h.Add(f.Attacks); h.Add(f.ThrowFrames); h.Add(f.ThrowTarget); h.Add(f.DashFrames); h.Add(f.DashDirection); h.Add(f.RollFrames); h.Add(f.RollDirection); h.Add(f.ControlImmunity); h.Add(f.LastBearHit);
                h.Add(f.HitConfirm); h.Add(f.HitTarget); h.Add(f.WakeupUsed); h.Add((int)f.LastInput.Bits);
                h.Add(f.Buffered != null);
                if (f.Buffered != null)
                {
                    var b = f.Buffered; h.Add((int)b.Held.Bits); h.Add(b.Attack); h.Add(b.Heavy); h.Add(b.Kick); h.Add(b.Up); h.Add(b.Block); h.Add(b.Dodge); h.Add(b.Power); h.Add(b.Tag); h.Add(b.Face); h.Add(b.SpecialSlot);
                }
                h.Add(f.Motions.Count); foreach (var m in f.Motions) { h.Add(m.Direction); h.Add(m.Tick); }
            }
            h.Add(Projectiles.Count);
            foreach (var p in Projectiles)
            {
                h.Add(p.Id); h.Add(p.Owner); h.Add(p.Face); h.Add(p.Age); h.Add(p.Life); h.Add(p.Lane); h.Add(p.AttackSerial); h.Add(p.Hits); h.Add(p.NextHit); h.Add(p.Bounces); h.Add(p.Hp); h.Add(p.Kind); h.Add(p.Visual); h.Add(p.X); h.Add(p.Y); h.Add(p.VelocityX); h.Add(p.VelocityY); h.Add(p.Radius); h.Add(p.Dead); h.Add(p.Move);
            }
            var c = Cinematic; h.Add(c.Active); h.Add(c.Captured); h.Add(c.Frame); h.Add(c.Duration); h.Add(c.Attacker); h.Add(c.Defender); h.Add(c.Character); h.Add(c.ImpactPause); h.Add(c.Pulse); h.Add(c.ArmorDuration); h.Add(c.Phase); h.Add(c.Name); h.Add(c.DamageTotal); h.Add(c.DamageApplied); h.Add(c.OriginAttackerX); h.Add(c.OriginDefenderX);
            return h.Value;
        }
    }
}
