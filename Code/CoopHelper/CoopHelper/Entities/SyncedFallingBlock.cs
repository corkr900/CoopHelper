using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedFallingBlock")]
	public class SyncedFallingBlock : FallingBlock, ISynchronizable {
		EntityID id;

		public SyncedFallingBlock(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		public override void OnStaticMoverTrigger(StaticMover sm) {
			bool before = Triggered;
			base.OnStaticMoverTrigger(sm);
			if (Triggered && !before) {
				EntityStateTracker.PostUpdate(this);
			}
		}

		#region These 3 overrides MUST be defined for synced entities/triggers

		public override void Added(Scene scene) {
			base.Added(scene);
			EntityStateTracker.AddListener(this);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		#endregion

		#region ISynchronizable implementation

		public static int GetHeader() => 3;

		public EntityID GetID() => id;

		public static bool ParseState(CelesteNetBinaryReader r) {
			bool triggered = r.ReadBoolean();
			return triggered;
		}

		public void ApplyState(object state) {
			if (state is bool triggered) {
				Triggered = triggered;
			}
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(Triggered);
		}

		#endregion
	}
}
