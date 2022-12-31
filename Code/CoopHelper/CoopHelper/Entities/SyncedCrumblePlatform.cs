using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedCrumbleBlocks")]
	public class SyncedCrumblePlatform : Solid, ISynchronizable {

		private List<Image> images;
		private List<Image> outline;
		private List<Coroutine> falls;
		private List<int> fallOrder;
		private ShakerList shaker;
		private LightOcclude occluder;
		private Coroutine outlineFader;
		public string OverrideTexture;
		private EntityID id;
		private bool onTop = false;
		private bool otherPlayerTriggered = false;

		private float shakeTimeSide = 1.0f;
		private float shakeTimeTop = 0.6f;
		private float respawnDelay = 2.0f;
		private bool breakOnJump = true;

		public SyncedCrumblePlatform(EntityData data, Vector2 offset)
			: base(data.Position + offset, data.Width, 8f, false) {
			EnableAssistModeChecks = false;
			id = new EntityID(data.Level.Name, data.ID);
			shakeTimeSide = data.Float("shakeTimeSide", 1.0f);
			shakeTimeTop = data.Float("shakeTimeTop", 0.6f);
			respawnDelay = data.Float("respawnDelay", 2.0f);
			breakOnJump = data.Bool("breakOnJump", true);
		}

		public override void Added(Scene scene) {
			AreaData areaData = AreaData.Get(scene);
			string crumbleBlock = areaData.CrumbleBlock;
			if (OverrideTexture != null) {
				areaData.CrumbleBlock = OverrideTexture;
			}
			base.Added(scene);
			MTexture mTexture = GFX.Game["objects/crumbleBlock/outline"];
			outline = new List<Image>();
			if (base.Width <= 8f) {
				Image image = new Image(mTexture.GetSubtexture(24, 0, 8, 8));
				image.Color = Color.White * 0f;
				Add(image);
				outline.Add(image);
			}
			else {
				for (int i = 0; (float)i < base.Width; i += 8) {
					int num = ((i != 0) ? ((i > 0 && (float)i < base.Width - 8f) ? 1 : 2) : 0);
					Image image2 = new Image(mTexture.GetSubtexture(num * 8, 0, 8, 8));
					image2.Position = new Vector2(i, 0f);
					image2.Color = Color.White * 0f;
					Add(image2);
					outline.Add(image2);
				}
			}
			Add(outlineFader = new Coroutine());
			outlineFader.RemoveOnComplete = false;
			images = new List<Image>();
			falls = new List<Coroutine>();
			fallOrder = new List<int>();
			MTexture mTexture2 = GFX.Game["objects/crumbleBlock/" + AreaData.Get(scene).CrumbleBlock];
			for (int j = 0; (float)j < base.Width; j += 8) {
				int num2 = (int)((Math.Abs(base.X) + (float)j) / 8f) % 4;
				Image image3 = new Image(mTexture2.GetSubtexture(num2 * 8, 0, 8, 8));
				image3.Position = new Vector2(4 + j, 4f);
				image3.CenterOrigin();
				Add(image3);
				images.Add(image3);
				Coroutine coroutine = new Coroutine();
				coroutine.RemoveOnComplete = false;
				falls.Add(coroutine);
				Add(coroutine);
				fallOrder.Add(j / 8);
			}
			fallOrder.Shuffle();
			Add(new Coroutine(Sequence()));
			Add(shaker = new ShakerList(images.Count, on: false, delegate (Vector2[] v) {
				for (int k = 0; k < images.Count; k++) {
					images[k].Position = new Vector2(4 + k * 8, 4f) + v[k];
				}
			}));
			Add(occluder = new LightOcclude(0.2f));
			areaData.CrumbleBlock = crumbleBlock;
			EntityStateTracker.AddListener(this);
		}

		private IEnumerator Sequence() {
			while (true) {
				if (GetPlayerOnTop() != null) {
					onTop = true;
					EntityStateTracker.PostUpdate(this);
				}
				else if (GetPlayerClimbing() != null) {
					onTop = false;
					EntityStateTracker.PostUpdate(this);
				}
				else if (otherPlayerTriggered) {
					yield return null;
					continue;
				}

				Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
				Audio.Play("event:/game/general/platform_disintegrate", Center);
				float timer = onTop ? shakeTimeTop : shakeTimeSide;
				shaker.ShakeFor(timer, removeOnFinish: false);
				foreach (Image image in images) {
					SceneAs<Level>().Particles.Emit(CrumblePlatform.P_Crumble, 2, Position + image.Position + new Vector2(0f, 2f), Vector2.One * 3f);
				}
				while (timer > 0f && (otherPlayerTriggered || !onTop || !breakOnJump || GetPlayerOnTop() != null)) {
					yield return null;
					timer -= Engine.DeltaTime;
				}
				outlineFader.Replace(OutlineFade(1f));
				occluder.Visible = false;
				Collidable = false;
				float num = 0.05f;
				for (int j = 0; j < 4; j++) {
					for (int k = 0; k < images.Count; k++) {
						if (k % 4 - j == 0) {
							falls[k].Replace(TileOut(images[fallOrder[k]], num * (float)j));
						}
					}
				}

				yield return respawnDelay / 2;
				// reset the semaphore a little early to effectively buffer incoming updates
				otherPlayerTriggered = false;
				yield return respawnDelay / 2;

				while (CollideCheck<Actor>() || CollideCheck<Solid>()) {
					yield return null;
				}
				outlineFader.Replace(OutlineFade(0f));
				occluder.Visible = true;
				Collidable = true;
				for (int l = 0; l < 4; l++) {
					for (int m = 0; m < images.Count; m++) {
						if (m % 4 - l == 0) {
							falls[m].Replace(TileIn(m, images[fallOrder[m]], 0.05f * (float)l));
						}
					}
				}
			}
		}

		private IEnumerator OutlineFade(float to) {
			float from = 1f - to;
			for (float t = 0f; t < 1f; t += Engine.DeltaTime * 2f) {
				Color color = Color.White * (from + (to - from) * Ease.CubeInOut(t));
				foreach (Image item in outline) {
					item.Color = color;
				}
				yield return null;
			}
		}

		private IEnumerator TileOut(Image img, float delay) {
			img.Color = Color.Gray;
			yield return delay;
			float distance = (img.X * 7f % 3f + 1f) * 12f;
			Vector2 from = img.Position;
			for (float time = 0f; time < 1f; time += Engine.DeltaTime / 0.4f) {
				yield return null;
				img.Position = from + Vector2.UnitY * Ease.CubeIn(time) * distance;
				img.Color = Color.Gray * (1f - time);
				img.Scale = Vector2.One * (1f - time * 0.5f);
			}
			img.Visible = false;
		}

		private IEnumerator TileIn(int index, Image img, float delay) {
			yield return delay;
			Audio.Play("event:/game/general/platform_return", Center);
			img.Visible = true;
			img.Color = Color.White;
			img.Position = new Vector2(index * 8 + 4, 4f);
			for (float time = 0f; time < 1f; time += Engine.DeltaTime / 0.25f) {
				yield return null;
				img.Scale = Vector2.One * (1f + Ease.BounceOut(1f - time) * 0.2f);
			}
			img.Scale = Vector2.One;
		}






		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public static int GetHeader() => 11;

		public static bool ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool ontop) {
				otherPlayerTriggered = true;
				onTop = ontop;
			}
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(onTop);
		}
	}
}
