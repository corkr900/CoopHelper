using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Module {
	public class CoopHelperModuleSession : EverestModuleSession {
		public bool IsInCoopSession;
		public CoopSessionID SessionID;
		public int SessionRole = -1;
		public List<PlayerID> SessionMembers = new List<PlayerID>();
	}
}
