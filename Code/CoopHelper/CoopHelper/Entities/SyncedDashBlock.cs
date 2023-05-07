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
	[CustomEntity("corkr900CoopHelper/SyncedDashBlock")]
	[TrackedAs(typeof(DashBlock))]
	public class SyncedDashBlock : DashBlock, ISynchronizable {
		private EntityID id;

		public SyncedDashBlock(EntityData data, Vector2 offset) : base(data, offset, new EntityID(data.Level.Name, data.ID)) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		internal void OnBreak() {
			EntityStateTracker.PostUpdate(this);
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			EntityStateTracker.AddListener(this, false);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 13,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = true,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool b && b) {
				Break(Position + new Vector2(Width/2f, Height/2f), Vector2.Zero, true, true);
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(true);
		}
	}
}
