using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Celeste.ObjModel;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SessionGate")]
	class SessionGate : Solid {
		public int requireRole = -1;
		private Sprite sprite;
		private Shaker shaker;
		private bool open = false;
		private int closedHeight;
		private float drawHeight;
		private float drawHeightMoveSpeed;

		public SessionGate(EntityData data, Vector2 offset) : base(data.Position + offset, 8f, 48, true) {
			string spriteName = data.Attr("sprite", "default");
			requireRole = -1;
			if (int.TryParse(data.Attr("requiredRole", "-1"), out int parsedRole)) {
				requireRole = parsedRole;
			}
			Add(sprite = GFX.SpriteBank.Create("templegate_" + spriteName));
			sprite.X = base.Collider.Width / 2f;
			sprite.Play("idle");
			Add(shaker = new Shaker(on: false));
			Depth = -9000;
			open = false;
			closedHeight = 48;
			drawHeight = closedHeight;
		}

		public override void Awake(Scene scene) {
			base.Awake(scene);
			UpdateState();
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			CoopHelperModule.OnSessionInfoChanged += UpdateState;
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			CoopHelperModule.OnSessionInfoChanged -= UpdateState;
		}

		public void UpdateState() {
			bool inSession = CoopHelperModule.Session?.IsInCoopSession ?? false;
			bool shouldOpen = inSession && (requireRole < 0 || requireRole == CoopHelperModule.Session.SessionRole);
			if (shouldOpen && !open) {
				Open();
			}
			else if (!shouldOpen && open) {
				Close();
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
			if (height < Collider.Height) {
				Collider.Height = height;
				return;
			}
			float y = Y;
			int num = (int)Collider.Height;
			if (Collider.Height < 64f) {
				Y -= 64f - Collider.Height;
				Collider.Height = 64f;
			}
			try {
				MoveVExact(height - num);
			}
			catch(Exception) {
				// IDK why but MoveVExact will randomly crash sometimes????? idk just ignore it i guess
			}
			Y = y;
		}
	}
}
