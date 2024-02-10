using Celeste;
using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Entities.Helper;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SessionPicker")]
	public class SessionPickerEntity : Entity {
		private Sprite sprite;
		private int PlayersNeeded = 2;
		private bool removeIfSessionExists;
		private SessionPickerHUD hud;
		private Player player;
		private TalkComponent talkComponent;
		private string[] forceSkin = null;
		private int[] dashes = null;
		private string[] abilities = null;
		private DeathSyncMode DeathMode = DeathSyncMode.SameRoomOnly;
		private EntityID ID;
		private string[] roleNames = null;
		private SessionPickerAvailabilityInfo availabilityInfo;

		public SessionPickerEntity(EntityData data, Vector2 offset) : base(data.Position + offset) {
			availabilityInfo = new SessionPickerAvailabilityInfo();
			string idOverride = data.Attr("idOverride");
			string[] idSplit = idOverride?.Split(':');
			if (idSplit != null && idSplit.Length == 2 && int.TryParse(idSplit[1], out int idNum)) {
				ID = new EntityID(PlayerState.Mine?.CurrentMap.SID + PlayerState.Mine?.CurrentRoom + idSplit[0], idNum);
			}
			else {
				ID = new EntityID(PlayerState.Mine?.CurrentMap.SID + (data.Level?.Name ?? PlayerState.Mine?.CurrentRoom), data.ID);
			}
			Position = data.Position + offset;
			removeIfSessionExists = data.Bool("removeIfSessionExists", true);
			Add(sprite = GFX.SpriteBank.Create("corkr900_CoopHelper_SessionPicker"));
			sprite.Play("idle");
			sprite.Position = new Vector2(-8, -16);
			Add(talkComponent = new TalkComponent(
				new Rectangle(-16, -16, 32, 32),
				new Vector2(0, -16),
				Open
			) { PlayerMustBeFacing = false });

			// Role names
			string namesArg = data.Attr("roleNames", null);
			if (!string.IsNullOrEmpty(namesArg)) {
				string[] split = namesArg.Split(',');
				if (split?.Length == PlayersNeeded) {
					bool valid = true;
					for (int i = 0; i < split.Length; i++) {
						split[i] = split[i].Trim();
						if (string.IsNullOrEmpty(split[i])) {
							valid = false;
							break;
						}
					}
					if (valid) roleNames = split;
				}
			}

			// Role-skin attr
			string skinArg = data.Attr("skins", null);
			if (!string.IsNullOrEmpty(skinArg)) {
				forceSkin = skinArg.Split(',');
				for (int i = 0; i < forceSkin.Length; i++) {
					forceSkin[i] = forceSkin[i].Trim();
				}
			}
			// Role-dashes attr
			string dashesarg = data.Attr("dashes", null);
			if (!string.IsNullOrEmpty(dashesarg)) {
				string[] split = dashesarg.Split(',');
				dashes = new int[split.Length];
				for (int i = 0; i < split.Length; i++) {
					if (int.TryParse(split[i].Trim(), out int numdash)) {
						dashes[i] = numdash;
					}
					else {
						Logger.Log("Co-op Helper", "Invalid dash count attribute in session picker. Room " + data.Level.Name);
						dashes = null;
						break;
					}
				}
			}
			// Role-abilities attr
			string abilitiesarg = data.Attr("abilities", null);
			if (!string.IsNullOrEmpty(abilitiesarg)) {
				string[] split = abilitiesarg.Split(',');
				abilities = new string[split.Length];
				for (int i = 0; i < split.Length; i++) {
					abilities[i] = split[i].Trim().ToLower();
					if (!IsValidAbility(abilities[i])) {
						Logger.Log(LogLevel.Warn, "Co-op Helper", "Session picker has invalid ability: " + abilities[i]);
						abilities[i] = "none";
					}
				}
			}
			// Misc sync behavior
			string deathMode = data.Attr("deathSyncMode", "SameRoomOnly");
			switch (deathMode?.ToLower()) {
				default:
				case "sameroomonly":
					DeathMode = DeathSyncMode.SameRoomOnly;
					break;
				case "none":
					DeathMode = DeathSyncMode.None;
					break;
				case "everywhere":
					DeathMode = DeathSyncMode.Everywhere;
					break;
			}
		}

		private bool IsValidAbility(string v) {
			switch (v) {
				default:
					return false;
				case "none":
				case "grapple":
				case "":
				case null:
					return true;
			}
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			if (!CheckRemove()) {
				AddHooks();
			}
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			RemoveHooks();
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			RemoveHooks();
		}

		private void SessionInfoChanged() {
			CheckRemove();
		}

		private bool CheckRemove() {
			if (removeIfSessionExists && CoopHelperModule.Session?.IsInCoopSession == true) {
				if (talkComponent != null) {
					talkComponent.Enabled = false;
				}
				if (hud != null) {
					hud.CloseSelf();
					hud = null;
				}
				RemoveSelf();
				return true;
			}
			return false;
		}

		public void Open(Player player) {
			if (hud != null) return;  // Already open
			hud = new SessionPickerHUD(availabilityInfo, PlayersNeeded, ID, roleNames, Close);
			Scene.Add(hud);
			player.StateMachine.State = Player.StDummy;
			this.player = player;
			Audio.Play("event:/ui/game/pause");
		}

		public void Close(SessionPickerHUDCloseArgs args) {
			if (hud == null) return;  // Already closed
			player.StateMachine.State = Player.StNormal;
			Scene.Remove(hud);
			hud = null;
			Audio.Play("event:/ui/game/unpause");
			Session currentSession = SceneAs<Level>()?.Session;
			if (args.CreateNewSession && currentSession != null) {
				MakeSession(currentSession, args.Players, args.ID);
			}
			else {
				LeaveSession(currentSession);
			}
			availabilityInfo.ResetPending();
			CheckRemove();
		}

		internal void LeaveSession(Session currentSession) {
			//currentSession.SetFlag("CoopHelper_InSession", false);
			//for (int i = 0; i < PlayersNeeded; i++) {
			//	currentSession.SetFlag("CoopHelper_SessionRole_" + i, false);
			//}
		}

		/// <summary>
		/// Creates a co-op session using the role-specific properties defined by this entity's settings
		/// </summary>
		/// <param name="currentSession">Session object for the current playthrough</param>
		/// <param name="players">List of Player IDs included in the session</param>
		/// <param name="id">Session ID to use, if available. A new ID will be generated if one is not provided</param>
		internal void MakeSession(Session currentSession, PlayerID[] players, CoopSessionID? id = null) {
			int? dash = null;
			string skin = "";
			string ability = "";

			// Figure out my role
			int myRole = -1;
			for (int i = 0; i < players.Length; i++) {
				if (players[i].Equals(PlayerID.MyID)) {
					myRole = i;
				}
			}
			if (myRole < 0) return;  // I'm not in this one

			// Apply role-specific dash count
			if (dashes != null && dashes.Length > 0) {
				Session session = SceneAs<Level>()?.Session;
				if (session != null) dash = dashes[myRole % dashes.Length];
				else Logger.Log(LogLevel.Warn, "Co-op Helper", "Could not change dash count: no session avilable");
			}

			// Get role-specific skins
			if (forceSkin != null && forceSkin.Length > 0) {
				skin = forceSkin[myRole % forceSkin.Length];
			}

			// Apply role-specific abilities
			if (abilities != null && abilities.Length > 0) {
				ability = abilities[myRole % abilities.Length];
			}

			// Apply it
			CoopHelperModule.MakeSession(currentSession, players, id, dash, DeathMode, ability, skin);
		}

		#region Lifecycle & Comms Stuff

		private void AddHooks() {
			CoopHelperModule.OnSessionInfoChanged += SessionInfoChanged;
			CNetComm.OnReceiveSessionJoinAvailable += OnAvailable;
			CNetComm.OnReceiveSessionJoinRequest += OnRequest;
			CNetComm.OnReceiveSessionJoinResponse += OnResponse;
			CNetComm.OnReceiveSessionJoinFinalize += OnFinalize;
			Everest.Events.Level.OnPause += OnPause;
		}

		private void RemoveHooks() {
			CoopHelperModule.OnSessionInfoChanged -= SessionInfoChanged;
			CNetComm.OnReceiveSessionJoinAvailable -= OnAvailable;
			CNetComm.OnReceiveSessionJoinRequest -= OnRequest;
			CNetComm.OnReceiveSessionJoinResponse -= OnResponse;
			CNetComm.OnReceiveSessionJoinFinalize -= OnFinalize;
			Everest.Events.Level.OnPause -= OnPause;
		}

		private void OnAvailable(DataSessionJoinAvailable data) {
			PlayerRequestState newstate = data.newAvailability && data.pickerID.Equals(ID) ? PlayerRequestState.Available : PlayerRequestState.Left;
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Received DataSessionJoinAvailable from {data.senderID.Name}. New state: {newstate}");
			availabilityInfo.Set(data.senderID, newstate, null);
			hud?.OnAvailable(data.senderID);
		}

		private void OnRequest(DataSessionJoinRequest data) {
			bool pickingRole = hud?.PickingRole ?? false;
			bool acceptRoleRequest = hud?.CanAcceptRoleRequest(data.SessionID, data.Role) ?? false;
			// Player selection
			if (data.Role < 0) {
				if (!data.TargetID.Equals(PlayerID.MyID)) return;  // Requesting to join myself?? Weird. Ignore.
				if (pickingRole || data.SessionID.creator.Equals(PlayerID.MyID)) {
					// Auto-reject if I'm already in role selection. Session creator check is desync protection.
					CNetComm.Instance.Send(new DataSessionJoinResponse() {
						SessionID = data.SessionID,
						Response = false,
					}, false);
				}
				else availabilityInfo.Set(data.SenderID, PlayerRequestState.AddedMe, data.SessionID);  // Normal request
			}
			// Role selection
			else if (pickingRole && acceptRoleRequest) {
				hud?.RoleRequestReceived(data.SenderID, data.Role);
			}
		}

		private void OnResponse(DataSessionJoinResponse data) {
			if (data.SenderID.Equals(PlayerID.MyID)) return;  // Probably not needed but double checking
			PickerPlayerStatus? pps = availabilityInfo.Get(data.SenderID, data.SessionID);
			if (pps == null) return;
			if (pps.Value.State == PlayerRequestState.Conflict) {  // Resolve conflicted state
				availabilityInfo.Set(data.SenderID, PlayerRequestState.AddedMe, data.SessionID);
			}
			else if (data.SessionID == pps?.SessionID) {
				if (data.Response) {
					availabilityInfo.Set(data.SenderID, PlayerRequestState.Joined, data.SessionID);
					hud?.CheckFinalizeSession(pps.Value);
				}
				else {
					availabilityInfo.Set(data.SenderID, PlayerRequestState.Available, null);
				}
			}
		}

		private void OnFinalize(DataSessionJoinFinalize data) {
			if (!data.sessionPlayers.Contains(PlayerID.MyID)) return;
			hud?.OnFinalize(data.sessionID, data.sessionPlayers, data.RolesFinalized);
		}

		private void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
			hud?.CloseSelf();
		}

		#endregion

	}
}
