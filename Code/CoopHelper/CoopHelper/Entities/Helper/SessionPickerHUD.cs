using Celeste;
using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	public class SessionPickerHUD : Entity {

		private enum PlayerRequestState {
			Left,
			Available,
			RequestPending,
			Joined,
			AddedMe,
			ResponsePending,
		}

		private struct PickerPlayerStatus {
			public PlayerID Player;
			public PlayerRequestState State;
			public CoopSessionID? SessionID;
		}

		private enum RoleRequestState {
			Available,
			RequestSent,
			RequestReceived
		}

		private struct PickerRoleStatus {
			public string DisplayName;
			public RoleRequestState State;
			public PlayerID Player;
		}

		private Action<SessionPickerHUDCloseArgs> onClose;
		private int membersNeeded;
		private PickerRoleStatus[] roleSelection;
		private List<PickerPlayerStatus> availablePlayers = new List<PickerPlayerStatus>();
		private int hovered;
		private EntityID pickerID;
		private bool pickingRole = false;
		private CoopSessionID finalizedSession;
		private PlayerID[] finalizedPlayers;
		bool sessionCancelled = false;

		private int PendingInvites {
			get {
				int cnt = 0;
				foreach (PickerPlayerStatus stat in availablePlayers) {
					if (stat.State == PlayerRequestState.RequestPending) ++cnt;
				}
				return cnt;
			}
		}
		private int JoinedPlayers {
			get {
				int cnt = 0;
				foreach(PickerPlayerStatus status in availablePlayers) {
					if (status.State == PlayerRequestState.Joined) ++cnt;
				}
				return cnt;
			}
		}

		public SessionPickerHUD(int membersNeeded, EntityID id, string[] roleNames, Action<SessionPickerHUDCloseArgs> onClose) {
			Tag = Tags.HUD;
			this.onClose = onClose;
			this.membersNeeded = membersNeeded;
			pickerID = id;
			if (roleNames != null) {
				roleSelection = new PickerRoleStatus[roleNames.Length];
				for (int i = 0; i < roleNames.Length; i++) {
					roleSelection[i] = new PickerRoleStatus() {
						DisplayName = Dialog.Get(roleNames[i]),
						State = RoleRequestState.Available,
					};
				}
			}
		}

		private void AddHooks() {
			CNetComm.OnReceiveSessionJoinAvailable += OnAvailable;
			CNetComm.OnReceiveSessionJoinRequest += OnRequest;
			CNetComm.OnReceiveSessionJoinResponse += OnResponse;
			CNetComm.OnReceiveSessionJoinFinalize += OnFinalize;
			Everest.Events.Level.OnPause += OnPause;
		}

		private void RemoveHooks() {
			CNetComm.OnReceiveSessionJoinAvailable -= OnAvailable;
			CNetComm.OnReceiveSessionJoinRequest -= OnRequest;
			CNetComm.OnReceiveSessionJoinResponse -= OnResponse;
			CNetComm.OnReceiveSessionJoinFinalize -= OnFinalize;
			Everest.Events.Level.OnPause -= OnPause;
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			AddHooks();
			CNetComm.Instance.Send(new DataSessionJoinAvailable() {
				newAvailability = true,
				pickerID = pickerID,
			}, false);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			RemoveHooks();
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			RemoveHooks();
		}

		//////////////////////////////////////////////////////////////

		private void OnAvailable(DataSessionJoinAvailable data) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", "Received DataSessionJoinAvailable");
			SetStatePlayer(data.senderID, data.newAvailability && data.pickerID.Equals(pickerID) ? PlayerRequestState.Available : PlayerRequestState.Left, null);
			if (finalizedPlayers?.Contains(data.senderID) ?? false) {
				sessionCancelled = true;
			}
		}

		private void OnRequest(DataSessionJoinRequest data) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", "Received DataSessionJoinRequest");
			// Player selection
			if (data.Role < 0) {
				if (!data.TargetID.Equals(PlayerID.MyID)) return;
				if (pickingRole || data.SessionID.creator.Equals(PlayerID.MyID)) {
					CNetComm.Instance.Send(new DataSessionJoinResponse() {
						SessionID = data.SessionID,
						Response = false,
					}, false);
				}
				else SetStatePlayer(data.SenderID, PlayerRequestState.AddedMe, data.SessionID);
			}
			// Role selection
			else if (pickingRole && data.SessionID.Equals(finalizedSession) && data.Role >= 0 && data.Role < (roleSelection?.Length ?? 0)) {
				roleSelection[data.Role].State = RoleRequestState.RequestReceived;
				roleSelection[data.Role].Player = data.SenderID;
				CheckFinalizeRole();
			}
		}

		private void OnResponse(DataSessionJoinResponse data) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", "Received DataSessionJoinResponse");
			int idx = availablePlayers.FindIndex((PickerPlayerStatus t) => {
				return t.SessionID?.Equals(data.SessionID) ?? false;
			});
			if (idx < 0 || idx >= availablePlayers.Count) return;
			PickerPlayerStatus pps = availablePlayers[idx];
			if (data.SessionID == pps.SessionID) {
				if (data.Response) {
					SetStatePlayer(data.SenderID, PlayerRequestState.Joined, data.SessionID);
					CheckFinalizeSession(pps);
				}
				else {
					SetStatePlayer(data.SenderID, PlayerRequestState.Available, null);
				}
			}
		}

		private void OnFinalize(DataSessionJoinFinalize data) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", "Received DataSessionJoinFinalize");
			if (!data.sessionPlayers.Contains(PlayerID.MyID)) return;

			if (data.RolesFinalized) CloseWithSession(data.sessionID, data.sessionPlayers);
			else if (!data.sessionID.Equals(finalizedSession)) FinalizeSession(data.sessionID, data.sessionPlayers);
		}

		private void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
			CloseSelf();
		}

		private void CheckFinalizeSession(PickerPlayerStatus lastResponder) {
			if (lastResponder.SessionID == null) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "Cannot check-finalize session creation; responder doesn't have a known session ID");
				return;
			}
			if (JoinedPlayers + 1 == membersNeeded) {
				FinalizeSession(lastResponder.SessionID.Value, null);
			}
		}

		private bool CheckFinalizeRole() {
			// Basic status check
			if (!pickingRole) return false;
			// Only the session creator is allowed to finalize roles
			if (!finalizedSession.creator.Equals(PlayerID.MyID)) return false;

			// reorder the player list per role selection and check for duplicates
			PlayerID[] idArr = new PlayerID[roleSelection.Length];
			bool readyToFinalize = true;
			bool dupesFound = false;
			for (int i = 0; i < roleSelection.Length; i++) {

				// Make sure all roles are claimed
				if (roleSelection[i].State == RoleRequestState.Available) {
					readyToFinalize = false;
					break;
				}

				// Populate the new array and check for duplicate player IDs
				idArr[i] = roleSelection[i].Player;
				for (int j = 0; j < i; j++) {
					if (idArr[j].Equals(idArr[i])) {
						dupesFound = true;
						break;
					}
				}
				if (dupesFound || !readyToFinalize) break;
			}

			// Handle not-ready conditions
			if (!readyToFinalize) return false;
			if (dupesFound) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "Found duplicate Player IDs when finalizing session; cancelling.");
				sessionCancelled = true;
				return false;
			}

			// apply the reordered array, send fionalization, and close the UI with the new session
			finalizedPlayers = idArr;
			CNetComm.Instance.Send(new DataSessionJoinFinalize() {
				sessionID = finalizedSession,
				sessionPlayers = idArr,
				RolesFinalized = true,
			}, false);
			CloseWithSession(finalizedSession, finalizedPlayers);
			return true;
		}

		private void FinalizeSession(CoopSessionID id, PlayerID[] playerIDs) {
			if (playerIDs == null) {
				playerIDs = new PlayerID[JoinedPlayers + 1];
				playerIDs[0] = PlayerID.MyID;
				int i = 0;
				foreach (PickerPlayerStatus pps in availablePlayers) {
					if (pps.State == PlayerRequestState.Joined) {
						playerIDs[++i] = pps.Player;
					}
				}
			}

			finalizedPlayers = playerIDs;
			finalizedSession = id;

			bool rolesFinalized = roleSelection == null;

			CNetComm.Instance.Send(new DataSessionJoinFinalize() {
				sessionID = id,
				sessionPlayers = playerIDs,
				RolesFinalized = rolesFinalized,
			}, false);

			if (rolesFinalized) {
				CloseWithSession(id, playerIDs);
			}
			else {
				pickingRole = true;
				hovered = 0;
			}
		}

		private void CloseWithSession(CoopSessionID id, PlayerID[] playerIDs) {
			onClose?.Invoke(new SessionPickerHUDCloseArgs() {
				CreateNewSession = true,
				ID = id,
				Players = playerIDs,
			});
		}

		public void CloseSelf() {
			CNetComm.Instance.Send(new DataSessionJoinAvailable() {
				newAvailability = false,
				pickerID = pickerID,
			}, false);
			onClose?.Invoke(new SessionPickerHUDCloseArgs() {
				CreateNewSession = false,
			});
		}

		public override void Update() {
			base.Update();

			if (Input.MenuCancel.Pressed) {
				CloseSelf();
				return;
			}

			int numOptions = pickingRole ? roleSelection.Length : availablePlayers.Count;

			if (Input.MenuDown.Pressed) {
				if (hovered < numOptions - 1) {
					hovered++;
					Audio.Play("event:/ui/main/rollover_down");
				}
				else Audio.Play("event:/ui/main/button_invalid");
			}
			else if (Input.MenuUp.Pressed) {
				if (hovered > 0) {
					hovered--;
					Audio.Play("event:/ui/main/rollover_up");
				}
				else Audio.Play("event:/ui/main/button_invalid");
			}

			if (pickingRole) {
				// Role selection
				if (Input.MenuConfirm.Pressed) {
					if (MenuSelectRole()) Audio.Play("event:/ui/main/button_select");
					else Audio.Play("event:/ui/main/button_invalid");
				}
			}
			else {
				// Players selection
				if (Input.MenuConfirm.Pressed) {
					if (MenuSelectPlayer()) Audio.Play("event:/ui/main/button_select");
					else Audio.Play("event:/ui/main/button_invalid");
				}
			}
		}

		private bool MenuSelectPlayer() {
			if (hovered < 0 || hovered >= availablePlayers.Count) return false;
			PickerPlayerStatus pss = availablePlayers[hovered];
			if (pss.State == PlayerRequestState.Available) {
				PlayerID target = pss.Player;
				CoopSessionID newID = CoopSessionID.GetNewID();
				SetStatePlayer(target, PlayerRequestState.RequestPending, newID);
				CNetComm.Instance.Send(new DataSessionJoinRequest() {
					SessionID = newID,
					TargetID = target,
					Role = -1,
				}, false);
				return true;
			}
			else if (pss.State == PlayerRequestState.AddedMe) {
				CNetComm.Instance.Send(new DataSessionJoinResponse() {
					SessionID = pss.SessionID.Value,
					Response = true,
				}, false);
				return true;
			}
			return false;
		}

		private bool MenuSelectRole() {
			if (sessionCancelled) return false;
			if (hovered < 0 || hovered >= roleSelection.Length) return false;
			for (int i = 0; i < roleSelection.Length; i++) {
				if (roleSelection[i].State == RoleRequestState.RequestSent) return false;
			}
			if (roleSelection[hovered].State == RoleRequestState.RequestSent) return false;

			roleSelection[hovered].State = RoleRequestState.RequestSent;
			roleSelection[hovered].Player = PlayerID.MyID;
			if (!CheckFinalizeRole()) {
				CNetComm.Instance.Send(new DataSessionJoinRequest() {
					SessionID = finalizedSession,
					Role = hovered,
				}, false);
			}
			return true;
		}

		private void SetStatePlayer(PlayerID id, PlayerRequestState st, CoopSessionID? sessionID) {
			if (st == PlayerRequestState.AddedMe && sessionID == null) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "sessionID cannot be null when setting player status to AddedMe");
				st = PlayerRequestState.Left;
			}

			int idx = availablePlayers.FindIndex((PickerPlayerStatus t) => {
				return t.Player.Equals(id);
			});
			PickerPlayerStatus pps = new PickerPlayerStatus() {
				Player = id,
				State = st,
				SessionID = sessionID,
			};

			if (idx < 0) {
				if (st != PlayerRequestState.Left) availablePlayers.Add(pps);
			}
			else availablePlayers[idx] = pps;
		}

		public override void Render() {
			base.Render();

			float yPos = 100;
			List<PlayerID> joined = new List<PlayerID>();
			foreach (PickerPlayerStatus pps in availablePlayers) {
				if (pps.State == PlayerRequestState.Joined) {
					joined.Add(pps.Player);
				}
			}

			// Title
			ActiveFont.DrawOutline(string.Format(Dialog.Get("corkr900_CoopHelper_SessionPickerTitle"), membersNeeded),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 1.5f, Color.White, 2f, Color.Black);
			yPos += 100;

			if (pickingRole) RenderRoleList(ref yPos);
			else RenderPlayerList(ref yPos);
		}

		private void RenderPlayerList(ref float yPos) {
			ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerAvailableTitle"),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			if (availablePlayers.Count == 0) {
				string placeholderText = Dialog.Clean("corkr900_CoopHelper_SessionPickerWaitingForPlayers");
				ActiveFont.DrawOutline(placeholderText, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, Color.Gray, 2f, Color.Black);
				yPos += 60;
			}
			for (int i = 0; i < availablePlayers.Count; i++) {
				PickerPlayerStatus pps = availablePlayers[i];
				if (pps.State == PlayerRequestState.Joined) continue;
				string display = pps.Player.Name;
				Color color = Color.White;
				if (hovered == i) {
					display = "> " + display;
				}
				// TODO translate status names
				if (pps.State == PlayerRequestState.RequestPending) {
					display += " (Pending)";
					color = Color.Yellow;
				}
				if (pps.State == PlayerRequestState.Left) {
					display += " (Left)";
					color = Color.Gray;
				}
				if (pps.State == PlayerRequestState.Joined) {
					display += " (Joined!)";
					color = new Color(0.5f, 1f, 0.5f);
				}
				if (pps.State == PlayerRequestState.AddedMe) {
					display += " (Requested to Join)";
					color = new Color(0.5f, 1f, 0.5f);
				}

				ActiveFont.DrawOutline(display, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, color, 2f, Color.Black);
				yPos += 60;
			}
		}

		private void RenderRoleList(ref float yPos) {
			string subtitle = sessionCancelled ? "corkr900_CoopHelper_SessionPickerCancelled" : "corkr900_CoopHelper_SessionPickerRoleTitle";
			ActiveFont.DrawOutline(Dialog.Get(subtitle), new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			for (int i = 0; i < roleSelection.Length; i++) {
				PickerRoleStatus prs = roleSelection[i];
				string display = prs.DisplayName;
				Color color = Color.White;
				if (hovered == i) {
					display = "> " + display;
				}
				if (prs.State == RoleRequestState.RequestReceived) {
					display += " (" + prs.Player.Name + ")";
				}
				else if (prs.State == RoleRequestState.RequestSent) {
					display += " (" + Dialog.Clean("corkr900_CoopHelper_SessionPickerRequested") + ")";
				}

				ActiveFont.DrawOutline(display, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, color, 1f, Color.Black);
				yPos += 60;
			}
		}

	}

	public class SessionPickerHUDCloseArgs {
		internal CoopSessionID ID;
		internal PlayerID[] Players;
		internal bool CreateNewSession;
	}
}
