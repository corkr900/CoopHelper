using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using FMOD.Studio;
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
			EntityStateTracker.AddListener(this, false);
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
			Header = 9,
			Parser = ParseState,
			StaticHandler = StaticHandler,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = true,
		};

		public static SyncedKeyState ParseState(CelesteNetBinaryReader r) {
			return new SyncedKeyState() {
				Used = r.ReadBoolean(),
			};
		}

		public static bool StaticHandler(EntityID id, object state) {
			if (!(state is SyncedKeyState sks)) return false;
			if (!(Engine.Scene is Level level)) return false;
			Session session = level.Session;
			if (session.DoNotLoad.Contains(id)) {
				if (sks.Used && session.Keys.Contains(id)) {
					session.Keys.Remove(id);
				}
				return true;
			}
			Player player = level.Tracker.GetEntity<Player>();
			if (player == null) {
				session.DoNotLoad.Add(id);
				session.Keys.Add(id);
			}
			else {
				session.DoNotLoad.Add(id);
				session.Keys.Add(id);
				level.Add(new SyncedKey(player, id));
			}
			return !sks.Used;
		}

		public void ApplyState(object state) {
			if (state is SyncedKeyState sks) {
				DynamicData dd = new DynamicData(this);
				if (sks.Used) {
					AnotherPlayerUsed = true;
					if (!IsUsed) {
						Session session = SceneAs<Level>().Session;
						Collidable = false;
						if (!session.DoNotLoad.Contains(ID)) session.DoNotLoad.Add(ID);
						Depth = -1000000;
						dd.Get<Sprite>("sprite").Visible = false;
						Follower follower = dd.Get<Follower>("follower");
						if (follower?.HasLeader == true) {
							follower.Leader.LoseFollower(follower);
						}
						RegisterUsed();
						EventInstance evt = Audio.Play("event:/game/03_resort/key_unlock");
						Alarm.Set(this, 2.7f, () => {
							RemoveSelf();
						}, Alarm.AlarmMode.Oneshot);
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
