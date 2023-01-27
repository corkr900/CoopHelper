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

		private object syncDeltaLock = new object();
		private float mySyncedMovement = 0;

		private bool MovesVertically { get { return direction == Directions.Up || direction == Directions.Down; } }

		private enum MovementState {
			Idling = 0,
			Moving = 1,
			Breaking = 2,
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
				mySyncedMovement = 0f;
				lock (syncDeltaLock) {
					mySyncedMovement = 0;
				}
			}
			if (state != lastState) {
				lastState = state;
				EntityStateTracker.PostUpdate(this);
			}
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
			s.MovementDelta = r.ReadSingle();
			s.Position = r.ReadVector2();
			return s;
		}

		public void ApplyState(object state) {
			if (state is SyncedMoveBlockState mbs) {
				DynamicData dd = new DynamicData(this);
				if (lastState == MovementState.Idling && mbs.Moving) {
					dd.Set("triggered", true);
					lastState = MovementState.Moving;
				}
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
				w.Write(Position);
				w.Write(0f);
			}
		}
	}

	public class SyncedMoveBlockState {
		public bool Moving;
		public Vector2 Position;
		public float MovementDelta;
	}
}
