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

		public DataSessionJoinRequest() {
			senderID = PlayerID.MyID;
			DataID = "corkr900CoopHelper_JoinRequest_" + CoopHelperModule.ProtocolVersion;
		}

		public override DataFlags DataFlags { get { return DataFlags.None; } }

		public override MetaType[] GenerateMeta(DataContext ctx) {
			return (MetaType[])(base.GenerateMeta(ctx).Concat(new MetaType[] { new MetaPlayerPrivateState() }));
		}

		public override void FixupMeta(DataContext ctx) {
			player = Get<MetaPlayerPrivateState>(ctx);
		}

		protected override void Read(CelesteNetBinaryReader reader) {
			senderID = reader.ReadPlayerID();
			sessionID = reader.ReadSessionID();
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(senderID);
			writer.Write(sessionID);
		}
	}
}
