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
		private bool triggeredRemotely = false;

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
				triggeredRemotely = false;
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
			s.moving = r.ReadBoolean();
			s.movementDelta = r.ReadSingle();
			return s;
		}

		public void ApplyState(object state) {
			if (state is SyncedMoveBlockState mbs) {
				DynamicData dd = new DynamicData(this);
				if (lastState == MovementState.Idling && mbs.moving) {
					dd.Set("triggered", true);
					lastState = MovementState.Moving;
					triggeredRemotely = true;
				}
				if (canSteer && lastState == MovementState.Moving && mbs.moving) {
					otherPlayerTotalMovement += mbs.movementDelta;
					if (MovesVertically) Position.X += mbs.movementDelta;
					else Position.Y += mbs.movementDelta;
				}
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() {
			return canSteer && lastState == MovementState.Moving;
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(lastState == MovementState.Moving);
			if (canSteer) {
				lock (syncDeltaLock) {
					Vector2 totalMovement = Position - startPosition;
					float myMovement = (MovesVertically ? totalMovement.X : totalMovement.Y) - otherPlayerTotalMovement;
					float deltaMovement = myMovement - mySyncedMovement;
					w.Write(deltaMovement);
					mySyncedMovement = myMovement;
				}
			}
			else {
				w.Write(0f);
			}
		}
	}

	public class SyncedMoveBlockState {
		public bool moving;
		public float movementDelta;
	}
}
