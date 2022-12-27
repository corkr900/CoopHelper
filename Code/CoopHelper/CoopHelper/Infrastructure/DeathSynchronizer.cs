using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public class DeathSyncState {
		public PlayerID player;
		public DateTime instant;
	}

	public class DeathSynchronizer : Component, ISynchronizable {
		private static DateTime lastTriggeredDeath = SyncTime.Now;
		private static bool CurrentDeathIsSecondary = false;

		public Player playerEntity { get; private set; }

		public DeathSynchronizer(Player p, bool active, bool visible) : base(active, visible) {
			playerEntity = p;
		}

		public override void EntityAdded(Scene scene) {
			base.EntityAdded(scene);
			EntityStateTracker.AddListener(this);
		}

		public override void Added(Entity entity) {
			base.Added(entity);
			EntityStateTracker.AddListener(this);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void EntityRemoved(Scene scene) {
			base.EntityRemoved(scene);
			EntityStateTracker.RemoveListener(this);
		}

		internal void PlayerDied() {
			if (!CurrentDeathIsSecondary) {
				lastTriggeredDeath = SyncTime.Now;
				EntityStateTracker.PostUpdate(this);
			}
		}

		public static int GetHeader() => 1;

		public static DeathSyncState ParseState(CelesteNetBinaryReader r) {
			return new DeathSyncState {
				player = r.ReadPlayerID(),
				instant = r.ReadDateTime(),
			};
		}

		public void ApplyState(object state) {
			if (state is DeathSyncState dss) {
				if (!dss.player.Equals(PlayerID.MyID)
					&& CoopHelperModule.Session?.IsInCoopSession == true
					&& CoopHelperModule.Session.SessionMembers.Contains(dss.player)
					&& lastTriggeredDeath < dss.instant
					&& (dss.instant - lastTriggeredDeath).TotalMilliseconds > 1000)
				{
					CurrentDeathIsSecondary = true;  // Prevents death signals from just bouncing back & forth forever
					EntityAs<Player>()?.Die(Vector2.Zero, true, true);
					CurrentDeathIsSecondary = false;
					lastTriggeredDeath = dss.instant;
				}
			}
		}

		public EntityID GetID() {
			return new EntityID("%DEATHSYNC%", 99999);
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(PlayerID.MyID);
			w.Write(lastTriggeredDeath);
		}
	}
}
