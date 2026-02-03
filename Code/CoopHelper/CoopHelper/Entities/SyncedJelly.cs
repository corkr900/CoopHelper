using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Celeste.Mod.CoopHelper.Entities
{
    [CustomEntity("corkr900CoopHelper/SyncedJelly")]
    [Tracked(true)]
    internal class SyncedJelly : Actor, ISynchronizable
    {
        public static ParticleType P_Glide => Glider.P_Glide;
        public static ParticleType P_GlideUp => Glider.P_GlideUp;
        public static ParticleType P_Platform => Glider.P_Platform;
        public static ParticleType P_Glow => Glider.P_Glow;
        public static ParticleType P_Expand => Glider.P_Expand;

        public Vector2 Speed;
        public Holdable Hold;
        private Level level;
        private Collision onCollideH;
        private Collision onCollideV;
        private Vector2 prevLiftSpeed;
        private Vector2 startPos;
        private float noGravityTimer;
        private float highFrictionTimer;
        private bool bubble;
        private bool destroyed;
        private Sprite sprite;
        private Wiggler wiggler;
        private SineWave platformSine;
        private SoundSource fallingSfx;

        private EntityID id;
        private PlayerID? holderID;
        private TransitionListener transitionListener;
        private bool IsHeldByOtherPlayer => holderID != null && !holderID.Value.Equals(PlayerID.MyID);

        public SyncedJelly(Vector2 position, bool bubble, EntityID id) : base(position)
        {
            this.bubble = bubble;
            startPos = Position;
            Collider = new Hitbox(8f, 10f, -4f, -10f);
            onCollideH = OnCollideH;
            onCollideV = OnCollideV;
            Add(sprite = GFX.SpriteBank.Create("glider"));
            Add(wiggler = Wiggler.Create(0.25f, 4f));
            Depth = -5;
            Add(Hold = new Holdable(0.3f));
            Hold.PickupCollider = new Hitbox(20f, 22f, -10f, -16f);
            Hold.SlowFall = true;
            Hold.SlowRun = false;
            Hold.OnPickup = OnPickup;
            Hold.OnRelease = OnRelease;
            Hold.SpeedGetter = () => Speed;
            Hold.OnHitSpring = HitSpring;
            platformSine = new SineWave(0.3f, 0f);
            Add(platformSine);
            fallingSfx = new SoundSource();
            Add(fallingSfx);
            Add(new WindMover(WindMode));
            Hold.SpeedSetter = delegate (Vector2 speed)
            {
                Speed = speed;
            };
            this.id = id;
        }

        public SyncedJelly(EntityData e, Vector2 offset)
            : this(e.Position + offset, e.Bool("bubble"), new EntityID(e.Level.Name, e.ID))
        {
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            level = SceneAs<Level>();
            if (scene.Tracker.GetEntities<SyncedJelly>().FirstOrDefault(other
                => other != this && other is SyncedJelly j && j.id.Equals(id)) is SyncedJelly sj)
            {
                // If another jelly with the same ID exists, remove this one and make the other post an update
                // This would happen when this entity is carried out of the room and back in
                RemoveSelf();
                EntityStateTracker.PostUpdate(sj);
                return;
            }
            EntityStateTracker.AddListener(this, false);
        }

        public override void Update()
        {
            if (Scene.OnInterval(0.05f))
            {
                level.Particles.Emit(P_Glow, 1, base.Center + Vector2.UnitY * -9f, new Vector2(10f, 4f));
            }
            float target = ((!Hold.IsHeld) ? 0f : ((!Hold.Holder.OnGround()) ? Calc.ClampedMap(Hold.Holder.Speed.X, -300f, 300f, MathF.PI / 3f, -MathF.PI / 3f) : Calc.ClampedMap(Hold.Holder.Speed.X, -300f, 300f, 0.6981317f, -0.6981317f)));
            sprite.Rotation = Calc.Approach(sprite.Rotation, target, MathF.PI * Engine.DeltaTime);
            if (Hold.IsHeld && !Hold.Holder.OnGround() && (sprite.CurrentAnimationID == "fall" || sprite.CurrentAnimationID == "fallLoop"))
            {
                if (!fallingSfx.Playing)
                {
                    Audio.Play("event:/new_content/game/10_farewell/glider_engage", Position);
                    fallingSfx.Play("event:/new_content/game/10_farewell/glider_movement");
                }
                Vector2 speed = Hold.Holder.Speed;
                Vector2 vector = new Vector2(speed.X * 0.5f, (speed.Y < 0f) ? (speed.Y * 2f) : speed.Y);
                float value = Calc.Map(vector.Length(), 0f, 120f, 0f, 0.7f);
                fallingSfx.Param("glider_speed", value);
            }
            else
            {
                fallingSfx.Stop();
            }
            base.Update();
            if (!destroyed)
            {
                foreach (SeekerBarrier entity in Scene.Tracker.GetEntities<SeekerBarrier>())
                {
                    entity.Collidable = true;
                    bool num = CollideCheck(entity);
                    entity.Collidable = false;
                    if (num)
                    {
                        Destroy();
                        return;
                    }
                }
                if (Hold.IsHeld)
                {
                    prevLiftSpeed = Vector2.Zero;
                }
                else if (!bubble)
                {
                    if (highFrictionTimer > 0f)
                    {
                        highFrictionTimer -= Engine.DeltaTime;
                    }
                    if (OnGround())
                    {
                        float target2 = ((!OnGround(Position + Vector2.UnitX * 3f)) ? 20f : (OnGround(Position - Vector2.UnitX * 3f) ? 0f : (-20f)));
                        Speed.X = Calc.Approach(Speed.X, target2, 800f * Engine.DeltaTime);
                        Vector2 liftSpeed = base.LiftSpeed;
                        if (liftSpeed == Vector2.Zero && prevLiftSpeed != Vector2.Zero)
                        {
                            Speed = prevLiftSpeed;
                            prevLiftSpeed = Vector2.Zero;
                            Speed.Y = Math.Min(Speed.Y * 0.6f, 0f);
                            if (Speed.X != 0f && Speed.Y == 0f)
                            {
                                Speed.Y = -60f;
                            }
                            if (Speed.Y < 0f)
                            {
                                noGravityTimer = 0.15f;
                            }
                        }
                        else
                        {
                            prevLiftSpeed = liftSpeed;
                            if (liftSpeed.Y < 0f && Speed.Y < 0f)
                            {
                                Speed.Y = 0f;
                            }
                        }
                    }
                    else if (Hold.ShouldHaveGravity)
                    {
                        float num2 = 200f;
                        if (Speed.Y >= -30f)
                        {
                            num2 *= 0.5f;
                        }
                        float num3 = ((Speed.Y < 0f) ? 40f : ((!(highFrictionTimer <= 0f)) ? 10f : 40f));
                        Speed.X = Calc.Approach(Speed.X, 0f, num3 * Engine.DeltaTime);
                        if (noGravityTimer > 0f)
                        {
                            noGravityTimer -= Engine.DeltaTime;
                        }
                        else if (level.Wind.Y < 0f)
                        {
                            Speed.Y = Calc.Approach(Speed.Y, 0f, num2 * Engine.DeltaTime);
                        }
                        else
                        {
                            Speed.Y = Calc.Approach(Speed.Y, 30f, num2 * Engine.DeltaTime);
                        }
                    }
                    MoveH(Speed.X * Engine.DeltaTime, onCollideH);
                    MoveV(Speed.Y * Engine.DeltaTime, onCollideV);
                    if (Left < level.Bounds.Left)
                    {
                        Left = level.Bounds.Left;
                        OnCollideH(new CollisionData
                        {
                            Direction = -Vector2.UnitX
                        });
                    }
                    else if (Right > level.Bounds.Right)
                    {
                        Right = level.Bounds.Right;
                        OnCollideH(new CollisionData
                        {
                            Direction = Vector2.UnitX
                        });
                    }
                    if (Top < level.Bounds.Top)
                    {
                        Top = level.Bounds.Top;
                        OnCollideV(new CollisionData
                        {
                            Direction = -Vector2.UnitY
                        });
                    }
                    else if (Top > level.Bounds.Bottom + 16)
                    {
                        RemoveSelf();
                        return;
                    }
                    Hold.CheckAgainstColliders();
                }
                else
                {
                    Position = startPos + Vector2.UnitY * platformSine.Value * 1f;
                }
                Vector2 one = Vector2.One;
                if (!Hold.IsHeld)
                {
                    if (level.Wind.Y < 0f)
                    {
                        PlayOpen();
                    }
                    else
                    {
                        sprite.Play("idle");
                    }
                }
                else if (Hold.Holder.Speed.Y > 20f || level.Wind.Y < 0f)
                {
                    if (level.OnInterval(0.04f))
                    {
                        if (level.Wind.Y < 0f)
                        {
                            level.ParticlesBG.Emit(P_GlideUp, 1, Position - Vector2.UnitY * 20f, new Vector2(6f, 4f));
                        }
                        else
                        {
                            level.ParticlesBG.Emit(P_Glide, 1, Position - Vector2.UnitY * 10f, new Vector2(6f, 4f));
                        }
                    }
                    PlayOpen();
                    if (Input.GliderMoveY.Value > 0)
                    {
                        one.X = 0.7f;
                        one.Y = 1.4f;
                    }
                    else if (Input.GliderMoveY.Value < 0)
                    {
                        one.X = 1.2f;
                        one.Y = 0.8f;
                    }
                    Input.Rumble(RumbleStrength.Climb, RumbleLength.Short);
                }
                else
                {
                    sprite.Play("held");
                }
                sprite.Scale.Y = Calc.Approach(sprite.Scale.Y, one.Y, Engine.DeltaTime * 2f);
                sprite.Scale.X = Calc.Approach(sprite.Scale.X, Math.Sign(sprite.Scale.X) * one.X, Engine.DeltaTime * 2f);
            }
            else
            {
                Position += Speed * Engine.DeltaTime;
            }
        }

        private void PlayOpen()
        {
            if (sprite.CurrentAnimationID != "fall" && sprite.CurrentAnimationID != "fallLoop")
            {
                sprite.Play("fall");
                sprite.Scale = new Vector2(1.5f, 0.6f);
                level.Particles.Emit(P_Expand, 16, base.Center + (Vector2.UnitY * -12f).Rotate(sprite.Rotation), new Vector2(8f, 3f), -MathF.PI / 2f + sprite.Rotation);
                if (Hold.IsHeld)
                {
                    Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
                }
            }
        }

        public override void Render()
        {
            if (!destroyed)
            {
                sprite.DrawSimpleOutline();
            }
            base.Render();
            if (bubble)
            {
                for (int i = 0; i < 24; i++)
                {
                    Draw.Point(Position + PlatformAdd(i), PlatformColor(i));
                }
            }
        }

        private void WindMode(Vector2 wind)
        {
            if (!Hold.IsHeld)
            {
                if (wind.X != 0f)
                {
                    MoveH(wind.X * 0.5f);
                }
                if (wind.Y != 0f)
                {
                    MoveV(wind.Y);
                }
            }
        }

        private Vector2 PlatformAdd(int num)
        {
            return new Vector2(-12 + num, -5 + (int)Math.Round(Math.Sin(base.Scene.TimeActive + (float)num * 0.2f) * 1.7999999523162842));
        }

        private Color PlatformColor(int num)
        {
            if (num <= 1 || num >= 22)
            {
                return Color.White * 0.4f;
            }
            return Color.White * 0.8f;
        }

        private void OnCollideH(CollisionData data)
        {
            if (data.Hit is DashSwitch)
            {
                (data.Hit as DashSwitch).OnDashCollide(null, Vector2.UnitX * Math.Sign(Speed.X));
            }
            if (Speed.X < 0f)
            {
                Audio.Play("event:/new_content/game/10_farewell/glider_wallbounce_left", Position);
            }
            else
            {
                Audio.Play("event:/new_content/game/10_farewell/glider_wallbounce_right", Position);
            }
            Speed.X *= -1f;
            sprite.Scale = new Vector2(0.8f, 1.2f);
            EntityStateTracker.PostUpdate(this);
        }

        private void OnCollideV(CollisionData data)
        {
            if (Math.Abs(Speed.Y) > 8f)
            {
                sprite.Scale = new Vector2(1.2f, 0.8f);
                Audio.Play("event:/new_content/game/10_farewell/glider_land", Position);
            }
            if (Speed.Y < 0f)
            {
                Speed.Y *= -0.5f;
            }
            else
            {
                Speed.Y = 0f;
            }
            EntityStateTracker.PostUpdate(this);
        }

        private void OnPickup()
        {
            if (bubble)
            {
                for (int i = 0; i < 24; i++)
                {
                    level.Particles.Emit(P_Platform, Position + PlatformAdd(i), PlatformColor(i));
                }
            }
            AllowPushing = false;
            Speed = Vector2.Zero;
            AddTag(Tags.Persistent);
            highFrictionTimer = 0.5f;
            bubble = false;
            wiggler.Start();
            holderID = PlayerID.MyID;
            EntityStateTracker.PostUpdate(this);
        }

        private void OnRelease(Vector2 force)
        {
            if (force.X == 0f)
            {
                Audio.Play("event:/new_content/char/madeline/glider_drop", Position);
            }
            AllowPushing = true;
            RemoveTag(Tags.Persistent);
            force.Y *= 0.5f;
            if (force.X != 0f && force.Y == 0f)
            {
                force.Y = -0.4f;
            }
            Speed = force * 100f;
            wiggler.Start();
            holderID = null;
            EntityStateTracker.PostUpdate(this);
        }

        public override void OnSquish(CollisionData data)
        {
            if (IsHeldByOtherPlayer) return;
            if (!TrySquishWiggle(data, 3, 3))
            {
                Destroy();
            }
        }

        public bool HitSpring(Spring spring)
        {
            if (!Hold.IsHeld)
            {
                if (spring.Orientation == Spring.Orientations.Floor && Speed.Y >= 0f)
                {
                    Speed.X *= 0.5f;
                    Speed.Y = -160f;
                    noGravityTimer = 0.15f;
                    wiggler.Start();
                    EntityStateTracker.PostUpdate(this);
                    return true;
                }
                if (spring.Orientation == Spring.Orientations.WallLeft && Speed.X <= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = 160f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    wiggler.Start();
                    EntityStateTracker.PostUpdate(this);
                    return true;
                }
                if (spring.Orientation == Spring.Orientations.WallRight && Speed.X >= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = -160f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    wiggler.Start();
                    EntityStateTracker.PostUpdate(this);
                    return true;
                }
            }
            return false;
        }

        private IEnumerator DestroyAnimationRoutine()
        {
            Audio.Play("event:/new_content/game/10_farewell/glider_emancipate", Position);
            sprite.Play("death");
            yield return 1f;
            RemoveSelf();
        }

        private void Destroy(bool remoteUpdate = false)
        {
            if (destroyed) return;
            if (!remoteUpdate && IsHeldByOtherPlayer) return;
            destroyed = true;
            Collidable = false;
            if (Hold.IsHeld)
            {
                Vector2 speed2 = Hold.Holder.Speed;
                Hold.Holder.Drop();
                Speed = speed2 * 0.333f;
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            }
            if (!remoteUpdate) EntityStateTracker.PostUpdate(this);
            Add(new Coroutine(DestroyAnimationRoutine()));
        }

        private void BeginOtherPlayerControl(PlayerID controllingPlayer)
        {
            holderID = controllingPlayer;
            bubble = false;
            Hold.cannotHoldTimer = 999999f;
            Visible = false;
            Collidable = false;
            Active = false;
        }

        private void EndOtherPlayerControl()
        {
            Hold.cannotHoldTimer = Hold.cannotHoldDelay;
            Visible = true;
            Collidable = true;
            Active = true;
            if (holderID?.Equals(PlayerID.MyID) == false)
            {
                holderID = null;
            }
        }

        ////////////////////////////////////////////////

        private static bool StaticSyncHandler(EntityID id, object st)
        {
            // Create the jelly if it doesn't exist; it was probably unloaded due to screen transition.
            if (st is not SyncedJellyState sjs) return false;
            if (sjs.Destroyed) return true;
            Level level = Engine.Scene as Level;
            if (level == null) return true;
            SyncedJelly jelly = new SyncedJelly(sjs.Position, false, id);
            level.Add(jelly);
            jelly.Speed = sjs.Speed;
            jelly.holderID = sjs.Holder;
            return true;
        }

        public override void SceneEnd(Scene scene)
        {
            base.SceneEnd(scene);
            EntityStateTracker.RemoveListener(this);
        }

        public override void Removed(Scene scene)
        {
            base.Removed(scene);
            EntityStateTracker.RemoveListener(this);
        }

        public static SyncBehavior GetSyncBehavior() => new SyncBehavior()
        {
            Header = Headers.SyncedJelly,
            Parser = ParseState,
            StaticHandler = StaticSyncHandler,
            DiscardIfNoListener = false,
            DiscardDuplicates = false,
            Critical = false,
        };

        private static object ParseState(CelesteNetBinaryReader r)
        {
            return new SyncedJellyState()
            {
                Position = r.ReadVector2(),
                Speed = r.ReadVector2(),
                Destroyed = r.ReadBoolean(),
                Holder = r.ReadBoolean() ? r.ReadPlayerID() : null,
            };
        }

        public EntityID GetID()
        {
            return id;
        }

        public void WriteState(CelesteNetBinaryWriter w)
        {
            w.Write(Position);
            w.Write(Speed);
            w.Write(destroyed);
            if (holderID == null)
            {
                w.Write(false);
            }
            else
            {
                w.Write(true);
                w.Write(holderID.Value);
            }
        }

        public void ApplyState(object state)
        {
            if (state is not SyncedJellyState sjs) return;

            if (sjs.Holder != null)
            {
                if (holderID != null)  // Conflict; drop it.
                {
                    holderID = null;
                    Hold.Holder?.Drop();
                }
                else  // Another player grabbed it
                {
                    BeginOtherPlayerControl(sjs.Holder.Value);
                }
            }
            else
            {
                if (holderID != null)  // The holder dropped it (probably)
                {
                    if (holderID.Value.Equals(PlayerID.MyID))
                    {
                        // Local player is holding but the remote doesn't know; ignore this update.
                        return;
                    }
                    EndOtherPlayerControl();
                }
            }

            Position = sjs.Position;
            Speed = sjs.Speed;
            if (sjs.Destroyed && !destroyed)
            {
                Destroy(true);
                return;
            }

        }

        public bool CheckRecurringUpdate()
        {
            throw new NotImplementedException();
        }

        private class SyncedJellyState
        {
            public Vector2 Position;
            public Vector2 Speed;
            public bool Destroyed;
            public PlayerID? Holder;
        }
    }
}
