using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using FMOD;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MonoMod.InlineRT.MonoModRule;

namespace Celeste.Mod.CoopHelper.Entities {

	[CustomEntity("corkr900CoopHelper/SyncedBounceBlock")]
	internal class SyncedBounceBlock : BounceBlock, ISynchronizable {

		private EntityID id;
		private Session.CoreModes coreMode = Session.CoreModes.None;

		private bool usedByOtherPlayer = false;

		public SyncedBounceBlock(EntityData data, Vector2 offset) : base(data, offset) {
			notCoreMode = false;  // We'll be handling it ourselves
			id = new EntityID(data.Level.Name, data.ID);
			coreMode = Enum.TryParse(data.Attr("coreMode", "None"), out Session.CoreModes _mode) ? _mode : Session.CoreModes.None;
			if (coreMode != Session.CoreModes.None) {
				Get<CoreModeListener>()?.RemoveSelf();
				iceMode = iceModeNext = coreMode == Session.CoreModes.Cold;
			}
		}

		private void UsedByOtherPlayer(SyncedBounceBlockState otherState) {
			if (state == States.Waiting || state == States.BounceEnd) {
				usedByOtherPlayer = true;
				bounceDir = otherState.BounceDir;
				iceMode = iceModeNext = otherState.IceMode;
			}
		}

		private Vector2 GetBounceDirSafe(Player player) => player == null ? bounceDir : (player.Center - Center).SafeNormalize();

		/// <summary>
		/// This exists to skip the BounceBlock implementation of Update and go straight to Solid::Update
		/// </summary>
		[MonoModLinkTo("Celeste.Solid", "System.Void Update()")]
		public void Solid_Update() {
			base.Update();
		}

		public override void Update() {
			Solid_Update();
			reappearFlash = Calc.Approach(reappearFlash, 0f, Engine.DeltaTime * 8f);
			switch (state) {
				case States.Waiting:
					Update_Waiting();
					break;
				case States.WindingUp:
					Update_WindingUp();
					break;
				case States.Bouncing:
					Update_Bouncing();
					break;
				case States.BounceEnd:
					Update_BounceEnd();
					break;
				case States.Broken:
					Update_Broken();
					break;
			}
		}

		private void Update_Waiting() {
			CheckModeChange();
			moveSpeed = Calc.Approach(moveSpeed, 100f, 400f * Engine.DeltaTime);
			Vector2 vector = Calc.Approach(ExactPosition, startPos, moveSpeed * Engine.DeltaTime);
			Vector2 liftSpeed = (vector - ExactPosition).SafeNormalize(moveSpeed);
			liftSpeed.X *= 0.75f;
			MoveTo(vector, liftSpeed);
			windUpProgress = Calc.Approach(windUpProgress, 0f, 1f * Engine.DeltaTime);
			Player player = WindUpPlayerCheck();
			if (player != null || usedByOtherPlayer) {
				moveSpeed = 80f;
				windUpStartTimer = 0f;
				if (iceMode) {
					bounceDir = -Vector2.UnitY;
				}
				else {
					bounceDir = GetBounceDirSafe(player);
				}
				state = States.WindingUp;
				EntityStateTracker.PostUpdate(this);
				Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
				if (iceMode) {
					StartShaking(0.2f);
					Audio.Play("event:/game/09_core/iceblock_touch", Center);
				}
				else {
					Audio.Play("event:/game/09_core/bounceblock_touch", Center);
				}
			}
		}

		private void Update_WindingUp() {
			usedByOtherPlayer = false;
			Player player = WindUpPlayerCheck();
			if (player != null || usedByOtherPlayer) {
				if (iceMode) {
					bounceDir = -Vector2.UnitY;
				}
				else {
					bounceDir = GetBounceDirSafe(player);
				}
			}
			if (windUpStartTimer > 0f) {
				windUpStartTimer -= Engine.DeltaTime;
				windUpProgress = Calc.Approach(windUpProgress, 0f, 1f * Engine.DeltaTime);
				return;
			}
			moveSpeed = Calc.Approach(moveSpeed, iceMode ? 35f : 40f, 600f * Engine.DeltaTime);
			float num = (iceMode ? 0.333f : 1f);
			Vector2 vector2 = startPos - bounceDir * (iceMode ? 16f : 10f);
			Vector2 vector3 = Calc.Approach(ExactPosition, vector2, moveSpeed * num * Engine.DeltaTime);
			Vector2 liftSpeed2 = (vector3 - ExactPosition).SafeNormalize(moveSpeed * num);
			liftSpeed2.X *= 0.75f;
			MoveTo(vector3, liftSpeed2);
			windUpProgress = Calc.ClampedMap(Vector2.Distance(ExactPosition, vector2), 16f, 2f);
			if (iceMode && Vector2.DistanceSquared(ExactPosition, vector2) <= 12f) {
				StartShaking(0.1f);
			}
			else if (!iceMode && windUpProgress >= 0.5f) {
				StartShaking(0.1f);
			}
			if (Vector2.DistanceSquared(ExactPosition, vector2) <= 2f) {
				if (iceMode) {
					Break();
				}
				else {
					state = States.Bouncing;
				}
				moveSpeed = 0f;
			}
		}

		private void Update_Bouncing() {
			moveSpeed = Calc.Approach(moveSpeed, 140f, 800f * Engine.DeltaTime);
			Vector2 target = startPos + bounceDir * 24f;
			Vector2 newPos = Calc.Approach(ExactPosition, target, moveSpeed * Engine.DeltaTime);
			bounceLift = (newPos - ExactPosition).SafeNormalize(Math.Min(moveSpeed * 3f, 200f));
			bounceLift.X *= 0.75f;
			MoveTo(newPos, bounceLift);
			windUpProgress = 1f;
			if (ExactPosition == target || (!iceMode && !usedByOtherPlayer && WindUpPlayerCheck() == null)) {
				debrisDirection = (target - startPos).SafeNormalize();
				state = States.BounceEnd;
				Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
				moveSpeed = 0f;
				bounceEndTimer = 0.05f;
				ShakeOffPlayer(bounceLift);
			}
		}

		private void Update_BounceEnd() {
			bounceEndTimer -= Engine.DeltaTime;
			if (bounceEndTimer <= 0f) {
				Break();
			}
		}

		private void Update_Broken() {
			Depth = Depths.BGDecals - 10;
			reformed = false;
			if (respawnTimer > 0f) {
				respawnTimer -= Engine.DeltaTime;
				return;
			}
			Vector2 position = Position;
			Position = startPos;
			if (!CollideCheck<Actor>() && !CollideCheck<Solid>()) {
				CheckModeChange();
				Audio.Play(iceMode ? "event:/game/09_core/iceblock_reappear" : "event:/game/09_core/bounceblock_reappear", Center);
				float duration = 0.35f;
				for (int i = 0; i < Width; i += 8) {
					for (int j = 0; j < Height; j += 8) {
						Vector2 vector6 = new Vector2(X + i + 4f, Y + j + 4f);
						Scene.Add(Engine.Pooler.Create<RespawnDebris>().Init(vector6 + (vector6 - Center).SafeNormalize() * 12f, vector6, iceMode, duration));
					}
				}
				Alarm.Set(this, duration, delegate
				{
					reformed = true;
					reappearFlash = 0.6f;
					EnableStaticMovers();
					ReformParticles();
				});
				Depth = Depths.Solids;
				MoveStaticMovers(Position - position);
				Collidable = true;
				state = States.Waiting;
			}
			else {
				Position = position;
			}
		}

		public override void Render() {
			Vector2 position = Position;
			Position += base.Shake;
			if (state != States.Broken && reformed) {
				base.Render();
			}
			if (reappearFlash > 0f) {
				float num = Ease.CubeOut(reappearFlash);
				float num2 = num * 2f;
				Draw.Rect(base.X - num2, base.Y - num2, base.Width + num2 * 2f, base.Height + num2 * 2f, Color.White * num);
			}
			Position = position;
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			if (coreMode != Session.CoreModes.None) {
				iceMode = iceModeNext = coreMode == Session.CoreModes.Cold;
			}
			ToggleSprite();
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
			Header = 25,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public void ApplyState(object st) {
			if (st is SyncedBounceBlockState newState) {
				UsedByOtherPlayer(newState);
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(bounceDir);
			w.Write(iceMode);
		}

		public static object ParseState(CelesteNetBinaryReader r) {
			return new SyncedBounceBlockState {
				BounceDir = r.ReadVector2(),
				IceMode = r.ReadBoolean(),
			};
		}

	}

	public class SyncedBounceBlockState {
		internal Vector2 BounceDir;
		internal bool IceMode;
	}
}
