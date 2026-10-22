using System;

namespace UFN.Core
{
    /// <summary>Seeded, reaction-limited CPU. Reads only visible present state, never player inputs or future states.</summary>
    public sealed class CombatAI
    {
        public int Difficulty { get; set; }
        private uint random;
        private int untilDecision;
        private InputFrame held;
        public CombatAI(uint seed = 1, int difficulty = 1) { random = seed == 0 ? 1u : seed; Difficulty = difficulty; }
        private uint Next() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random; }
        private float Chance() => (Next() & 0xffffff) / 16777216f;

        public InputFrame Decide(CombatWorld world, int player)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            // Attack buttons last one simulation frame. Remaining movement/guard is a committed decision.
            held.Attack = held.Heavy = held.Kick = held.Power = held.Dodge = held.Up = held.Tag = false;
            if (world.Winner >= 0 || world.Cinematic.Active) { held = default; return held; }
            if (untilDecision-- > 0) return held;
            int level = Math.Max(0, Math.Min(5, Difficulty)); untilDecision = new[] { 29, 20, 13, 9, 7, 5 }[level] + (int)(Next() % 6);
            var f = world.Fighters[player]; var opponent = world.Fighters[1 - player];
            float distance = Math.Abs(opponent.X - f.X); int face = opponent.X >= f.X ? 1 : -1;
            held = default;
            if (world.TeamMode && f.Hp < 280 && world.Reserve(player).Hp > f.Hp && world.TagCooldowns[player] == 0 && CombatWorld.IsFree(f)) { held.Tag = true; return held; }
            if (f.State == "thrown") { if (Chance() < .08f + .1f * level) { held.Block = true; held.Attack = true; } return held; }
            if (f.Stun > 0) { if (f.State == "knockdown" && Chance() < .18f * level) held.Up = true; return held; }
            bool threat = opponent.State == "attack" && distance < 275;
            foreach (var p in world.Projectiles) if (p.Owner != player && p.Kind == "projectile" && Math.Abs(p.X - f.X) < 210) threat = true;
            if (threat && Chance() < new[] { .27f, .46f, .66f, .80f, .86f, .9f }[level])
            {
                held.Block = true; held.Down = opponent.Move?.Level == "low";
                // Intentional human-like late block; no perfect three-frame prediction.
                if (Chance() < .06f * level && f.DodgeCooldown == 0) { held.Block = false; held.Dodge = true; SetDirection(-face); }
                return held;
            }
            if (f.State == "attack" && f.HitConfirm && level >= 2)
            {
                if (f.Meter >= 100 && distance < 275) held.Power = true;
                else if (f.Meter >= 20) { SetDirection(-face); held.Attack = true; }
                else held.Attack = true;
                return held;
            }
            if (f.Meter >= 100 && (distance < 290 || f.Id == 1) && Chance() < .15f + .1f * level) { held.Power = true; return held; }
            if (distance > 230)
            {
                if (f.Meter >= 18 && Chance() < .16f * level) { SetDirection(-face); held.Attack = true; }
                else { SetDirection(face); if (distance < 420 && Chance() < .035f * level) held.Up = true; }
                return held;
            }
            float choice = Chance();
            if (distance < 85 && choice < .10f + .02f * level) { held.Block = true; held.Attack = true; }
            else if (f.Meter >= 26 && choice < .26f) { SetDirection(face); held.Down = true; held.Heavy = true; }
            else if (f.Meter >= 18 && choice < .42f) { SetDirection(-face); held.Down = true; held.Attack = true; }
            else if (choice < .65f) { held.Attack = true; if (distance > 110) SetDirection(face); }
            else if (choice < .82f) { held.Kick = true; held.Down = Chance() < .35f; }
            else if (choice < .95f) held.Heavy = true;
            else { held.Block = true; SetDirection(-face); }
            return held;
        }

        private void SetDirection(int direction) { held.Left = direction < 0; held.Right = direction > 0; }
    }
}
