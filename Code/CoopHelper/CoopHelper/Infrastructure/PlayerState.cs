using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public class PlayerState : ISynchronizable {
		public Vector2 RespawnPoint;
		public string CurrentRoom;
		public PlayerID pid { get; private set; }

		public PlayerState(CelesteNetBinaryReader r) {
			RespawnPoint = r.ReadVector2();
			CurrentRoom = r.ReadString();
			pid = r.ReadPlayerID();
		}

		public PlayerState(Player p) {
			Session s = p.SceneAs<Level>().Session;
			RespawnPoint = s.RespawnPoint ?? Vector2.Zero;
			CurrentRoom = s.Level;
			pid = PlayerID.MyID;
		}

		#region ISynchronizable implementation

		public static int GetHeader() => 1;

		public static PlayerState ParseState(CelesteNetBinaryReader r) => new PlayerState(r);

		public EntityID GetID() {
			return new EntityID("%Player%" + pid.Name, pid.GetHashCode());
		}

		public void ApplyState(object state) {
			if (state is PlayerState st) {
				RespawnPoint = st.RespawnPoint;
				CurrentRoom = st.CurrentRoom;
				pid = st.pid;
			}
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(RespawnPoint);
			w.Write(CurrentRoom);
			w.Write(pid);
		}

		#endregion
	}
}
