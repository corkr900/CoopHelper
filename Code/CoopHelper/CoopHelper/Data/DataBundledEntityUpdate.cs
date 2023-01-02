using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Data {
	public class DataBundledEntityUpdate : DataType<DataBundledEntityUpdate> {
		public DataPlayerInfo player;

		public PlayerID senderID;
		public CoopSessionID SessionID;

		static DataBundledEntityUpdate() {
			DataID = "corkr900CoopHelper_BundledEntityUpdate_" + CoopHelperModule.ProtocolVersion;
		}

		public DataBundledEntityUpdate() {
			senderID = PlayerID.MyID;
			SessionID = CoopHelperModule.Session.SessionID;
		}

		public override DataFlags DataFlags { get { return DataFlags.Unreliable; } }

		public override void FixupMeta(DataContext ctx) {
			player = Get<MetaPlayerPrivateState>(ctx);
		}

		public override MetaType[] GenerateMeta(DataContext ctx) {
			return new MetaType[] { new MetaPlayerPrivateState(player) };
		}

		protected override void Read(CelesteNetBinaryReader reader) {
			senderID = reader.ReadPlayerID();
			SessionID = reader.ReadSessionID();
			bool isMySession = !PlayerState.Mine.CurrentMap.IsOverworld
				&& CoopHelperModule.Session.IsInCoopSession
				&& CoopHelperModule.Session.SessionID == SessionID;
			EntityStateTracker.ReceiveUpdates(reader, isMySession);
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(senderID);
			writer.Write(SessionID);
			EntityStateTracker.FlushOutgoing(writer);
		}
	}
}
