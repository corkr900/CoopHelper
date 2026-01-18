using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Triggers
{
    [CustomEntity("corkr900CoopHelper/SyncedFlagTrigger")]
    public class SyncedFlagTrigger : Trigger, ISynchronizable
    {
        public string Flag { get; private set; }
        public bool Value { get; private set; }
        private EntityID id;

        public SyncedFlagTrigger(EntityData data, Vector2 offset) : base(data, offset)
        {
            Flag = data.Attr("flag");
            Value = data.Bool("value", true);
            id = new EntityID(data.Level.Name, data.ID);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            SceneAs<Level>()?.Session?.SetFlag(Flag, Value);
            Engine.Commands.Log($"Setting {Flag} to {Value} from SyncedFlagTrigger (local)");
            EntityStateTracker.PostUpdate(this);
        }

        public static SyncBehavior GetSyncBehavior() => new SyncBehavior()
        {
            Header = Headers.SyncedFlagTrigger,
            Parser = ParseState,
            StaticHandler = StaticHandler,
            DiscardIfNoListener = false,
            DiscardDuplicates = false,
            Critical = true,
        };

        private static object ParseState(CelesteNetBinaryReader r)
        {
            return new SyncedFlagState()
            {
                Flag = r.ReadString(),
                Value = r.ReadBoolean(),
            };
        }

        private static bool StaticHandler(EntityID id, object obj)
        {
            if (Engine.Scene is Level level && obj is SyncedFlagState syncedState)
            {
                Engine.Commands.Log($"Setting {syncedState.Flag} to {syncedState.Value} from SyncedFlagTrigger (static)");
                level.Session.SetFlag(syncedState.Flag, syncedState.Value);
                return true;
            }
            return false;
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            EntityStateTracker.AddListener(this, false);
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

        public void ApplyState(object state)
        {
            SyncedFlagState syncedState = state as SyncedFlagState;
            if (syncedState == null) return;
            Engine.Commands.Log($"Setting {syncedState.Flag} to {syncedState.Value} from SyncedFlagTrigger (remote)");
            SceneAs<Level>()?.Session?.SetFlag(syncedState.Flag, syncedState.Value);
        }

        public bool CheckRecurringUpdate()
        {
            return false;
        }

        public EntityID GetID()
        {
            return id;
        }

        public void WriteState(CelesteNetBinaryWriter w)
        {
            w.Write(Flag);
            w.Write(Value);
        }

        private class SyncedFlagState
        {
            public string Flag;
            public bool Value;
        }
    }
}
