﻿using Celeste.Mod.CoopHelper.Infrastructure;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	public class Commands {

		[Command("coop_ses", "make a co-op session for debugging")]
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
			}

		}
	}
}
