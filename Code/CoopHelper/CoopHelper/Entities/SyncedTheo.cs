using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Celeste.LavaRect;

namespace Celeste.Mod.CoopHelper.Entities
{
    [TrackedAs(typeof(TheoCrystal))]
    public class SyncedTheoCrystal : TheoCrystal, ISynchronizable
    {

        private EntityID id;
        private PlayerID? holderID = null;
        private bool killedByOtherPlayer = false;
        private bool IsHeldByOtherPlayer => holderID != null && !holderID.Value.Equals(PlayerID.MyID);

        public SyncedTheoCrystal(EntityData e, Vector2 offset)
            : base(e.Position + offset)
        {
            id = new EntityID(e.Level.Name, e.ID);
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
        }

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
            holderID = controllingPlayer;
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

        public override void SceneEnd(Scene scene)
        {
            base.SceneEnd(scene);
            EntityStateTracker.RemoveListener(this);
        }

        public override void Added(Scene scene)
        {
            EntityStateTracker.AddListener(this, false);
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
            StaticHandler = null,
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
            if (sjs.Destroyed && !dead)
            {
                killedByOtherPlayer = true;
                Die();
                return;
            }

        }

        public bool CheckRecurringUpdate()
        {
            throw new NotImplementedException();
        }

        private class SyncedTheoCrystalState
        {
            public Vector2 Position;
            public Vector2 Speed;
            public bool Destroyed;
            public PlayerID? Holder;
        }
    }
}
