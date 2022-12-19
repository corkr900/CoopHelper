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

		static DataBundledEntityUpdate() {
			DataID = "corkr900CoopHelper_BundledEntityUpdate_" + CoopHelperModule.ProtocolVersion;
		}

		public DataBundledEntityUpdate() {
			senderID = PlayerID.MyID;
		}

		public override DataFlags DataFlags { get { return CelesteNet.DataTypes.DataFlags.None; } }

		public override void FixupMeta(DataContext ctx) {
			player = Get<MetaPlayerPrivateState>(ctx);
		}

		public override MetaType[] GenerateMeta(DataContext ctx) {
			return new MetaType[] { new MetaPlayerPrivateState(player) };
		}

		protected override void Read(CelesteNetBinaryReader reader) {
			senderID = reader.ReadPlayerID();
			EntityStateTracker.ReceiveUpdates(reader);
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(senderID);
			EntityStateTracker.FlushUpdates(writer);
		}
	}
}
