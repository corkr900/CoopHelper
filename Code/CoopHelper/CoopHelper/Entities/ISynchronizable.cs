using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	public interface ISynchronizable<TState> {
		TState GetState();
		void ApplyState(TState newState);
	}
}
