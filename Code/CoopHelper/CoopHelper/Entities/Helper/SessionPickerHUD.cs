using Celeste;
using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Entities.Helper;
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
using static MonoMod.InlineRT.MonoModRule;

namespace Celeste.Mod.CoopHelper.Entities {
	public class SessionPickerHUD : Entity {

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

		private SessionPickerAvailabilityInfo availabilityInfo;
		private Action<SessionPickerHUDCloseArgs> onClose;
		private int membersNeeded;
		private PickerRoleStatus[] roleSelection;
		private int hovered;
		private EntityID pickerID;
		private CoopSessionID finalizedSession;
		private PlayerID[] finalizedPlayers;
		bool sessionCancelled = false;

		public bool PickingRole { get; private set; } = false;

		public SessionPickerHUD(SessionPickerAvailabilityInfo availabilityInfo, int membersNeeded, EntityID id, string[] roleNames, Action<SessionPickerHUDCloseArgs> onClose) {
			Tag = Tags.HUD;
			this.availabilityInfo = availabilityInfo;
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

		public override void Added(Scene scene) {
			base.Added(scene);
			CNetComm.Instance.Send(new DataSessionJoinAvailable() {
				newAvailability = true,
				pickerID = pickerID,
			}, false);
		}

		//////////////////////////////////////////////////////////////

		public void OnAvailable(PlayerID sender) {
			if (finalizedPlayers?.Contains(sender) ?? false) {
				sessionCancelled = true;
			}
		}

		public void RoleRequestReceived(PlayerID senderID, int role) {
			if (role < 0 || role >= roleSelection.Length) return;
			roleSelection[role].State = RoleRequestState.RequestReceived;
			roleSelection[role].Player = senderID;
			CheckFinalizeRole();
		}

		public void OnFinalize(CoopSessionID sessionID, PlayerID[] sessionPlayers, bool rolesFinalized) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", "Received DataSessionJoinFinalize");
			if (!sessionPlayers.Contains(PlayerID.MyID)) return;

			if (rolesFinalized) CloseWithSession(sessionID, sessionPlayers);
			else if (!sessionID.Equals(finalizedSession)) FinalizeSession(sessionID, sessionPlayers);
		}

		public bool CanAcceptRoleRequest(CoopSessionID sessionID, int role) {
			return sessionID.Equals(finalizedSession) && role >= 0 && role < (roleSelection?.Length ?? 0);
		}

		public void CheckFinalizeSession(PickerPlayerStatus lastResponder) {
			if (lastResponder.SessionID == null) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "Cannot check-finalize session creation; responder doesn't have a known session ID");
				return;
			}
			if (availabilityInfo.JoinedPlayers + 1 == membersNeeded) {
				FinalizeSession(lastResponder.SessionID.Value, null);
			}
		}

		private bool CheckFinalizeRole() {
			// Basic status check
			if (!PickingRole) return false;
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
				playerIDs = new PlayerID[availabilityInfo.JoinedPlayers + 1];
				playerIDs[0] = PlayerID.MyID;
				int i = 0;
				foreach (PickerPlayerStatus pps in availabilityInfo.AvailablePlayers) {
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
				PickingRole = true;
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

			int numOptions = PickingRole ? roleSelection.Length : availabilityInfo.AvailablePlayers.Count;

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

			if (PickingRole) {
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
			if (hovered < 0 || hovered >= availabilityInfo.TotalCount) return false;
			PickerPlayerStatus pss = availabilityInfo.Get(hovered).Value;
			if (pss.State == PlayerRequestState.Available || pss.State == PlayerRequestState.Conflict) {
				PlayerID target = pss.Player;
				CoopSessionID newID = CoopSessionID.GetNewID();
				availabilityInfo.Set(target, PlayerRequestState.RequestPending, newID);
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
				availabilityInfo.Set(pss.Player, PlayerRequestState.ResponsePending, pss.SessionID.Value);
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

		public override void Render() {
			base.Render();
			float yPos = 100;

			// Shade the background so the text is easier to read
			Draw.Rect(0, 0, 1920, 1080, Color.Black * 0.4f);

			// Title
			ActiveFont.DrawOutline(string.Format(Dialog.Get("corkr900_CoopHelper_SessionPickerTitle"), membersNeeded),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 1.5f, Color.White, 2f, Color.Black);
			yPos += 100;

			if (CNetComm.Instance?.IsConnected != true) {
				yPos += 100;
				ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerConnectToCnet"),
					new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			}
			else if (PickingRole) RenderRoleList(ref yPos);
			else RenderPlayerList(ref yPos);
		}

		private void RenderPlayerList(ref float yPos) {
			ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerAvailableTitle"),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			if (availabilityInfo.TotalCount == 0) {
				string placeholderText = Dialog.Clean("corkr900_CoopHelper_SessionPickerWaitingForPlayers");
				ActiveFont.DrawOutline(placeholderText, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, Color.LightGray, 2f, Color.Black);
				yPos += 60;
				placeholderText = Dialog.Clean("corkr900_CoopHelper_SessionPickerCheckVersion");
				ActiveFont.DrawOutline(placeholderText, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, Color.LightGray, 2f, Color.Black);
				yPos += 60;
			}
			for (int i = 0; i < availabilityInfo.TotalCount; i++) {
				PickerPlayerStatus pps = availabilityInfo.Get(i).Value;
				if (pps.State == PlayerRequestState.Joined) continue;
				string display = pps.Player.Name;
				Color color = Color.White;
				if (hovered == i) {
					display = "> " + display;
				}
				// TODO translate status names
				if (pps.State == PlayerRequestState.RequestPending) {
					display += " (Request Pending)";
					color = Color.Yellow;
				}
				if (pps.State == PlayerRequestState.ResponsePending) {
					display += " (Response Pending)";
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
				if (pps.State == PlayerRequestState.Conflict) {
					display += " (Conflicted; Try Again)";
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
