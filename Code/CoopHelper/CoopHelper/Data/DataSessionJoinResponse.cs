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

		public PlayerID senderID;
		public CoopSessionID sessionID;
		public bool response;
		public bool respondingToRoleRequest;

		static DataSessionJoinResponse() {
			DataID = "corkr900CoopHelper_JoinResponse_" + CoopHelperModule.ProtocolVersion;
		}

		public DataSessionJoinResponse() {
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
			response = reader.ReadBoolean();
			respondingToRoleRequest = reader.ReadBoolean();
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(senderID);
			writer.Write(sessionID);
			writer.Write(response);
			writer.Write(respondingToRoleRequest);
		}
	}
}
