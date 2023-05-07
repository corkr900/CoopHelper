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
	[CustomEntity("corkr900CoopHelper/SyncedRefill")]
	public class SyncedRefill : Refill, ISynchronizable {
		private EntityID id;
		private float respawnTime = 2.5f;

		public SyncedRefill(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			PlayerCollider pcoll = Get<PlayerCollider>();
			Action<Player> orig_OnPlayer = pcoll.OnCollide;
			respawnTime = data.Float("respawnTime", 2.5f);
			pcoll.OnCollide = (Player player) => {
				bool before = Collidable;
				orig_OnPlayer(player);
				if (before && !Collidable) EntityStateTracker.PostUpdate(this);
			};
		}

		public void UsedByOtherPlayer() {
			Collidable = false;
			Add(new Coroutine(RemoteRefillRoutine()));
			respawnTimer = respawnTime;
			FMOD.Studio.EventInstance instance = Audio.Play(twoDashes ? "event:/new_content/game/10_farewell/pinkdiamond_touch" : "event:/game/general/diamond_touch", Position);
			instance.setPitch(0.8f);
			instance.setVolume(0.6f);
		}

		private IEnumerator RemoteRefillRoutine() {
			Level level = SceneAs<Level>();
			yield return null;
			sprite.Visible = false;
			flash.Visible = false;
			if (!oneUse) {
				outline.Visible = true;
			}
			Depth = 8999;
			yield return 0.05f;
			float num = Vector2.Zero.Angle();
			level.ParticlesFG.Emit(p_shatter, 5, Position, Vector2.One * 4f, num - (float)Math.PI / 2f);
			level.ParticlesFG.Emit(p_shatter, 5, Position, Vector2.One * 4f, num + (float)Math.PI / 2f);
			SlashFx.Burst(Position, num);
			if (oneUse) {
				RemoveSelf();
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

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 23,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool used && used) {
				UsedByOtherPlayer();
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(true);
		}
	}
}
