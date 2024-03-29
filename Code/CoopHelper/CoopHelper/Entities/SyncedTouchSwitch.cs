﻿using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedTouchSwitch")]
	[TrackedAs(typeof(TouchSwitch))]
	public class SyncedTouchSwitch : TouchSwitch, ISynchronizable {
		private EntityID id;

		public SyncedTouchSwitch(EntityData data, Vector2 offset) : base(data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
			Action oldOnActivate = Switch.OnActivate;
			Switch.OnActivate = delegate {
				oldOnActivate();
				EntityStateTracker.PostUpdate(this);
			};
		}

		#region These 3 overrides MUST be defined for synced entities/triggers

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

		#endregion

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 6,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool on && on) {
				TurnOn();
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(Switch.Active);
		}
	}
}
