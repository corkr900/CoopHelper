using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Data {
	public class DataSessionJoinResponse : DataType<DataSessionJoinResponse> {
		public DataPlayerInfo player;

		public PlayerID SenderID;
		public CoopSessionID SessionID;
		public bool Response;

		static DataSessionJoinResponse() {
			DataID = "corkr900CoopHelper_JoinResponse_" + CoopHelperModule.ProtocolVersion;
		}

		public DataSessionJoinResponse() {
			SenderID = PlayerID.MyID;
		}

		public override DataFlags DataFlags { get { return DataFlags.None; } }

		public override void FixupMeta(DataContext ctx) {
			player = Get<MetaPlayerPrivateState>(ctx);
		}

		public override MetaType[] GenerateMeta(DataContext ctx) {
			return new MetaType[] { new MetaPlayerPrivateState(player) };
		}

		protected override void Read(CelesteNetBinaryReader reader) {
			SenderID = reader.ReadPlayerID();
			SessionID = reader.ReadSessionID();
			Response = reader.ReadBoolean();
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(SenderID);
			writer.Write(SessionID);
			writer.Write(Response);
		}
	}
}
