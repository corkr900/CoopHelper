using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
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
		Conflict,
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

		public PickerPlayerStatus? Get(int idx) {
			if (idx < 0 || idx >= AvailablePlayers.Count) return null;
			return AvailablePlayers[idx];
		}

		public PickerPlayerStatus? Get(PlayerID player, CoopSessionID? session) {
			for (int i = 0; i < AvailablePlayers.Count; i++) {
				if (AvailablePlayers[i].Player.Equals(player)) {
					if (AvailablePlayers[i].State == PlayerRequestState.Left) {
						return null;
					}
					else if (AvailablePlayers[i].SessionID == session) {
						return AvailablePlayers[i];
					}
					else {
						PickerPlayerStatus sts = AvailablePlayers[i];
						sts.State = PlayerRequestState.Conflict;
						sts.SessionID = null;
						AvailablePlayers[i] = sts;
						return null;
					}
				}
			}
			return null;
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

		public void ResetPending() {
			for (int i = 0; i  < AvailablePlayers.Count; i++) {
				PickerPlayerStatus pps = AvailablePlayers[i];
				switch(pps.State) {
					case PlayerRequestState.AddedMe:
						if (pps.SessionID != null) {
							CNetComm.Instance.Send(new DataSessionJoinResponse() {
								SessionID = pps.SessionID.Value,
								Response = false,
							}, false);
						}
						Set(pps.Player, PlayerRequestState.Available, null);
						break;

					case PlayerRequestState.RequestPending:
					case PlayerRequestState.ResponsePending:
					case PlayerRequestState.Conflict:
						Set(pps.Player, PlayerRequestState.Available, null);
						break;
					default:
						break;
				}
			}
		}

	}
}
