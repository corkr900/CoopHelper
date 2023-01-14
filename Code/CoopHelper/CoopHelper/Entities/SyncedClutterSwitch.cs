using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
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
	[CustomEntity("corkr900CoopHelper/SyncedClutterSwitch")]
	public class SyncedClutterSwitch : ClutterSwitch, ISynchronizable {

		private EntityID id;

		public SyncedClutterSwitch(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			DashCollision orig_OnDashCollide = OnDashCollide;
			OnDashCollide = (Player player, Vector2 direction) => {
				if (!new DynamicData(this).Get<bool>("pressed") && direction == Vector2.UnitY) {
					EntityStateTracker.PostUpdate(this);
				}
				return orig_OnDashCollide(player, direction);
			};
		}

		private static void DoStaticCutscene(ClutterBlock.Colors color) {
			if (!(Engine.Scene is Level level)) return;

			Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
			level.Session.SetFlag("oshiro_clutter_cleared_" + (int)color);
			level.Session.SetFlag("oshiro_clutter_door_open", setTo: false);
			level.DirectionalShake(Vector2.UnitY, 0.6f);

			Entity cutsceneEntity = new Entity();
			level.Add(cutsceneEntity);
			cutsceneEntity.Add(new Coroutine(AbsorbRoutine(cutsceneEntity, color), true));
		}

		private static IEnumerator AbsorbRoutine(Entity entity, ClutterBlock.Colors color) {
			SoundSource cutsceneSfx;
			Level level = entity.SceneAs<Level>();

			entity.Add(cutsceneSfx = new SoundSource());
			float duration = 0f;
			if (color == ClutterBlock.Colors.Green) {
				cutsceneSfx.Play("event:/game/03_resort/clutterswitch_books");
				duration = 6.366f;
			}
			else if (color == ClutterBlock.Colors.Red) {
				cutsceneSfx.Play("event:/game/03_resort/clutterswitch_linens");
				duration = 6.15f;
			}
			else if (color == ClutterBlock.Colors.Yellow) {
				cutsceneSfx.Play("event:/game/03_resort/clutterswitch_boxes");
				duration = 6.066f;
			}
			entity.Add(Alarm.Create(Alarm.AlarmMode.Oneshot, delegate
			{
				Audio.Play("event:/game/03_resort/clutterswitch_finish");
			}, duration, start: true));
			Vector2 target = level.Camera.CameraToScreen(Vector2.One / 2f) /*+ new Vector2(Width / 2f, 0f)*/;
			ClutterAbsorbEffect effect = new ClutterAbsorbEffect();
			level.Add(effect);
			level.Session.Audio.Music.Progress++;
			level.Session.Audio.Apply(forceSixteenthNoteHack: false);
			level.Session.LightingAlphaAdd -= 0.05f;
			float start = level.Lighting.Alpha;
			Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.SineInOut, 2f, start: true);
			tween.OnUpdate = delegate (Tween t)
			{
				level.Lighting.Alpha = MathHelper.Lerp(start, 0.05f, t.Eased);
			};
			entity.Add(tween);
			Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
			foreach (ClutterBlock item in level.Entities.FindAll<ClutterBlock>()) {
				if (item.BlockColor == color) {
					item.Absorb(effect);
				}
			}
			foreach (ClutterBlockBase item2 in level.Entities.FindAll<ClutterBlockBase>()) {
				if (item2.BlockColor == color) {
					item2.Deactivate();
				}
			}
			yield return 1.5f;
			List<MTexture> images = GFX.Game.GetAtlasSubtextures("objects/resortclutter/" + color.ToString() + "_");
			for (int i = 0; i < 25; i++) {
				for (int j = 0; j < 5; j++) {
					Vector2 position = target + Calc.AngleToVector(Calc.Random.NextFloat((float)Math.PI * 2f), 320f);
					effect.FlyClutter(position, Calc.Random.Choose(images), shake: false, 0f);
				}
				level.Shake();
				Input.Rumble(RumbleStrength.Light, RumbleLength.Long);
				yield return 0.05f;
			}
			yield return 1.5f;
			effect.CloseCabinets();
			yield return 0.2f;
			Input.Rumble(RumbleStrength.Medium, RumbleLength.FullSecond);
			yield return 0.3f;
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

		public static int GetHeader() => 19;

		public static ClutterBlock.Colors ParseState(CelesteNetBinaryReader r) {
			ClutterBlock.Colors color;
			Enum.TryParse(r.ReadString(), out color);
			return color;
		}

		public static bool StaticHandler(object state) {
			if (!(state is ClutterBlock.Colors color)) return false;
			DoStaticCutscene(color);
			return true;
		}

		public void ApplyState(object state) {
			if (state is ClutterBlock.Colors color) {
				DoStaticCutscene(color);
			}
		}

		public bool CheckRecurringUpdate() => false;

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(new DynamicData(this).Get<ClutterBlock.Colors>("color").ToString() ?? "");
		}
	}
}
