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
		private Dictionary<PlayerID, RequestState> availablePlayers = new Dictionary<PlayerID, RequestState>();
		private CoopSessionID sessionID;
		private PlayerID hovered;

		public SessionPickerHUD(int membersNeeded, Action<SessionPickerHUDCloseArgs> onClose) {
			Tag = Tags.HUD;
			this.onClose = onClose;
			this.membersNeeded = membersNeeded;
		}

		public override void Added(Scene scene) {
			base.Added(scene);

			CNetComm.OnReceiveSessionJoinRequest += OnRequest;

			members.Add(PlayerID.MyID);
			sessionID = CoopSessionID.GetNewID();
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);

			CNetComm.OnReceiveSessionJoinRequest -= OnRequest;
		}

		private void OnRequest(DataSessionJoinRequest data) {
			if (!availablePlayers.ContainsKey(data.senderID)) {
				availablePlayers.Add(data.senderID, RequestState.Available);
			}
			else if (availablePlayers[data.senderID] != RequestState.Joined) {
				availablePlayers[data.senderID] = RequestState.Available;
			}
		}

		private void OnResponse(DataSessionJoinResponse data) {

		}

		private void OnConfirmation(DataSessionJoinRequest data) {

		}

		public override void Render() {
			base.Update();

			float yPos = 100;
			List<PlayerID> joined = new List<PlayerID>();
			foreach (KeyValuePair<PlayerID, RequestState> kvp in availablePlayers) {
				if (kvp.Value == RequestState.Joined) {
					joined.Add(kvp.Key);
				}
			}

			// Title
			ActiveFont.DrawOutline(string.Format(Dialog.Get("corkr900_CoopHelper_SessionPickerTitle"), membersNeeded),
			new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 1.5f, Color.White, 2f, Color.Black);
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
			foreach (KeyValuePair<PlayerID,RequestState> kvp in availablePlayers) {
				if (kvp.Value == RequestState.Joined) continue;
				string display = kvp.Key.Name;
				if (kvp.Value == RequestState.Pending) display += " (Pending)";  // TODO do better
				ActiveFont.DrawOutline(kvp.Key.Name,
				new Vector2(960, yPos), Vector2.UnitX / 2f, Vector2.One * 0.7f, Color.White, 1f, Color.Black);
				yPos += 100;
			}
		}

		public override void Update() {
			base.Update();

			if (Input.MenuCancel.Pressed) {
				onClose?.Invoke(new SessionPickerHUDCloseArgs());
			}
			if (Input.MenuConfirm.Pressed) {
				if (availablePlayers.ContainsKey(hovered) && availablePlayers[hovered] == RequestState.Available) {
					Audio.Play("event:/ui/main/button_select");
					// TODO send a response
				}
				else Audio.Play("event:/ui/main/button_invalid");
			}
		}
	}

	public class SessionPickerHUDCloseArgs {

	}
}
