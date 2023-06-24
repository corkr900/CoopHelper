using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedCloud")]
	public class SyncedCloud : Cloud, ISynchronizable {
		private EntityID id;

		public SyncedCloud(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		public void UsedByOtherPlayer() {
			canRumble = true;
			speed = 180f;
			scale = new Vector2(1.3f, 0.7f);
			waiting = false;
			FMOD.Studio.EventInstance instance;
			if (fragile) {
				instance = Audio.Play("event:/game/04_cliffside/cloud_pink_boost", Position);
			}
			else {
				instance = Audio.Play("event:/game/04_cliffside/cloud_blue_boost", Position);
			}
			instance.setPitch(0.8f);
			instance.setVolume(0.8f);
		}

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
			Header = 26,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool used && used) {
				UsedByOtherPlayer();
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(true);
		}
	}
}
