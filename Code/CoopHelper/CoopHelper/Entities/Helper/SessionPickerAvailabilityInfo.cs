using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities.Helper {

	public enum PlayerRequestState {
		Left,
		Available,
		RequestPending,
		Joined,
		AddedMe,
		ResponsePending,
	}

	public struct PickerPlayerStatus {
		public PlayerID Player;
		public PlayerRequestState State;
		public CoopSessionID? SessionID;
	}

	public class SessionPickerAvailabilityInfo {

		public List<PickerPlayerStatus> AvailablePlayers = new List<PickerPlayerStatus>();

		public int PendingInvites {
			get {
				int cnt = 0;
				foreach (PickerPlayerStatus stat in AvailablePlayers) {
					if (stat.State == PlayerRequestState.RequestPending) ++cnt;
				}
				return cnt;
			}
		}
		public int JoinedPlayers {
			get {
				int cnt = 0;
				foreach (PickerPlayerStatus status in AvailablePlayers) {
					if (status.State == PlayerRequestState.Joined) ++cnt;
				}
				return cnt;
			}
		}

		public int TotalCount { get { return AvailablePlayers.Count; } }

		public PickerPlayerStatus? Get(CoopSessionID sessionID) {
			int idx = AvailablePlayers.FindIndex((PickerPlayerStatus t) => {
				return t.SessionID?.Equals(sessionID) ?? false;
			});
			if (idx < 0 || idx >= AvailablePlayers.Count) return null;
			return AvailablePlayers[idx];
		}

		public PickerPlayerStatus? Get(int idx) {
			if (idx < 0 || idx >= AvailablePlayers.Count) return null;
			return AvailablePlayers[idx];
		}

		public void Set(PlayerID id, PlayerRequestState st, CoopSessionID? sessionID) {
			if (st == PlayerRequestState.AddedMe && sessionID == null) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "sessionID cannot be null when setting player status to AddedMe");
				st = PlayerRequestState.Left;
			}

			int idx = AvailablePlayers.FindIndex((PickerPlayerStatus t) => {
				return t.Player.Equals(id);
			});
			PickerPlayerStatus pps = new PickerPlayerStatus() {
				Player = id,
				State = st,
				SessionID = sessionID,
			};

			if (idx < 0) {
				if (st != PlayerRequestState.Left) AvailablePlayers.Add(pps);
			}
			else AvailablePlayers[idx] = pps;
		}

		//internal List<PlayerID> GetAll(PlayerRequestState state) {
		//	List<PlayerID> ret = new List<PlayerID>();
		//	foreach (PickerPlayerStatus pps in AvailablePlayers) {
		//		if (pps.State == state) {
		//			ret.Add(pps.Player);
		//		}
		//	}
		//	return ret;
		//}
	}
}
