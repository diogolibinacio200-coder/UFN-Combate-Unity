using System;

namespace UFN.Core
{
    [Serializable]
    public struct InputFrame
    {
        public bool Left, Right, Up, Down, Attack, Heavy, Kick, Block, Dodge, Power, Tag;
        public static InputFrame Empty => default;
        public int Direction => (Right ? 1 : 0) - (Left ? 1 : 0);
        public ushort Bits => (ushort)((Left ? 1 : 0) | (Right ? 2 : 0) | (Up ? 4 : 0) | (Down ? 8 : 0) | (Attack ? 16 : 0) | (Heavy ? 32 : 0) | (Kick ? 64 : 0) | (Block ? 128 : 0) | (Dodge ? 256 : 0) | (Power ? 512 : 0) | (Tag ? 1024 : 0));
        public static InputFrame FromBits(ushort b) => new InputFrame { Left = (b & 1) != 0, Right = (b & 2) != 0, Up = (b & 4) != 0, Down = (b & 8) != 0, Attack = (b & 16) != 0, Heavy = (b & 32) != 0, Kick = (b & 64) != 0, Block = (b & 128) != 0, Dodge = (b & 256) != 0, Power = (b & 512) != 0, Tag = (b & 1024) != 0 };
    }

    internal struct MotionInput
    {
        internal string Direction;
        internal int Tick;
        internal MotionInput(string direction, int tick) { Direction = direction; Tick = tick; }
    }

    internal sealed class InputCommand
    {
        internal InputFrame Held;
        internal bool Attack, Heavy, Kick, Up, Block, Dodge, Power, Tag;
        internal int Face, SpecialSlot = -1;
        internal InputCommand Clone() => (InputCommand)MemberwiseClone();
    }
}
