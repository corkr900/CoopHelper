using Celeste.Mod.CelesteNet;
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
	[CustomEntity(
		"corkr900CoopHelper/TriggerSpikesUp",
		"corkr900CoopHelper/TriggerSpikesDown",
		"corkr900CoopHelper/TriggerSpikesLeft",
		"corkr900CoopHelper/TriggerSpikesRight"
	)]
	public class SyncedTriggerSpikes : TriggerSpikes, ISynchronizable {
		EntityID id;
		private bool[] lastSyncedState = new bool[0];
		private object syncLock = new object();

		private static Directions getDirection(EntityData data) {
			switch (data.Name) {
				default:
				case "corkr900CoopHelper/TriggerSpikesUp":
					return Directions.Up;
				case "corkr900CoopHelper/TriggerSpikesDown":
					return Directions.Down;
				case "corkr900CoopHelper/TriggerSpikesLeft":
					return Directions.Left;
				case "corkr900CoopHelper/TriggerSpikesRight":
					return Directions.Right;
			}
		}

		public SyncedTriggerSpikes(EntityData data, Vector2 offset) : base(data.Position + offset, GetSize(data, getDirection(data)), getDirection(data)) {
			id = new EntityID(data.Level.Name, data.ID);
			//spikeType = data.Attr("type", "dust");
		}

		private bool[] GetStateArray() {
			bool[] ret = new bool[spikes?.Length ?? 0];
			for (int i = 0; i < ret.Length; i++) {
				ret[i] = spikes[i].Triggered;
			}
			return ret;
		}

		private static bool ShouldSync(bool[] oldState, bool[] newState) {
			if (newState == null) return false;
			for (int i = 0; i < newState.Length; i++) {
				if (newState[i] && ((oldState?.Length ?? 0) <= i || !oldState[i])) return true;
			}
			return false;
		}

		internal void OnTriggered() {
			bool[] state = GetStateArray();
			lock (syncLock) {
				if (ShouldSync(lastSyncedState, state)) {
					EntityStateTracker.PostUpdate(this);
				}
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
			Header = 28,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = false,
		};

		public void ApplyState(object s) {
			if (s is SyncedTriggerSpikesState state) {
				lock(syncLock) {
					for (int i = 0; i < Calc.Min(state.triggeredInfo.Length, spikes?.Length ?? 0); i++) {
						spikes[i].Triggered |= state.triggeredInfo[i];
						lastSyncedState[i] |= state.triggeredInfo[i];
					}
				}
			}
		}

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public void WriteState(CelesteNetBinaryWriter w) {
			lock(syncLock) {
				int ct = spikes?.Length ?? 0;
				w.Write(ct);
				lastSyncedState = new bool[ct];
				for (int i = 0; i < ct; i++) {
					w.Write(spikes[i].Triggered);
					lastSyncedState[i] |= spikes[i].Triggered;
				}
			}
		}

		public static object ParseState(CelesteNetBinaryReader r) {
			int ct = r.ReadInt32();
			bool[] arr = new bool[ct];
			for (int i = 0; i < ct; i++) {
				arr[i] = r.ReadBoolean();
			}
			return new SyncedTriggerSpikesState {
				triggeredInfo = arr,
			};
		}

	}

	public class SyncedTriggerSpikesState {
		public bool[] triggeredInfo;
	}
}
