using System;

namespace UFN.Core
{
    public sealed partial class CombatWorld
    {
        private ProjectileState Spawn(FighterState f, MoveDefinition m, string kind, float x, float y)
        {
            var p = new ProjectileState { Id = ++serial, Owner = f.Player, Kind = kind, Visual = m.Visual, X = x, Y = y, Face = f.Face, VelocityX = kind == "projectile" ? m.Speed * f.Face : 0, VelocityY = kind == "projectile" ? m.Vy : 0, Life = m.Duration, Radius = kind == "projectile" ? m.Range * .5f : kind == "trap" ? 58 : 42, Move = m.Clone(), Hp = kind == "barrier" || kind == "sentry" ? (m.Visual == "firewall" ? 1 : 2) : 1, Bounces = m.Bounces };
            Projectiles.Add(p); var e = Emit("projectile", f, m.Name); e.X = x; e.Y = y; return p;
        }

        private void UpdateProjectiles(InputFrame[] inputs)
        {
            // Devices can emit projectiles; process only the objects present at tick start.
            int count = Projectiles.Count;
            for (int i = 0; i < count; i++)
            {
                var p = Projectiles[i]; if (p.Dead) continue;
                if (p.Kind == "robot_strike")
                {
                    var owner = Fighters[p.Owner];
                    if (owner.State != "attack" || owner.Move?.Kind != "robot_super" || owner.Attacks != p.AttackSerial) { p.Dead = true; continue; }
                }
                p.Life--; p.Age++;
                if (p.Age > p.Move.SetupTime) { p.VelocityY += p.Move.Gravity; p.X += p.VelocityX; p.Y += p.VelocityY; }
                if (p.Y >= Ground - 25 && p.Kind == "projectile")
                {
                    if (p.Bounces > 0) { p.Y = Ground - 25; p.VelocityY = -Math.Abs(p.VelocityY) * .72f; p.Bounces--; }
                    else p.Dead = true;
                }
                var target = Fighters[1 - p.Owner];
                if (p.Kind == "projectile" && target.Id == 13 && target.BearStun == 0 && Math.Abs(p.X - target.BearX) < p.Radius + 36 && p.Y > Ground - 170) { BearDamage(target, p.Move.Damage); p.Dead = true; }
                if (p.Kind == "cloud" && Math.Abs(target.X - p.X) < 150 && target.Grounded) { target.Status = "slow"; target.StatusFrames = 8; }
                if ((p.Kind == "drone" && p.Age == 25) || (p.Kind == "sentry" && p.Age % 55 == 0))
                {
                    var shot = p.Move.Clone(); shot.Kind = "projectile"; shot.SetupTime = 0; shot.Duration = 100; shot.Visual = "pulse";
                    Spawn(Fighters[p.Owner], shot, "projectile", p.X, p.Y - 45);
                    if (p.Kind == "drone") p.Dead = true;
                }
                if (p.Visual == "packet" && p.Age == 18) p.VelocityX *= 1.65f;
                if ((p.Kind == "sentry" || p.Kind == "barrier") && target.State == "attack" && target.Move != null && target.Move.Damage > 0 && Active(target) && Hitbox(target).Intersects(new HitRect(p.X - 1, p.Y - 1, 2, 2))) p.Dead = true;
                if (p.Kind == "loop" && p.Visual == "gas" && p.Move.Family == "super")
                {
                    p.Radius = 83; p.X = 340 + 300 * Math.Min(2, Math.Max(0, p.Age - p.Move.SetupTime) / Math.Max(1, p.Move.PulseInterval));
                }
                if (p.Kind == "loop" && p.Hits < p.Move.PulseCount && p.Age >= p.Move.SetupTime && (p.Age - p.Move.SetupTime) % Math.Max(1, p.Move.PulseInterval) == 0)
                {
                    p.NextHit = 0; p.Hits++; var e = Emit("special", Fighters[p.Owner], "ONDA " + p.Hits); e.X = p.X; e.Y = p.Y;
                }
                if (p.Life <= 0 || p.X < 25 || p.X > 1255) p.Dead = true;
            }
            for (int i = 0; i < Projectiles.Count; i++)
            {
                var a = Projectiles[i]; if (a.Dead || !Is(a.Kind, "projectile", "barrier")) continue;
                for (int j = i + 1; j < Projectiles.Count && !a.Dead; j++)
                {
                    var b = Projectiles[j];
                    if (b.Dead || a.Owner == b.Owner || !Is(b.Kind, "projectile", "barrier") || (a.Kind == "barrier" && b.Kind == "barrier")) continue;
                    if (Math.Abs(a.X - b.X) >= a.Radius + b.Radius || Math.Abs(a.Y - b.Y) >= 100) continue;
                    if (a.Kind == "barrier") a.Hp--; else a.Dead = true;
                    if (b.Kind == "barrier") b.Hp--; else b.Dead = true;
                    if (a.Hp <= 0) a.Dead = true; if (b.Hp <= 0) b.Dead = true;
                    var e = Emit("clash", Fighters[a.Owner], "COLISÃO"); e.X = (a.X + b.X) * .5f; e.Y = a.Y;
                }
            }
            for (int i = 0; i < Projectiles.Count; i++)
            {
                var p = Projectiles[i];
                if (p.Dead || Is(p.Kind, "barrier", "cloud", "drone", "sentry") || p.Age < p.Move.SetupTime) continue;
                if (p.Kind == "loop" && (p.Hits <= 0 || p.NextHit > 0 || (p.Age - p.Move.SetupTime) % Math.Max(1, p.Move.PulseInterval) > 10)) continue;
                if (p.Kind == "trap" && p.Age < 18) continue;
                var d = Fighters[1 - p.Owner]; var box = new HitRect(p.X - p.Radius, p.Y - p.Radius, p.Radius * 2, p.Radius * 2);
                if (!box.Intersects(d.Hurtbox)) continue;
                if (p.Kind == "projectile" && d.State == "attack" && d.Move != null && d.Move.Reflect && Active(d))
                {
                    p.Owner = d.Player; p.VelocityX *= -1; p.Face *= -1; p.X += p.Face * 85; Emit("parry", d, "REFLEXO ADAPTATIVO"); continue;
                }
                if (!ApplyHit(Fighters[p.Owner], d, p.Move, inputs[1 - p.Owner], true)) continue;
                if (Cinematic.Active) return;
                if (p.Kind == "robot_strike") foreach (var robot in Projectiles) if (robot.Kind == "robot_strike" && robot.Owner == p.Owner) robot.Dead = true;
                if (p.Kind == "loop") p.NextHit = 1; else p.Dead = true;
            }
            Projectiles.RemoveAll(p => p.Dead);
        }
    }
}
