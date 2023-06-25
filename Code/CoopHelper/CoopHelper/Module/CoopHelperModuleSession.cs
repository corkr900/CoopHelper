using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Module {
	public class CoopHelperModuleSession : EverestModuleSession {
		public bool IsInCoopSession;
		public int SessionRole = -1;
		public List<PlayerID> SessionMembers = new List<PlayerID>();

		private bool usingCoopEverywhere { get { return CoopHelperModule.Settings?.CoopEverywhere ?? false; } }

		private CoopSessionID _sessionID;
		private bool? _forceCnetInteractions = null;
		private DeathSyncMode _deathMode = DeathSyncMode.None;
		private int? _dashCount = null;
		private string _skin = null;
		private string _ability = null;

		public CoopSessionID SessionID {
			get { return usingCoopEverywhere ? CoopSessionID.CoopEverywhereID : _sessionID; }
			set { _sessionID = value; }
		}
		public bool? ForceCNetInteractions {
			get { return usingCoopEverywhere ? null : _forceCnetInteractions; }
			internal set { _forceCnetInteractions = value; }
		}
		public DeathSyncMode DeathSync {
			get { return usingCoopEverywhere ? DeathSyncMode.SameRoomOnly : _deathMode; }
			set { _deathMode = value; }
		}
		internal int? DashCount {
			get { return usingCoopEverywhere ? null : _dashCount; }
			set { _dashCount = value; }
		}
		internal string Skin {
			get { return usingCoopEverywhere ? null : _skin; }
			set { _skin = value; }
		}
		internal string Ability {
			get { return usingCoopEverywhere ? null : _ability; }
			set { _ability = value; }
		}

		internal HashSet<EntityID> SyncedKeys = new HashSet<EntityID>();
	}

	public enum DeathSyncMode {
		None,
		SameRoomOnly,
		Everywhere,
	}
}
