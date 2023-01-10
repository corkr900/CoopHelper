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
	[CustomEntity("corkr900CoopHelper/SyncedLightningBreakerBox")]
	public class SyncedLightningBreakerBox : LightningBreakerBox, ISynchronizable {
		private object healthDiffLock = new object();
		private int healthLost = 0;
		private EntityID id;

		public SyncedLightningBreakerBox(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			DashCollision orig_OnDashed = OnDashCollide;
			OnDashCollide = (Player player, Vector2 dir) => {
				DynamicData dd = new DynamicData(this);
				int healthBefore = dd.Get<int>("health");
				DashCollisionResults result = orig_OnDashed(player, dir);
				int healthAfter = dd.Get<int>("health");
				if (healthAfter != healthBefore) {
					lock (healthDiffLock) {
						healthLost++;
						EntityStateTracker.PostUpdate(this);
					}
				}
				return result;
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

		public static int GetHeader() => 16;

		public static int ParseState(CelesteNetBinaryReader r) {
			return r.ReadInt32();
		}

		public void ApplyState(object state) {
			if (state is int remoteHealthLost) {
				DynamicData dd = new DynamicData(this);
				int health = dd.Get<int>("health");
				health -= remoteHealthLost;
				dd.Set("health", health);
				// TODO (!!!) simulate getting dashed
			}
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			lock (healthDiffLock) {
				w.Write(healthLost);
				healthLost = 0;
			}
		}
	}
}
