using Celeste.Mod.CelesteNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public struct CoopSessionID : IEquatable<CoopSessionID> {
		private static uint localIDGenCounter = 0;

		public PlayerID creator { get; internal set; }
		public DateTime creationInstant { get; internal set; }
		internal uint idcounter { get; set; }


		public static CoopSessionID GetNewID() {
			localIDGenCounter += 1;
			return new CoopSessionID() {
				idcounter = localIDGenCounter,
				creator = PlayerID.MyID,
				creationInstant = SyncTime.Now,
			};
		}

		public bool Equals(CoopSessionID other)
			=> idcounter.Equals(other.idcounter)
				&& creator.Equals(other.creator)
				&& creationInstant.Equals(other.creationInstant);
		public override bool Equals(object obj) => obj is CoopSessionID id && Equals(id);
		public override int GetHashCode() => idcounter.GetHashCode() * creator.GetHashCode() * creationInstant.GetHashCode();
		public static bool operator !=(CoopSessionID l, CoopSessionID r) => !(l == r);
		public static bool operator ==(CoopSessionID l, CoopSessionID r) {
			if (l == null && r == null) return true;
			if (l == null || r == null) return false;
			return l.Equals(r);
		}

		public override string ToString() {
			return (creator.GetHashCode() + creationInstant.GetHashCode() + idcounter.GetHashCode()).ToString();
		}
	}

	public static class CoopSessionIDExt {
		public static void Write(this CelesteNetBinaryWriter w, CoopSessionID id) {
			w.Write(id.creator);
			w.Write(id.creationInstant);
			w.Write(id.idcounter);
		}
		public static CoopSessionID ReadSessionID(this CelesteNetBinaryReader r) {
			PlayerID pid = r.ReadPlayerID();
			DateTime dt = r.ReadDateTime();
			uint idc = r.ReadUInt32();
			return new CoopSessionID() {
				creator = pid,
				creationInstant = dt,
				idcounter = idc,
			};
		}
	}
}
