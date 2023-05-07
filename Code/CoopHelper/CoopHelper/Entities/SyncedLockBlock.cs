﻿using Celeste.Mod.CelesteNet;
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
	[CustomEntity("corkr900CoopHelper/SyncedLockBlock")]
	public class SyncedLockBlock : LockBlock, ISynchronizable {
		private bool remotePlayerOpened = false;
		private EntityID usedKeyID;
		private bool transitionComplete = false;

		public SyncedLockBlock(EntityData data, Vector2 offset) : base(data, offset, new EntityID(data.Level.Name, data.ID)) {
			TransitionListener tlisten = new TransitionListener();
			tlisten.OnInEnd = delegate () {
				transitionComplete = true;
				if (remotePlayerOpened) {
					DoRemoteUnlock();
				}
				tlisten?.RemoveSelf();
				tlisten = null;
			};
			Add(tlisten);
		}

		private void DoRemoteUnlock() {
			List<Entity> syncKeys = SceneAs<Level>().Tracker.GetEntities<SyncedKey>();
			SyncedKey usedKey = null;
			foreach (SyncedKey key in syncKeys) {
				if (key.ID.Equals(usedKeyID)) {
					usedKey = key;
					break;
				}
			}
			Add(new Coroutine(UnlockRoutineOverride(usedKey)));
		}

		internal IEnumerator UnlockRoutineOverride(Key key) {
			Level level = SceneAs<Level>();

			// Register as opened immediately so deaths don't result in
			// the update getting seemingly discarded
			RegisterOpened(level, key);
			if (!remotePlayerOpened) EntityStateTracker.PostUpdate(this);

			SoundEmitter emitter = SoundEmitter.Play(unlockSfxName, this);
			emitter.Source.DisposeOnTransition = true;
			if (key != null) {
				key.Visible = true;
				usedKeyID = key.ID;
				Add(new Coroutine(key.UseRoutine(Center + new Vector2(0f, 2f))));
				if (!remotePlayerOpened) EntityStateTracker.PostUpdate(this);
			}
			yield return 1.2f;
			UnlockingRegistered = true;
			if (stepMusicProgress) {
				level.Session.Audio.Music.Progress++;
				level.Session.Audio.Apply(forceSixteenthNoteHack: false);
			}

			// Vanilla doors would register as opened at his point

			while (key?.Turning ?? false) {
				yield return null;
			}
			Tag |= Tags.TransitionUpdate;
			Collidable = false;
			emitter.Source.DisposeOnTransition = false;
			yield return sprite.PlayRoutine("open");
			level.Shake();
			Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
			yield return sprite.PlayRoutine("burst");
			RemoveSelf();
		}

		private void RegisterOpened(Level level, Key key) {
			if (!level.Session.DoNotLoad.Contains(ID)) {
				level.Session.DoNotLoad.Add(ID);
			}
			// this is based on key.RegiserUsed except safe to call twice
			// and will still do the necessary flagging without the key
			if (key != null) {
				key.IsUsed = true;
				Follower follower = key.follower;
				if (follower?.Leader != null) {
					follower.Leader.LoseFollower(follower);
				}
			}
			if (key != null) usedKeyID = key.ID;
			if (SceneAs<Level>().Session.Keys.Contains(usedKeyID)) {
				SceneAs<Level>().Session.Keys.Remove(usedKeyID);
			}
		}

		#region ISync implementation

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
			Header = 10,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = true,
		};

		public static SyncedLockBlockStatus ParseState(CelesteNetBinaryReader r) {
			SyncedLockBlockStatus s = new SyncedLockBlockStatus();
			s.UnlockingRegistered = r.ReadBoolean();
			s.KeyUsed = r.ReadEntityID();
			return s;
		}

		public void ApplyState(object state) {
			if (state is SyncedLockBlockStatus s) {
				remotePlayerOpened = true;
				usedKeyID = s.KeyUsed;
				if (transitionComplete) DoRemoteUnlock();
				else remotePlayerOpened = true;
			}
		}

		public EntityID GetID() => ID;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(UnlockingRegistered);
			w.Write(usedKeyID);
		}

		#endregion
	}

	public class SyncedLockBlockStatus {
		public bool UnlockingRegistered;
		public EntityID KeyUsed;
	}
}
