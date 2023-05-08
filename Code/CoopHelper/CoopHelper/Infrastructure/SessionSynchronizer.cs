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
		public bool heart;
		public string heartPoem;
		internal bool heartEndsLevel;
	}

	public class SessionSynchronizer : Component, ISynchronizable {
		private static DateTime lastTriggeredDeathLocal = DateTime.MinValue;
		private static DateTime lastTriggeredDeathRemote = DateTime.MinValue;
		private static bool CurrentDeathIsSecondary = false;

		public static string IDString = "%SESSIONSYNC%";

		private object basicFlagsLock = new object();
		private bool deathPending = false;
		private bool cassettePending = false;
		private bool heartPending = false;
		private string heartPoem;
		private bool heartEndsLevel;
		public List<EntityID> newlyCollectedStrawbs = new List<EntityID>();
		private bool levelEndTriggeredRemotely = false;

		public Player playerEntity { get; private set; }

		public SessionSynchronizer(Player p, bool active, bool visible) : base(active, visible) {
			playerEntity = p;
		}

		public override void EntityAdded(Scene scene) {
			base.EntityAdded(scene);
			EntityStateTracker.AddListener(this, false);
		}

		public override void EntityRemoved(Scene scene) {
			base.EntityRemoved(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Added(Entity entity) {
			base.Added(entity);
			EntityStateTracker.AddListener(this, false);
		}

		public override void Removed(Entity entity) {
			base.Removed(entity);
			EntityStateTracker.RemoveListener(this);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
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

		internal void HeartCollected(bool endLevel, string poemID) {
			lock (basicFlagsLock) {
				heartPending = true;
				heartPoem = poemID;
				heartEndsLevel = endLevel;
				EntityStateTracker.PostUpdate(this);
			}
		}

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 1,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = true,
		};

		public static SessionSyncState ParseState(CelesteNetBinaryReader r) {
			SessionSyncState state = new SessionSyncState {
				dead = r.ReadBoolean(),
				cassette = r.ReadBoolean(),
				heart = r.ReadBoolean(),
				heartEndsLevel = r.ReadBoolean(),
				heartPoem = r.ReadString(),
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
							SaveData.Instance.AddStrawberry(id, false);
							Session session = level.Session;
							session.DoNotLoad.Add(id);
							session.Strawberries.Add(id);
							// handle if the strawb is in the current room
							foreach (Entity e in Scene.Entities) {
								if (e is Strawberry strawb && strawb.ID.Equals(id)) {
									if (strawb.Follower.HasLeader) {
										strawb.Follower.Leader.LoseFollower(strawb.Follower);
									}
									if (!strawb.collected) {
										strawb.collected = true;
										Player player = EntityAs<Player>();
										strawb.Add(new Coroutine(strawb.CollectRoutine(player.StrawberryCollectIndex++)));
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
								cass.OnPlayer(EntityAs<Player>());
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

					// heart sync
					if (dss.heart && !level.Session.HeartGem && !levelEndTriggeredRemotely) {
						// This is basically just copied from HeartGem.RegisterAsCollected
						levelEndTriggeredRemotely = true;
						level.Session.HeartGem = true;
						int unlockedModes = SaveData.Instance.UnlockedModes;
						SaveData.Instance.RegisterHeartGem(level.Session.Area);
						if (!string.IsNullOrEmpty(dss.heartPoem)) {
							SaveData.Instance.RegisterPoemEntry(dss.heartPoem);
						}
						if (unlockedModes < 3 && SaveData.Instance.UnlockedModes >= 3) {
							level.Session.UnlockedCSide = true;
						}
						if (SaveData.Instance.TotalHeartGemsInVanilla >= 24) {
							Achievements.Register(Achievement.CSIDES);
						}
						Entity e = new Entity();
						e.Tag = Tags.FrozenUpdate;
						e.Add(new Coroutine(RemoteHeartCollectionRoutine(level, e, Entity as Player, level.Session.Area, dss.heartPoem, dss.heartEndsLevel)));
						level.Add(e);

						foreach (Entity heart in level.Tracker.GetEntities<HeartGem>()) {
							heart.RemoveSelf();
						}
					}
				}
			}
		}

		private IEnumerator RemoteHeartCollectionRoutine(Level level, Entity coroutineEnity, Player player, AreaKey area, string poemID, bool completeArea) {
			while (level.Transitioning) {
				yield return null;
			}

			// Setup
			player.Depth = Depths.FormationSequences;
			level.Frozen = true;
			level.CanRetry = false;
			level.FormationBackdrop.Display = true;

			// Immediate actions
			if (completeArea) {
				List<Strawberry> list = new List<Strawberry>();
				foreach (Follower follower in player.Leader.Followers) {
					if (follower.Entity is Strawberry) {
						list.Add(follower.Entity as Strawberry);
					}
				}
				foreach (Strawberry item in list) {
					item.OnCollect();
				}
			}

			// Animation
			string poemText = null;
			if (!string.IsNullOrEmpty(poemID)) {
				poemText = Dialog.Clean("poem_" + poemID);
			}
			Poem poem = new Poem(poemText, (int)area.Mode, string.IsNullOrEmpty(poemText) ? 1f : 0.6f);
			poem.Alpha = 0f;
			Scene.Add(poem);
			for (float t3 = 0f; t3 < 1f; t3 += Engine.RawDeltaTime) {
				poem.Alpha = Ease.CubeOut(t3);
				yield return null;
			}

			// Animation finished
			while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed) {
				yield return null;
			}
			//sfx.Source.Param("end", 1f);
			if (!completeArea) {
				level.FormationBackdrop.Display = false;
				for (float t3 = 0f; t3 < 1f; t3 += Engine.RawDeltaTime * 2f) {
					poem.Alpha = Ease.CubeIn(1f - t3);
					yield return null;
				}

				// Cleanup
				player.Depth = Depths.Player;
				level.Frozen = false;
				level.CanRetry = true;
				level.FormationBackdrop.Display = false;
				if (poem != null) {
					poem.RemoveSelf();
				}
				coroutineEnity.RemoveSelf();
			}
			else {
				FadeWipe fadeWipe = new FadeWipe(level, wipeIn: false);
				fadeWipe.Duration = 3.25f;
				yield return fadeWipe.Duration;
				level.CompleteArea(spotlightWipe: false, skipScreenWipe: true, skipCompleteScreen: false);
			}
		}

		public EntityID GetID() => GetIDStatic();

		public static EntityID GetIDStatic() => new EntityID(IDString, 99999);

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			lock (basicFlagsLock) {
				w.Write(deathPending);
				deathPending = false;
				w.Write(cassettePending);
				cassettePending = false;
				w.Write(heartPending);
				heartPending = false;
				w.Write(heartEndsLevel);
				w.Write(heartPoem ?? "");
				heartPoem = "";
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
