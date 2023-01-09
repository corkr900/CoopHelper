using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public class SessionSyncState {
		public PlayerID player;
		public DateTime instant;
		public string room;
		public bool dead;
		public List<EntityID> collectedStrawbs;
		public bool cassette;
	}

	public class SessionSynchronizer : Component, ISynchronizable {
		private static DateTime lastTriggeredDeathLocal = DateTime.MinValue;
		private static DateTime lastTriggeredDeathRemote = DateTime.MinValue;
		private static bool CurrentDeathIsSecondary = false;

		private object basicFlagsLock = new object();
		private bool deathPending = false;
		private bool cassettePending = false;
		public List<EntityID> newlyCollectedStrawbs = new List<EntityID>();

		public Player playerEntity { get; private set; }

		public SessionSynchronizer(Player p, bool active, bool visible) : base(active, visible) {
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
				lock (basicFlagsLock) {
					deathPending = true;
					lastTriggeredDeathLocal = SyncTime.Now;
					EntityStateTracker.PostUpdate(this);
				}
			}
		}

		internal void StrawberryCollected(EntityID id) {
			lock (newlyCollectedStrawbs) {
				newlyCollectedStrawbs.Add(id);
				EntityStateTracker.PostUpdate(this);
			}
		}

		internal void CassetteCollected() {
			lock (basicFlagsLock) {
				cassettePending = true;
				EntityStateTracker.PostUpdate(this);
			}
		}

		public static int GetHeader() => 1;

		public static SessionSyncState ParseState(CelesteNetBinaryReader r) {
			SessionSyncState state = new SessionSyncState {
				dead = r.ReadBoolean(),
				cassette = r.ReadBoolean(),
				player = r.ReadPlayerID(),
				instant = r.ReadDateTime(),
				room = r.ReadString(),
			};
			List<EntityID> strawbs = new List<EntityID>();
			int count = r.ReadInt32();
			for (int i = 0; i < count; i++) {
				strawbs.Add(r.ReadEntityID());
			}
			state.collectedStrawbs = strawbs;
			return state;
		}

		public void ApplyState(object state) {
			if (state is SessionSyncState dss) {
				if (!dss.player.Equals(PlayerID.MyID)
					&& CoopHelperModule.Session?.IsInCoopSession == true
					&& CoopHelperModule.Session.SessionMembers.Contains(dss.player))
				{
					Level level = SceneAs<Level>();

					// death sync
					if (dss.dead
						&& PlayerState.Mine?.CurrentRoom != null
						&& dss.room == PlayerState.Mine.CurrentRoom
						&& (dss.instant - lastTriggeredDeathRemote).TotalMilliseconds > 1000
						&& level?.Transitioning == false)
					{
						CurrentDeathIsSecondary = true;  // Prevents death signals from just bouncing back & forth forever
						EntityAs<Player>()?.Die(Vector2.Zero, true, true);
						CurrentDeathIsSecondary = false;
						lastTriggeredDeathRemote = dss.instant;
					}

					// strawberry sync
					if (dss.collectedStrawbs != null && dss.collectedStrawbs.Count > 0) {
						foreach (EntityID id in dss.collectedStrawbs) {
							// register strawb as collected
							SaveData.Instance.AddStrawberry(id, false);  // TODO handle golden strawbs
							Session session = level.Session;
							session.DoNotLoad.Add(id);
							session.Strawberries.Add(id);
							// handle if the strawb is in the current room
							foreach (Entity e in Scene.Entities) {
								// TODO handle strawberries that don't inherit Strawberry
								if (e is Strawberry strawb && strawb.ID.Equals(id)) {
									if (strawb.Follower.HasLeader) {
										strawb.Follower.Leader.LoseFollower(strawb.Follower);
									}
									DynamicData dd = new DynamicData(strawb);
									if (!dd.Get<bool>("collected")) {
										dd.Set("collected", true);
										Player player = EntityAs<Player>();
										strawb.Add(new Coroutine(dd.Invoke<IEnumerator>("CollectRoutine", player.StrawberryCollectIndex++)));
										player.StrawberryCollectResetTimer = 2.5f;
									}
									break;
								}
							}
						}
					}

					// cassette sync
					if (dss.cassette && !level.Session.Cassette) {
						bool cassetteFound = false;
						foreach (Entity e in Scene.Entities) {
							if (e is Cassette cass) {
								DynamicData dd = new DynamicData(cass);
								dd.Invoke("OnPlayer", EntityAs<Player>());
								cassetteFound = true;
								break;
							}
						}
						if (!cassetteFound) {
							level.Session.Cassette = true;
							SaveData.Instance.RegisterCassette(level.Session.Area);
							CassetteBlockManager cbm = Scene.Tracker.GetEntity<CassetteBlockManager>();
							cbm?.StopBlocks();
							cbm?.Finish();
						}
					}

					// TODO heart sync
				}
			}
		}

		public EntityID GetID() {
			return new EntityID("%SESSIONSYNC%", 99999);
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			lock (basicFlagsLock) {
				w.Write(deathPending);
				deathPending = false;
				w.Write(cassettePending);
				cassettePending = false;
			}
			w.Write(PlayerID.MyID);
			w.Write(lastTriggeredDeathLocal);
			w.Write(PlayerState.Mine?.CurrentRoom ?? "");
			lock (newlyCollectedStrawbs) {
				w.Write(newlyCollectedStrawbs.Count);
				foreach (EntityID id in newlyCollectedStrawbs) {
					w.Write(id);
				}
				newlyCollectedStrawbs.Clear();
			}
		}
	}
}
