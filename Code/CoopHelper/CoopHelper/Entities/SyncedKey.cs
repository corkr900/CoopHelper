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
	[CustomEntity("corkr900CoopHelper/SyncedKey")]
	[Tracked]
	public class SyncedKey : Key, ISynchronizable {
		internal bool AnotherPlayerUsed { get; private set; }

		public SyncedKey(EntityData data, Vector2 offset) : base(data, offset, new EntityID(data.Level.Name, data.ID)) {
			PlayerCollider coll = Get<PlayerCollider>();
			Action<Player> orig_OnPlayer = coll.OnCollide;
			coll.OnCollide = (Player p) => {
				orig_OnPlayer(p);
				if (!AnotherPlayerUsed) EntityStateTracker.PostUpdate(this);
			};
		}

		internal void OnRegisterUsed() {
			if (!AnotherPlayerUsed) EntityStateTracker.PostUpdate(this);
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

		public static bool StaticHandler(object state) {
			// TODO
			return false;
		}

		public void ApplyState(object state) {
			if (state is bool newIsUsed) {
				DynamicData dd = new DynamicData(this);
				if (newIsUsed) {
					AnotherPlayerUsed = true;
					if (!IsUsed) {
						Session session = SceneAs<Level>().Session;
						Collidable = false;
						session.DoNotLoad.Add(ID);
						session.Keys.Add(ID);
						session.UpdateLevelStartDashes();
						Depth = -1000000;
						// TODO setting on key for whether to bubble the player who didn't get it
						if (dd.Get<Follower>("follower")?.HasLeader == false) {
							Vector2[] nodes = dd.Get<Vector2[]>("nodes");
							if (dd.Get<Vector2[]>("nodes") != null && nodes.Length >= 2) {
								Add(new Coroutine(dd.Invoke<IEnumerator>("NodeRoutine", GetPlayer())));
							}
						}
						RegisterUsed();
					}
				}
				else {
					dd.Invoke("OnPlayer", GetPlayer());
				}
			}
		}

		private Player GetPlayer() {
			List<Entity> players = SceneAs<Level>().Tracker.GetEntities<Player>();
			foreach (Entity player in players) {
				if (player.GetType().Equals(typeof(Player))) {  // Filter out doppelgangers, etc
					return player as Player;
				}
			}
			return null;
		}

		public EntityID GetID() => ID;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(IsUsed);
		}

		#endregion
	}
}
