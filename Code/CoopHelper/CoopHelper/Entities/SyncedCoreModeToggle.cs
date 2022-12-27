using Celeste.Mod.CelesteNet;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
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

		public SyncedCoreModeToggle(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		internal bool UsableAndOffCooldown() {
			DynamicData dd = new DynamicData(this);
			return dd.Get<bool>("Usable") && dd.Get<float>("cooldownTimer") <= 0f;
		}

		public static int GetHeader() => 5;

		public static Session.CoreModes ParseState(CelesteNetBinaryReader r) {
			Session.CoreModes mode;
			Enum.TryParse(r.ReadString(), out mode);
			return mode;
		}

		public void ApplyState(object state) {
			Level level = SceneAs<Level>();
			if (state is Session.CoreModes newMode && level.CoreMode != newMode) {
				level.CoreMode = newMode;
				DynamicData dd = new DynamicData(this);
				if (dd.Get<bool>("persistent")) level.Session.CoreMode = level.CoreMode;
				level.Flash(Color.White * 0.15f, drawPlayerOver: true);
				dd.Set("cooldownTimer", 1f);
			}
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(SceneAs<Level>().CoreMode.ToString());
		}
	}
}
