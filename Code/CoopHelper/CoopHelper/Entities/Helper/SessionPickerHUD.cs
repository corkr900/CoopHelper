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

		private enum RequestState {
			Left,
			Available,
			RequestPending,
			Joined,
			AddedMe,
			ResponsePending,
		}

		private struct PickerPlayerStatus {
			public PlayerID Player;
			public RequestState State;
			public CoopSessionID? SessionID;
		}

		private Action<SessionPickerHUDCloseArgs> onClose;
		private int membersNeeded;
		private string[] roleSelection;
		private List<PickerPlayerStatus> availablePlayers = new List<PickerPlayerStatus>();
		private int hovered;
		private EntityID pickerID;

		private int PendingInvites {
			get {
				int cnt = 0;
				foreach (PickerPlayerStatus stat in availablePlayers) {
					if (stat.State == RequestState.RequestPending) ++cnt;
				}
				return cnt;
			}
		}
		private int JoinedPlayers {
			get {
				int cnt = 0;
				foreach(PickerPlayerStatus status in availablePlayers) {
					if (status.State == RequestState.Joined) ++cnt;
				}
				return cnt;
			}
		}

		public SessionPickerHUD(int membersNeeded, EntityID id, string[] roleNames, Action<SessionPickerHUDCloseArgs> onClose) {
			Tag = Tags.HUD;
			this.onClose = onClose;
			this.membersNeeded = membersNeeded;
			pickerID = id;
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
			SetState(data.senderID, data.newAvailability && data.pickerID.Equals(pickerID) ? RequestState.Available : RequestState.Left, null);
		}

		private void OnRequest(DataSessionJoinRequest data) {
			if (!data.targetID.Equals(PlayerID.MyID)) return;
			if (JoinedPlayers == 0 && PendingInvites == 0 && !data.sessionID.creator.Equals(PlayerID.MyID)) {
				SetState(data.senderID, RequestState.AddedMe, data.sessionID);
			}
			else {
				CNetComm.Instance.Send(new DataSessionJoinResponse() {
					sessionID = data.sessionID,
					response = false,
					respondingToRoleRequest = false,
				}, false);
			}
		}

		private void OnResponse(DataSessionJoinResponse data) {
			int idx = availablePlayers.FindIndex((PickerPlayerStatus t) => {
				return t.SessionID?.Equals(data.sessionID) ?? false;
			});
			if (idx < 0 || idx >= availablePlayers.Count) return;
			PickerPlayerStatus pps = availablePlayers[idx];
			if (data.sessionID == pps.SessionID) {
				if (data.response) {
					SetState(data.senderID, RequestState.Joined, data.sessionID);
					CheckFinalizeSession(pps);
				}
				else {
					SetState(data.senderID, RequestState.Available, null);
				}
			}
		}

		private void OnFinalize(DataSessionJoinFinalize data) {
			int idx = availablePlayers.FindIndex((PickerPlayerStatus t) => {
				return t.SessionID?.Equals(data.sessionID) ?? false;
			});
			if (idx < 0 || idx >= availablePlayers.Count) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "Could not find session with ID matching the finalize message");
				return;
			}
			FinalizeSession(availablePlayers[idx].SessionID.Value, data.sessionPlayers);
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

		private void FinalizeSession(CoopSessionID id, PlayerID[] playerIDs) {
			if (playerIDs == null) {
				playerIDs = new PlayerID[JoinedPlayers + 1];
				playerIDs[0] = PlayerID.MyID;
				int i = 0;
				foreach (PickerPlayerStatus pps in availablePlayers) {
					if (pps.State == RequestState.Joined) {
						playerIDs[++i] = pps.Player;
					}
				}
			}

			if (roleSelection == null) {
				FinalizeAndClose(id, playerIDs);
			}
			else {
				// TODO
				FinalizeAndClose(id, playerIDs);
			}
		}

		private void FinalizeAndClose(CoopSessionID id, PlayerID[] playerIDs) {
			CNetComm.Instance.Send(new DataSessionJoinFinalize() {
				sessionID = id,
				sessionPlayers = playerIDs,
			}, false);

			onClose?.Invoke(new SessionPickerHUDCloseArgs() {
				CreateNewSession = true,
				ID = id,
				Players = playerIDs,
			});
		}

		public override void Render() {
			base.Render();

			float yPos = 100;
			List<PlayerID> joined = new List<PlayerID>();
			foreach (PickerPlayerStatus pps in availablePlayers) {
				if (pps.State == RequestState.Joined) {
					joined.Add(pps.Player);
				}
			}

			// Title
			ActiveFont.DrawOutline(string.Format(Dialog.Get("corkr900_CoopHelper_SessionPickerTitle"), membersNeeded),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 1.5f, Color.White, 2f, Color.Black);
			yPos += 100;

			// Player List
			ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerAvailableTitle"),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			for (int i = 0; i < availablePlayers.Count; i++) {
				PickerPlayerStatus pps = availablePlayers[i];
				if (pps.State == RequestState.Joined) continue;
				string display = pps.Player.Name;
				Color color = Color.White;
				if (hovered == i) {
					display = "> " + display;
				}
				// TODO translate status names
				if (pps.State == RequestState.RequestPending) {
					display += " (Pending)";
					color = Color.Yellow;
				}
				if (pps.State == RequestState.Left) {
					display += " (Left)";
					color = Color.Gray;
				}
				if (pps.State == RequestState.Joined) {
					display += " (Joined!)";
					color = new Color(0.5f, 1f, 0.5f);
				}

				ActiveFont.DrawOutline(display, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, color, 1f, Color.Black);
				yPos += 100;
			}
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

			if (Input.MenuDown.Pressed) {
				if (hovered < availablePlayers.Count - 1) {
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

			if (Input.MenuConfirm.Pressed) {
				if (MenuSelectPlayer()) Audio.Play("event:/ui/main/button_select");
				else Audio.Play("event:/ui/main/button_invalid");
			}
		}

		private bool MenuSelectPlayer() {
			if (hovered < 0 || hovered >= availablePlayers.Count) return false;
			PickerPlayerStatus pss = availablePlayers[hovered];
			if (pss.State == RequestState.Available) {
				PlayerID target = pss.Player;
				CoopSessionID newID = CoopSessionID.GetNewID();
				SetState(target, RequestState.RequestPending, newID);
				CNetComm.Instance.Send(new DataSessionJoinRequest() {
					sessionID = newID,
					targetID = target,
					role = -1,
				}, false);
				return true;
			}
			else if (pss.State == RequestState.AddedMe) {
				CNetComm.Instance.Send(new DataSessionJoinResponse() {
					sessionID = pss.SessionID.Value,
					response = true,
					respondingToRoleRequest = false,
				}, false);
				return true;
			}
			return false;
		}

		private void SetState(PlayerID id, RequestState st, CoopSessionID? sessionID) {
			if (st == RequestState.AddedMe && sessionID == null) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "sessionID cannot be null when setting player status to AddedMe");
				st = RequestState.Left;
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
				if (st != RequestState.Left) availablePlayers.Add(pps);
			}
			else availablePlayers[idx] = pps;
		}
	}

	public class SessionPickerHUDCloseArgs {
		internal CoopSessionID ID;
		internal PlayerID[] Players;
		internal bool CreateNewSession;
	}
}
