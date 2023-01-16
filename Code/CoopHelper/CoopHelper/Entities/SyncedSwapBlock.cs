using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedSwapBlock")]
	[TrackedAs(typeof(SwapBlock))]
	public class SyncedSwapBlock : SwapBlock, ISynchronizable {
		private EntityID id;
		private Action<Vector2> orig_OnDash;
		private Vector2 lastDashDir = Vector2.Zero;

		public SyncedSwapBlock(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			DashListener listener = Get<DashListener>();
			orig_OnDash = listener.OnDash;
			listener.OnDash = SyncedOnDash;
		}

		private void SyncedOnDash(Vector2 direction) {
			orig_OnDash(direction);
			lastDashDir = direction;
			EntityStateTracker.PostUpdate(this);
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

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 7,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadVector2();
		}

		public void ApplyState(object state) {
			if (state is Vector2 dir) {
				DynamicData dd = new DynamicData(this);
				dd.Invoke("OnDash", dir);
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(lastDashDir);
		}
	}
}
