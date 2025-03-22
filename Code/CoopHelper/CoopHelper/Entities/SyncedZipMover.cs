using Celeste.Mod.CoopHelper.Infrastructure;
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
	[CustomEntity("corkr900CoopHelper/SyncedZipMover")]
	public class SyncedZipMover : Solid, ISynchronizable {

		public class SyncedZipMoverPathRenderer : Entity {
			public SyncedZipMover zipMover;
			private MTexture cog;
			private Vector2 from;
			private Vector2 to;
			private Vector2 sparkAdd;
			private float sparkDirFromA;
			private float sparkDirFromB;
			private float sparkDirToA;
			private float sparkDirToB;

			public SyncedZipMoverPathRenderer(SyncedZipMover szm) {
				base.Depth = 5000;
				this.zipMover = szm;
				from = this.zipMover.start + new Vector2(this.zipMover.Width / 2f, this.zipMover.Height / 2f);
				to = this.zipMover.target + new Vector2(this.zipMover.Width / 2f, this.zipMover.Height / 2f);
				sparkAdd = (from - to).SafeNormalize(5f).Perpendicular();
				float num = (from - to).Angle();
				sparkDirFromA = num + (float)Math.PI / 8f;
				sparkDirFromB = num - (float)Math.PI / 8f;
				sparkDirToA = num + (float)Math.PI - (float)Math.PI / 8f;
				sparkDirToB = num + (float)Math.PI + (float)Math.PI / 8f;
				if (szm.theme == ZipMover.Themes.Moon) {
					cog = GFX.Game["objects/zipmover/moon/cog"];
				}
				else {
					cog = GFX.Game["objects/zipmover/cog"];
				}
			}

			public void CreateSparks() {
				SceneAs<Level>()?.ParticlesBG?.Emit(ZipMover.P_Sparks, from + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromA);
				SceneAs<Level>()?.ParticlesBG.Emit(ZipMover.P_Sparks, from - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromB);
				SceneAs<Level>().ParticlesBG?.Emit(ZipMover.P_Sparks, to + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToA);
				SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, to - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToB);
			}

			public override void Render() {
				DrawCogs(Vector2.UnitY, Color.Black);
				DrawCogs(Vector2.Zero);
				if (zipMover.drawBlackBorder) {
					Draw.Rect(new Rectangle((int)(zipMover.X + zipMover.Shake.X - 1f), (int)(zipMover.Y + zipMover.Shake.Y - 1f), (int)zipMover.Width + 2, (int)zipMover.Height + 2), Color.Black);
				}
			}

			private void DrawCogs(Vector2 offset, Color? colorOverride = null) {
				Vector2 vector = (to - from).SafeNormalize();
				Vector2 vector2 = vector.Perpendicular() * 3f;
				Vector2 vector3 = -vector.Perpendicular() * 4f;
				float rotation = zipMover.percent * (float)Math.PI * 2f;
				Draw.Line(from + vector2 + offset, to + vector2 + offset, colorOverride.HasValue ? colorOverride.Value : ropeColor);
				Draw.Line(from + vector3 + offset, to + vector3 + offset, colorOverride.HasValue ? colorOverride.Value : ropeColor);
				for (float num = 4f - zipMover.percent * (float)Math.PI * 8f % 4f; num < (to - from).Length(); num += 4f) {
					Vector2 vector4 = from + vector2 + vector.Perpendicular() + vector * num;
					Vector2 vector5 = to + vector3 - vector * num;
					Draw.Line(vector4 + offset, vector4 + vector * 2f + offset, colorOverride.HasValue ? colorOverride.Value : ropeLightColor);
					Draw.Line(vector5 + offset, vector5 - vector * 2f + offset, colorOverride.HasValue ? colorOverride.Value : ropeLightColor);
				}
				cog.DrawCentered(from + offset, colorOverride.HasValue ? colorOverride.Value : Color.White, 1f, rotation);
				cog.DrawCentered(to + offset, colorOverride.HasValue ? colorOverride.Value : Color.White, 1f, rotation);
			}
		}

		private ZipMover.Themes theme;
		private MTexture[,] edges = new MTexture[3, 3];
		private Sprite streetlight;
		private BloomPoint bloom;
		private SyncedZipMoverPathRenderer pathRenderer;
		private List<MTexture> innerCogs;
		private MTexture temp = new MTexture();
		private bool drawBlackBorder;
		private Vector2 start;
		private Vector2 target;
		private float percent;
		private static readonly Color ropeColor = Calc.HexToColor("663931");
		private static readonly Color ropeLightColor = Calc.HexToColor("9b6157");
		private SoundSource sfx = new SoundSource();

		public enum State {
			IdleStart = 0,
			MovingForward = 1,
			StopEnd = 2,
			IdleEnd = 3,
			MovingBack = 4,
			StopStart = 5,
		}
		private State state = State.IdleStart;
		private float moveTimeForward = 0.5f;
		private float moveTimeReverse = 2.0f;
		private float stopTimeStart = 0.5f;
		private float stopTimeEnd = 0.5f;
		private bool toggleMode = false;
		private EntityID id;

		private SyncedZipMover(Vector2 position, int width, int height, Vector2 target, ZipMover.Themes theme)
			: base(position, width, height, safe: false) {
			base.Depth = -9999;
			start = Position;
			this.target = target;
			this.theme = theme;
			Add(new Coroutine(Sequence()));
			Add(new LightOcclude());
			string path;
			string id;
			string key;
			if (theme == ZipMover.Themes.Moon) {
				path = "objects/zipmover/moon/light";
				id = "objects/zipmover/moon/block";
				key = "objects/zipmover/moon/innercog";
				drawBlackBorder = false;
			}
			else {
				path = "objects/zipmover/light";
				id = "objects/zipmover/block";
				key = "objects/zipmover/innercog";
				drawBlackBorder = true;
			}
			innerCogs = GFX.Game.GetAtlasSubtextures(key);
			Add(streetlight = new Sprite(GFX.Game, path));
			streetlight.Add("frames", "", 1f);
			streetlight.Play("frames");
			streetlight.Active = false;
			streetlight.SetAnimationFrame(1);
			streetlight.Position = new Vector2(base.Width / 2f - streetlight.Width / 2f, 0f);
			Add(bloom = new BloomPoint(1f, 6f));
			bloom.Position = new Vector2(base.Width / 2f, 4f);
			for (int i = 0; i < 3; i++) {
				for (int j = 0; j < 3; j++) {
					edges[i, j] = GFX.Game[id].GetSubtexture(i * 8, j * 8, 8, 8);
				}
			}
			SurfaceSoundIndex = 7;
			sfx.Position = new Vector2(base.Width, base.Height) / 2f;
			Add(sfx);
		}

		public SyncedZipMover(EntityData data, Vector2 offset)
			: this(data.Position + offset, data.Width, data.Height, data.Nodes[0] + offset, data.Enum("theme", ZipMover.Themes.Normal)) {
			id = new EntityID(data.Level.Name, data.ID);
			toggleMode = data.Bool("noReturn", false);
			moveTimeForward = Calc.Max(0.05f, data.Float("moveTimeForward", 0.5f));
			moveTimeReverse = Calc.Max(0.05f, data.Float("moveTimeReverse", toggleMode ? 0.5f : 2.0f));
			stopTimeStart = Calc.Max(0.05f, data.Float("stopTimeStart", 0.5f));
			stopTimeEnd = Calc.Max(0.05f, data.Float("stopTimeEnd", 0.5f));
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			scene.Add(pathRenderer = new SyncedZipMoverPathRenderer(this));
			EntityStateTracker.AddListener(this, false);
		}

		public override void Removed(Scene scene) {
			scene.Remove(pathRenderer);
			pathRenderer = null;
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Update() {
			base.Update();
			bloom.Y = streetlight.CurrentAnimationFrame * 3;
		}

		public override void Render() {
			Vector2 position = Position;
			Position += base.Shake;
			Draw.Rect(base.X + 1f, base.Y + 1f, base.Width - 2f, base.Height - 2f, Color.Black);
			int num = 1;
			float num2 = 0f;
			int count = innerCogs.Count;
			for (int i = 4; (float)i <= base.Height - 4f; i += 8) {
				int num3 = num;
				for (int j = 4; (float)j <= base.Width - 4f; j += 8) {
					int index = (int)(mod((num2 + (float)num * percent * (float)Math.PI * 4f) / ((float)Math.PI / 2f), 1f) * (float)count);
					MTexture mTexture = innerCogs[index];
					Rectangle rectangle = new Rectangle(0, 0, mTexture.Width, mTexture.Height);
					Vector2 zero = Vector2.Zero;
					if (j <= 4) {
						zero.X = 2f;
						rectangle.X = 2;
						rectangle.Width -= 2;
					}
					else if ((float)j >= base.Width - 4f) {
						zero.X = -2f;
						rectangle.Width -= 2;
					}
					if (i <= 4) {
						zero.Y = 2f;
						rectangle.Y = 2;
						rectangle.Height -= 2;
					}
					else if ((float)i >= base.Height - 4f) {
						zero.Y = -2f;
						rectangle.Height -= 2;
					}
					mTexture = mTexture.GetSubtexture(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, temp);
					mTexture.DrawCentered(Position + new Vector2(j, i) + zero, Color.White * ((num < 0) ? 0.5f : 1f));
					num = -num;
					num2 += (float)Math.PI / 3f;
				}
				if (num3 == num) {
					num = -num;
				}
			}
			for (int k = 0; (float)k < base.Width / 8f; k++) {
				for (int l = 0; (float)l < base.Height / 8f; l++) {
					int num4 = ((k != 0) ? (((float)k != base.Width / 8f - 1f) ? 1 : 2) : 0);
					int num5 = ((l != 0) ? (((float)l != base.Height / 8f - 1f) ? 1 : 2) : 0);
					if (num4 != 1 || num5 != 1) {
						edges[num4, num5].Draw(new Vector2(base.X + (float)(k * 8), base.Y + (float)(l * 8)));
					}
				}
			}
			base.Render();
			Position = position;
		}

		private void ScrapeParticlesCheck(Vector2 to) {
			if (!base.Scene.OnInterval(0.03f)) {
				return;
			}
			bool flag = to.Y != base.ExactPosition.Y;
			bool flag2 = to.X != base.ExactPosition.X;
			if (flag && !flag2) {
				int num = Math.Sign(to.Y - base.ExactPosition.Y);
				Vector2 vector = ((num != 1) ? base.TopLeft : base.BottomLeft);
				int num2 = 4;
				if (num == 1) {
					num2 = Math.Min((int)base.Height - 12, 20);
				}
				int num3 = (int)base.Height;
				if (num == -1) {
					num3 = Math.Max(16, (int)base.Height - 16);
				}
				if (base.Scene.CollideCheck<Solid>(vector + new Vector2(-2f, num * -2))) {
					for (int i = num2; i < num3; i += 8) {
						SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, base.TopLeft + new Vector2(0f, (float)i + (float)num * 2f), (num == 1) ? (-(float)Math.PI / 4f) : ((float)Math.PI / 4f));
					}
				}
				if (base.Scene.CollideCheck<Solid>(vector + new Vector2(base.Width + 2f, num * -2))) {
					for (int j = num2; j < num3; j += 8) {
						SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, base.TopRight + new Vector2(-1f, (float)j + (float)num * 2f), (num == 1) ? ((float)Math.PI * -3f / 4f) : ((float)Math.PI * 3f / 4f));
					}
				}
			}
			else {
				if (!flag2 || flag) {
					return;
				}
				int num4 = Math.Sign(to.X - base.ExactPosition.X);
				Vector2 vector2 = ((num4 != 1) ? base.TopLeft : base.TopRight);
				int num5 = 4;
				if (num4 == 1) {
					num5 = Math.Min((int)base.Width - 12, 20);
				}
				int num6 = (int)base.Width;
				if (num4 == -1) {
					num6 = Math.Max(16, (int)base.Width - 16);
				}
				if (base.Scene.CollideCheck<Solid>(vector2 + new Vector2(num4 * -2, -2f))) {
					for (int k = num5; k < num6; k += 8) {
						SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, base.TopLeft + new Vector2((float)k + (float)num4 * 2f, -1f), (num4 == 1) ? ((float)Math.PI * 3f / 4f) : ((float)Math.PI / 4f));
					}
				}
				if (base.Scene.CollideCheck<Solid>(vector2 + new Vector2(num4 * -2, base.Height + 2f))) {
					for (int l = num5; l < num6; l += 8) {
						SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, base.BottomLeft + new Vector2((float)l + (float)num4 * 2f, 0f), (num4 == 1) ? ((float)Math.PI * -3f / 4f) : (-(float)Math.PI / 4f));
					}
				}
			}
		}

		private void PlaySound(State st) {
			string baseAudio = theme == ZipMover.Themes.Normal ?
				"event:/game/01_forsaken_city/zip_mover" : "event:/new_content/game/10_farewell/zip_mover";
			bool isAttack = st == State.MovingForward || (toggleMode && st == State.MovingBack);
			bool isReturn = st == State.MovingBack && !toggleMode;
			float start = isAttack ? 0f : isReturn ? 1f : 3f;
			float end = isAttack ? 1f : isReturn ? 3f : 4.3f;
			// if not attack or end, assume it's the stop at start position
			sfx.Play(baseAudio);
			EventInstance ei = sfx.instance;
			if (ei == null) return;
			ei.setTimelinePosition((int)(start * 1000));
			Add(new Coroutine(EndSoundAtTime(ei, end)));
		}

		private IEnumerator EndSoundAtTime(EventInstance ei, float endTime) {
			PLAYBACK_STATE pbstate;
			while (true) {
				yield return null;
				ei.getPlaybackState(out pbstate);
				if (pbstate == PLAYBACK_STATE.STOPPED || pbstate == PLAYBACK_STATE.STOPPING) break;
				int position;
				ei.getTimelinePosition(out position);
				if (position >= endTime * 1000) {
					ei.stop(STOP_MODE.ALLOWFADEOUT);
					break;
				}
			}
		}

		private IEnumerator Sequence() {
			Vector2 start = Position;
			state = State.StopStart;
			while (true) {
				// idle start
				if (state == State.StopStart) {
					state = State.IdleStart;
					streetlight.SetAnimationFrame(1);
					while (state == State.IdleStart && !HasPlayerRider()) {
						yield return null;
					}
					if (state == State.IdleStart) {
						EntityStateTracker.PostUpdate(this);
					}
				}

				// Move forward
				if (state == State.IdleStart || state == State.MovingForward) {
					state = State.MovingForward;
					PlaySound(State.MovingForward);
					Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
					StartShaking(0.1f);
					yield return 0.1f;
					streetlight.SetAnimationFrame(3);
					StopPlayerRunIntoAnimation = false;
					float timeLerp = 0f;
					while (timeLerp < 1f) {
						yield return null;
						timeLerp = Calc.Approach(timeLerp, 1f, Engine.DeltaTime / moveTimeForward);
						if (state != State.MovingForward) {
							MoveTo(target);
							break;
						}
						percent = Ease.SineIn(timeLerp);
						Vector2 vector = Vector2.Lerp(start, target, percent);
						ScrapeParticlesCheck(vector);
						if (Scene.OnInterval(0.1f)) {
							pathRenderer.CreateSparks();
						}
						MoveTo(vector);
					}
				}

				// Stop at end node
				if (state == State.MovingForward) {
					state = State.StopEnd;
					StartShaking(0.2f);
					Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
					SceneAs<Level>().Shake();
					StopPlayerRunIntoAnimation = true;
					if (toggleMode) {
						streetlight.SetAnimationFrame(2);
					}
					float timeLerp = 0f;
					while (timeLerp < stopTimeEnd && state == State.StopEnd) {
						timeLerp += Engine.DeltaTime;
						yield return null;
					}
				}

				// idle end
				if (toggleMode && state == State.StopEnd) {
					state = State.IdleEnd;
					streetlight.SetAnimationFrame(1);
					while (state == State.IdleEnd && !HasPlayerRider()) {
						yield return null;
					}
					if (state == State.IdleEnd) {
						EntityStateTracker.PostUpdate(this);
					}
				}

				// Move Reverse
				if ((!toggleMode && state == State.StopEnd) || state == State.IdleEnd || state == State.MovingBack) {
					state = State.MovingBack;
					PlaySound(State.MovingBack);
					if (toggleMode) {
						Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
						StartShaking(0.1f);
						yield return 0.1f;
					}
					StopPlayerRunIntoAnimation = false;
					streetlight.SetAnimationFrame(toggleMode ? 3 : 2);
					float timeLerp = 0f;
					while (timeLerp < 1f) {
						yield return null;
						timeLerp = Calc.Approach(timeLerp, 1f, Engine.DeltaTime / moveTimeReverse);
						if (state != State.MovingBack) {
							MoveTo(start);
							break;
						}
						percent = 1f - Ease.SineIn(timeLerp);
						Vector2 position = Vector2.Lerp(target, start, Ease.SineIn(timeLerp));
						MoveTo(position);
					}
				}

				// stop at start node
				if (state == State.MovingBack) {
					state = State.StopStart;
					if (!toggleMode) PlaySound(State.StopStart);
					StopPlayerRunIntoAnimation = true;
					StartShaking(0.2f);
					streetlight.SetAnimationFrame(2);
					float timeLerp = 0f;
					while (timeLerp < stopTimeStart && state == State.StopStart) {
						timeLerp += Engine.DeltaTime;
						yield return null;
					}
				}
			}
		}

		private float mod(float x, float m) {
			return (x % m + m) % m;
		}

		#region Synchronization

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 8,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static object ParseState(CelesteNet.CelesteNetBinaryReader r) {
			State s;
			Enum.TryParse(r.ReadString(), out s);
			return s;
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNet.CelesteNetBinaryWriter w) {
			w.Write(state.ToString() ?? "");
		}

		public void ApplyState(object newstate) {
			if (newstate is State st) {
				state = st;
			}
		}

		#endregion
	}
}
