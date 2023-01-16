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

		private SyncedKey(Player player, EntityID id) : base(player, id) { }

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

		public static SyncedKeyState ParseState(CelesteNetBinaryReader r) {
			return new SyncedKeyState() {
				Used = r.ReadBoolean(),
			};
		}

		public static bool StaticHandler(EntityID id, object state) {
			if (!(state is SyncedKeyState sks)) return false;
			if (!(Engine.Scene is Level level)) return false;
			Session session = level.Session;
			Player player = level.Tracker.GetEntity<Player>();
			if (session.DoNotLoad.Contains(id)) return false;
			if (player == null) {
				session.DoNotLoad.Add(id);
				session.Keys.Add(id);
			}
			else {
				session.DoNotLoad.Add(id);
				session.Keys.Add(id);
				level.Add(new SyncedKey(player, id));
			}
			return true;
		}

		public void ApplyState(object state) {
			if (state is SyncedKeyState sks) {
				DynamicData dd = new DynamicData(this);
				if (sks.Used) {
					AnotherPlayerUsed = true;
					if (!IsUsed) {
						Session session = SceneAs<Level>().Session;
						Collidable = false;
						session.DoNotLoad.Add(ID);
						session.Keys.Add(ID);
						session.UpdateLevelStartDashes();
						Depth = -1000000;

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

	public class SyncedKeyState {
		public bool Used;
	}
}
