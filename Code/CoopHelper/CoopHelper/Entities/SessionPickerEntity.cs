using Celeste;
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

		public SessionPickerEntity(EntityData data, Vector2 offset) : base(data.Position + offset) {
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
				string[] split = dashesarg.Split(',');
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
			hud = new SessionPickerHUD(PlayersNeeded, Close);
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
			currentSession.SetFlag("CoopHelper_InSession", false);
			for (int i = 0; i < PlayersNeeded; i++) {
				currentSession.SetFlag("CoopHelper_SessionRole_" + i, false);
			}
		}

		internal void MakeSession(Session currentSession, PlayerID[] players, CoopSessionID? id = null) {
			// Set up basic session data and flags
			if (id == null) id = CoopSessionID.GetNewID();
			CoopHelperModule.Instance.ChangeSessionInfo(id.Value, players, DeathMode);
			int role = CoopHelperModule.Session.SessionRole;
			currentSession.SetFlag("CoopHelper_InSession", true);
			for (int i = 0; i < PlayersNeeded; i++) {
				currentSession.SetFlag("CoopHelper_SessionRole_" + i, i == role);
			}

			// Apply role-specific skins
			if (forceSkin != null && forceSkin.Length > 0) {
				string newSkin = forceSkin[role % forceSkin.Length];
				Type t_SkinModHelperModule = Type.GetType("SkinModHelper.Module.SkinModHelperModule,SkinModHelper");
				if (t_SkinModHelperModule != null) {
					MethodInfo m_UpdateSkin = t_SkinModHelperModule?.GetMethod("UpdateSkin");
					try {
						m_UpdateSkin?.Invoke(null, new object[] { newSkin });
					}
					catch (Exception) {
						Logger.Log(LogLevel.Error, "Co-op Helper + SkinModHelper", "Could not change skin: skin \"" + newSkin + "\" is not defined.");
						m_UpdateSkin?.Invoke(null, new object[] { "Default" });
					}
				}
				else {
					Logger.Log(LogLevel.Info, "Co-op Helper + SkinModHelper", "Could not change skin: SkinModHelper is not installed.");
				}
			}

			// Apply role-specific dash count
			if (dashes != null && dashes.Length > 0) {
				Session session = SceneAs<Level>()?.Session;
				if (session != null) session.Inventory.Dashes = dashes[role % dashes.Length];
				else Logger.Log(LogLevel.Warn, "Co-op Helper", "Could not change dash count: no session avilable");
			}

			// Apply role-specific abilities
			if (abilities != null && abilities.Length > 0) {
				string ability = abilities[role % abilities.Length];
				if (ability == "grapple") {
					Type t_JackalModule = Type.GetType("Celeste.Mod.JackalHelper.JackalModule,JackalHelper");
					if (t_JackalModule != null) {
						PropertyInfo p_Session = t_JackalModule.GetProperty("Session");
						EverestModuleSession JackalSession = p_Session?.GetValue(null) as EverestModuleSession;
						DynamicData dd = DynamicData.For(JackalSession);
						dd.Set("hasGrapple", true);
					}
				}
			}
		}
	}
}
