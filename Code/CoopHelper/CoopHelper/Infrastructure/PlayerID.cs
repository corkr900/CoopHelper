using Celeste.Mod;
using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public struct PlayerID {

		private const char SerializeDelim = '\u001f';  // unit separator (ascii 31). Used because display names will never contain it.

		public static PlayerID MyID {
			get {
				CNetComm comm = CNetComm.Instance;
				int macHash = LocalMACAddressHash;
				string name = GetName();
				uint id = comm?.CnetID ?? uint.MaxValue;
				return new PlayerID(macHash, id, name);
			}
		}
		public static int LocalMACAddressHash {
			get {
				if (_localMACHash == null) SearchMACAddress();
				return _localMACHash ?? 0;
			}
		}
		private static int? _localMACHash = null;
		private static string _lastKnownName;
		private static string GetName() {
			string name = CNetComm.Instance?.CnetClient?.PlayerInfo?.Name;
			if (string.IsNullOrEmpty(name)) {
				name = _lastKnownName;
			}
			_lastKnownName = name;
			return _lastKnownName;
		}
		private static void SearchMACAddress() {
			try {
				string macAddr = NetworkInterface.GetAllNetworkInterfaces()
					.Where(i => i.OperationalStatus == OperationalStatus.Up)
					.Select(i => i.GetPhysicalAddress().ToString())
					.Where(s => !string.IsNullOrEmpty(s))
					.Order().FirstOrDefault();
				_localMACHash = GetDeterministicHashCode(macAddr);
			}
			catch (Exception e) {
				Logger.Log("CoopHelper", "Could not get MAC address: " + e.Message);
			}
		}
		private static int? GetDeterministicHashCode(string mac) {
			if (string.IsNullOrEmpty(mac)) return null;
			unchecked {
				int hash1 = (5381 << 16) + 5381;
				int hash2 = hash1;

				for (int i = 0; i < mac.Length; i += 2) {
					hash1 = ((hash1 << 5) + hash1) ^ mac[i];
					if (i == mac.Length - 1)
						break;
					hash2 = ((hash2 << 5) + hash2) ^ mac[i + 1];
				}

				return hash1 + (hash2 * 1566083941);
			}
		}

		public PlayerID(int? addrHash, uint cnetID, string name) {
			MacAddressHash = addrHash;
			CNetID = cnetID;
			Name = name;
		}
		public PlayerID(PlayerID orig) {
			MacAddressHash = orig.MacAddressHash;
			CNetID = orig.CNetID;
			Name = orig.Name;
		}
		public int? MacAddressHash { get; private set; }
		public string Name { get; private set; }
		public uint CNetID { get; private set; }

		public string SerializedID {
			get => $"{MacAddressHash ?? -1}{SerializeDelim}{CNetID}{SerializeDelim}{Name}";
		}

		public static PlayerID? FromSerialized(string serialized) {
			if (TryDeserializeID(serialized, out int? addrHash, out string name, out uint cnetID)) return new(addrHash, cnetID, name);
			return null;

		}

		private static bool TryDeserializeID(string serID, out int? addrHash, out string name, out uint cnetID) {
			addrHash = null;
			cnetID = uint.MaxValue;
			name = "";
			if (string.IsNullOrEmpty(serID)) return false;
			string[] split = serID.Split(SerializeDelim);
			if (split.Length != 3) return false;
			addrHash = int.TryParse(split[0], out int _addrHash) ? _addrHash : null;
			cnetID = uint.TryParse(split[1], out cnetID) ? cnetID : uint.MaxValue;
			name = split[2];
			return true;
		}


		public bool MatchAndUpdate(PlayerID id) {
			if (this.Equals(id)) {
				CNetID = id.CNetID;
				return true;
			}
			return false;
		}

		public bool IsDefault() {
			return MacAddressHash == null && string.IsNullOrEmpty(Name);
		}

		public override bool Equals(object obj) {
			return obj != null && obj is PlayerID id && id.MacAddressHash == MacAddressHash && id.Name == Name;
		}

		public override int GetHashCode() {
			return ((MacAddressHash ?? 0) + Name).GetHashCode();
		}
	}

	public static class PlayerIDExt {
		public static PlayerID ReadPlayerID(this CelesteNetBinaryReader r) {
			bool hasmac = r.ReadBoolean();
			int? mac = hasmac ? (int?)r.ReadInt32() : null;
			string name = r.ReadString();
			uint cnetid = r.ReadUInt32();
			return new PlayerID(mac, cnetid, name);
		}
		public static void Write(this CelesteNetBinaryWriter w, PlayerID id) {
			if (id.MacAddressHash == null) {
				w.Write(false);
			}
			else {
				w.Write(true);
				w.Write(id.MacAddressHash ?? 0);
			}
			w.Write(id.Name ?? "");
			w.Write(id.CNetID);
		}
	}
}
