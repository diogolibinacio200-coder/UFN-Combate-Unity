using System;
using System.Collections.Generic;
namespace UFN.App
{
    [Serializable] public sealed class RecordedFrame { public int P1,P2; public string Hash=""; }
    [Serializable] public sealed class RecordedMatch
    {
        public int Version=1,Player1,Player2,Stage,Skin;
        public uint Seed;
        public int Difficulty=1;
        public bool Teams;
        public int SecondSkin,Reserve1,Reserve2,ReserveSkin1,ReserveSkin2;
        public float StartingHp=1000;
        public float HealthMultiplier=1,DamageMultiplier=1,StartingMeter;
        public string CatalogHash="";
        public List<RecordedFrame> Frames=new List<RecordedFrame>();
    }
}
