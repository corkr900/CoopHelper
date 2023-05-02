using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	/// <summary>
	/// Wrapper object for a synced entity added by another mod via the ModInterop API
	/// </summary>
	public class ExternalSyncedEntity : ISynchronizable {
		private object targetObject;
		private EntityID id;
		private DynamicData dd;
		internal bool DoRecurringUpdate;

		internal int Header { get; private set; }

		public ExternalSyncedEntity(object target, EntityID ID, int header, bool doRecurringUpdate) {
			Header = header;
			targetObject = target;
			id = ID;
			DoRecurringUpdate = doRecurringUpdate;
			dd = DynamicData.For(target);
		}

		public void ApplyState(object state) {
			dd.Invoke("ApplyState", state);
		}

		public bool CheckRecurringUpdate() {
			if (!DoRecurringUpdate) return false;
			return ((bool?)targetObject.GetType().GetMethod("CheckRecurringUpdate")?.Invoke(targetObject, null)) ?? false;
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			dd.Invoke("WriteState", w);
		}
	}
}
