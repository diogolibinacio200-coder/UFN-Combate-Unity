using System;
using System.Collections.Generic;

namespace UFN.Core
{
    [Serializable]
    public struct KartPoint
    {
        public float X, Y;
        public KartPoint(float x, float y) { X = x; Y = y; }
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public KartPoint Normalized => Length < .0001f ? new KartPoint(1, 0) : this / Length;
        public static KartPoint operator +(KartPoint a, KartPoint b) => new KartPoint(a.X + b.X, a.Y + b.Y);
        public static KartPoint operator -(KartPoint a, KartPoint b) => new KartPoint(a.X - b.X, a.Y - b.Y);
        public static KartPoint operator *(KartPoint a, float n) => new KartPoint(a.X * n, a.Y * n);
        public static KartPoint operator /(KartPoint a, float n) => new KartPoint(a.X / n, a.Y / n);
        public static float Dot(KartPoint a, KartPoint b) => a.X * b.X + a.Y * b.Y;
        public static float Distance(KartPoint a, KartPoint b) => (a - b).Length;
    }

    public struct KartTrackSample
    {
        public KartPoint Position, Tangent;
        public float Distance;
    }

    public sealed class KartTrack
    {
        public KartPoint[] Points { get; }
        public float Width => 15.4f;
        public int Id { get; }
        public string Name => KartContent.TrackNames[Id];
        public float Length { get; }
        public const int CheckpointCount = 20;
        private readonly float[] distances;

        public KartTrack(int id = 0)
        {
            if(id<0||id>=KartContent.TrackNames.Length)throw new ArgumentOutOfRangeException(nameof(id));
            Id=id;
            var anchors = KartContent.TrackAnchors(id);
            Points = new KartPoint[anchors.Length * 12];
            for (int i = 0; i < Points.Length; i++)
            {
                int n = i / 12;
                float t = (i % 12) / 12f;
                KartPoint a = anchors[(n + anchors.Length - 1) % anchors.Length], b = anchors[n], c = anchors[(n + 1) % anchors.Length], d = anchors[(n + 2) % anchors.Length];
                Points[i] = (b * 2 + (c - a) * t + (a * 2 - b * 5 + c * 4 - d) * (t * t) + (b * 3 - a - c * 3 + d) * (t * t * t)) * .5f;
            }
            distances = new float[Points.Length + 1];
            for (int i = 0; i < Points.Length; i++) distances[i + 1] = distances[i] + KartPoint.Distance(Points[i], Points[(i + 1) % Points.Length]);
            Length = distances[Points.Length];
        }

        public KartTrackSample Sample(float distance)
        {
            distance = Wrap(distance, Length);
            int segment = Array.BinarySearch(distances, distance);
            if (segment < 0) segment = Math.Max(0, ~segment - 1);
            segment = Math.Min(segment, Points.Length - 1);
            KartPoint start = Points[segment], delta = Points[(segment + 1) % Points.Length] - start;
            float t = (distance - distances[segment]) / (distances[segment + 1] - distances[segment]);
            return new KartTrackSample { Position = start + delta * t, Tangent = delta.Normalized, Distance = distance };
        }

        public KartTrackSample Project(KartPoint position)
        {
            float best = float.MaxValue;
            KartTrackSample result = default;
            for (int i = 0; i < Points.Length; i++)
            {
                KartPoint a = Points[i], line = Points[(i + 1) % Points.Length] - a;
                float t = Clamp(KartPoint.Dot(position - a, line) / KartPoint.Dot(line, line), 0, 1);
                KartPoint closest = a + line * t;
                float squared = KartPoint.Dot(position - closest, position - closest);
                if (squared < best) { best = squared; result = new KartTrackSample { Position = closest, Tangent = line.Normalized, Distance = distances[i] + line.Length * t }; }
            }
            return result;
        }
        internal static float Wrap(float value, float length) => (value % length + length) % length;
        internal static float Clamp(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
    }

    public enum KartItem { None, Turbo, Shield, Pulse, Oil, Shock, Capybara, Signature }
    public enum KartRaceMode { Quick, Championship, TimeAttack }

    public struct KartInput
    {
        public float Throttle;
        public float Brake;
        public float Steering;
        public bool Drift;
        public bool UseItem;
        public bool LaunchBack;
    }

    public sealed class KartRacer
    {
        public int Index { get; internal set; }
        public int CharacterId { get; internal set; }
        public int Skin { get; set; }
        public KartPoint Position { get; internal set; }
        public KartPoint Velocity { get; internal set; }
        public float Heading { get; internal set; }
        public float Speed => Velocity.Length;
        public int Lap { get; internal set; }
        public int Place { get; internal set; }
        public int NextCheckpoint { get; internal set; } = 1;
        public int CheckpointsPassed { get; internal set; }
        public float DriftCharge { get; internal set; }
        public bool Drifting { get; internal set; }
        public int BoostTicks { get; internal set; }
        public int ShieldTicks { get; internal set; }
        public int StunTicks { get; internal set; }
        public KartItem Item { get; internal set; }
        public bool Finished { get; internal set; }
        public int FinishTick { get; internal set; }
        public float TrackDistance { get; internal set; }
        public float TotalDistance { get; internal set; }
        public float OffroadDistance { get; internal set; }
        public int ItemsUsed { get; internal set; }
        public float Steering { get; internal set; }
        public float ForwardSpeed { get; internal set; }
        public int InvincibleTicks { get; internal set; }
        public int SignatureTicks { get; internal set; }
        public int JumpTicks { get; internal set; }
        public int RecoveryTicks { get; internal set; }
        public bool WrongWay { get; internal set; }
        public int[] LapTicks { get; } = new int[3];
        public int BestLapMilliseconds { get { int best=int.MaxValue;foreach(int ticks in LapTicks)if(ticks>0)best=Math.Min(best,ticks);return best==int.MaxValue?0:(int)Math.Round(best*1000.0/60); } }
        internal int LastLapStartTick;
        internal int ItemCooldown;
        internal int CollisionCooldown;
        internal bool ItemPressed;
        internal float LastProjection;
        internal int NextBox = 1;
    }

    public sealed class KartHazard
    {
        public KartPoint Position;
        public int Owner;
        public int Ticks;
        public string Kind = "oil";
        public float Radius = 2.4f;
        public int GraceTicks = 72;
    }

    /// <summary>60 Hz simulation shared by human and AI drivers. No scripted position snaps.</summary>
    public sealed partial class KartRace
    {
        public const float DeltaTime = 1f / 60;
        public const int LapsToWin = 3;
        public KartTrack Track { get; }
        public KartRacer[] Racers { get; }
        public KartRaceMode Mode { get; }
        public int HumanCount { get; }
        public int Difficulty { get; }
        public int[] ChampionshipPoints { get; private set; } = new int[8];
        public bool ChampionshipComplete => Mode==KartRaceMode.Championship && Track.Id>=3 && Completed;
        public bool Completed { get { for(int i=0;i<HumanCount;i++)if(!Racers[i].Finished)return false;return true; } }
        public IReadOnlyList<KartHazard> Hazards => hazards;
        public int ElapsedTicks { get; private set; }
        public bool Finished => Racers[0].Finished;
        public int PlayerPlace => Racers[0].Place;
        public int CountdownTicks => Math.Max(0, 240 - ElapsedTicks);
        public float TimeSeconds => Math.Max(0, ElapsedTicks - 240) / 60f;
        private readonly List<KartHazard> hazards = new List<KartHazard>();
        private uint random;
        private int finishers;
        private readonly int initialSeed;
        private bool pointsAwarded;

        public KartRace(uint seed,int playerCharacterId,int trackId=0,KartRaceMode mode=KartRaceMode.Quick,int secondCharacterId=-1,int playerSkin=0,int secondSkin=0,int difficulty=1)
            :this(unchecked((int)seed),playerCharacterId,trackId,mode,secondCharacterId,playerSkin,secondSkin,difficulty){}
        public KartRace(int seed, int playerCharacterId,int trackId=0,KartRaceMode mode=KartRaceMode.Quick,int secondCharacterId=-1,int playerSkin=0,int secondSkin=0,int difficulty=1)
        {
            if (playerCharacterId < 0 || playerCharacterId > 13) throw new ArgumentOutOfRangeException(nameof(playerCharacterId));
            if(secondCharacterId>13||secondCharacterId< -1)throw new ArgumentOutOfRangeException(nameof(secondCharacterId));
            Track=new KartTrack(trackId);Mode=mode;HumanCount=secondCharacterId>=0&&mode!=KartRaceMode.TimeAttack?2:1;
            Difficulty=Math.Max(0,Math.Min(2,difficulty));initialSeed=seed;
            Racers=new KartRacer[mode==KartRaceMode.TimeAttack?1:8];
            random = unchecked((uint)seed) ^ 0x85ebca6bu;
            if (random == 0) random = 1;
            var used = new HashSet<int> { playerCharacterId };
            if(HumanCount==2)used.Add(secondCharacterId);
            for (int i = 0; i < Racers.Length; i++)
            {
                int character = playerCharacterId;
                if(i==1&&HumanCount==2)character=secondCharacterId;
                else if (i >= HumanCount) { do { character = NextInt(14); } while (!used.Add(character)); }
                int gridSlot=Racers.Length==1?0:i<HumanCount?8-HumanCount+i:i-HumanCount;
                float gridDistance = -10-gridSlot/2*5;
                KartTrackSample sample = Track.Sample(gridDistance);
                var normal = new KartPoint(-sample.Tangent.Y, sample.Tangent.X);
                Racers[i] = new KartRacer { Index = i, CharacterId = character,Skin=i==0?playerSkin:i==1&&HumanCount==2?secondSkin:0, Position = sample.Position + normal * (gridSlot % 2 == 0 ? -2.2f : 2.2f), Heading = (float)Math.Atan2(sample.Tangent.Y, sample.Tangent.X), TrackDistance = sample.Distance, LastProjection = sample.Distance, TotalDistance = gridDistance, Place = gridSlot + 1 };
            }
        }

        public void Step(KartInput playerInput,KartInput secondInput=default)
        {
            // The original mode ends when every human finishes. Keep the classification
            // fixed while the result screen is open, including championship points.
            if (Completed) { Events.Clear(); return; }
            ElapsedTicks++;
            Events.Clear();
            if (ElapsedTicks <= 240) return;
            for (int i = 0; i < Racers.Length; i++) StepRacer(Racers[i], i<HumanCount&&!Racers[i].Finished?(i==0?playerInput:secondInput):GetAIInput(i));
            ResolveCollisions();
            UpdateProjectiles();
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                KartHazard hazard = hazards[i];
                hazard.Ticks--;
                if(hazard.GraceTicks>0)hazard.GraceTicks--;
                if (hazard.Ticks <= 0) { hazards.RemoveAt(i); continue; }
                foreach (KartRacer racer in Racers)
                    if (!(racer.Index==hazard.Owner&&hazard.GraceTicks>0) && racer.CollisionCooldown == 0 && KartPoint.Distance(racer.Position, hazard.Position) < hazard.Radius)
                    { Hit(racer, 38); racer.CollisionCooldown = 55; hazard.Ticks = 0; break; }
            }
            UpdatePlaces();
            if(Completed&&Mode==KartRaceMode.Championship&&!pointsAwarded){pointsAwarded=true;int[] points={15,12,10,8,6,4,2,1};foreach(var racer in Racers)ChampionshipPoints[racer.Index]+=points[Math.Min(7,racer.Place-1)];}
        }

        // Public to support demonstrations/accessibility and deterministic AI driving tests.
        public KartInput GetAIInput(int index)
        {
            if (index < 0 || index >= Racers.Length) throw new ArgumentOutOfRangeException(nameof(index));
            KartRacer racer = Racers[index];
            KartTrackSample nearest = Track.Project(racer.Position);
            float lookahead = 7 + racer.Speed * .48f;
            KartTrackSample target = Track.Sample(nearest.Distance + lookahead);
            float desiredLane = (index % 3 - 1) * 1.7f;
            var normal = new KartPoint(-target.Tangent.Y, target.Tangent.X);
            KartPoint direction = target.Position + normal * desiredLane - racer.Position;
            float angle = Angle((float)Math.Atan2(direction.Y, direction.X) - racer.Heading);
            float targetSpeed = 27f+Difficulty*1.2f - Math.Min(13, Math.Abs(angle) * 12f) + (index % 3) * .4f;
            // Drivers use steering/braking and the same world physics as the player.
            return new KartInput { Throttle = racer.Speed < targetSpeed ? 1 : .2f, Brake = racer.Speed > targetSpeed + 3 ? .3f : 0, Steering = KartTrack.Clamp(-angle * 2.0f, -1, 1), Drift = Math.Abs(angle) > .35f && Math.Abs(angle) < .8f && racer.Speed > 18, UseItem = racer.Item != KartItem.None && (ElapsedTicks + index * 29) % 91 == 0 };
        }

        private void StepRacer(KartRacer racer, KartInput input)
        {
            if (float.IsNaN(input.Throttle) || float.IsInfinity(input.Throttle)) input.Throttle = 0;
            if (float.IsNaN(input.Steering) || float.IsInfinity(input.Steering)) input.Steering = 0;
            if (float.IsNaN(input.Brake) || float.IsInfinity(input.Brake)) input.Brake = 0;
            float throttle = KartTrack.Clamp(input.Throttle, -1, 1), steer = KartTrack.Clamp(input.Steering, -1, 1);
            float brake = KartTrack.Clamp(input.Brake, 0, 1);
            if (racer.BoostTicks > 0) racer.BoostTicks--;
            if (racer.ShieldTicks > 0) racer.ShieldTicks--;
            bool stunned = racer.StunTicks > 0;
            if (stunned) { racer.StunTicks--; throttle = 0; steer = 0; }
            if (racer.ItemCooldown > 0) racer.ItemCooldown--;
            if (racer.CollisionCooldown > 0) racer.CollisionCooldown--;
            if(racer.InvincibleTicks>0)racer.InvincibleTicks--;
            if(racer.SignatureTicks>0)racer.SignatureTicks--;
            if(racer.JumpTicks>0)racer.JumpTicks--;
            if(racer.RecoveryTicks>0)racer.RecoveryTicks--;
            if (input.UseItem && !racer.ItemPressed) ActivateItem(racer,input.LaunchBack);
            racer.ItemPressed = input.UseItem;
            KartTrackSample projection = Track.Project(racer.Position);
            racer.OffroadDistance = KartPoint.Distance(racer.Position, projection.Position);
            bool offroad = racer.OffroadDistance > Track.Width * .55f;
            bool drifting = !stunned && input.Drift && racer.Speed > 12 && Math.Abs(steer) > .15f && !offroad;
            if (drifting) racer.DriftCharge = Math.Min(100, racer.DriftCharge + Math.Abs(steer) * .8f);
            else if (racer.Drifting)
            {
                if (racer.DriftCharge >= 25) racer.BoostTicks = Math.Max(racer.BoostTicks, 25 + (int)(racer.DriftCharge * .8f));
                racer.DriftCharge = 0;
            }
            racer.Drifting = drifting;
            racer.Steering=steer;
            float speed = racer.Speed;
            var forward = new KartPoint((float)Math.Cos(racer.Heading), (float)Math.Sin(racer.Heading));
            float signedSpeed = racer.ForwardSpeed;
            float maxSpeed = stunned ? 3 : offroad ? 31.8f*.42f : racer.BoostTicks > 0 ? 31.8f*1.42f : 31.8f;
            float acceleration = throttle * (racer.BoostTicks > 0 ? 24 : 15) - signedSpeed * .40f;
            if (brake > 0) acceleration -= Math.Sign(signedSpeed) * brake * 28;
            signedSpeed = KartTrack.Clamp(signedSpeed + acceleration * DeltaTime, -8, maxSpeed);
            racer.ForwardSpeed=signedSpeed;
            if (speed > maxSpeed) signedSpeed = Math.Min(signedSpeed, speed - 35 * DeltaTime);
            float turning = (drifting ? 2.05f : 1.65f) * Math.Min(1, Math.Abs(signedSpeed) / 7);
            racer.Heading = Angle(racer.Heading - steer * turning * Math.Sign(signedSpeed) * DeltaTime);
            forward = new KartPoint((float)Math.Cos(racer.Heading), (float)Math.Sin(racer.Heading));
            float grip = (drifting ? 3.4f : offroad ? 6 : 10) * DeltaTime;
            KartPoint lateral = racer.Velocity - forward * KartPoint.Dot(racer.Velocity, forward);
            racer.Velocity = forward * signedSpeed + lateral * (1 - grip);
            racer.Position += racer.Velocity * DeltaTime;
            racer.WrongWay=KartPoint.Dot(forward,projection.Tangent)<-.3f&&speed>3;
            if(Track.Id==3&&racer.JumpTicks==0&&speed>15&&KartTrack.Wrap(projection.Distance,Track.Length/3)<2.5f)racer.JumpTicks=60;
            ResolveTrackContact(racer);
            UpdateProgress(racer);
        }

        private void UpdateProgress(KartRacer racer)
        {
            if (racer.Finished) return;
            KartTrackSample current = Track.Project(racer.Position);
            float delta = current.Distance - racer.LastProjection;
            if (delta < -Track.Length / 2) delta += Track.Length;
            if (delta > Track.Length / 2) delta -= Track.Length;
            float laneDistance = KartPoint.Distance(current.Position, racer.Position);
            bool onCourse = laneDistance < Track.Width * .85f && Math.Abs(delta) < 4;
            if (onCourse)
            {
                racer.TotalDistance += delta;
                float checkpoint = racer.NextCheckpoint * Track.Length / KartTrack.CheckpointCount;
                if (Crossed(racer.LastProjection, delta, checkpoint))
                {
                    racer.CheckpointsPassed++;
                    if (racer.NextCheckpoint == 0 && !racer.Finished)
                    {
                        racer.Lap++;
                        racer.LapTicks[racer.Lap-1]=ElapsedTicks-240-racer.LastLapStartTick;racer.LastLapStartTick=ElapsedTicks-240;
                        Events.Add(new KartEvent{Kind="lap",Player=racer.Index,Position=racer.Position});
                        if (racer.Lap >= LapsToWin) { racer.Finished = true; racer.FinishTick = ElapsedTicks - 240; racer.Place = ++finishers; }
                    }
                    racer.NextCheckpoint = (racer.NextCheckpoint + 1) % KartTrack.CheckpointCount;
                }
                // Four box groups per lap. Item availability depends on actual ordered passage.
                float boxDistance = racer.NextBox * Track.Length / 4;
                if (Crossed(racer.LastProjection, delta, boxDistance))
                {
                    if (Mode!=KartRaceMode.TimeAttack&&racer.Item == KartItem.None && racer.ItemCooldown == 0) { racer.Item = RollItem(racer.Place); racer.ItemCooldown = 120; }
                    racer.NextBox = (racer.NextBox + 1) % 4;
                }
            }
            racer.TrackDistance = current.Distance;
            racer.LastProjection = current.Distance;
        }

        private bool Crossed(float before, float delta, float target)
        {
            if (delta <= 0) return false;
            float gap = KartTrack.Wrap(target - before, Track.Length);
            return gap > .00001f && gap <= delta + .0001f;
        }

        /// <summary>Explicit rescue to the last validated checkpoint, with a time penalty.
        /// Does not award checkpoints, laps, items, or a new finishing time.</summary>
        public bool Recover(int player)
        {
            if(player<0||player>=Racers.Length)throw new ArgumentOutOfRangeException(nameof(player));
            KartRacer racer=Racers[player];
            if(CountdownTicks>0||Completed||racer.Finished||racer.RecoveryTicks>0)return false;
            float distance=KartTrack.Wrap((racer.NextCheckpoint-1)*Track.Length/KartTrack.CheckpointCount,Track.Length);
            KartTrackSample checkpoint=Track.Sample(distance+2);
            racer.Position=checkpoint.Position;
            racer.Heading=(float)Math.Atan2(checkpoint.Tangent.Y,checkpoint.Tangent.X);
            racer.Velocity=default;racer.ForwardSpeed=0;racer.Steering=0;
            racer.StunTicks=60;racer.RecoveryTicks=180;racer.DriftCharge=0;racer.Drifting=false;
            racer.BoostTicks=0;racer.JumpTicks=0;racer.WrongWay=false;
            racer.TrackDistance=checkpoint.Distance;racer.LastProjection=checkpoint.Distance;
            racer.TotalDistance=racer.CheckpointsPassed*Track.Length/KartTrack.CheckpointCount+2;
            Events.Add(new KartEvent{Kind="recover",Player=player,Position=racer.Position});
            return true;
        }

        private void ActivateItem(KartRacer racer,bool backwards=false)
        {
            if (racer.Item == KartItem.None || racer.Finished) return;
            KartPoint direction=new KartPoint((float)Math.Cos(racer.Heading),(float)Math.Sin(racer.Heading))*(backwards?-1:1);
            switch (racer.Item)
            {
                case KartItem.Signature: Signature(racer,direction);break;
                case KartItem.Turbo: racer.BoostTicks = Math.Max(racer.BoostTicks, 180); break;
                case KartItem.Shield: racer.ShieldTicks = 420; break;
                case KartItem.Capybara: AddHazard(racer,racer.Position+direction*5,"capybara",960,2);break;
                case KartItem.Oil:
                    hazards.Add(new KartHazard { Owner = racer.Index, Position = racer.Position - direction*2.6f, Ticks = 960 });
                    if (hazards.Count > 24) hazards.RemoveAt(0);
                    break;
                case KartItem.Pulse:
                    Launch(racer,direction,"pulse",49,300,false,false,0,69);break;
                case KartItem.Shock:
                    foreach (KartRacer other in Racers) if (other != racer && KartPoint.Distance(other.Position, racer.Position) < 22) Hit(other, 93);
                    break;
            }
            Events.Add(new KartEvent{Kind="item",Player=racer.Index,Position=racer.Position,Visual=ItemName(racer)});
            racer.Item = KartItem.None;
            racer.ItemsUsed++;
        }

        private static void Hit(KartRacer racer, int ticks)
        {
            if(racer.InvincibleTicks>0||racer.Finished)return;
            racer.InvincibleTicks=120;
            if (racer.ShieldTicks > 0) { racer.ShieldTicks = 0; return; }
            racer.StunTicks = Math.Max(racer.StunTicks, ticks);
            racer.Velocity *= .35f;racer.ForwardSpeed*=.30f;
        }

        private void ResolveCollisions()
        {
            for (int a = 0; a < Racers.Length; a++) for (int b = a + 1; b < Racers.Length; b++)
            {
                KartPoint delta = Racers[b].Position - Racers[a].Position;
                float distance = delta.Length;
                if (distance >= 2 || distance < .0001f) continue;
                KartPoint normal = delta / distance;
                // Bounded contact separation is a physical collision correction, never path snapping.
                KartPoint correction = normal * Math.Min(.15f, (2 - distance) * .5f);
                Racers[a].Position -= correction; Racers[b].Position += correction;
                float approach = KartPoint.Dot(Racers[a].Velocity - Racers[b].Velocity, normal);
                if (approach > 0) { Racers[a].Velocity -= normal * (approach * .4f); Racers[b].Velocity += normal * (approach * .4f); }
            }
        }
        private void UpdatePlaces()
        {
            foreach (KartRacer racer in Racers)
            {
                if (racer.Finished) continue;
                int rank = finishers + 1;
                float score = ProgressScore(racer);
                foreach (KartRacer other in Racers) if (!other.Finished && other != racer && (ProgressScore(other) > score || (ProgressScore(other) == score && other.Index < racer.Index))) rank++;
                racer.Place = rank;
            }
        }
        private float ProgressScore(KartRacer racer)
        {
            // Position is subordinate to validated checkpoints, so shortcuts cannot win a race.
            if (racer.CheckpointsPassed == 0 && racer.TotalDistance < 0) return racer.TotalDistance;
            float previous = KartTrack.Wrap((racer.NextCheckpoint - 1) * Track.Length / KartTrack.CheckpointCount, Track.Length);
            float progress = Math.Min(Track.Length / KartTrack.CheckpointCount, KartTrack.Wrap(racer.TrackDistance - previous, Track.Length));
            return racer.CheckpointsPassed * Track.Length / KartTrack.CheckpointCount + progress;
        }
        private int NextInt(int count) { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (int)(random % (uint)count); }
        private static float Angle(float value) { while (value > Math.PI) value -= (float)(Math.PI * 2); while (value < -Math.PI) value += (float)(Math.PI * 2); return value; }
    }
}
