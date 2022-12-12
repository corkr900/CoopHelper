using Celeste;
using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
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
		}

		private void OnRequest(DataSessionJoinRequest data) {
			if (JoinedPlayers == 0 && PendingInvites == 0 && !data.sessionID.creator.Equals(PlayerID.MyID)) {
				sessionID = data.sessionID;
			}
			// TODO send response
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
			// TODO
		}

		private void CheckFinalize() {
			if (JoinedPlayers == membersNeeded) {
				// TODO
			}
		}

		public override void Render() {
			base.Update();

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

			// In Session
			ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerCurrentTitle"),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			foreach (PlayerID id in joined) {
				ActiveFont.DrawOutline(id.Name,
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One*0.7f, Color.White, 1f, Color.Black);
				yPos += 70;
			}

			// Available
			ActiveFont.DrawOutline(Dialog.Get("corkr900_CoopHelper_SessionPickerAvailableTitle"),
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One, Color.LightGray, 2f, Color.Black);
			yPos += 100;
			foreach (Tuple<PlayerID,RequestState> kvp in availablePlayers) {
				if (kvp.Item2 == RequestState.Joined) continue;
				string display = kvp.Item1.Name;
				if (kvp.Item2 == RequestState.Pending) display += " (Pending)";  // TODO do better
				ActiveFont.DrawOutline(kvp.Item1.Name,
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, Color.White, 1f, Color.Black);
				yPos += 100;
			}
		}

		public override void Update() {
			base.Update();

			// TODO

			if (Input.MenuCancel.Pressed) {
				onClose?.Invoke(new SessionPickerHUDCloseArgs());
			}
			if (Input.MenuConfirm.Pressed) {
				if (hovered >= 0 && hovered < availablePlayers.Count && availablePlayers[hovered].Item2 == RequestState.Available) {
					Audio.Play("event:/ui/main/button_select");
					// TODO send a response
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
