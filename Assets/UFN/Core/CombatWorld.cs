using System;
using System.Collections.Generic;

namespace UFN.Core
{
    /// <summary>Authoritative, fixed-step 60 Hz simulation. No clocks, devices, rendering or Unity dependencies.</summary>
    public sealed partial class CombatWorld
    {
        public const float Ground = 590f;
        public const int FramesPerSecond = 60;
        public GameCatalog Catalog { get; }
        public FighterState[] Fighters { get; private set; }
        public List<ProjectileState> Projectiles { get; } = new List<ProjectileState>();
        public List<CombatEvent> Events { get; } = new List<CombatEvent>();
        public CinematicState Cinematic { get; private set; } = new CinematicState();
        public TrainingSettings Training { get; private set; } = new TrainingSettings();
        public int Tick { get; private set; }
        public int TimerFrames { get; set; } = 99 * 60;
        public int Hitstop { get; private set; }
        public int Winner { get; private set; } = -1;
        public int Round { get; private set; } = 1;
        public int Difficulty { get; set; } = 1;
        public string Phase => Winner >= 0 ? "result" : Cinematic.Active ? "cinematic" : "fight";
        private int serial;
        private readonly int[] selectedIds = new int[2];
        private uint randomState;

        public CombatWorld(GameCatalog catalog, int p1Id, int p2Id, uint seed = 1, int difficulty = 1)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (catalog.Normals.Length < 9) throw new ArgumentException("The catalog requires all nine normal attacks.");
            randomState = seed == 0 ? 1u : seed;
            Difficulty = Math.Max(0, Math.Min(3, difficulty));
            selectedIds[0] = p1Id; selectedIds[1] = p2Id; ResetRound();
        }

        public void StartRound(int p1Id, int p2Id)
        {
            Catalog.Character(p1Id); Catalog.Character(p2Id);
            selectedIds[0] = p1Id; selectedIds[1] = p2Id; TeamMode = false; Round++; ResetRound();
        }

        public void ResetRound()
        {
            Fighters = new FighterState[2];
            Teams = new FighterState[2][]; TeamActive = new int[2]; TagCooldowns = new int[2];
            for (int i = 0; i < 2; i++)
            {
                Catalog.Character(selectedIds[i]);
                Teams[i] = new FighterState[2];
                for (int slot = 0; slot < 2; slot++)
                {
                    int id = slot == 0 || !TeamMode ? selectedIds[i] : selectedReserves[i];
                    int level = Math.Max(0, Math.Min(3, Difficulty));
                    float hp = id == 14 ? new[] { 1100f, 1150f, 1250f, 1300f }[level] : id == 15 ? new[] { 1150f, 1250f, 1300f, 1400f }[level] : 1000;
                    Teams[i][slot] = new FighterState { Id = id, Player = i, X = i == 0 ? 370 : 910, Face = i == 0 ? 1 : -1, BearX = i == 0 ? 450 : 830, MaxHp = hp, Hp = hp };
                }
                Fighters[i] = Teams[i][0];
            }
            Projectiles.Clear(); Events.Clear(); Cinematic = new CinematicState(); Hitstop = 0; Winner = -1;
            Tick = 0; TimerFrames = (TeamMode ? 150 : 99) * 60; serial = 0;
            if (Training.Enabled && Training.InfiniteMeter) foreach (var f in Fighters) f.Meter = 100;
        }

        public void Step(InputFrame p1, InputFrame p2)
        {
            Events.Clear();
            if (Winner >= 0) return;
            if (Cinematic.Active) { Tick++; TickCinematic(); return; }
            var inputs = new[] { p1, p2 };
            for (int i = 0; i < 2; i++)
            {
                if (IsFree(Fighters[i])) Fighters[i].Face = Facing(Fighters[i], Fighters[1 - i]);
                Capture(Fighters[i], inputs[i]);
            }
            if (Hitstop > 0) { Hitstop--; return; }
            Tick++;
            for (int i = 0; i < 2; i++) TagCooldowns[i] = Dec(TagCooldowns[i]);
            if (!Training.Enabled) TimerFrames = Math.Max(0, TimerFrames - 1);
            for (int i = 0; i < 2; i++) UpdateFighter(Fighters[i], Fighters[1 - i], inputs[i]);
            Pushboxes();
            var contacts = new List<Contact>(2);
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                if (f.State != "attack" && f.State != "throw" && f.State != "thrown") f.Face = Facing(f, Fighters[1 - i]);
                if (f.State == "attack" && f.Move != null) ProcessMove(f, Fighters[1 - i], contacts);
            }
            ResolveContacts(contacts, inputs);
            if (Cinematic.Active) return;
            UpdateProjectiles(inputs);
            if (Cinematic.Active) return;
            if (Training.Enabled)
            {
                foreach (var f in Fighters)
                {
                    if (Training.InfiniteMeter) f.Meter = 100;
                    if (Training.InfiniteHealth && (f.Hp <= 0 || (f.Stun == 0 && Is(f.State, "idle", "walk", "crouch", "block", "crouch_block") && f.ComboTimer == 0))) f.Hp = f.MaxHp;
                }
            }
            CheckRoundEnd();
        }

        public static bool IsFree(FighterState f) => f.Stun == 0 && Is(f.State, "idle", "walk", "crouch", "block", "crouch_block", "jump", "wakeup");
        private static int Facing(FighterState a, FighterState b) => b.X >= a.X ? 1 : -1;
        private static bool Is(string value, params string[] values) { foreach (var v in values) if (value == v) return true; return false; }
        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
        private static float Toward(float from, float to, float delta) => from < to ? Math.Min(from + delta, to) : Math.Max(from - delta, to);
        private static int Dec(int value) => Math.Max(0, value - 1);

        private CombatEvent Emit(string type, FighterState f, string text = "", int target = -1, float damage = 0)
        {
            var e = new CombatEvent { Type = type, Player = f.Player, Target = target, Text = text, Damage = damage, X = f.X, Y = f.Y - 110, Combo = f.Combo };
            Events.Add(e); return e;
        }

        private void AddMeter(FighterState f, float value)
        {
            float previous = f.Meter; f.Meter = Clamp(f.Meter + value, 0, 100);
            if (previous < 100 && f.Meter >= 100) Emit("super_ready", f, "ULTIMATE PRONTA");
        }

        private void UpdateFighter(FighterState f, FighterState other, InputFrame input)
        {
            f.Frame++; f.GuardAge++; f.ParryAge++;
            f.Invuln = Dec(f.Invuln); f.HitFlash = Dec(f.HitFlash); f.DodgeCooldown = Dec(f.DodgeCooldown);
            f.GuardDelay = Dec(f.GuardDelay); f.TransformFrames = Dec(f.TransformFrames); f.OverclockFrames = Dec(f.OverclockFrames);
            f.PreparedFrames = Dec(f.PreparedFrames); f.FeintFrames = Dec(f.FeintFrames); f.DashFrames = Dec(f.DashFrames);
            f.ControlImmunity = Dec(f.ControlImmunity); f.EmbaloFrames = Dec(f.EmbaloFrames); if (f.EmbaloFrames == 0) f.Embalo = 0;
            f.StatusFrames = Dec(f.StatusFrames); if (f.StatusFrames == 0) f.Status = "";
            if (f.State != "attack" || f.Move?.Kind != "rush_red") f.UltimateTint = false;
            if (f.Id == 1 && f.ComboTimer == 0 && f.Stun == 0) f.Rage = Math.Max(0, f.Rage - .035f);
            f.ComboTimer = Dec(f.ComboTimer);
            if (f.ComboTimer == 0) { f.Combo = 0; f.ComboDamage = 0; f.CancelCount = 0; }
            if (f.GuardDelay == 0 && !input.Block && !Is(f.State, "blockstun", "guardbreak")) f.Guard = Math.Min(100, f.Guard + .27f);
            if (f.Id == 13) UpdateBear(f, other);
            if (f.State == "thrown") { UpdateThrow(f, other, input); return; }
            if (f.State == "throw") { f.Animation = "grab"; return; }
            if (f.State == "evade")
            {
                f.DodgeFrames = Dec(f.DodgeFrames); f.VelocityX = f.DashDirection * 6.4f * (.55f + .45f * f.DodgeFrames / 18f);
                Physics(f); f.BufferFrames = Dec(f.BufferFrames); f.Animation = "dash";
                if (f.DodgeFrames == 0) { f.State = "idle"; f.Frame = 0; f.VelocityX = 0; }
                return;
            }
            if (f.Stun > 0)
            {
                WakeupInput(f, input); f.Stun--; f.BufferFrames = Dec(f.BufferFrames); Physics(f);
                if (f.Stun == 0)
                {
                    if (Is(f.State, "knockdown", "guardbreak")) { f.State = "wakeup"; f.Stun = 10; f.Invuln = 12; f.Frame = 0; }
                    else { f.State = f.Grounded ? "idle" : "jump"; if (f.Grounded) f.Juggle = 0; }
                }
                Animate(f); return;
            }
            if (f.State == "attack" && f.Move != null)
            {
                var move = f.Move;
                if (move.Kind == "robot_super")
                {
                    int phase = f.Frame - move.Startup - move.Active;
                    if (phase == 20 || phase == 30) Emit("ultimate_phase", f, move.Name).Phase = phase == 20 ? "recall" : "assembly";
                }
                if (f.Frame > move.EndFrame)
                {
                    if (move.Kind == "robot_super") EquipArmor(f, move.Duration);
                    if (move.Kind == "drink") { f.Embalo = Math.Min(3, f.Embalo + 1); f.EmbaloFrames = 480; Emit("buff", f, "EMBALO " + f.Embalo); }
                    if (move.Kind == "reload") { f.Ammo = 6; Emit("buff", f, "MUNIÇÃO RECARREGADA"); }
                    f.State = f.Grounded ? "idle" : "jump"; f.Move = null; f.Frame = 0; f.UltimateTint = false;
                }
            }
            if (f.BufferFrames > 0 && f.Buffered != null)
            {
                if (Command(f, other, f.Buffered)) f.BufferFrames = 0;
                f.BufferFrames = Dec(f.BufferFrames);
            }
            if (IsFree(f))
            {
                if (f.Grounded && input.Block) { f.State = input.Down ? "crouch_block" : "block"; f.VelocityX = 0; }
                else if (f.Grounded && input.Down) { f.State = "crouch"; f.VelocityX = 0; }
                else
                {
                    float speed = Catalog.Character(f.Id).WalkSpeed;
                    f.VelocityX = input.Direction * speed * (f.Status == "root" ? 0 : f.Status == "slow" ? .63f : 1);
                    if (f.DashFrames > 0 && f.Grounded && f.Status != "root") f.VelocityX = f.DashDirection * speed * 2.2f;
                    f.State = f.Grounded ? (input.Direction == 0 ? "idle" : "walk") : "jump";
                }
            }
            else if (f.State == "attack" && f.Move != null)
            {
                var m = f.Move; bool active = f.Frame >= m.Startup && f.Frame < m.Startup + m.Active;
                if (m.Kind == "rush_red" && active)
                {
                    f.Face = Facing(f, other); f.VelocityX = f.Face * Math.Min(m.Speed, Math.Max(0, Math.Abs(other.X - f.X) - 86));
                }
                else f.VelocityX = active && Is(m.Kind, "dash", "slide", "strike", "uppercut", "reload", "rush_combo") ? m.Speed * f.Face : f.VelocityX * .8f;
            }
            Physics(f); Animate(f);
        }

        private void Physics(FighterState f)
        {
            if (f.RollFrames > 0) { f.VelocityX = f.RollDirection * 5; f.RollFrames--; }
            f.X = Clamp(f.X + f.VelocityX, 90, 1190);
            if (f.Y < Ground || f.VelocityY < 0) { f.VelocityY += .68f * (1 + f.Juggle * .17f); f.Y += f.VelocityY; }
            if (f.Y >= Ground)
            {
                bool landed = f.VelocityY > 0; f.Y = Ground; f.VelocityY = 0;
                if (landed)
                {
                    Emit("land", f);
                    if (f.State == "hitstun" && f.Juggle > 0) { f.State = "knockdown"; f.Stun = 24; f.Frame = 0; f.WakeupUsed = false; }
                    else if (f.State == "jump") { f.State = "idle"; f.Frame = 0; }
                }
            }
            if (Is(f.State, "hitstun", "blockstun", "knockdown", "guardbreak", "wakeup")) f.VelocityX *= .83f;
        }

        private void Pushboxes()
        {
            var a = Fighters[0]; var b = Fighters[1];
            if (Is(a.State, "throw", "thrown") || Is(b.State, "throw", "thrown") || Math.Abs(a.Y - b.Y) >= 135) return;
            float distance = Math.Abs(a.X - b.X); if (distance >= 66) return;
            int dir = b.X >= a.X ? 1 : -1; float shift = (66 - distance) * .5f;
            a.X = Clamp(a.X - shift * dir, 90, 1190); b.X = Clamp(b.X + shift * dir, 90, 1190);
            if (Math.Abs(a.X - b.X) < 66)
            {
                if (a.X <= 90 || a.X >= 1190) b.X = a.X + dir * 66; else a.X = b.X - dir * 66;
            }
        }

        private void WakeupInput(FighterState f, InputFrame input)
        {
            if (f.State != "knockdown" || f.Frame < 6 || f.WakeupUsed || !f.Grounded || f.Buffered == null || f.BufferFrames == 0) return;
            if (input.Block && input.Direction != 0 && f.Buffered.Block && f.Meter >= 20)
            {
                f.Meter -= 20; f.RollFrames = 12; f.RollDirection = input.Direction; f.Invuln = Math.Max(f.Invuln, 12);
                f.Stun = Math.Min(f.Stun, 12); f.WakeupUsed = true; Emit("wakeup_roll", f, "ROLAMENTO");
            }
            else if (f.Buffered.Up || f.Buffered.Attack) { f.Stun = Math.Min(f.Stun, 8); f.WakeupUsed = true; Emit("quick_rise", f, "RECUPERAÇÃO RÁPIDA"); }
        }

        private static void Animate(FighterState f)
        {
            switch (f.State)
            {
                case "walk": f.Animation = "walk"; break;
                case "evade": f.Animation = "dash"; break;
                case "crouch": f.Animation = "crouch"; break;
                case "crouch_block": f.Animation = "lowguard"; break;
                case "block": case "blockstun": f.Animation = "guard"; break;
                case "hitstun": case "guardbreak": case "super_victim": f.Animation = "hurt"; break;
                case "knockdown": case "ko": f.Animation = "down"; break;
                case "wakeup": f.Animation = "recover"; break;
                case "jump": f.Animation = f.VelocityY < 0 ? "jump" : "fall"; break;
                case "victory": f.Animation = "victory"; break;
                case "attack": case "throw":
                    var m = f.Move; if (m == null) { f.Animation = "idle"; break; }
                    if (m.Kind == "rush_red") { f.Animation = f.Frame >= m.Startup && f.Frame < m.Startup + m.Active ? "dash" : "guard"; break; }
                    string pose = m.BodyPose;
                    switch (pose)
                    {
                        case "punch": pose = "light"; break;
                        case "projectile": pose = "specialA"; break;
                        case "spin": pose = "specialB"; break;
                        case "grab": case "throw": pose = "grab"; break;
                        case "crouch": case "sweep": case "slide": pose = "lowkick"; break;
                        case "run": pose = "dash"; break;
                        case "charge": pose = "intro"; break;
                        case "block": pose = "guard"; break;
                    }
                    f.Animation = pose; break;
                default: f.Animation = "idle"; break;
            }
        }

        private void CheckRoundEnd()
        {
            if (Training.Enabled || Cinematic.Active) return;
            if (TeamMode) for (int i = 0; i < 2; i++) if (Fighters[i].Hp <= 0 && Reserve(i).Hp > 0) TryTag(i, true);
            if (TeamHealth(0) > 0 && TeamHealth(1) > 0 && TimerFrames > 0) return;
            float ah = TeamHealth(0) / TeamMaxHealth(0), bh = TeamHealth(1) / TeamMaxHealth(1);
            Winner = Math.Abs(ah - bh) < .00001f ? 2 : ah > bh ? 0 : 1;
            Emit("ko", Fighters[Winner < 2 ? Winner : 0], TimerFrames == 0 ? "TEMPO ESGOTADO" : "K.O.");
            for (int i = 0; i < 2; i++)
            {
                Fighters[i].State = i == Winner ? "victory" : "ko"; Fighters[i].Move = null;
                Fighters[i].Frame = 0; Fighters[i].UltimateTint = false; Animate(Fighters[i]);
            }
            Projectiles.Clear();
        }

        private void UpdateBear(FighterState f, FighterState other)
        {
            f.BearStun = Dec(f.BearStun); if (f.BearStun == 0) f.BearPosture = Math.Min(100, f.BearPosture + .19f);
            float offset = f.State == "attack" && f.Move != null && f.BearStun == 0 ? (f.Move.Visual == "bear_return" ? 50 : 165) : 85;
            f.BearX = Clamp(Toward(f.BearX, f.X + f.Face * offset, f.Stun > 0 ? 8 : 6), 110, 1170);
            if (f.Hp <= 0 || f.Stun > 0) { f.BearX = Toward(f.BearX, f.X + f.Face * 70, 8); return; }
            if (other.State == "attack" && other.Move != null && other.Move.Damage > 0 && Active(other) && f.LastBearHit != other.Attacks && Hitbox(other).Intersects(new HitRect(f.BearX - 43, Ground - 167, 86, 157)))
            {
                f.LastBearHit = other.Attacks; BearDamage(f, other.Move.Damage);
            }
        }

        private void BearDamage(FighterState f, float damage)
        {
            f.BearPosture = Math.Max(0, f.BearPosture - damage * .55f);
            if (f.BearPosture > 0) return;
            f.BearStun = 100; f.BearPosture = 35;
            if (f.State == "attack") { f.State = "idle"; f.Move = null; f.BufferFrames = 0; }
            Emit("buff", f, "URSO DESORGANIZADO");
        }
    }
}
