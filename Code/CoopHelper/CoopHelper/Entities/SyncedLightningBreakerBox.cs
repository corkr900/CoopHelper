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
				PersistentBroken = r.ReadBoolean(),
				HealthLost = r.ReadInt32(),
				Direction = r.ReadVector2(),
				MusicStoreInSession = r.ReadBoolean(),
				Music = r.ReadString(),
				MusicProgress = r.ReadInt32(),
			};
		}

		public static bool StaticHandler(object state) {
			if (state is SyncedLightningBreakerBoxState slbbs && slbbs.PersistentBroken) {
				Level level = (Engine.Scene as Level);
				if (level?.Session == null) return false;
				Session session = level.Session;

				Audio.Play("event:/new_content/game/10_farewell/fusebox_hit_2");
				Input.Rumble(RumbleStrength.Strong, RumbleLength.Long);
				session.SetFlag("disable_lightning");
				if (slbbs.MusicStoreInSession) {
					if (!string.IsNullOrEmpty(slbbs.Music)) {
						session.Audio.Music.Event = SFX.EventnameByHandle(slbbs.Music);
					}
					if (slbbs.MusicProgress >= 0) {
						session.Audio.Music.SetProgress(slbbs.MusicProgress);
					}
					session.Audio.Apply(forceSixteenthNoteHack: false);
				}
				else {
					if (!string.IsNullOrEmpty(slbbs.Music)) {
						Audio.SetMusic(SFX.EventnameByHandle(slbbs.Music), startPlaying: false);
					}
					if (slbbs.MusicProgress >= 0) {
						Audio.SetMusicParam("progress", slbbs.MusicProgress);
					}
					if (!string.IsNullOrEmpty(slbbs.Music) && Audio.CurrentMusicEventInstance != null) {
						Audio.CurrentMusicEventInstance.start();
					}
				}
				Entity coroutineEntity = new Entity() {
					Tag = Tags.Persistent,
				};
				level.Add(coroutineEntity);
				coroutineEntity.Add(new Coroutine(Lightning.RemoveRoutine(level)));
			}
			return false;
		}

		public void ApplyState(object state) {
			if (state is SyncedLightningBreakerBoxState st) {
				// Handle multiple health lost at once
				if (st.HealthLost > 1) {
					DynamicData dd = new DynamicData(this);
					int health = dd.Get<int>("health");
					dd.Set("health", health - st.HealthLost + 1);
				}
				// I don't want to duplicate the Dashed function or IL Hook it...
				// If I don't give Dashed a Player it crashes, but all it does is restore dashes.
				// But i don't want it to restore dashes here so i have to undo any changes to the dash count
				Player player = Scene.Tracker.GetEntity<Player>();
				int dashCtBefore = player.Dashes;
				Dashed(player, st.Direction);
				player.Dashes = dashCtBefore; 
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			DynamicData dd = new DynamicData(this);
			lock (healthDiffLock) {
				w.Write(dd.Get<bool>("flag") && dd.Get<int>("health") <= 0);
				w.Write(healthLost);
				w.Write(lastDashedDir);
				healthLost = 0;
				w.Write(dd.Get<bool>("musicStoreInSession"));
				w.Write(dd.Get<string>("music") ?? "");
				w.Write(dd.Get<int>("musicProgress"));
			}
		}
	}

	public class SyncedLightningBreakerBoxState {
		public bool PersistentBroken;
		public int HealthLost;
		public Vector2 Direction;

		public bool MusicStoreInSession;
		public string Music;
		public int MusicProgress;
	}
}
