using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.Triggers;
using Celeste.Mod.Entities;
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
	[CustomEntity("corkr900CoopHelper/SyncedSummitBackgroundManager")]
	public class SyncedSummitBackgroundManager : AscendManager, ISynchronizable {

		private class Fader : Entity {
			public float Fade;

			private AscendManager manager;

			public Fader(AscendManager manager) {
				this.manager = manager;
				base.Depth = -1000010;
			}

			public override void Render() {
				if (Fade > 0f) {
					Vector2 position = (base.Scene as Level).Camera.Position;
					Draw.Rect(position.X - 10f, position.Y - 10f, 340f, 200f, (manager.Dark ? Color.Black : Color.White) * Fade);
				}
			}
		}


		public EntityID id;
		private string cutscene;
		private bool dark;
		private string ambience;
		private Player player;
		private int otherPlayersInTrigger;

		private int PlayersNeeded {
			get {
				return (CoopHelperModule.Session?.IsInCoopSession == true) ?
					CoopHelperModule.Session.SessionMembers?.Count ?? 1 : 1;
			}
		}

		public SyncedSummitBackgroundManager(EntityData data, Vector2 offset) : base (data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			cutscene = data.Attr("cutscene");
			dark = data.Bool("dark");
			ambience = data.Attr("ambience");
		}

		internal IEnumerator RoutineOverride() {
			Level level = SceneAs<Level>();

			// idle until the player reaches the place
			do {
				yield return null;
				player = Scene.Tracker.GetEntity<Player>();
			} while (player == null || player.Y > Y);
			Streaks streaks = new Streaks(this);
			Scene.Add(streaks);
			if (!Dark) {
				Clouds clouds = new Clouds(this);
				Scene.Add(clouds);
			}

			// Stop the player and do the effects
			player.Sprite.Play("launch");
			player.Speed = Vector2.Zero;
			player.StateMachine.State = Player.StDummy;
			player.DummyGravity = false;
			player.DummyAutoAnimate = false;
			if (!string.IsNullOrWhiteSpace(ambience)) {
				if (ambience.Equals("null", StringComparison.InvariantCultureIgnoreCase)) {
					Audio.SetAmbience(null);
				}
				else {
					Audio.SetAmbience(SFX.EventnameByHandle(ambience));
				}
			}
			yield return new DynamicData(this).Invoke<IEnumerator>("FadeTo", 1f, Dark ? 2f : 0.8f);
			EntityStateTracker.PostUpdate(this);

			// Wait for all players
			if (!level.Session.GetFlag("CoopHelper_Debug") && otherPlayersInTrigger + 1 < PlayersNeeded) {
				WaitingForPlayersMessage message = new WaitingForPlayersMessage();
				Scene.Add(message);
				do {
					yield return null;
				} while (otherPlayersInTrigger + 1 < PlayersNeeded);
				message.RemoveSelf();
			}

			// Proceed with the cutscene
			if (!string.IsNullOrEmpty(cutscene)) {
				yield return 0.25f;
				CS07_Ascend cs = new CS07_Ascend(-1, cutscene, dark);
				level.Add(cs);
				yield return null;
				while (cs.Running) {
					yield return null;
				}
				player.Dashes = 0;  // The cutscene is hardcoded to give 2 dashes :(
			}
			else {
				yield return 0.5f;
			}

			// Fly off to the next place
			level.CanRetry = false;
			player.Sprite.Play("launch");
			Audio.Play("event:/char/madeline/summit_flytonext", player.Position);
			yield return 0.25f;
			Vector2 from2 = player.Position;
			for (float p2 = 0f; p2 < 1f; p2 += Engine.DeltaTime / 1f) {
				player.Position = Vector2.Lerp(from2, from2 + new Vector2(0f, 60f), Ease.CubeInOut(p2)) + Calc.Random.ShakeVector();
				Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
				yield return null;
			}
			Fader fader = new Fader(this);
			Scene.Add(fader);
			player.X = from2.X;
			from2 = player.Position;
			Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
			for (float p2 = 0f; p2 < 1f; p2 += Engine.DeltaTime / 0.5f) {
				float y = player.Y;
				player.Position = Vector2.Lerp(from2, from2 + new Vector2(0f, -160f), Ease.SineIn(p2));
				if (p2 == 0f || Calc.OnInterval(player.Y, y, 16f)) {
					level.Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, new Vector2(0f, -1f).Angle(), Color.White));
				}
				if (p2 >= 0.5f) {
					fader.Fade = (p2 - 0.5f) * 2f;
				}
				else {
					fader.Fade = 0f;
				}
				yield return null;
			}
			level.CanRetry = true;
			player.Y = level.Bounds.Top;
			player.SummitLaunch(player.X);
			player.DummyGravity = true;
			player.DummyAutoAnimate = true;
			level.NextTransitionDuration = 0.05f;
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
			Header = 22,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = true,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public EntityID GetID() => id;
		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(player != null);
		}

		public void ApplyState(object state) {
			if (state is bool playerEntered && playerEntered) {
				otherPlayersInTrigger++;
			}
		}
	}
}
