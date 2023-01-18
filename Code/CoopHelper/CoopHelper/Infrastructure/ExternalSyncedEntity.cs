using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public class ExternalSyncedEntity : ISynchronizable {
		private object targetObject;
		private EntityID id;
		private DynamicData dd;

		internal int Header { get; private set; }

		public ExternalSyncedEntity(object target, EntityID ID, int header) {
			Header = header;
			targetObject = target;
			id = ID;
			dd = new DynamicData(target);
			Dictionary<string, Func<object, object[], object>> methods = dd.Methods;
		}

		public void ApplyState(object state) {
			dd.Invoke("ApplyState", state);
		}

		public bool CheckRecurringUpdate() {
			return ((bool?)targetObject.GetType().GetMethod("CheckRecurringUpdate")?.Invoke(targetObject, null)) ?? false;
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			dd.Invoke("WriteState", w);
		}
	}
}
