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

	[CustomEntity("corkr900CoopHelper/SyncedBooster")]
	public class SyncedBooster : Booster, ISynchronizable {
		private EntityID id;
		internal bool SuppressUpdate = false;
		private bool red;
		private bool pendingUseByMe = false;
		private bool inUseByOtherPlayer = false;

		public bool InUseByMe { get { return pendingUseByMe || BoostingPlayer; } }
		public bool InUse { get { return pendingUseByMe || BoostingPlayer || inUseByOtherPlayer; } }

		public SyncedBooster(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			red = data.Bool("red");

			PlayerCollider coll = Get<PlayerCollider>();
			Action<Player> orig_OnPlayer = coll.OnCollide;
			coll.OnCollide = (Player p) => {
				DynamicData dd = DynamicData.For(this);
				float respawnTimer = dd.Get<float>("respawnTimer");
				float cannotUseTimer = dd.Get<float>("cannotUseTimer");

				if (respawnTimer <= 0f && cannotUseTimer <= 0f && !InUse) {
					orig_OnPlayer(p);
					pendingUseByMe = true;
					EntityStateTracker.PostUpdate(this);
				}
			};
		}

		internal void OnPlayerBoosted() {
			pendingUseByMe = false;
		}

		internal void OnPlayerReleased() {
			inUseByOtherPlayer = false;
			if (!SuppressUpdate) {
				EntityStateTracker.PostUpdate(this);
			}
		}

		private void UsedByOtherPlayer() {
			DynamicData dd = DynamicData.For(this);
			Wiggler wiggler = dd.Get<Wiggler>("wiggler");
			Sprite sprite = dd.Get<Sprite>("sprite");

			inUseByOtherPlayer = true;
			Audio.Play(red ? "event:/game/05_mirror_temple/redbooster_enter" : "event:/game/04_cliffside/greenbooster_enter", Position);
			wiggler.Start();
			sprite.Play("pop");
			dd.Set("cannotUseTimer", 0.45f);
			dd.Set("respawnTimer", 99999999f);
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
			Header = 24,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static SyncedBoosterState ParseState(CelesteNetBinaryReader r) {
			return new SyncedBoosterState() {
				BeingUsed = r.ReadBoolean(),
			};
		}

		public void ApplyState(object state) {
			if (state is SyncedBoosterState sbs) {
				SuppressUpdate = true;
				if (sbs.BeingUsed) {
					if (BoostingPlayer) PlayerReleased();
					else UsedByOtherPlayer();
				}
				else {
					PlayerReleased();
				}
				SuppressUpdate = false;
			}
		}

		public bool CheckRecurringUpdate() => false;

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(InUseByMe);
		}

	}

	public class SyncedBoosterState {
		public bool BeingUsed;
	}
}
