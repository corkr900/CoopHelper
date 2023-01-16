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
	[CustomEntity("corkr900CoopHelper/SyncedTempleCrackedBlock")]
	[TrackedAs(typeof(TempleCrackedBlock))]
	public class SyncedTempleCrackedBlock : TempleCrackedBlock, ISynchronizable {
		private EntityID id;
		private Vector2 brokenFrom;
		private bool brokenRemotely = false;

		public SyncedTempleCrackedBlock(EntityData data, Vector2 offset) : base(new EntityID(data.Level.Name, data.ID), data, offset) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		internal void OnBreak(Vector2 from) {
			if (!brokenRemotely) {
				brokenFrom = from;
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
			Header = 15,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = true,
		};

		public static object ParseState(CelesteNetBinaryReader r) {
			return r.ReadVector2();
		}

		public void ApplyState(object state) {
			if (state is Vector2 from && !new DynamicData(this).Get<bool>("broken")) {
				brokenRemotely = true;
				Break(from);
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(brokenFrom);
		}
	}
}
