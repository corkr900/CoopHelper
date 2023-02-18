using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	// TODO finish and test this

	[CustomEntity("corkr900CoopHelper/SyncedBooster")]
	public class SyncedBooster : Booster, ISynchronizable {

		public SyncedBooster(EntityData data, Vector2 offset) : base(data, offset) {
			PlayerCollider coll = Get<PlayerCollider>();
			var orig_OnPlayer = coll.OnCollide;
			coll.OnCollide = (Player p) => {
				orig_OnPlayer(p);
				EntityStateTracker.PostUpdate(this);
			};
		}

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

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 6,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static SyncedBoosterState ParseState(CelesteNetBinaryReader r) {
			throw new NotImplementedException();
		}

		public void ApplyState(object state) {
			throw new NotImplementedException();
		}

		public bool CheckRecurringUpdate() {
			throw new NotImplementedException();
		}

		public EntityID GetID() {
			throw new NotImplementedException();
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			throw new NotImplementedException();
		}
	}

	public class SyncedBoosterState {

	}
}
