using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Data {
	public class DataSessionJoinAvailable : DataType<DataSessionJoinAvailable> {
		public DataPlayerInfo player;

		public PlayerID senderID;
		public bool newAvailability;

		public DataSessionJoinAvailable() {
			senderID = PlayerID.MyID;
			DataID = "corkr900CoopHelper_JoinAvailable_" + CoopHelperModule.ProtocolVersion;
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
			newAvailability = reader.ReadBoolean();
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(senderID);
			writer.Write(newAvailability);
		}
	}
}
