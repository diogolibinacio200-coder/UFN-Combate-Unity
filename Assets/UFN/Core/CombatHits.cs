using System;
using System.Collections.Generic;

namespace UFN.Core
{
    public sealed partial class CombatWorld
    {
        private sealed class Contact { internal int Owner; internal MoveDefinition Move; }
        private static bool Active(FighterState f) => f.Move != null && f.Frame >= f.Move.Startup && f.Frame < f.Move.Startup + f.Move.Active;
        public HitRect Hitbox(FighterState f)
        {
            var m = f.Move; if (m == null) return default;
            float origin = f.Id == 13 ? f.BearX : f.X;
            float x = f.Face == 1 ? origin + 15 : origin - m.Range;
            if (m.Kind == "burst") x = f.X - m.Range;
            return new HitRect(x, f.Y + m.OffsetY - m.Height * .5f, m.Kind == "burst" ? m.Range * 2 : m.Range - 15, m.Height);
        }

        private void ProcessMove(FighterState f, FighterState other, List<Contact> contacts)
        {
            var m = f.Move; if (!Active(f)) return;
            if (f.Frame == m.Startup)
            {
                if (Is(m.Kind, "robot_super", "rush_combo", "rush_red")) Emit("ultimate_phase", f, m.Name, other.Player).Phase = f.Id == 4 ? "robots" : "capture";
                switch (m.Kind)
                {
                    case "robot_super":
                        for (int lane = 0; lane < 2; lane++)
                        {
                            float offset = 52 - lane * 46;
                            var robot = Spawn(f, m, "robot_strike", f.X + f.Face * offset, Ground - 105);
                            robot.VelocityX = f.Face * 9; robot.Radius = 32; robot.Life = Math.Max(1, (int)Math.Ceiling((m.Range - offset - 32) / 9)); robot.Lane = lane; robot.AttackSerial = f.Attacks;
                        }
                        break;
                    case "projectile": case "jump_toss": Spawn(f, m, "projectile", f.X + f.Face * 67, f.Y - 112); break;
                    case "prepare": f.PreparedFrames = m.Duration; Emit("buff", f, m.Name); break;
                    case "transform": EquipArmor(f, m.Duration); break;
                    case "cloud": case "loop": case "sentry": case "drone":
                        foreach (var old in Projectiles) if (old.Owner == f.Player && old.Kind == m.Kind) old.Dead = true;
                        Spawn(f, m, m.Kind, f.X + f.Face * (m.Kind == "sentry" || m.Kind == "drone" ? 65 : m.Range), Ground - 50); break;
                    case "allies":
                        Spawn(f, m, "projectile", f.X + f.Face * 65, f.Y - 95);
                        var allyMove = m.Clone(); allyMove.SetupTime = 22;
                        Spawn(f, allyMove, "projectile", f.X - f.Face * 25, f.Y - 115); break;
                    case "feint": f.X = Clamp(f.X + f.Face * m.Range, 90, 1190); f.FeintFrames = m.Duration; f.Invuln = 5; Emit("teleport", f, m.Name); break;
                    case "trap":
                        foreach (var old in Projectiles) if (old.Owner == f.Player && old.Kind == "trap") old.Dead = true;
                        Spawn(f, m, "trap", Clamp(f.X + f.Face * m.Range, 110, 1170), Ground - 22); break;
                    case "barrier":
                        foreach (var old in Projectiles) if (old.Owner == f.Player && old.Kind == "barrier") old.Dead = true;
                        Spawn(f, m, "barrier", f.X + f.Face * 100, Ground - 92); break;
                    case "teleport": case "evade": f.X = Clamp(f.X + f.Face * m.Range, 90, 1190); f.Invuln = 5; Emit("teleport", f, m.Name); break;
                    case "crossup": f.X = Clamp(other.X + other.Face * 90, 90, 1190); f.Face = Facing(f, other); Emit("teleport", f, m.Name); break;
                    case "blink_strike": f.X = Clamp(f.X + f.Face * Math.Min(90, Math.Max(0, Math.Abs(f.X - other.X) - 110)), 90, 1190); Emit("teleport", f, m.Name); break;
                    case "buff": f.OverclockFrames = f.Rage >= 20 ? m.Duration : 0; f.Rage = Math.Max(0, f.Rage - 35); Emit("buff", f, m.Name); break;
                    case "uppercut": if (f.Id != 13) f.VelocityY = -6.8f; break;
                    case "throw":
                        if (Math.Abs((f.Id == 13 ? f.BearX : f.X) - other.X) <= m.Range && other.Grounded && other.Invuln == 0 && other.Stun == 0 && !Is(other.State, "throw", "thrown"))
                            contacts.Add(new Contact { Owner = f.Player, Move = m.Clone() });
                        break;
                }
            }
            if (Is(m.Kind, "robot_super", "projectile", "trap", "barrier", "teleport", "counter", "buff", "evade", "throw", "cloud", "loop", "sentry", "drone", "allies", "reload", "drink", "transform", "prepare", "feint", "jump_toss")) return;
            if (f.HitTarget || other.Invuln > 0 || Is(other.State, "throw", "thrown", "knockdown", "wakeup")) return;
            if (Hitbox(f).Intersects(other.Hurtbox)) contacts.Add(new Contact { Owner = f.Player, Move = m.Clone() });
        }

        private void ResolveContacts(List<Contact> contacts, InputFrame[] inputs)
        {
            if (contacts.Count == 2 && contacts[0].Move.Kind == "throw" && contacts[1].Move.Kind == "throw")
            {
                foreach (var f in Fighters) { f.State = "idle"; f.Move = null; f.Invuln = 12; f.VelocityX = -f.Face * 7; }
                Emit("throw_break", Fighters[0], "AGARRÕES ANULADOS"); Hitstop = 8; return;
            }
            int strikes = 0; foreach (var c in contacts) if (c.Move.Kind != "throw") strikes++;
            bool trade = strikes == 2 && Fighters[0].Invuln == 0 && Fighters[1].Invuln == 0;
            foreach (var c in contacts)
            {
                if (Cinematic.Active) break;
                var a = Fighters[c.Owner]; var d = Fighters[1 - c.Owner];
                if (c.Move.Kind == "throw") { if (strikes == 0) BeginThrow(a, d); continue; }
                if (ApplyHit(a, d, c.Move, inputs[1 - c.Owner], false, trade))
                {
                    a.HitTarget = true;
                    if (c.Move.Kind == "rush_red" && !Cinematic.Active)
                    {
                        a.UltimateTint = false;
                        if (a.State == "attack" && a.Move != null) { a.Frame = a.Move.Startup + a.Move.Active; a.VelocityX = 0; }
                    }
                }
            }
        }

        private bool ApplyHit(FighterState a, FighterState d, MoveDefinition m, InputFrame input, bool projectile, bool trade = false)
        {
            if (Cinematic.Active || d.Invuln > 0 || Is(d.State, "thrown", "throw", "knockdown", "wakeup")) return false;
            if (d.State == "attack" && d.Move?.Kind == "counter" && Active(d))
            {
                var counterMove = d.Move.Clone();
                if (counterMove.Family == "super") { StartCinematic(d, a, counterMove); return true; }
                d.Move = null; d.State = "idle"; d.Invuln = 8;
                if (projectile) { AddMeter(d, 8); Emit("counter", d, counterMove.Name + " · ABSORVIDO", a.Player); return true; }
                a.State = "hitstun"; a.UltimateTint = false; a.Stun = 29; a.Frame = 0; a.Hp = Math.Max(0, a.Hp - counterMove.Damage); a.VelocityX = d.Face * counterMove.Push; a.Move = null;
                if (counterMove.Freeze > 0) { a.Status = "freeze"; a.StatusFrames = counterMove.Freeze; a.Stun += counterMove.Freeze; }
                a.DamageReceived += counterMove.Damage; d.DamageDealt += counterMove.Damage; AddMeter(d, 13); Hitstop = Math.Max(Hitstop, 10); Emit("counter", d, counterMove.Name, a.Player, counterMove.Damage); return true;
            }
            bool canGuard = Is(d.State, "idle", "walk", "crouch", "block", "crouch_block", "blockstun") && d.Grounded;
            bool correctLevel = (input.Down && m.Level != "high") || (!input.Down && m.Level != "low");
            if (canGuard && input.Block && correctLevel)
            {
                if (d.ParryAge <= 3 && !projectile)
                {
                    d.ParryAge = d.GuardAge = 999; d.State = "idle"; d.Stun = 0; d.Invuln = 7;
                    a.State = "hitstun"; a.Stun = 21; a.Frame = 0; a.UltimateTint = false; a.Move = null;
                    AddMeter(d, 15); Hitstop = Math.Max(Hitstop, 9); Emit("parry", d, "APARO!", a.Player); return true;
                }
                bool perfect = d.GuardAge <= 5; d.GuardAge = 999;
                d.Guard = Math.Max(0, d.Guard - m.GuardDamage * (perfect ? .18f : 1)); d.GuardDelay = 100;
                if (d.Id == 1) d.Rage = Math.Min(100, d.Rage + 8);
                d.Hp = Math.Max(1, d.Hp - (perfect ? 0 : m.Damage * m.Chip)); d.State = "blockstun"; d.Stun = perfect ? 5 : m.Blockstun;
                d.Frame = 0; d.VelocityX = a.Face * (perfect ? 1.3f : 3); a.VelocityX = -a.Face * 1.4f;
                AddMeter(d, perfect ? 10 : 3); AddMeter(a, 2); Hitstop = Math.Max(Hitstop, perfect ? 7 : 4);
                Emit(perfect ? "perfect_block" : "block", d, perfect ? "DEFESA PERFEITA" : "DEFESA", a.Player);
                if (d.Guard <= 0) { d.State = "guardbreak"; d.Stun = 52; d.Guard = 22; Hitstop = Math.Max(Hitstop, 15); Emit("guard_break", d, "GUARDA QUEBRADA!"); }
                return true;
            }
            if (m.Family == "super" && a.Id != 14 && !trade) { StartCinematic(a, d, m); return true; }
            bool counter = d.State == "attack" && d.Move != null && d.Frame < d.Move.Startup;
            if (a.ComboTimer <= 0 || !Is(d.State, "hitstun", "guardbreak")) { a.Combo = 0; a.ComboDamage = 0; a.CancelCount = 0; }
            float scaling = Math.Max(.32f, 1 - a.Combo * .12f);
            float damage = (float)Math.Round(m.Damage * scaling * (counter ? 1.08f : 1), MidpointRounding.AwayFromZero);
            DealDamage(a, d, damage);
            if (d.Id == 1) d.Rage = Math.Min(100, d.Rage + damage * .24f);
            if (a.OverclockFrames > 0 && m.Family == "normal") a.OverclockFrames = 0;
            a.HitConfirm = true; d.Frame = 0; d.State = "hitstun"; d.Stun = Math.Max(8, m.Hitstun - (a.Combo - 1) * 2 + (counter ? 7 : 0)); d.Move = null; d.UltimateTint = false;
            d.VelocityX = a.Face * (m.Push + a.Combo * .35f); if (m.Pull > 0) d.VelocityX = -a.Face * m.Pull;
            if (m.Launch < 0 && d.Juggle < 4) { d.VelocityY = m.Launch * Math.Max(.5f, 1 - d.Juggle * .16f); d.Juggle++; }
            else if (!d.Grounded) d.Juggle++;
            if (m.Freeze > 0 && a.Combo <= 3) { d.Stun += m.Freeze; d.Status = "freeze"; d.StatusFrames = m.Freeze; }
            if (m.Root > 0 && d.ControlImmunity <= 0) { d.Status = "root"; d.StatusFrames = m.Root; d.Stun = Math.Max(d.Stun, m.Root); d.ControlImmunity = m.Root + 90; }
            if (m.Slow > 0) { d.Status = "slow"; d.StatusFrames = m.Slow; }
            if (a.Combo >= 8 || d.Juggle >= 5) { d.State = "knockdown"; d.Stun = 32; d.Invuln = 35; d.WakeupUsed = false; d.VelocityY = 3; d.VelocityX = a.Face * 11; }
            a.ComboTimer = Math.Max(45, d.Stun + 16); AddMeter(a, counter ? 8 : 6); AddMeter(d, 4); Hitstop = Math.Max(Hitstop, m.Damage < 90 ? 7 : 11); d.HitFlash = 5;
            var e = Emit("hit", a, m.Name, d.Player, damage); e.X = d.X; e.Y = d.Y - 105;
            if (counter) Emit("counter", a, "CONTRA-ATAQUE!", d.Player);
            if (a.Combo >= 2) Emit("combo", a, a.Combo + " ACERTOS", d.Player, a.ComboDamage);
            if (d.Id == 13) { d.BearStun = Math.Max(d.BearStun, 24); d.BearX = Toward(d.BearX, d.X + d.Face * 80, 55); }
            return true;
        }

        private static void DealDamage(FighterState a, FighterState d, float damage)
        {
            d.Hp = Math.Max(0, d.Hp - damage); d.DamageReceived += damage; a.DamageDealt += damage;
            a.Combo++; a.ComboDamage += damage; a.MaxCombo = Math.Max(a.MaxCombo, a.Combo);
        }

        private void BeginThrow(FighterState a, FighterState d)
        {
            if (a.Move.Family == "super") { StartCinematic(a, d, a.Move); return; }
            a.State = "throw"; a.ThrowTarget = d.Player; d.State = "thrown"; d.ThrowTarget = a.Player; d.ThrowFrames = 11; d.Move = null; d.UltimateTint = false; Emit("throw_start", a, "AGARRÃO");
        }

        private void UpdateThrow(FighterState d, FighterState a, InputFrame input)
        {
            d.ThrowFrames--; d.X = Clamp((a.Id == 13 ? a.BearX : a.X) + a.Face * 70, 90, 1190); d.Y = Ground; d.Animation = "hurt";
            if (input.Block && d.BufferFrames > 0 && d.Buffered != null && d.Buffered.Attack)
            {
                a.State = d.State = "idle"; a.Move = d.Move = null; a.Invuln = d.Invuln = 12; a.ThrowTarget = d.ThrowTarget = -1;
                d.VelocityX = a.Face * 7; a.VelocityX = -a.Face * 7; Hitstop = Math.Max(Hitstop, 8); Emit("throw_break", d, "AGARRÃO ANULADO!"); return;
            }
            if (d.ThrowFrames > 0) return;
            float damage = a.Move?.Damage ?? 105; d.Hp = Math.Max(0, d.Hp - damage); d.State = "knockdown"; d.Stun = 34; d.WakeupUsed = false;
            d.VelocityX = a.Face * 11; d.VelocityY = -5; a.State = "idle"; a.ThrowTarget = d.ThrowTarget = -1;
            d.DamageReceived += damage; a.DamageDealt += damage; a.Move = null; AddMeter(a, 9); AddMeter(d, 5); Hitstop = Math.Max(Hitstop, 12); Emit("throw", a, "AGARRÃO", d.Player, damage);
        }
    }
}
