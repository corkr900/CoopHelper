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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedKevin")]
	public class SyncedKevin : CrushBlock, ISynchronizable {

		private EntityID id;
		internal bool Attacking;
		private Vector2 attackDir;
		private bool boppedRemotely = false;

		public SyncedKevin(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			DashCollision orig_DashCollide = OnDashCollide;
			OnDashCollide = (Player player, Vector2 direction) => {
				DashCollisionResults result = orig_DashCollide(player, direction);
				if (result == DashCollisionResults.Rebound) {
					Attacking = true;
					attackDir = -direction;
					boppedRemotely = false;
					EntityStateTracker.PostUpdate(this);
				}
				return result;
			};
		}

		internal void OnReturnBegin() {
			Attacking = false;
			if (!boppedRemotely) {
				EntityStateTracker.PostUpdate(this);
			}
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

		public bool CheckRecurringUpdate() => false;

		public EntityID GetID() => id;

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 18,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public void ApplyState(object state) {
			if (state is SyncedKevinState sks) {
				Position = sks.Position;
				if (sks.Attacking && !Attacking) {
					attackDir = sks.AttackDirection;
					boppedRemotely = true;
					Attack(sks.AttackDirection);
				}
				Attacking = sks.Attacking;
				returnStack.Clear();
				if (sks.ReturnStack?.Count > 0) {
					Type moveStateType = typeof(CrushBlock).GetNestedType("MoveState", System.Reflection.BindingFlags.NonPublic);
					FieldInfo from = moveStateType.GetField("From");
					FieldInfo direction = moveStateType.GetField("Direction");
					foreach (Tuple<Vector2, Vector2> moveStateTup in sks.ReturnStack) {
						MoveState moveState = (MoveState)Activator.CreateInstance(moveStateType);
						from.SetValue(moveState, moveStateTup.Item1);
						direction.SetValue(moveState, moveStateTup.Item2);
						returnStack.Add(moveState);
					}
				}
			}
		}

		public static SyncedKevinState ParseState(CelesteNetBinaryReader r) {
			SyncedKevinState s = new SyncedKevinState() {
				Position = r.ReadVector2(),
				Attacking = r.ReadBoolean(),
				AttackDirection = r.ReadVector2(),
				ReturnStack = new List<Tuple<Vector2, Vector2>>(),
			};
			int count = r.ReadInt32();
			for (int i = 0; i < count; i++) {
				s.ReturnStack.Add(new Tuple<Vector2, Vector2>(
					r.ReadVector2(),
					r.ReadVector2()
				));
			}
			return s;
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(Position);
			w.Write(Attacking);
			w.Write(attackDir);
			w.Write(returnStack.Count);
			foreach (MoveState moveState in returnStack) {
				w.Write(moveState.From);
				w.Write(moveState.Direction);
			}
		}
	}

	public class SyncedKevinState {
		public Vector2 Position;
		public bool Attacking;
		public Vector2 AttackDirection;
		public List<Tuple<Vector2, Vector2>> ReturnStack;
	}
}
