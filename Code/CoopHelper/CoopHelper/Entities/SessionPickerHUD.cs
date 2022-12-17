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
			Pending,
			Joined,
		}

		private Action<SessionPickerHUDCloseArgs> onClose;
		private int membersNeeded;
		private List<Tuple<PlayerID, RequestState>> availablePlayers = new List<Tuple<PlayerID, RequestState>>();
		private CoopSessionID sessionID;
		private int hovered;

		private int PendingInvites {
			get {
				int cnt = 0;
				foreach (Tuple<PlayerID, RequestState> tup in availablePlayers) {
					if (tup.Item2 == RequestState.Pending) ++cnt;
				}
				return cnt;
			}
		}
		private int JoinedPlayers {
			get {
				int cnt = 0;
				foreach(Tuple<PlayerID, RequestState> tup in availablePlayers) {
					if (tup.Item2 == RequestState.Joined) ++cnt;
				}
				return cnt;
			}
		}

		private bool CanSendRequests { get { return sessionID.creator.Equals(PlayerID.MyID); } }

		public SessionPickerHUD(int membersNeeded, Action<SessionPickerHUDCloseArgs> onClose) {
			Tag = Tags.HUD;
			this.onClose = onClose;
			this.membersNeeded = membersNeeded;
		}

		public override void Added(Scene scene) {
			base.Added(scene);

			CNetComm.OnReceiveSessionJoinAvailable += OnAvailable;
			CNetComm.OnReceiveSessionJoinRequest += OnRequest;
			CNetComm.OnReceiveSessionJoinResponse += OnResponse;
			CNetComm.OnReceiveSessionJoinFinalize += OnFinalize;

			sessionID = CoopSessionID.GetNewID();

			CNetComm.Instance.Send(new DataSessionJoinAvailable() {
				newAvailability = true,
			}, false);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);

			CNetComm.OnReceiveSessionJoinAvailable -= OnAvailable;
			CNetComm.OnReceiveSessionJoinRequest -= OnRequest;
			CNetComm.OnReceiveSessionJoinResponse -= OnResponse;
			CNetComm.OnReceiveSessionJoinFinalize -= OnFinalize;
		}

		private void OnAvailable(DataSessionJoinAvailable data) {
			SetState(data.senderID, data.newAvailability ? RequestState.Available : RequestState.Left);
			if (data.senderID.Equals(sessionID.creator)) {
				sessionID = CoopSessionID.GetNewID();
			}
		}

		private void OnRequest(DataSessionJoinRequest data) {
			if (!data.targetID.Equals(PlayerID.MyID)) return;
			if (JoinedPlayers == 0 && PendingInvites == 0 && !data.sessionID.creator.Equals(PlayerID.MyID)) {
				sessionID = data.sessionID;
				CNetComm.Instance.Send(new DataSessionJoinResponse() {
					sessionID = sessionID,
					response = true,
				}, false);
			}
			else {
				CNetComm.Instance.Send(new DataSessionJoinResponse() {
					sessionID = sessionID,
					response = false,
				}, false);
			}
		}

		private void OnResponse(DataSessionJoinResponse data) {
			if (CanSendRequests && data.sessionID == sessionID) {
				if (data.response) {
					SetState(data.senderID, RequestState.Joined);
					CheckFinalize();
				}
				else {
					SetState(data.senderID, RequestState.Available);
				}
			}
		}

		private void OnFinalize(DataSessionJoinFinalize data) {
			DoFinalize(data.sessionID, data.sessionPlayers);
		}

		private void CheckFinalize() {
			int joined = JoinedPlayers + 1;
			if (joined == membersNeeded) {
				PlayerID[] ids = new PlayerID[joined];
				ids[0] = PlayerID.MyID;
				int i = 0;
				foreach (Tuple<PlayerID, RequestState> tup in availablePlayers) {
					if (tup.Item2 == RequestState.Joined) {
						ids[++i] = tup.Item1;
					}
				}
				CNetComm.Instance.Send(new DataSessionJoinFinalize() {
					sessionID = sessionID,
					sessionPlayers = ids,
				}, false);
				DoFinalize(sessionID, ids);
			}
		}

		private void DoFinalize(CoopSessionID id, PlayerID[] players) {
			CoopHelperModuleSession ses = CoopHelperModule.Session;
			if (ses == null) return;

			int myRole = -1;
			for (int i = 0; i < players.Length; i++) {
				if (players[i].Equals(PlayerID.MyID)) {
					myRole = i;
				}
			}
			if (myRole < 0) return;  // I'm not in this

			ses.IsInCoopSession = true;
			ses.SessionID = id;
			ses.SessionRole = myRole;
			ses.SessionMembers = new List<PlayerID>(players);

			onClose?.Invoke(new SessionPickerHUDCloseArgs());
		}

		public override void Render() {
			base.Render();

			float yPos = 100;
			List<PlayerID> joined = new List<PlayerID>();
			foreach (Tuple<PlayerID, RequestState> kvp in availablePlayers) {
				if (kvp.Item2 == RequestState.Joined) {
					joined.Add(kvp.Item1);
				}
			}

			// Title
			if (CanSendRequests) {
				ActiveFont.DrawOutline(string.Format(Dialog.Get("corkr900_CoopHelper_SessionPickerTitle"), membersNeeded),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 1.5f, Color.White, 2f, Color.Black);
			}
			else {
				ActiveFont.DrawOutline(string.Format(Dialog.Get("corkr900_CoopHelper_SessionPickerTitleAlt"), sessionID.creator.Name),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 1.5f, Color.White, 2f, Color.Black);
			}
			yPos += 100;

			// Player List
			ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerAvailableTitle"),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			foreach (Tuple<PlayerID,RequestState> kvp in availablePlayers) {
				if (kvp.Item2 == RequestState.Joined) continue;
				string display = kvp.Item1.Name;
				Color color = Color.White;
				// TODO do better
				if (kvp.Item2 == RequestState.Pending) {
					display += " (Pending)";
					color = Color.Yellow;
				}
				if (kvp.Item2 == RequestState.Left) {
					display += " (Left)";
					color = Color.Gray;
				}
				if (kvp.Item2 == RequestState.Joined) {
					display += " (Joined!)";
					color = new Color(0.5f, 1f, 0.5f);
				}

				ActiveFont.DrawOutline(display, new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, Color.White, 1f, Color.Black);
				yPos += 100;
			}
		}

		public override void Update() {
			base.Update();

			// TODO

			if (Input.MenuCancel.Pressed) {
				CNetComm.Instance.Send(new DataSessionJoinAvailable() {
					newAvailability = false,
				}, false);
				onClose?.Invoke(new SessionPickerHUDCloseArgs());
				return;
			}
			if (CanSendRequests && Input.MenuConfirm.Pressed) {
				if (hovered >= 0 && hovered < availablePlayers.Count && availablePlayers[hovered].Item2 == RequestState.Available) {
					PlayerID target = availablePlayers[hovered].Item1;
					Audio.Play("event:/ui/main/button_select");
					SetState(target, RequestState.Pending);
					CNetComm.Instance.Send(new DataSessionJoinRequest() {
						sessionID = sessionID,
						targetID = target,
					}, false);
				}
				else Audio.Play("event:/ui/main/button_invalid");
			}
		}

		private void SetState(PlayerID id, RequestState st) {
			int idx = availablePlayers.FindIndex((Tuple<PlayerID, RequestState> t) => {
				return t.Item1.Equals(id);
			});
			if (idx < 0) {
				if (st != RequestState.Left) availablePlayers.Add(new Tuple<PlayerID, RequestState>(id, st));
			}
			else availablePlayers[idx] = new Tuple<PlayerID, RequestState>(id, st);
		}
	}

	public class SessionPickerHUDCloseArgs {

	}
}
