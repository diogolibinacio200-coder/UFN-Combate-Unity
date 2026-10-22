using System;

namespace UFN.Core
{
    public sealed partial class CombatWorld
    {
        public bool TeamMode { get; private set; }
        public FighterState[][] Teams { get; private set; }
        public int[] TeamActive { get; private set; } = new int[2];
        public int[] TagCooldowns { get; private set; } = new int[2];
        private readonly int[] selectedReserves = new int[2];

        /// <summary>Enable the original two-fighter teams and reset this round to 150 seconds.</summary>
        public void SetupTeams(int p1ReserveId, int p2ReserveId)
        {
            Catalog.Character(p1ReserveId); Catalog.Character(p2ReserveId);
            selectedReserves[0] = p1ReserveId; selectedReserves[1] = p2ReserveId; TeamMode = true; ResetRound();
        }

        public void ConfigureTeams(int p1LeadId, int p1ReserveId, int p2LeadId, int p2ReserveId)
        {
            Catalog.Character(p1LeadId); Catalog.Character(p2LeadId); Catalog.Character(p1ReserveId); Catalog.Character(p2ReserveId);
            selectedIds[0] = p1LeadId; selectedIds[1] = p2LeadId; SetupTeams(p1ReserveId, p2ReserveId);
        }

        public FighterState Reserve(int player)
        {
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            return Teams[player][1 - TeamActive[player]];
        }

        public float TeamHealth(int player) => TeamMode ? Teams[player][0].Hp + Teams[player][1].Hp : Fighters[player].Hp;
        public float TeamMaxHealth(int player) => TeamMode ? Teams[player][0].MaxHp + Teams[player][1].MaxHp : Fighters[player].MaxHp;

        public bool TryTag(int player, bool forced = false)
        {
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            if (!TeamMode || Cinematic.Active || Winner >= 0) return false;
            var old = Fighters[player]; var next = Reserve(player);
            if (next.Hp <= 0 || (!forced && (TagCooldowns[player] > 0 || !IsFree(old) || !old.Grounded))) return false;
            next.X = old.X; next.Y = Ground; next.Face = old.Face; next.BearX = Clamp(next.X + old.Face * 90, 110, 1170); next.BearStun = 0;
            next.State = "idle"; next.Animation = "idle"; next.Move = null; next.Stun = 0; next.Frame = 0;
            next.VelocityX = next.VelocityY = 0; next.BufferFrames = 0; next.Buffered = null; next.Invuln = forced ? 40 : 18;
            next.UltimateTint = false; next.HitFlash = 0; next.LastInput = old.LastInput; next.ThrowTarget = -1; next.RollFrames = 0;
            old.BufferFrames = 0; old.Buffered = null; old.Motions.Clear(); old.UltimateTint = false;
            Projectiles.RemoveAll(p => p.Owner == player);
            TeamActive[player] = 1 - TeamActive[player]; Fighters[player] = next; TagCooldowns[player] = 300;
            Emit("tag", next, "TROCA · " + Catalog.Character(next.Id).Name); return true;
        }
    }
}
