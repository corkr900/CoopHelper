using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Module {
	public class CoopHelperModuleSession : EverestModuleSession {

		private CoopSessionID _sessionID;
		private DeathSyncMode _deathMode = DeathSyncMode.None;
		private bool? _forceCnetInteractions = null;
		private int? _dashCount = null;
		private string _skin = null;
		private string _ability = null;
		private bool? _coopEverywhereOverride = null;

		public bool CoopEverywhere => _coopEverywhereOverride ?? (CoopHelperModule.Settings?.CoopEverywhere == true);

		public bool InSessionIncludingEverywhere => IsInCoopSession || CoopEverywhere;

		public bool IsInCoopSession { get; set; }

		public int SessionRole { get; set; } = -1;

		public List<PlayerID> SessionMembers { get; set; } = new List<PlayerID>();

		public CoopSessionID SessionID {
			get { return CoopEverywhere ? CoopSessionID.CoopEverywhereID : _sessionID; }
			set { _sessionID = value; }
		}

		public bool? ForceCNetInteractions {
			get { return CoopEverywhere ? null : _forceCnetInteractions; }
			internal set { _forceCnetInteractions = value; }
		}

		public DeathSyncMode DeathSync {
			get { return CoopEverywhere ? DeathSyncMode.SameRoomOnly : _deathMode; }
			set { _deathMode = value; }
		}

		internal int? DashCount {
			get { return CoopEverywhere ? null : _dashCount; }
			set { _dashCount = value; }
		}

		internal string Skin {
			get { return CoopEverywhere ? null : _skin; }
			set { _skin = value; }
		}

		internal string Ability {
			get { return CoopEverywhere ? null : _ability; }
			set { _ability = value; }
		}

		internal HashSet<EntityID> SyncedKeys = new();

		public bool SetCoopEverywhereOverride(bool? newVal) {
			_coopEverywhereOverride = newVal;
			return CoopEverywhere;
		}
	}

	public enum DeathSyncMode {
		None,
		SameRoomOnly,
		Everywhere,
	}
}
