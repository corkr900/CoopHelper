using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Triggers {
	[CustomEntity("corkr900/CoopHelper/SyncedChangeRespawnTrigger")]
	public class SyncedChangeRespawnTrigger : ChangeRespawnTrigger, ISynchronizable {
		private EntityID id;
		private Vector2 assignedState;

		public SyncedChangeRespawnTrigger(EntityData data, Vector2 offset) 
			: base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		public override void OnEnter(Player player) {
			Session s = (base.Scene as Level).Session;
			Vector2? before = s.RespawnPoint;
			base.OnEnter(player);
			Vector2? after = s.RespawnPoint;
			if (after != null && before != after) {
				assignedState = after.Value;
				EntityStateTracker.PostUpdate(this);
			}
		}

		#region ISynchronizable implementation

		public static int GetHeader() => 2;

		public EntityID GetID() => id;

		public static Vector2 ParseState(CelesteNetBinaryReader r) {
			return r.ReadVector2();
		}

		public void ApplyState(object state) {
			if (state is Vector2 assignedPoint) {
				Session session = (base.Scene as Level).Session;
				session.HitCheckpoint = true;
				session.RespawnPoint = assignedPoint;
				session.UpdateLevelStartDashes();
			}
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(assignedState);
		}

		#endregion
	}
}
