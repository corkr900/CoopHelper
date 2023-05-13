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

		public PlayerID SenderID;
		public CoopSessionID SessionID;
		public PlayerID TargetID;
		/// <summary>
		/// If the request is requesting to join a player, this is -1.
		/// If the request is requesting a certain role, this will be the role index.
		/// </summary>
		public int Role;

		static DataSessionJoinRequest() {
			DataID = "corkr900CoopHelper_JoinRequest_" + CoopHelperModule.ProtocolVersion;
		}

		public DataSessionJoinRequest() {
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
			TargetID = reader.ReadPlayerID();
			Role = reader.ReadInt32();
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(SenderID);
			writer.Write(SessionID);
			writer.Write(TargetID);
			writer.Write(Role);
		}
	}
}
