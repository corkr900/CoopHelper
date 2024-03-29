﻿using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Data {
	public class DataSessionJoinFinalize : DataType<DataSessionJoinFinalize> {
		public DataPlayerInfo player;
		public CoopSessionID sessionID;
		public PlayerID[] sessionPlayers = new PlayerID[0];
		public bool RolesFinalized;

		static DataSessionJoinFinalize() {
			DataID = "corkr900CoopHelper_JoinConfirmation_" + CoopHelperModule.ProtocolVersion;
		}

		public DataSessionJoinFinalize() {
		}

		public override DataFlags DataFlags { get { return DataFlags.None; } }

		public override void FixupMeta(DataContext ctx) {
			player = Get<MetaPlayerPrivateState>(ctx);
		}

		public override MetaType[] GenerateMeta(DataContext ctx) {
			return new MetaType[] { new MetaPlayerPrivateState(player) };
		}

		protected override void Read(CelesteNetBinaryReader reader) {
			sessionID = reader.ReadSessionID();
			RolesFinalized = reader.ReadBoolean();
			int numPlayers = reader.ReadInt32();
			sessionPlayers = new PlayerID[numPlayers];
			for (int i = 0; i < numPlayers; i++) {
				sessionPlayers[i] = reader.ReadPlayerID();
			}
		}

		protected override void Write(CelesteNetBinaryWriter writer) {
			writer.Write(sessionID);
			writer.Write(RolesFinalized);
			int numPlayers = sessionPlayers?.Length ?? 0;
			writer.Write(numPlayers);
			for (int i = 0; i < numPlayers; i++) {
				writer.Write(sessionPlayers[i]);
			}
		}
	}
}
