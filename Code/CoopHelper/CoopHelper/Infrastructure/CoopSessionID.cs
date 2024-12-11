using Celeste.Mod.CelesteNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public struct CoopSessionID : IEquatable<CoopSessionID> {
		private static uint localIDGenCounter = 0;
		private const char SerializeDelim = '\u001e';  // record separator (ascii 30). Used because display names will never contain it. Different from PlayerID delim because this contains a PlayerID

		internal CoopSessionID(PlayerID _creator, DateTime _createInstant, uint _counter) : this() {
			creator = _creator;
			creationInstant = _createInstant;
			idcounter = _counter;
		}

		public PlayerID creator { get; internal set; }
		public DateTime creationInstant { get; internal set; }
		public uint idcounter { get; internal set; }

		public static CoopSessionID CoopEverywhereID {
			get {
				return new CoopSessionID() {
					creator = new PlayerID(0, 0, ""),
					creationInstant = DateTime.MinValue,
					idcounter = 0,
				};
			}
		}

		public static CoopSessionID GetNewID() {
			localIDGenCounter += 1;
			return new CoopSessionID() {
				idcounter = localIDGenCounter,
				creator = PlayerID.MyID,
				creationInstant = DateTime.Now,
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
			//if (l == null && r == null) return true;
			//if (l == null || r == null) return false;
			return l.Equals(r);
		}

		public override string ToString() {
			return (creator.GetHashCode() + creationInstant.GetHashCode() + idcounter.GetHashCode()).ToString();
		}

		public string SerializedID {
			get => $"{creator.SerializedID}{SerializeDelim}{creationInstant.Ticks}{SerializeDelim}{idcounter}";
		}

		public static CoopSessionID? FromSerialized(string serialized) {
			if (TryDeserializeID(serialized, out PlayerID? _creator, out DateTime _createInstant, out uint _counter)) return new(_creator.Value, _createInstant, _counter);
			return null;

		}

		private static bool TryDeserializeID(string serID, out PlayerID? _creator, out DateTime _createInstant, out uint _counter) {
			_creator = null;
			_createInstant = DateTime.UnixEpoch;
			_counter = uint.MaxValue;
			if (string.IsNullOrEmpty(serID)) return false;
			string[] split = serID.Split(SerializeDelim);
			if (split.Length != 3) return false;
			_creator = PlayerID.FromSerialized(split[0]);
			if (_creator == null) return false;
			if (!long.TryParse(split[1], out long ticks)) return false;
			_createInstant = new DateTime(ticks);
			if (!uint.TryParse(split[2], out _counter)) return false;
			return true;
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
			CoopSessionID id = new CoopSessionID(pid, dt, idc);
			return id;
		}
	}
}
