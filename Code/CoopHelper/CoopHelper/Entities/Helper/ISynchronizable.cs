using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	public interface ISynchronizable {
		EntityID GetID();
		void WriteState(CelesteNetBinaryWriter w);
		void ApplyState(object state);

		bool CheckRecurringUpdate();
	}

	public struct SynchronizableComparer : IEqualityComparer<ISynchronizable> {
		public bool Equals(ISynchronizable x, ISynchronizable y) {
			return x == null ? y == null : x.GetID().Equals(y?.GetID());
		}

		public int GetHashCode(ISynchronizable obj) {
			return obj?.GetID().GetHashCode() ?? 0;
		}
	}
}
