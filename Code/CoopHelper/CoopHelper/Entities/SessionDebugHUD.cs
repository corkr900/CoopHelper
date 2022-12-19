using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CoopHelper.Entities {
	public class SessionDebugHUD : Entity {
		public SessionDebugHUD() {
			Tag = Tags.HUD | Tags.Persistent;
		}

		public override void Render() {
			base.Render();
			CoopHelperModuleSession ses = CoopHelperModule.Session;
			float y = 0;
			ActiveFont.DrawOutline(string.Format("In Session: {0}", ses.IsInCoopSession),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			if (ses == null) return;
			y += 24;
			ActiveFont.DrawOutline(string.Format("Members: {0}", ses.SessionMembers.Count),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			y += 24;
			ActiveFont.DrawOutline(string.Format("My Role: {0}", ses.SessionRole),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			y += 24;
			ActiveFont.DrawOutline(string.Format("Session ID: {0}", ses.SessionID),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One/2f, Color.White, 1f, Color.Black);
			y += 24;
			ActiveFont.DrawOutline(string.Format("Packets Sent: {0}", CNetComm.msgCount),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
		}
	}
}
