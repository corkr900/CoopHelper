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
		private float otherPlayerTotalMovement;
		private float myLastMovement;
		private bool currentlyActive;

		private enum MovementState {
			Idling = 0,
			Moving = 1,
			Breaking = 2,
		}

		public SyncedMoveBlock(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		public override void Update() {
			base.Update();
			DynamicData dd = new DynamicData(this);
			MovementState state = dd.Get<MovementState>("state");

			switch (state) {
				case MovementState.Moving:
					if (!currentlyActive) {
						EntityStateTracker.PostUpdate(this);
					}
					if (dd.Get<bool>("canSteer")) {
						Vector2 startPos = dd.Get<Vector2>("startPosition");
						Vector2 curOffset = Position - startPos;
						Directions dir = dd.Get<Directions>("direction");
						float MyMovement = ((dir == Directions.Down || dir == Directions.Up) ? curOffset.X : curOffset.Y) - otherPlayerTotalMovement;
						if (Math.Abs(MyMovement - myLastMovement) > 0.5f) {
							EntityStateTracker.PostUpdate(this);
							myLastMovement = MyMovement;
						}
					}
					break;

				case MovementState.Breaking:
				case MovementState.Idling:
					currentlyActive = false;
					myLastMovement = 0;
					otherPlayerTotalMovement = 0;
					break;

				default:
					break;
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

		public static int GetHeader() => 12;

		public static SyncedMoveBlockState ParseState(CelesteNetBinaryReader r) {
			SyncedMoveBlockState s = new SyncedMoveBlockState();
			s.moving = r.ReadBoolean();
			s.playersMovement = r.ReadSingle();
			return s;
		}
		public void ApplyState(object state) {
			if (state is SyncedMoveBlockState mbs) {
				DynamicData dd = new DynamicData(this);
				MovementState movingState = dd.Get<MovementState>("state");
				if (mbs.moving) {
					if (movingState != MovementState.Moving) {
						dd.Set("triggered", true);  // TODO this does not buffer at all. Could cause a desync
						currentlyActive = true;  // Prevents the check in Update from posting an update to the sync tracker
					}
					float diff = mbs.playersMovement - otherPlayerTotalMovement;
					otherPlayerTotalMovement = mbs.playersMovement;  // TODO this paradigm will only work with 2 players, not 3+
					Directions dir = dd.Get<Directions>("direction");
					Vector2 shift = (dir == Directions.Up || dir == Directions.Down ? Vector2.UnitX : Vector2.UnitY) * diff;
					Position += shift;
				}
				else if (movingState == MovementState.Moving) {
					// TODO break move blocks when the remote says to
				}
			}
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			MovementState state = new DynamicData(this).Get<MovementState>("state");
			w.Write(state == MovementState.Moving);
			w.Write(myLastMovement);
		}
	}

	public class SyncedMoveBlockState {
		public bool moving;
		public float playersMovement;
	}
}
