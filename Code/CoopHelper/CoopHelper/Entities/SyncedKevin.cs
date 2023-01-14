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

		public SyncedKevin(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			DashCollision orig_DashCollide = OnDashCollide;
			OnDashCollide = (Player player, Vector2 direction) => {
				DashCollisionResults result = orig_DashCollide(player, direction);
				if (result == DashCollisionResults.Rebound) {
					Attacking = true;
					attackDir = -direction;
					EntityStateTracker.PostUpdate(this);
				}
				return result;
			};
		}

		internal void OnReturnBegin() {
			Attacking = false;
			EntityStateTracker.PostUpdate(this);
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

		public bool CheckRecurringUpdate() => false;

		public EntityID GetID() => id;

		public static int GetHeader() => 18;

		public void ApplyState(object state) {
			if (state is SyncedKevinState sks) {
				DynamicData dd = new DynamicData(this);
				Position = sks.Position;
				if (sks.Attacking && !Attacking) {
					attackDir = sks.AttackDirection;
					dd.Invoke("Attack", sks.AttackDirection);
				}
				Attacking = sks.Attacking;
				IList returnStack = dd.Get<IList>("returnStack");
				returnStack.Clear();
				if (sks.ReturnStack?.Count > 0) {
					Type moveStateType = typeof(CrushBlock).GetNestedType("MoveState", System.Reflection.BindingFlags.NonPublic);
					FieldInfo from = moveStateType.GetField("From");
					FieldInfo direction = moveStateType.GetField("Direction");
					foreach (Tuple<Vector2, Vector2> moveStateTup in sks.ReturnStack) {
						object moveState = Activator.CreateInstance(moveStateType);
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
			DynamicData dd = new DynamicData(this);
			IList returnStack = dd.Get<IList>("returnStack");
			w.Write(returnStack.Count);
			foreach (object moveState in returnStack) {
				DynamicData msdd = new DynamicData(moveState);
				w.Write(msdd.Get<Vector2>("From"));
				w.Write(msdd.Get<Vector2>("Direction"));
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
