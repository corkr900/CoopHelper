using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.Module;
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
		internal Vector2 OpenTarget;
		internal bool AnotherPlayerUsed;

		internal SyncedKey(Player player, EntityID id) : base(player, id) {
			PlayerCollider coll = Get<PlayerCollider>();
			if (coll != null) coll.OnCollide = OnPlayerOverride;
		}

		public SyncedKey(EntityData data, Vector2 offset) : base(data, offset, new EntityID(data.Level.Name, data.ID)) {
			PlayerCollider coll = Get<PlayerCollider>();
			if (coll != null) coll.OnCollide = OnPlayerOverride;
		}

		internal void OnRegisterUsed() {
			if (!AnotherPlayerUsed) EntityStateTracker.PostUpdate(this);
			CoopHelperModule.Session?.SyncedKeys?.Remove(ID);
		}

		private void OnPlayerOverride(Player player) {
			SceneAs<Level>().Particles.Emit(P_Collect, 10, Position, Vector2.One * 3f);
			Audio.Play("event:/game/general/key_get", Position);
			Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
			player.Leader.GainFollower(follower);
			Collidable = false;
			Session session = SceneAs<Level>().Session;
			session.DoNotLoad.Add(ID);
			session.UpdateLevelStartDashes();
			wiggler.Start();
			Depth = -1000000;
			if (nodes != null && nodes.Length >= 2) {
				Add(new Coroutine(NodeRoutine(player)));
			}
			CoopHelperModule.Session?.SyncedKeys?.Add(ID);
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
			ApplyStateRequiresPlayer = true,
		};

		public static SyncedKeyState ParseState(CelesteNetBinaryReader r) {
			return new SyncedKeyState() {
				Used = r.ReadBoolean(),
				Target = r.ReadVector2(),
			};
		}

		public static bool StaticHandler(EntityID id, object state) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Key: running static handler...");
			if (!(state is SyncedKeyState sks)) return false;
			if (!(Engine.Scene is Level level)) return false;
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Key: static handler processing...");
			Session session = level.Session;
			CoopHelperModuleSession coopSession = CoopHelperModule.Session;
			if (session == null ||  coopSession == null) return false;
			if (session.DoNotLoad.Contains(id)) {
				if (sks.Used && coopSession.SyncedKeys.Contains(id)) {
					coopSession.SyncedKeys.Remove(id);
				}
				Logger.Log(LogLevel.Debug, "Co-op Helper", $"Key: static handler exited condition 1");
				return true;
			}
			Player player = level.Tracker.GetEntity<Player>();
			if (player == null) {
				session.DoNotLoad.Add(id);
				coopSession.SyncedKeys.Add(id);
				Logger.Log(LogLevel.Debug, "Co-op Helper", $"Key: static handler exited condition 2");
			}
			else {
				session.DoNotLoad.Add(id);
				coopSession.SyncedKeys.Add(id);
				level.Add(new SyncedKey(player, id));
				Logger.Log(LogLevel.Debug, "Co-op Helper", $"Key: static handler exited condition 3");
			}
			return !sks.Used;
		}
		
		public void ApplyState(object state) {
			if (state is SyncedKeyState sks) {
				if (sks.Used) {
					AnotherPlayerUsed = true;
					if (!IsUsed) {
						Session session = SceneAs<Level>().Session;
						Collidable = false;
						if (!session.DoNotLoad.Contains(ID)) session.DoNotLoad.Add(ID);
						Depth = -1000000;
						sprite.Visible = false;
						if (follower?.HasLeader == true) {
							follower.Leader.LoseFollower(follower);
						}
						RegisterUsed();
						Add(new Coroutine(UseRoutine(sks.Target), true));
						EventInstance evt = Audio.Play("event:/game/03_resort/key_unlock");
						Alarm.Set(this, 2.7f, () => {
							RemoveSelf();
						}, Alarm.AlarmMode.Oneshot);
					}
				}
				else {
					OnPlayer(GetPlayer());
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
			w.Write(OpenTarget);
		}

		#endregion
	}

	public class SyncedKeyState {
		public bool Used;
		public Vector2 Target;
	}
}
