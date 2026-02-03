using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Celeste.LavaRect;

namespace Celeste.Mod.CoopHelper.Entities
{
    [TrackedAs(typeof(TheoCrystal))]
    [CustomEntity("corkr900CoopHelper/SyncedTheoCrystal")]
    public class SyncedTheoCrystal : TheoCrystal, ISynchronizable
    {
        public bool EnforceLevelBounds { get; private set; }

        private float aliveTime = 0f;
        private float lastRecurringUpdate = 0f;
        private EntityID id;
        private PlayerID? holderID = null;
        private bool killedByOtherPlayer = false;
        private bool IsHeldByOtherPlayer => holderID != null && !holderID.Value.Equals(PlayerID.MyID);

        public SyncedTheoCrystal(EntityID entityId, Vector2 position, bool enforceLevelBounds)
            : base(position)
        {
            id = entityId;
            Action orig_OnPickup = Hold.OnPickup;
            Hold.OnPickup = () =>
            {
                orig_OnPickup();
                holderID = PlayerID.MyID;
                EntityStateTracker.PostUpdate(this);
            };
            Action<Vector2> orig_OnRelease = Hold.OnRelease;
            Hold.OnRelease = (Vector2 force) =>
            {
                orig_OnRelease(force);
                holderID = null;
                EntityStateTracker.PostUpdate(this);
            };
            EnforceLevelBounds = enforceLevelBounds;
        }

        public SyncedTheoCrystal(EntityData e, Vector2 offset)
            : this(new EntityID(e.Level.Name, e.ID), e.Position + offset, e.Bool("enforceBounds", false))
        { }

        public override void OnSquish(CollisionData data)
        {
            if (IsHeldByOtherPlayer) return;
            if (!TrySquishWiggle(data, 3, 3) && !SaveData.Instance.Assists.Invincible)
            {
                Die();
            }
        }

        internal bool TryDie()
        {
            if (dead) return false;
            if (!killedByOtherPlayer)
            {
                if (IsHeldByOtherPlayer) return false;
                EntityStateTracker.PostUpdate(this);
            }
            return true;
        }

        private void BeginOtherPlayerControl(PlayerID controllingPlayer)
        {
            Engine.Commands.Log($"Beginning other player hold");
            holderID = controllingPlayer;
            Hold.cannotHoldTimer = 999999f;
            Visible = false;
            Collidable = false;
            Active = false;
        }

        private void EndOtherPlayerControl()
        {
            Engine.Commands.Log($"Ending other player hold");
            Hold.cannotHoldTimer = Hold.cannotHoldDelay;
            Visible = true;
            Collidable = true;
            Active = true;
            if (holderID?.Equals(PlayerID.MyID) == false)
            {
                holderID = null;
            }
        }

        public override void Update()
        {
            base.Update();
            aliveTime += Engine.RawDeltaTime;
        }

        ////////////////////////////////////////////////

        public override void SceneEnd(Scene scene)
        {
            base.SceneEnd(scene);
            EntityStateTracker.RemoveListener(this);
        }

        public override void Added(Scene scene)
        {
            Actor_Added(scene);
            Level = SceneAs<Level>();
            foreach (TheoCrystal entity in Level.Tracker.GetEntities<TheoCrystal>())
            {
                if (entity == this) continue;
                if (entity.Hold.IsHeld || (entity is SyncedTheoCrystal tc && tc.id.Equals(id)))
                {
                    RemoveSelf();
                    if (entity is SyncedTheoCrystal stcHeld)
                    {
                        EntityStateTracker.PostUpdate(stcHeld);
                    }
                    return;
                }
            }
            EntityStateTracker.AddListener(this, true);
        }

        /// <summary>
        /// This exists to skip the TheoCrystal implementation of Added and go straight to Actor::Added
        /// This skips the deduplication of theos
        /// </summary>
        [MonoModLinkTo("Celeste.TheoCrystal", "System.Void Added(Monocle.Scene)")]
        public void Actor_Added(Scene scene)
        {
            base.Added(scene);
        }

        public override void Removed(Scene scene)
        {
            base.Removed(scene);
            EntityStateTracker.RemoveListener(this);
        }

        public static SyncBehavior GetSyncBehavior() => new SyncBehavior()
        {
            Header = Headers.SyncedTheo,
            Parser = ParseState,
            StaticHandler = StaticSyncHandler,
            DiscardIfNoListener = false,
            DiscardDuplicates = false,
            Critical = false,
        };

        private static object ParseState(CelesteNetBinaryReader r)
        {
            return new SyncedTheoCrystalState()
            {
                Position = r.ReadVector2(),
                Speed = r.ReadVector2(),
                Destroyed = r.ReadBoolean(),
                EnforceBounds = r.ReadBoolean(),
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
            w.Write(dead);
            w.Write(EnforceLevelBounds);
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
            if (state is not SyncedTheoCrystalState sjs) return;

            Engine.Commands.Log($"Updated. is player holding: {sjs.Holder != null}");
            if (sjs.Holder == null)
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
            else if (!sjs.Holder.Equals(holderID))
            {
                if (Hold.IsHeld)
                {
                    // Conflict; I'm holding it but so is someone else. Just drop it
                    holderID = null;
                    Hold.Holder?.Drop();
                }
                else {
                    // Another player has rightful control
                    BeginOtherPlayerControl(sjs.Holder.Value);
                }
            }

            Position = sjs.Position;
            Speed = sjs.Speed;
            if (sjs.Destroyed && !dead)
            {
                killedByOtherPlayer = true;
                Die();
                return;
            }

        }

        public bool CheckRecurringUpdate()
        {
            if (!Hold.IsHeld) return false;
            if (lastRecurringUpdate < aliveTime - 0.25f)
            {
                lastRecurringUpdate = aliveTime;
                return true;
            }
            return false;
        }

        private static bool StaticSyncHandler(EntityID id, object st)
        {
            // Create the jelly if it doesn't exist; it was probably unloaded due to screen transition.
            if (st is not SyncedTheoCrystalState stcs) return false;
            if (stcs.Destroyed) return true;
            Level level = Engine.Scene as Level;
            if (level == null) return true;
            SyncedTheoCrystal crystal = new SyncedTheoCrystal(id, stcs.Position, stcs.EnforceBounds);
            level.Add(crystal);
            crystal.Speed = stcs.Speed;
            crystal.holderID = stcs.Holder;
            return true;
        }

        private class SyncedTheoCrystalState
        {
            public Vector2 Position;
            public Vector2 Speed;
            public bool Destroyed;
            public bool EnforceBounds;
            public PlayerID? Holder;
        }
    }
}
