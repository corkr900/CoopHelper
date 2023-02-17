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
	[CustomEntity("corkr900CoopHelper/SyncedMoveBlock")]
	public class SyncedMoveBlock : MoveBlock, ISynchronizable {
		private EntityID id;
		private bool canSteer;
		private Vector2 startPosition;
		private Directions direction;

		private float otherPlayerTotalMovement = 0;
		private MovementState lastState = MovementState.Idling;
		internal bool LastMoveCheckResult = false;
		private MovementTriggeredBy triggeredBy = MovementTriggeredBy.None;
		private bool waitingForBreakConfirmation = false;

		private object syncDeltaLock = new object();
		private float mySyncedMovement = 0;

		private bool MovesVertically { get { return direction == Directions.Up || direction == Directions.Down; } }

		private enum MovementState {
			Idling = 0,
			Moving = 1,
			Breaking = 2,
		}

		private enum MovementTriggeredBy
		{
			None = 0,
			Me = 1,
			SomeoneElse = 2,
			Simultaneous = 3,
		}

		public SyncedMoveBlock(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			canSteer = data.Bool("canSteer", true);
			startPosition = data.Position + offset;
			direction = data.Enum("direction", Directions.Left);
		}

		public override void Update() {
			base.Update();
			DynamicData dd = new DynamicData(this);
			MovementState state = dd.Get<MovementState>("state");
			if (state != MovementState.Moving) {
				otherPlayerTotalMovement = 0;
				triggeredBy = MovementTriggeredBy.None;
				lock (syncDeltaLock) {
					mySyncedMovement = 0;
				}
			}
			if (state != lastState) {
				lastState = state;
				if (state == MovementState.Moving && triggeredBy == MovementTriggeredBy.None) {
					triggeredBy = MovementTriggeredBy.Me;
				}
				EntityStateTracker.PostUpdate(this);
			}
		}

		//private IEnumerator Controller()
		//{
		//	while (true)
		//	{
		//		triggered = false;
		//		state = MovementState.Idling;
		//		while (!triggered && !HasPlayerRider())
		//		{
		//			yield return null;
		//		}
		//		Audio.Play("event:/game/04_cliffside/arrowblock_activate", Position);
		//		state = MovementState.Moving;
		//		StartShaking(0.2f);
		//		ActivateParticles();
		//		yield return 0.2f;
		//		targetSpeed = (fast ? 75f : 60f);
		//		moveSfx.Play("event:/game/04_cliffside/arrowblock_move");
		//		moveSfx.Param("arrow_stop", 0f);
		//		StopPlayerRunIntoAnimation = false;
		//		float crashTimer = 0.15f;
		//		float crashResetTimer = 0.1f;
		//		float noSteerTimer = 0.2f;
		//		while (true)
		//		{
		//			if (canSteer)
		//			{
		//				targetAngle = homeAngle;
		//				bool flag = ((direction != Directions.Right && direction != 0) ? HasPlayerClimbing() : HasPlayerOnTop());
		//				if (flag && noSteerTimer > 0f)
		//				{
		//					noSteerTimer -= Engine.DeltaTime;
		//				}
		//				if (flag)
		//				{
		//					if (noSteerTimer <= 0f)
		//					{
		//						if (direction == Directions.Right || direction == Directions.Left)
		//						{
		//							targetAngle = homeAngle + (float)Math.PI / 4f * (float)angleSteerSign * (float)Input.MoveY.Value;
		//						}
		//						else
		//						{
		//							targetAngle = homeAngle + (float)Math.PI / 4f * (float)angleSteerSign * (float)Input.MoveX.Value;
		//						}
		//					}
		//				}
		//				else
		//				{
		//					noSteerTimer = 0.2f;
		//				}
		//			}
		//			if (Scene.OnInterval(0.02f))
		//			{
		//				MoveParticles();
		//			}
		//			speed = Calc.Approach(speed, targetSpeed, 300f * Engine.DeltaTime);
		//			angle = Calc.Approach(angle, targetAngle, (float)Math.PI * 16f * Engine.DeltaTime);
		//			Vector2 vector = Calc.AngleToVector(angle, speed);
		//			Vector2 vec = vector * Engine.DeltaTime;
		//			bool flag2;
		//			if (direction == Directions.Right || direction == Directions.Left)
		//			{
		//				flag2 = MoveCheck(vec.XComp());
		//				noSquish = Scene.Tracker.GetEntity<Player>();
		//				MoveVCollideSolids(vec.Y, thruDashBlocks: false);
		//				noSquish = null;
		//				LiftSpeed = vector;
		//				if (Scene.OnInterval(0.03f))
		//				{
		//					if (vec.Y > 0f)
		//					{
		//						ScrapeParticles(Vector2.UnitY);
		//					}
		//					else if (vec.Y < 0f)
		//					{
		//						ScrapeParticles(-Vector2.UnitY);
		//					}
		//				}
		//			}
		//			else
		//			{
		//				flag2 = MoveCheck(vec.YComp());
		//				noSquish = Scene.Tracker.GetEntity<Player>();
		//				MoveHCollideSolids(vec.X, thruDashBlocks: false);
		//				noSquish = null;
		//				LiftSpeed = vector;
		//				if (Scene.OnInterval(0.03f))
		//				{
		//					if (vec.X > 0f)
		//					{
		//						ScrapeParticles(Vector2.UnitX);
		//					}
		//					else if (vec.X < 0f)
		//					{
		//						ScrapeParticles(-Vector2.UnitX);
		//					}
		//				}
		//				if (direction == Directions.Down && Top > (float)(SceneAs<Level>().Bounds.Bottom + 32))
		//				{
		//					flag2 = true;
		//				}
		//			}
		//			if (flag2)
		//			{
		//				moveSfx.Param("arrow_stop", 1f);
		//				crashResetTimer = 0.1f;
		//				if (!(crashTimer > 0f))
		//				{
		//					break;
		//				}
		//				crashTimer -= Engine.DeltaTime;
		//			}
		//			else
		//			{
		//				moveSfx.Param("arrow_stop", 0f);
		//				if (crashResetTimer > 0f)
		//				{
		//					crashResetTimer -= Engine.DeltaTime;
		//				}
		//				else
		//				{
		//					crashTimer = 0.15f;
		//				}
		//			}
		//			Level level = Scene as Level;
		//			if (Left < (float)level.Bounds.Left || Top < (float)level.Bounds.Top || Right > (float)level.Bounds.Right)
		//			{
		//				break;
		//			}
		//			yield return null;
		//		}
		//		Audio.Play("event:/game/04_cliffside/arrowblock_break", Position);
		//		moveSfx.Stop();
		//		state = MovementState.Breaking;
		//		speed = (targetSpeed = 0f);
		//		angle = (targetAngle = homeAngle);
		//		StartShaking(0.2f);
		//		StopPlayerRunIntoAnimation = true;
		//		yield return 0.2f;
		//		BreakParticles();
		//		List<Debris> debris = new List<Debris>();
		//		for (int i = 0; (float)i < Width; i += 8)
		//		{
		//			for (int j = 0; (float)j < Height; j += 8)
		//			{
		//				Vector2 vector2 = new Vector2((float)i + 4f, (float)j + 4f);
		//				Debris debris2 = Engine.Pooler.Create<Debris>().Init(Position + vector2, Center, startPosition + vector2);
		//				debris.Add(debris2);
		//				Scene.Add(debris2);
		//			}
		//		}
		//		MoveBlock moveBlock = this;
		//		Vector2 amount = startPosition - Position;
		//		DisableStaticMovers();
		//		moveBlock.MoveStaticMovers(amount);
		//		Position = startPosition;
		//		Visible = (Collidable = false);
		//		yield return 2.2f;
		//		foreach (Debris item in debris)
		//		{
		//			item.StopMoving();
		//		}
		//		while (CollideCheck<Actor>() || CollideCheck<Solid>())
		//		{
		//			yield return null;
		//		}
		//		Collidable = true;
		//		EventInstance instance = Audio.Play("event:/game/04_cliffside/arrowblock_reform_begin", debris[0].Position);
		//		MoveBlock moveBlock2 = this;
		//		Coroutine component;
		//		Coroutine routine = (component = new Coroutine(SoundFollowsDebrisCenter(instance, debris)));
		//		moveBlock2.Add(component);
		//		foreach (Debris item2 in debris)
		//		{
		//			item2.StartShaking();
		//		}
		//		yield return 0.2f;
		//		foreach (Debris item3 in debris)
		//		{
		//			item3.ReturnHome(0.65f);
		//		}
		//		yield return 0.6f;
		//		routine.RemoveSelf();
		//		foreach (Debris item4 in debris)
		//		{
		//			item4.RemoveSelf();
		//		}
		//		Audio.Play("event:/game/04_cliffside/arrowblock_reappear", Position);
		//		Visible = true;
		//		EnableStaticMovers();
		//		speed = (targetSpeed = 0f);
		//		angle = (targetAngle = homeAngle);
		//		noSquish = null;
		//		fillColor = idleBgFill;
		//		UpdateColors();
		//		flash = 1f;
		//	}
		//}

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

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 12,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static SyncedMoveBlockState ParseState(CelesteNetBinaryReader r) {
			SyncedMoveBlockState s = new SyncedMoveBlockState();
			s.Moving = r.ReadBoolean();
			s.InitialTrigger = r.ReadBoolean();
			s.Position = r.ReadVector2();
			s.MovementDelta = r.ReadSingle();
			return s;
		}

		public void ApplyState(object state) {
			if (state is SyncedMoveBlockState mbs) {
				DynamicData dd = new DynamicData(this);
				// Handle other player triggering the block
				if (lastState == MovementState.Idling && mbs.Moving) {
					dd.Set("triggered", true);
					lastState = MovementState.Moving;
					triggeredBy = MovementTriggeredBy.SomeoneElse;
				}
				// Handle simultaneous trigger
				if (triggeredBy == MovementTriggeredBy.Me && mbs.InitialTrigger)
				{
					triggeredBy = MovementTriggeredBy.Simultaneous;
				}
				// Handle steering updates
				if (canSteer && lastState == MovementState.Moving && mbs.Moving) {
					if (LastMoveCheckResult && PositionIsFartherMovement(mbs.Position)) {
						otherPlayerTotalMovement += MovesVertically ? (mbs.Position - Position).X : (mbs.Position - Position).Y;
						Position = mbs.Position;
					}
					otherPlayerTotalMovement += mbs.MovementDelta;
					if (MovesVertically) MoveHExact((int)mbs.MovementDelta);
					else MoveVExact((int)mbs.MovementDelta);
				}
			}
		}

		private bool PositionIsFartherMovement(Vector2 pos) {
			switch (direction) {
				case Directions.Left:
					return pos.X < Position.X;
				case Directions.Right:
					return pos.X > Position.X;
				case Directions.Up:
					return pos.Y < Position.Y;
				case Directions.Down:
					return pos.Y > Position.Y;
				default:
					return false;
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() {
			return canSteer && lastState == MovementState.Moving;
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			if (canSteer) {
				w.Write(lastState == MovementState.Moving);
				w.Write(triggeredBy == MovementTriggeredBy.Me);
				w.Write(Position);
				lock (syncDeltaLock) {
					float deltaMovement = 0f;
					if (lastState == MovementState.Moving) {
						Vector2 totalMovement = Position - startPosition;
						float myMovement = (MovesVertically ? totalMovement.X : totalMovement.Y) - otherPlayerTotalMovement;
						deltaMovement = myMovement - mySyncedMovement;
						mySyncedMovement = myMovement;
					}
					w.Write(deltaMovement);
				}
			}
			else {
				w.Write(lastState == MovementState.Moving);
				w.Write(triggeredBy == MovementTriggeredBy.Me);
				w.Write(Position);
				w.Write(0f);
			}
		}
	}

	public class SyncedMoveBlockState {
		public bool Moving;
		public bool InitialTrigger;
		public Vector2 Position;
		public float MovementDelta;
	}
}
