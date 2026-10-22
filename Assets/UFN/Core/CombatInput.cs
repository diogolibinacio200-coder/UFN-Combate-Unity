using System;

namespace UFN.Core
{
    public sealed partial class CombatWorld
    {
        private void Capture(FighterState f, InputFrame input)
        {
            var last = f.LastInput; int face = Facing(f, Fighters[1 - f.Player]);
            f.LastInput = input;
            var command = new InputCommand { Held = input, Face = face, Attack = input.Attack && !last.Attack, Heavy = input.Heavy && !last.Heavy, Kick = input.Kick && !last.Kick, Up = input.Up && !last.Up, Block = input.Block && !last.Block, Dodge = input.Dodge && !last.Dodge, Power = input.Power && !last.Power, Tag = input.Tag && !last.Tag };
            bool[] current = { input.Down, input.Left, input.Right, input.Up };
            bool[] previous = { last.Down, last.Left, last.Right, last.Up };
            string[] names = { "down", face == -1 ? "front" : "back", face == 1 ? "front" : "back", "up" };
            for (int i = 0; i < 4; i++) if (current[i] && !previous[i])
            {
                if ((i == 1 || i == 2) && IsFree(f) && f.Grounded)
                    foreach (var old in f.Motions)
                        if (old.Direction == names[i] && Tick - old.Tick <= 14) { f.DashFrames = 11; f.DashDirection = i == 2 ? 1 : -1; Emit("dash", f); break; }
                f.Motions.Add(new MotionInput(names[i], Tick));
            }
            f.Motions.RemoveAll(m => Tick - m.Tick > 18);
            if (command.Block) { f.GuardAge = 0; if (input.Direction == face) f.ParryAge = 0; }
            if (command.Attack || command.Heavy || command.Kick || command.Power || command.Up || command.Block || command.Dodge || command.Tag)
            {
                f.Buffered = command; f.BufferFrames = 8;
            }
        }

        private static bool Motion(FighterState f, string first, string second)
        {
            bool found = false;
            foreach (var m in f.Motions) { if (found && m.Direction == second) return true; if (m.Direction == first) found = true; }
            return false;
        }

        private MoveDefinition Special(FighterState f, int slot)
        {
            f.Buffered.SpecialSlot = slot; f.Motions.Clear();
            var c = Catalog.Character(f.Id);
            return slot == 3 ? c.Offensive : c.Specials[slot];
        }

        private bool Command(FighterState f, FighterState other, InputCommand input)
        {
            bool forward = input.Held.Direction == input.Face, back = input.Held.Direction == -input.Face;
            var character = Catalog.Character(f.Id);
            if (input.Dodge)
            {
                if (!IsFree(f)) return false;
                if (f.DodgeCooldown > 0 || !f.Grounded || f.Status == "root") return true;
                f.State = "evade"; f.Animation = "dash"; f.Move = null; f.Frame = 0; f.DodgeFrames = 18; f.DodgeCooldown = 54; f.Invuln = 7;
                f.DashDirection = input.Held.Direction != 0 ? input.Held.Direction : -input.Face; f.Motions.Clear(); Emit("dodge", f, "ESQUIVA"); return true;
            }
            if (input.Tag || (input.Held.Block && input.Kick)) { if (!TryTag(f.Player)) Emit("no_meter", f, "TROCA INDISPONÍVEL"); return true; }
            MoveDefinition move = null;
            if (input.Power || (input.Held.Block && input.Heavy)) move = character.Ultimate;
            else if (input.Held.Block)
            {
                if (input.Attack) move = new MoveDefinition { Name = "AGARRÃO", Kind = "throw", Family = "throw", Damage = 105, Startup = 6, Active = 3, Recovery = 28, Range = 86, Cost = 0, Height = 180, BodyPose = "grab", GuardDamage = 0, Visual = f.Id == 13 ? "bear" : "impact" };
                else return false;
            }
            else if (input.SpecialSlot >= 0) move = Special(f, input.SpecialSlot);
            else if (input.Heavy && ((forward && input.Held.Down) || Motion(f, "front", "down"))) move = Special(f, 3);
            else if (input.Kick && ((forward && input.Held.Down) || Motion(f, "down", "front"))) move = Special(f, 2);
            else if (input.Attack && ((back && input.Held.Down) || Motion(f, "back", "down"))) move = Special(f, 1);
            else if (input.Attack && Motion(f, "down", "front")) move = Special(f, 0);
            else if (input.Attack && Motion(f, "down", "back")) move = Special(f, 1);
            else if (input.Heavy && Motion(f, "down", "down")) move = Special(f, 2);
            else if (input.Attack && back) move = Special(f, 0);
            else if (f.PreparedFrames > 0 && (input.Kick || input.Heavy))
            {
                move = new MoveDefinition { Name = input.Held.Down ? "CHUTE RASTEIRO" : "VOLEIO PREPARADO", Kind = "projectile", Damage = 110, Startup = 10, Active = 1, Recovery = 29, Range = 65, Cost = 0, Speed = 11, Level = input.Held.Down ? "low" : "mid", Visual = "football", BodyPose = "kick" };
            }
            else if (f.FeintFrames > 0 && f.Id == 6 && (input.Attack || input.Heavy)) move = new MoveDefinition { Name = "FINALIZAÇÃO DE QUADRA", Kind = "projectile", Damage = 89, Startup = 8, Active = 1, Recovery = 26, Range = 64, Cost = 0, Speed = 11, Visual = "handball", BodyPose = "throw" };
            else if (input.Heavy)
            {
                move = back ? character.Defense : Catalog.Normals[input.Held.Up ? 7 : input.Held.Down ? 3 : 6];
                if (input.Held.Down && !back) { move = move.Clone(); move.Name = "RASTEIRA FORTE"; move.Damage = 72; move.Startup = 13; move.Recovery = 28; move.Push = 8; move.GuardDamage = 20; }
                else if (forward) { move = move.Clone(); move.Name = "FORTE EM AVANÇO"; move.Speed = 3.8f; move.Recovery = 30; }
            }
            else if (input.Kick) move = f.Combo >= 2 && f.HitConfirm ? Special(f, 0) : Catalog.Normals[!f.Grounded ? 5 : input.Held.Down ? 3 : 8];
            else if (input.Attack)
            {
                int slot = !f.Grounded ? (forward || back ? 5 : 4) : input.Held.Down ? 3 : input.Held.Up ? 7 : forward ? 1 : back ? 2 : 0;
                move = Catalog.Normals[slot];
                if (slot == 0 && f.State == "attack" && f.HitConfirm && f.Move?.Family == "normal" && f.CancelCount < 2 && Catalog.Chains.Length >= 2)
                {
                    int stage = f.Move.ChainStage + 1;
                    foreach (var chain in Catalog.Chains) if (chain.ChainStage == stage) { move = chain; break; }
                }
            }
            else if (input.Up && IsFree(f) && f.Grounded && f.Status != "root")
            {
                f.VelocityY = -14.4f; f.State = "jump"; f.Animation = "jump"; f.Frame = 0; Emit("jump", f); return true;
            }
            if (move == null) return false;
            if (f.Meter < move.Cost) { Emit("no_meter", f, "ENERGIA INSUFICIENTE"); return true; }
            if (!IsFree(f))
            {
                if (f.State != "attack" || !f.HitConfirm || f.CancelCount >= 3 || f.Move == null) return false;
                if (f.Move.Family == "normal")
                {
                    if (!f.Move.Cancel || (move.Family == "normal" && (move.Name == f.Move.Name || f.CancelCount >= 2)) || !Is(move.Family, "normal", "special", "super")) return false;
                }
                else if (move.Family != "super") return false;
                f.CancelCount++;
            }
            if (f.Id == 2 && move.Name == "DISPARO CERTEIRO" && f.Ammo <= 0) { Emit("no_meter", f, "RECARREGUE"); return true; }
            if (f.Id == 13 && f.BearStun > 0) { Emit("no_meter", f, "URSO DESORGANIZADO"); return true; }
            f.Face = input.Face; StartMove(f, move);
            if (move.Name == "VOLEIO PREPARADO" || move.Name == "CHUTE RASTEIRO") f.PreparedFrames = 0;
            if (move.Name == "FINALIZAÇÃO DE QUADRA") f.FeintFrames = 0;
            return true;
        }

        private void StartMove(FighterState f, MoveDefinition source)
        {
            var m = source.Clone();
            if (f.Id == 13) { if (m.Family == "normal") m.Name = "URSO · " + m.Name; m.Visual = m.Visual == "bear_return" ? m.Visual : "bear"; m.BodyPose = "projectile"; }
            if (f.Id == 11 && f.Embalo > 0 && m.Damage > 0) { m.Damage *= 1 + f.Embalo * .12f; f.Embalo = 0; }
            if (m.Family == "normal")
            {
                if (f.TransformFrames > 0) { m.Range += 30; m.Damage *= 1.2f; m.Visual = "exosuit"; }
                if (f.OverclockFrames > 0) m.Damage *= 1.35f;
            }
            if (f.Id == 1 && m.Name == "PERDEU A PACIÊNCIA" && f.Rage >= 55) { m.Damage *= 1.35f; m.Visual = "rage_combo"; }
            if (f.Id == 2 && m.Name == "DISPARO CERTEIRO") f.Ammo--;
            if (f.Id == 8 && m.Name == "VETOR DIRECIONAL" && f.LastInput.Up) m.Vy = -5;
            // Campaign bonuses are captured by the move/projectile, never written to catalog data.
            m.Damage *= f.DamageMultiplier;
            if (m.Kind == "jump_toss") f.VelocityY = -10;
            f.Meter = Math.Max(0, f.Meter - m.Cost); f.State = "attack"; f.Frame = 0; f.Move = m;
            f.HitConfirm = false; f.HitTarget = false; f.Invuln = Math.Max(f.Invuln, m.Invuln); f.VelocityX *= .5f; f.DashFrames = 0; f.Attacks++;
            f.UltimateTint = f.Id == 1 && m.Kind == "rush_red"; Emit("attack", f, m.Name);
            if (m.Family == "super")
            {
                Hitstop = Math.Max(Hitstop, 12); Emit("super", f, m.Name);
                if (f.Id == 1 || f.Id == 4 || f.Id == 9) Emit("ultimate_phase", f, m.Name).Phase = "activation";
            }
            Animate(f);
        }
    }
}
