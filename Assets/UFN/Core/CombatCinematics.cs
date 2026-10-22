using System;

namespace UFN.Core
{
    public sealed partial class CombatWorld
    {
        private static readonly int[] VitorStrikes = { 1, 25, 31, 37, 43, 49, 55, 61, 67, 73, 79, 85, 91, 97, 111 };
        private static readonly int[] RobotStrikes = { 10, 27, 44, 61, 78, 92 };
        private static readonly int[][] AuthoredTimelines = {
            new[] { 28, 60, 83, 113, 144, 187 }, new[] { 20, 42, 65, 93, 124, 180 },
            new[] { 24, 51, 82, 106, 137, 180 }, new[] { 33, 59, 85, 113, 141, 184 },
            new[] { 25, 52, 80, 110, 140, 185 }, new[] { 26, 54, 79, 105, 137, 180 },
            new[] { 28, 54, 78, 102, 135, 180 }, new[] { 22, 49, 80, 115, 143, 180 },
            new[] { 29, 57, 85, 113, 141, 180 }, new[] { 26, 53, 78, 112, 139, 180 }
        };

        private void StartCinematic(FighterState a, FighterState d, MoveDefinition m)
        {
            if (a.ComboTimer <= 0 || !Is(d.State, "hitstun", "guardbreak")) { a.Combo = 0; a.ComboDamage = 0; }
            bool vitor = a.Id == 1 || a.Id == 9, robot = a.Id == 4;
            Cinematic = new CinematicState { Active = true, Captured = true, Attacker = a.Player, Defender = d.Player, Character = a.Id, Duration = robot ? 144 : vitor ? 126 : 120, Phase = robot ? "robots" : vitor ? "capture" : "sequence", Name = m.Name, OriginAttackerX = a.X, OriginDefenderX = d.X, DamageTotal = (float)Math.Round(m.Damage * Math.Max(.32f, 1 - a.Combo * .12f), MidpointRounding.AwayFromZero), ArmorDuration = m.Duration };
            a.UltimateTint = a.Id == 1; d.UltimateTint = false;
            if (a.Id == 1) a.X = Clamp(d.X - a.Face * 100, 90, 1190);
            a.State = "super"; d.State = "super_victim"; a.Animation = "ultimate"; d.Animation = "hurt";
            foreach (var f in Fighters)
            {
                f.VelocityX = f.VelocityY = 0; f.Y = Ground; f.Stun = 0; f.Frame = 0; f.BufferFrames = 0; f.Buffered = null;
                f.Motions.Clear(); f.DashFrames = 0; f.RollFrames = 0;
            }
            a.HitConfirm = true; d.Move = null; Hitstop = 0; Projectiles.Clear(); AddMeter(a, 6); AddMeter(d, 4);
            var e = Emit("cinematic_start", a, m.Name, d.Player); e.Phase = Cinematic.Phase;
        }

        private void SetCinematicPhase(string phase)
        {
            if (Cinematic.Phase == phase) return;
            Cinematic.Phase = phase; Emit("ultimate_phase", Fighters[Cinematic.Attacker], Cinematic.Name, Cinematic.Defender).Phase = phase;
        }

        private void TickCinematic()
        {
            var c = Cinematic;
            if (c.ImpactPause > 0) { c.ImpactPause--; return; }
            c.Frame++; var a = Fighters[c.Attacker]; var d = Fighters[c.Defender]; a.Frame = d.Frame = c.Frame; d.HitFlash = Dec(d.HitFlash);
            bool vitor = c.Character == 1 || c.Character == 9, robot = c.Character == 4;
            var timeline = vitor ? VitorStrikes : robot ? RobotStrikes : AuthoredTimelines[c.Character % AuthoredTimelines.Length];
            if (vitor)
            {
                if (c.Frame == 25) SetCinematicPhase("flurry"); if (c.Frame == 111) SetCinematicPhase("finisher");
                a.Animation = c.Frame < 7 || c.Frame >= 110 ? "heavy" : c.Frame >= 25 && c.Frame < 103 ? "light" : "guard";
            }
            if (robot) { if (c.Frame == 103) SetCinematicPhase("recall"); if (c.Frame == 117) SetCinematicPhase("assembly"); a.Animation = c.Frame < 117 ? "intro" : "guard"; }
            int index = -1;
            for (int i = 0; i < timeline.Length; i++)
            {
                int strikeFrame = vitor || robot ? timeline[i] : (int)Math.Round(timeline[i] * c.Duration / 210.0, MidpointRounding.AwayFromZero);
                if (strikeFrame == c.Frame) { index = i; break; }
            }
            if (index >= 0)
            {
                bool final = index == timeline.Length - 1;
                float share = vitor ? (index == 0 ? .14f : .04f) : .12f;
                float allocated = vitor ? (float)Math.Floor(c.DamageTotal * .14f) + 13 * (float)Math.Floor(c.DamageTotal * .04f) : 5 * (float)Math.Floor(c.DamageTotal * .12f);
                float damage = final ? c.DamageTotal - allocated : (float)Math.Floor(c.DamageTotal * share);
                HitRect box = CinematicHitbox(a, d, index);
                if (box.Intersects(d.Hurtbox))
                {
                    DealDamage(a, d, damage); c.DamageApplied += damage; c.Pulse = index + 1;
                    c.ImpactPause = !vitor && !robot ? 0 : final ? (vitor ? 7 : 6) : vitor ? (index == 0 ? 4 : 1) : 2; d.HitFlash = final ? 5 : 3;
                    if (final) c.Captured = false;
                    var hit = Emit("super_hit", a, c.Name, d.Player, damage); hit.X = d.X; hit.Y = d.Y - 115; hit.Pulse = index + 1; hit.Final = final; hit.Phase = c.Phase; hit.Hitbox = box;
                    // One event owns impact audio; the separate combo event updates HUD only.
                    Emit("combo", a, a.Combo + " ACERTOS", d.Player, a.ComboDamage);
                }
                else { var miss = Emit("super_miss", a, c.Name, d.Player); miss.Pulse = index + 1; }
            }
            if (vitor && c.Frame > 111) d.X = Clamp(c.OriginDefenderX + a.Face * (c.Frame - 111) * 5.5f, 90, 1190);
            if (c.Frame >= c.Duration) FinishCinematic();
        }

        private HitRect CinematicHitbox(FighterState a, FighterState d, int index)
        {
            if (a.Id == 1) return new HitRect(a.Face == 1 ? a.X + 15 : a.X - 156, Ground - 214, 141, 185);
            if (a.Id == 4)
            {
                int face = index % 2 == 0 ? 1 : -1;
                float x = Cinematic.OriginDefenderX - face * 76;
                return new HitRect(face > 0 ? x : x - 108, Ground - 188, 108, 160);
            }
            return d.Hurtbox;
        }

        private void EquipArmor(FighterState f, int duration)
        {
            f.TransformFrames = duration; Emit("buff", f, "EXOESQUELETO ATIVO");
        }

        private void FinishCinematic()
        {
            var a = Fighters[Cinematic.Attacker]; var d = Fighters[Cinematic.Defender];
            if (a.Id == 4) EquipArmor(a, Cinematic.ArmorDuration);
            Cinematic.Active = false; Cinematic.Captured = false; Cinematic.ImpactPause = 0;
            foreach (var f in Fighters)
            {
                f.UltimateTint = false; f.HitFlash = 0; f.Move = null; f.BufferFrames = 0; f.Buffered = null; f.Motions.Clear(); f.Frame = 0;
            }
            a.State = "idle"; a.Animation = "idle"; a.ComboTimer = 55; a.CancelCount = 3;
            d.State = "knockdown"; d.Animation = "down"; d.Stun = 32; d.Invuln = 35; d.WakeupUsed = false; d.VelocityX = a.Face * (a.Id == 1 || a.Id == 4 || a.Id == 9 ? 10 : 8); d.VelocityY = -4;
            Emit("cinematic_end", a, Cinematic.Name, d.Player);
            if (Training.Enabled && Training.InfiniteHealth) d.Hp = d.MaxHp;
            CheckRoundEnd();
        }

        public void CancelCinematic()
        {
            if (!Cinematic.Active) return;
            Cinematic.Active = false; Cinematic.Captured = false; Cinematic.ImpactPause = 0; Hitstop = 0; Projectiles.Clear();
            foreach (var f in Fighters)
            {
                f.State = "idle"; f.Animation = "idle"; f.Move = null; f.Stun = 0; f.Frame = 0; f.VelocityX = f.VelocityY = 0;
                f.UltimateTint = false; f.HitFlash = 0; f.BufferFrames = 0; f.Buffered = null; f.Motions.Clear();
            }
            Emit("cinematic_end", Fighters[Cinematic.Attacker], "CANCELADO", Cinematic.Defender); CheckRoundEnd();
        }
    }
}
