using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Data {
	public class DataSessionJoinRequest : DataType<DataSessionJoinRequest> {
		public DataPlayerInfo player;

		public PlayerID senderID;
		public CoopSessionID sessionID;
		public PlayerID targetID;

		static DataSessionJoinRequest() {
			DataID = "corkr900CoopHelper_JoinRequest_" + CoopHelperModule.ProtocolVersion;
		}

		public DataSessionJoinRequest() {
			senderID = PlayerID.MyID;
		}

		public override DataFlags DataFlags { get { return DataFlags.None; } }

		public override void FixupMeta(DataContext ctx) {
			player = Get<MetaPlayerPrivateState>(ctx);
		}

		public override MetaType[] GenerateMeta(DataContext ctx) {
			return new MetaType[] { new MetaPlayerPrivateState(player) };
		}

		protected override void Read(CelesteNetBinaryReader reader) {
			senderID = reader.ReadPlayerID();
			sessionID = reader.ReadSessionID();
			targetID = reader.ReadPlayerID();
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(senderID);
			writer.Write(sessionID);
			writer.Write(targetID);
		}
	}
}
