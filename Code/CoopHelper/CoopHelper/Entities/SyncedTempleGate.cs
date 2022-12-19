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

	[CustomEntity("corkr900CoopHelper/SyncedTempleGate")]
	public class SyncedTempleGate : Solid, ISynchronizable {

		public SyncedTempleGate(EntityData data, Vector2 offset, string levelID)
			: base(data.Position + offset, 8f, data.Height, true) {
			string spriteName = data.Attr("sprite", "default");
			requireRole = -1;
			if (int.TryParse(data.Attr("requiredRole", "-1"), out int parsedRole)) {
				requireRole = parsedRole;
			}
			Add(sprite = GFX.SpriteBank.Create("templegate_" + spriteName));  // TODO do better
			sprite.X = base.Collider.Width / 2f;
			sprite.Play("idle");
			Add(shaker = new Shaker(on: false));
			Depth = -9000;
			open = false;
			closedHeight = 48;
			drawHeight = closedHeight;
			id = new EntityID(data.Level.Name, data.ID);
		}

		public int requireRole = -1;
		private Sprite sprite;
		private Shaker shaker;
		private bool open = false;
		private int closedHeight;
		private float drawHeight;
		private float drawHeightMoveSpeed;
		private EntityID id;


		public override void Awake(Scene scene) {
			base.Awake(scene);
		}

		public override void Added(Scene scene) {
			base.Added(scene);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
		}

		public void UpdateState(bool shouldOpen, bool trackUpdate = true) {
			if (shouldOpen && !open) {
				Open();
				if (trackUpdate) EntityStateTracker.PostUpdate(this);
			}
			else if (!shouldOpen && open) {
				Close();
				if (trackUpdate) EntityStateTracker.PostUpdate(this);
			}
		}

		public override void Update() {
			base.Update();
			float num = open ? 4f : Math.Max(4f, closedHeight);
			if (drawHeight != num) {
				drawHeight = Calc.Approach(drawHeight, num, drawHeightMoveSpeed * Engine.DeltaTime);
			}
		}

		public override void Render() {
			Vector2 vector = new Vector2(Math.Sign(shaker.Value.X), 0f);
			Draw.Rect(base.X - 2f, base.Y - 8f, 14f, 10f, Color.Black);
			sprite.DrawSubrect(Vector2.Zero + vector, new Rectangle(0, (int)(sprite.Height - drawHeight), (int)sprite.Width, (int)drawHeight));
		}

		private void Open() {
			Audio.Play("event:/game/05_mirror_temple/gate_main_open", Position);
			drawHeightMoveSpeed = 200f;
			shaker.ShakeFor(0.2f, removeOnFinish: false);
			SetHeight(0);
			sprite.Play("open");
			open = true;
		}

		private void Close() {
			Audio.Play("event:/game/05_mirror_temple/gate_main_close", Position);
			drawHeightMoveSpeed = 300f;
			shaker.ShakeFor(0.2f, removeOnFinish: false);
			SetHeight(closedHeight);
			sprite.Play("hit");
			open = false;
		}

		private void SetHeight(int height) {
			if ((float)height < base.Collider.Height) {
				base.Collider.Height = height;
				return;
			}
			float y = base.Y;
			int num = (int)base.Collider.Height;
			if (base.Collider.Height < 64f) {
				base.Y -= 64f - base.Collider.Height;
				base.Collider.Height = 64f;
			}
			MoveVExact(height - num);
			base.Y = y;
		}

		#region ISynchronizable implementation

		public static int GetHeader() => 3;

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(open);
		}

		public static bool ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool shouldOpen) {
				UpdateState(shouldOpen, false);
			}
		}

		#endregion
	}
}
