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
using static Celeste.TrackSpinner;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedFeather")]
	public class SyncedFeather : FlyFeather, ISynchronizable {
		private EntityID id;
		private Vector2 usedSpeed;

		public SyncedFeather(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);

			var pcoll = Get<PlayerCollider>();
			var orig_onPlayer = pcoll.OnCollide;
			pcoll.OnCollide = (Player p) => OnPlayerOverride(p, orig_onPlayer);
		}

		private void OnPlayerOverride(Player p, Action<Player> orig) {
			if (p != null) {
				bool flyingBefore = p.StateMachine.State == Player.StStarFly;
				orig(p);
				bool flyingAfter = p.StateMachine.State == Player.StStarFly;
				if (flyingAfter && !flyingBefore) {
					usedSpeed = p.Speed;
					EntityStateTracker.PostUpdate(this);
				}
			}
		}

		public void UsedByOtherPlayer(Vector2 speed) {
			Collidable = false;
			Add(new Coroutine(RemoteCollectRoutine(speed)));
			if (!singleUse) {
				outline.Visible = true;
				respawnTimer = 3f;
			}
			var instance = Audio.Play(shielded ? "event:/game/06_reflection/feather_bubble_renew" : "event:/game/06_reflection/feather_renew", Position);
			instance.setPitch(0.7f);
			instance.setVolume(0.6f);
		}

		private IEnumerator RemoteCollectRoutine(Vector2 playerSpeed) {
			sprite.Visible = false;
			yield return 0.05f;
			float direction = playerSpeed.Angle();
			level.ParticlesFG.Emit(P_Collect, 10, Position, Vector2.One * 6f);
			SlashFx.Burst(Position, direction);
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
			Header = 27,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public void ApplyState(object state) {
			if (state is SyncedFeatherState sfs) {
				UsedByOtherPlayer(sfs.UseSpeed);
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNet.CelesteNetBinaryWriter w) {
			w.Write(usedSpeed);
		}

		public static object ParseState(CelesteNet.CelesteNetBinaryReader r) {
			return new SyncedFeatherState {
				UseSpeed = r.ReadVector2(),
			};
		}

	}

	public class SyncedFeatherState {
		public Vector2 UseSpeed;
	}
}
