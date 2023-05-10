﻿using Celeste;
using Celeste.Mod.CoopHelper.Infrastructure;
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

		public SessionPickerEntity(EntityData data, Vector2 offset) : base(data.Position + offset) {
			string idOverride = data.Attr("idOverride");
			string[] idSplit = idOverride?.Split(':');
			if (idSplit != null && idSplit.Length == 2 && int.TryParse(idSplit[1], out int idNum)) {
				ID = new EntityID(PlayerState.Mine?.CurrentMap.SID + PlayerState.Mine?.CurrentRoom + idSplit[0], idNum);
			}
			else {
				ID = new EntityID(PlayerState.Mine?.CurrentMap.SID + data.Level.Name, data.ID);
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
				CoopHelperModule.OnSessionInfoChanged += SessionInfoChanged;
			}
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			CoopHelperModule.OnSessionInfoChanged -= SessionInfoChanged;
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			CoopHelperModule.OnSessionInfoChanged -= SessionInfoChanged;
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
			hud = new SessionPickerHUD(PlayersNeeded, ID, Close);
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
	}
}
