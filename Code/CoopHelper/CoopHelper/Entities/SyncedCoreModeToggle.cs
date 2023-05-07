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

	[CustomEntity("corkr900CoopHelper/SyncedCoreModeToggle")]
	public class SyncedCoreModeToggle : CoreModeToggle, ISynchronizable {
		private EntityID id;
		private bool persistent;

		public SyncedCoreModeToggle(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			persistent = data.Bool("persistent");
		}

		internal bool UsableAndOffCooldown() {
			DynamicData dd = DynamicData.For(this);
			return dd.Get<bool>("Usable") && dd.Get<float>("cooldownTimer") <= 0f;
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
			Header = 5,
			Parser = ParseState,
			StaticHandler = StaticHandler,
			DiscardIfNoListener = false,
			DiscardDuplicates = true,
			Critical = false,
		};

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public static bool StaticHandler(EntityID id, object s) {
			if (s is SyncedCoreModeToggleState state && state.Persistent && Engine.Scene is Level level) {
				ApplyStateInternal(state, level);
				return true;
			}
			return false;
		}

		public void ApplyState(object state) {
			Level level = SceneAs<Level>();
			if (state is SyncedCoreModeToggleState newMode && level.CoreMode != newMode.Mode && newMode.Mode != Session.CoreModes.None) {
				ApplyStateInternal(newMode, level);
				DynamicData dd = DynamicData.For(this);
				dd.Set("cooldownTimer", 1f);
			}
		}

		private static void ApplyStateInternal(SyncedCoreModeToggleState state, Level level) {
			if (level.CoreMode != state.Mode) {
				level.Flash(Color.White * 0.15f, drawPlayerOver: true);
				level.CoreMode = state.Mode;
			}
			if (state.Persistent) {
				level.Session.CoreMode = level.CoreMode;
			}
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(SceneAs<Level>()?.CoreMode.ToString() ?? "");
			w.Write(persistent);
		}

		public static object ParseState(CelesteNetBinaryReader r) {
			Session.CoreModes mode;
			if (!Enum.TryParse(r.ReadString(), out mode)) {
				mode = Session.CoreModes.None;
			}
			bool persistent = r.ReadBoolean();
			return new SyncedCoreModeToggleState() {
				Mode = mode,
				Persistent = persistent,
			};
		}

	}

	public class SyncedCoreModeToggleState {
		public Session.CoreModes Mode;
		public bool Persistent;
	}
}
