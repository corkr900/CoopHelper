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
	[CustomEntity("corkr900CoopHelper/SyncedKey")]
	public class SyncedKey : Key, ISynchronizable {

		public SyncedKey(EntityData data, Vector2 offset) : base(data, offset, new EntityID(data.Level.Name, data.ID)) {
			PlayerCollider coll = Get<PlayerCollider>();
			Action<Player> orig_OnPlayer = coll.OnCollide;
			coll.OnCollide = (Player p) => {
				orig_OnPlayer(p);
				EntityStateTracker.PostUpdate(this);
			};
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

		public static int GetHeader() => 9;

		public static bool ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool b) {
				if (b) {
					List<Entity> players = SceneAs<Level>().Tracker.GetEntities<Player>();
					foreach (Entity player in players) {
						if (player.GetType().Equals(typeof(Player))) {  // Filter out doppelgangers, etc
							DynamicData dd = new DynamicData(this);
							dd.Invoke("OnPlayer", player);
						}
					}
				}
			}
		}

		public EntityID GetID() => ID;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(true);
		}

		#endregion
	}
}
