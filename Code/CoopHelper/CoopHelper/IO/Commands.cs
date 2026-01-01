using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	public class Commands {

		[Command("coopses", "make a co-op session")]
		public static void MakeSession(string arg) {
			int role = 0;
			int.TryParse(arg, out role);

			Level level = Engine.Scene as Level;
			Session session = level?.Session;
			SessionPickerEntity picker = level?.Entities?.FindFirst<SessionPickerEntity>();
			int sessionSize = (int)Calc.Max(2, role + 1);
			if (picker != null && session != null) {
				PlayerID[] players = new PlayerID[sessionSize];
				players[role] = PlayerID.MyID;
				picker.MakeSession(session, players);
			}
			else if (level?.Session != null && CoopHelperModule.Session != null) {
				CoopHelperModule.Session.IsInCoopSession = true;
				CoopHelperModule.Session.SessionID = CoopSessionID.GetNewID();
				CoopHelperModule.Session.SessionRole = role;
				CoopHelperModule.Session.SessionMembers = new List<PlayerID>(new PlayerID[sessionSize]);
				CoopHelperModule.Session.SessionMembers[role] = PlayerID.MyID;
				level.Session.SetFlag("CoopHelper_InSession", true);
				for (int i = 0; i < sessionSize; i++) {
					level.Session.SetFlag("CoopHelper_SessionRole_" + i, i == role);
				}
				CoopHelperModule.NotifySessionChanged();
				Engine.Commands.Log("Session created.");
			}
			else {
				Engine.Commands.Log("Could not create coop session (no session available)");
			}
		}

		[Command("coopdebugui", "toggle the co-op helper debug UI")]
		public static void ShowDebugUI() {
			Scene scene = Engine.Scene;
			SessionDebugHUD hud = scene.Tracker.GetEntity<SessionDebugHUD>();
			if (hud == null) {
				scene.Add(new SessionDebugHUD());
				Engine.Commands.Log("Added Debug UI.");
			}
			else {
				hud.RemoveSelf();
				Engine.Commands.Log("Removed Debug UI.");
			}
		}

		[Command("coopdebugflag", "set the co-op helper debug flag")]
		public static void SetDebugFlag(string argSetTo) {
			bool.TryParse(argSetTo, out bool setFlagTo);
			Session session = (Engine.Scene as Level)?.Session;
			if (session != null) {
				session.SetFlag("CoopHelper_Debug", setFlagTo);
				Engine.Commands.Log("Flag set to " + setFlagTo.ToString());
			}
			else {
				Engine.Commands.Log("Could not set flag (no session available)");
			}
		}

		// Example: coop_spawnsessionpicker deathSyncMode:everywhere

		[Command("coop_spawnsessionpicker", "Spawn a Co-op Helper Session Picker")]
		public static void SpawnSessionPicker(string arg) {
			Level level = Engine.Scene as Level;
			Player player = level?.Tracker?.GetEntity<Player>();
			if (player != null) {
				Vector2 roomPos = level.Bounds.Location.ToVector2();
				EntityData ed = new EntityData();
				ed.Position = player.Position - roomPos - Vector2.UnitY * 16f;
				ed.ID = -1;
				ed.Values = new Dictionary<string, object>();
				ed.Values.Add("removeIfSessionExists", true);
				ed.Values.Add("idOverride", "debugCMD:0");
				string[] subArgs = arg?.Split(',');
				if (subArgs != null) {
					foreach (string subarg in subArgs) {
						string[] split = subarg?.Split(':');
						if (split?.Length != 2 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1])) continue;
						ed.Values.Add(split[0], split[1]);
					}
				}
				level.Add(new SessionPickerEntity(ed, roomPos));
			}
		}
    }
}
