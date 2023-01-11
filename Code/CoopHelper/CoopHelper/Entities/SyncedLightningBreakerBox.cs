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
		public Vector2 lastDashedDir;

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
						lastDashedDir = dir;
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

		public static SyncedLightningBreakerBoxState ParseState(CelesteNetBinaryReader r) {
			return new SyncedLightningBreakerBoxState() {
				healthLost = r.ReadInt32(),
				dir = r.ReadVector2(),
			};
		}

		public void ApplyState(object state) {
			if (state is SyncedLightningBreakerBoxState st) {
				// Handle multiple health lost at once
				if (st.healthLost > 1) {
					DynamicData dd = new DynamicData(this);
					int health = dd.Get<int>("health");
					dd.Set("health", health - st.healthLost + 1);
				}
				// I don't want to duplicate the Dashed function or IL Hook it...
				// If I don't give Dashed a Player it crashes, but all it does is restore dashes.
				// But i don't want it to restore dashes here so i have to undo any changes to the dash count
				Player player = Scene.Tracker.GetEntity<Player>();
				int dashCtBefore = player.Dashes;
				Dashed(player, st.dir);
				player.Dashes = dashCtBefore; 
			}
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			lock (healthDiffLock) {
				w.Write(healthLost);
				w.Write(lastDashedDir);
				healthLost = 0;
			}
		}
	}

	public class SyncedLightningBreakerBoxState {
		public int healthLost;
		public Vector2 dir;
	}
}
