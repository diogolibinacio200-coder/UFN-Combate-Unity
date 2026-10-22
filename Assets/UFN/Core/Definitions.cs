using System;
using System.Collections.Generic;

namespace UFN.Core
{
    [Serializable]
    public sealed class GameCatalog
    {
        public CharacterDefinition[] Characters = Array.Empty<CharacterDefinition>();
        public MoveDefinition[] Normals = Array.Empty<MoveDefinition>();
        public MoveDefinition[] Chains = Array.Empty<MoveDefinition>();
        public CharacterDefinition Character(int id)
        {
            foreach (var c in Characters) if (c.Id == id) return c;
            throw new ArgumentOutOfRangeException(nameof(id), "Character missing from catalog");
        }
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public int Id;
        public string Key = "";
        public string Name = "";
        public string Title = "";
        public string ColorHex = "ffffff";
        public string UltimateName = "";
        public string Quote = "";
        public string Ending = "";
        public float WalkSpeed = 4.2f;
        public MoveDefinition[] Specials = Array.Empty<MoveDefinition>();
        public MoveDefinition Ultimate = new MoveDefinition();
        public MoveDefinition Offensive = new MoveDefinition();
        public MoveDefinition Defense = new MoveDefinition();
    }

    [Serializable]
    public sealed class MoveDefinition
    {
        public string Name = "";
        public string Kind = "strike";
        public string Level = "mid";
        public string Family = "special";
        public string Visual = "impact";
        public string BodyPose = "punch";
        public float Damage = 60;
        public float Range = 135;
        public float Height = 80;
        public float OffsetY = -116;
        public float GuardDamage = 18;
        public float Cost = 20;
        public float Push = 5;
        public float Launch;
        public float Speed;
        public float Chip = .06f;
        public float Pull;
        public float Gravity;
        public float Vy;
        public int Startup = 10;
        public int Active = 4;
        public int Recovery = 22;
        public int Hitstun = 23;
        public int Blockstun = 13;
        public int Duration = 90;
        public int Invuln;
        public int Freeze;
        public int Bounces;
        public int PulseCount = 1;
        public int PulseInterval = 24;
        public int Root;
        public int SetupTime;
        public int Slow;
        public int ChainStage;
        public bool Cancel;
        public bool Chargeable;
        public bool Reflect;
        public MoveDefinition Clone() => (MoveDefinition)MemberwiseClone();
        public int EndFrame => Startup + Active + Recovery;
    }

    public struct HitRect
    {
        public float X, Y, Width, Height;
        public HitRect(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        public bool Intersects(HitRect b) => Width > 0 && Height > 0 && b.Width > 0 && b.Height > 0 && X < b.X + b.Width && X + Width > b.X && Y < b.Y + b.Height && Y + Height > b.Y;
    }

    [Serializable]
    public sealed class FighterState
    {
        public int Id, Player, Face = 1, Frame, Stun, Invuln, HitFlash, TransformFrames, Combo, ComboTimer, MaxCombo, Skin;
        public int Ammo = 6, OverclockFrames, PreparedFrames, FeintFrames, StatusFrames, DodgeFrames, DodgeCooldown;
        public int BearStun, Embalo, EmbaloFrames;
        public float X, Y = 590, VelocityX, VelocityY, Hp = 1000, MaxHp = 1000, Guard = 100, Meter, Rage;
        public float ComboDamage, DamageDealt, DamageReceived, BearX, BearPosture = 100;
        public float DamageMultiplier = 1f;
        public string State = "idle", Animation = "idle", Status = "";
        public bool UltimateTint;
        public MoveDefinition Move;
        internal int BufferFrames, GuardAge = 999, ParryAge = 999, GuardDelay, CancelCount, Juggle, Attacks, ThrowFrames, ThrowTarget = -1, DashFrames, DashDirection, RollFrames, RollDirection, ControlImmunity, LastBearHit = -1;
        internal bool HitConfirm, HitTarget, WakeupUsed;
        internal InputFrame LastInput;
        internal InputCommand Buffered;
        internal List<MotionInput> Motions = new List<MotionInput>();
        public bool Grounded => Y >= 589.9f;
        public bool ArmorActive => TransformFrames > 0;
        public HitRect Hurtbox => new HitRect(X - 29, Y - ((State == "crouch" || State == "crouch_block") ? 128 : 212), 58, ((State == "crouch" || State == "crouch_block") ? 128 : 212) - 7);
        public FighterState Clone()
        {
            var f = (FighterState)MemberwiseClone(); f.Move = Move?.Clone(); f.Buffered = Buffered?.Clone(); f.Motions = new List<MotionInput>(Motions); return f;
        }
    }

    [Serializable]
    public sealed class ProjectileState
    {
        public int Id, Owner, Face, Age, Life, Lane, AttackSerial, Hits, NextHit, Bounces, Hp = 1;
        public string Kind = "projectile", Visual = "";
        public float X, Y, VelocityX, VelocityY, Radius;
        public bool Dead;
        public MoveDefinition Move;
        public ProjectileState Clone() { var p = (ProjectileState)MemberwiseClone(); p.Move = Move.Clone(); return p; }
    }

    public sealed class CombatEvent
    {
        public string Type, Text = "", Phase = "";
        public int Player, Target = -1, Combo, Pulse;
        public float X, Y, Damage;
        public bool Final;
        public HitRect Hitbox;
    }

    [Serializable]
    public sealed class CinematicState
    {
        public bool Active, Captured;
        public int Frame, Duration, Attacker, Defender, Character, ImpactPause, Pulse, ArmorDuration;
        public string Phase = "", Name = "";
        public float DamageTotal, DamageApplied, OriginAttackerX, OriginDefenderX;
        public CinematicState Clone() => (CinematicState)MemberwiseClone();
    }

    [Serializable]
    public sealed class TrainingSettings
    {
        public bool Enabled, InfiniteHealth = true, InfiniteMeter = true;
        public TrainingSettings Clone() => (TrainingSettings)MemberwiseClone();
    }
}
