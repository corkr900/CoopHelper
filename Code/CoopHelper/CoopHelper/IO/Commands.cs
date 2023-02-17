using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
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

			Session session = (Engine.Scene as Level)?.Session;
			if (session != null) {
				int sessionSize = (int)Calc.Max(2, role + 1);
				CoopHelperModule.Session.IsInCoopSession = true;
				CoopHelperModule.Session.SessionID = CoopSessionID.GetNewID();
				CoopHelperModule.Session.SessionRole = role;
				CoopHelperModule.Session.SessionMembers = new List<PlayerID>(new PlayerID[sessionSize]);
				CoopHelperModule.Session.SessionMembers[role] = PlayerID.MyID;
				session.SetFlag("CoopHelper_InSession", true);
				for (int i = 0; i < sessionSize; i++) {
					session.SetFlag("CoopHelper_SessionRole_" + i, i == role);
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
	}
}
