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
	[CustomEntity("corkr900CoopHelper/SyncedDashSwitch")]
	public class SyncedDashSwitch : DashSwitch, ISynchronizable {
		private static Sides GetSide(EntityData data) {
			switch (data.Attr("side", "Left")) {
				default:
				case "Left":
					return Sides.Left;
				case "Right":
					return Sides.Right;
				case "Down":
					return Sides.Down;
				case "Up":
					return Sides.Up;
			}
		}

		public SyncedDashSwitch(EntityData data, Vector2 offset)
			: base(data.Position + offset, GetSide(data), data.Bool("persistent"), data.Bool("allGates"),
				  new EntityID(data.Level.Name, data.ID), data.Attr("sprite", "default")) {
			id = new EntityID(data.Level.Name, data.ID);
			OnDashCollide = OnDashedOverride;
		}

		EntityID id;

		public DashCollisionResults OnDashedOverride(Player player, Vector2 direction) {
			DynamicData dd = new DynamicData(this);
			if (!dd.Get<bool>("pressed") && direction == dd.Get<Vector2>("pressDirection")) {
				EntityStateTracker.PostUpdate(this);
			}
			return OnDashed(player, direction);
		}

		#region These 3 overrides MUST be defined for synced entities/triggers

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

		#endregion

		#region ISynchronizable implementation

		public static int GetHeader() => 4;

		public EntityID GetID() => id;

		public static bool ParseState(CelesteNetBinaryReader r) {
			bool pressed = r.ReadBoolean();
			return pressed;
		}

		public void ApplyState(object state) {
			DynamicData dd = new DynamicData(this);
			Vector2 dir = dd.Get<Vector2>("pressDirection");
			OnDashed(null, dir);
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			DynamicData dd = new DynamicData(this);
			bool pressed = dd.Get<bool>("pressed");
			w.Write(pressed);
		}

		#endregion
	}
}
