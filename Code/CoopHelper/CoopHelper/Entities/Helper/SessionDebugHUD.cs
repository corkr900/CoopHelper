using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CoopHelper.Entities {
	[Tracked]
	public class SessionDebugHUD : Entity {
		public SessionDebugHUD() {
			Tag = Tags.HUD | Tags.Global | Tags.Persistent;
		}

		public override void Render() {
			base.Render();
			const float LineOffset = 24;
			CoopHelperModuleSession ses = CoopHelperModule.Session;
			float y = 0;

            ActiveFont.DrawOutline(string.Format("Sent: {0} Packets, {1} Sync Updates",
					CNetComm.SentMsgs, EntityStateTracker.SentUpdates),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			y += LineOffset;
			ActiveFont.DrawOutline(string.Format("Received: {0} Packets, {1} Processed, {2} Discarded",
					CNetComm.ReceivedMsgs, EntityStateTracker.ProcessedUpdates, EntityStateTracker.DiscardedUpdates),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			y += LineOffset;
			ActiveFont.DrawOutline(string.Format("Listeners: {0}", EntityStateTracker.CurrentListeners),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			y += LineOffset;
            ActiveFont.DrawOutline(string.Format("Pending messages: {0}", EntityStateTracker.CountPendingUpdates()),
                Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
            y += LineOffset;

            bool inSession = ses?.IsInCoopSession ?? false;
			ActiveFont.DrawOutline(string.Format("In Session: {0}", inSession),
				Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
			y += LineOffset;

			if (inSession) {
				ActiveFont.DrawOutline(string.Format("Session ID: {0}", ses.SessionID),
					Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
				y += LineOffset;
				ActiveFont.DrawOutline(string.Format("Members: {0}", ses.SessionMembers.Count),
					Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
				y += LineOffset;
				ActiveFont.DrawOutline(string.Format("My Role: {0}", ses.SessionRole),
					Vector2.UnitY * y, Vector2.Zero, Vector2.One / 2f, Color.White, 1f, Color.Black);
				y += LineOffset;
			}
		}
	}
}
