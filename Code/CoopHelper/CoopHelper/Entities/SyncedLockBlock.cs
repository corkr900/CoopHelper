using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedLockBlock")]
	public class SyncedLockBlock : LockBlock, ISynchronizable {

		public class DummyKey : Key {
			public DummyKey() : base (Vector2.Zero, EntityID.None, new Vector2[0]) { }
		}

		public SyncedLockBlock(EntityData data, Vector2 offset) : base(data, offset, new EntityID(data.Level.Name, data.ID)) {

		}

		#region ISync implementation

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

		public static int GetHeader() => 10;

		public static bool ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool b && b) {
				DynamicData dd = new DynamicData(this);
				Add(new Coroutine(new DynamicData(this).Invoke<IEnumerator>("UnlockRoutine",
					new DynamicData(new DummyKey()).Get<Follower>("follower"))));
			}
		}

		public EntityID GetID() => ID;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(true);
		}

		#endregion
	}
}
