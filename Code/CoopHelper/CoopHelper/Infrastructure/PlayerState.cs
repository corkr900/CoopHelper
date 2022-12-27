using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.IO;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public class PlayerState {

		public static PlayerState Mine { get; private set; }

		static PlayerState() {
			Mine = new PlayerState() {
				pid = PlayerID.MyID,
				CurrentMap = GlobalAreaKey.Overworld,
				CurrentRoom = "",
				RespawnPoint = Vector2.Zero,
				LastUpdateReceived = SyncTime.Now,
			};
		}

		internal static void OnConnected() {
			Mine.pid = PlayerID.MyID;
			Mine.SendUpdateImmediate();
		}

		#region Remote State Information

		private static readonly float idleUpdateTime = 30;  // Send fresh updates every 30 seconds even if nothing changed
		private static readonly float purgeTime = 600;		// Data is stale enough to purge after 600 seconds (10 minutes)

		private static Dictionary<PlayerID, PlayerState> _playerStates = new Dictionary<PlayerID, PlayerState>();
		private static BidirectionalDictionary<PlayerID, uint> _idDictionary = new BidirectionalDictionary<PlayerID, uint>();

		/// <summary>
		/// Gets the PlayerState for the given player, or null if it is not known
		/// </summary>
		/// <param name="id">ID of the player</param>
		/// <returns>Player's state if known, otherwise null</returns>
		public PlayerState GetPlayerState(PlayerID id) {
			return _playerStates.ContainsKey(id) ? _playerStates[id] : null;
		}

		internal static void OnPlayerStateReceived(Data.DataPlayerState data) {
			PlayerID id = data.senderID;
			PlayerState state = data.newState;
			uint cnetID = data.player.ID;
			// update id map
			if (!_idDictionary.Contains(id, cnetID)) {
				_idDictionary.Add(id, cnetID);
			}
			// update state map
			if (_playerStates.ContainsKey(id)) {
				_playerStates[id].ApplyUpdate(state);
			}
			else {
				_playerStates.Add(id, state);
			}
		}

		internal static void OnConnectionDataReceived(DataConnectionInfo data) {
			if (_idDictionary.Contains(data.Player.ID)) {
				PlayerID id = _idDictionary.Reverse[data.Player.ID];
				if (_playerStates.ContainsKey(id)) {
					_playerStates[id].SetPing(data.TCPPingMs, data.UDPPingMs);
				}
			}
		}

		internal static void PurgeStale() {
			List<PlayerID> toRemove = new List<PlayerID>();
			foreach (KeyValuePair<PlayerID, PlayerState> v in _playerStates) {
				if ((SyncTime.Now - v.Value.LastUpdateReceived).TotalSeconds > purgeTime) {
					toRemove.Add(v.Key);
				}
			}
			foreach (PlayerID id in toRemove) {
				_playerStates.Remove(id);
				_idDictionary.Remove(id);
			}
		}

		#endregion

		public DateTime LastUpdateReceived;
		public Vector2 RespawnPoint;
		public GlobalAreaKey CurrentMap;
		public string CurrentRoom;
		public PlayerID pid { get; private set; }
		/// <summary>
		/// Last Measured UDP ping time (reliable/slow packets) in ms (or 300 if unknown)
		/// </summary>
		public int Ping_UDP { get; private set; } = 300;
		/// <summary>
		/// Last Measured TCP ping time (unreliable/fast packets) in ms (or 100 if unknown)
		/// </summary>
		public int Ping_TCP { get; private set; } = 100;

		public static PlayerState Default {
			get {
				return new PlayerState();
			}
		}

		private PlayerState() {
			pid = default(PlayerID);
			CurrentMap = GlobalAreaKey.Overworld;
			CurrentRoom = "";
			RespawnPoint = Vector2.Zero;
			LastUpdateReceived = SyncTime.Now;
		}

		public PlayerState(Player p) {
			Session s = p.SceneAs<Level>().Session;
			pid = PlayerID.MyID;
			CurrentMap = new GlobalAreaKey(s.Area);
			CurrentRoom = s.Level;
			RespawnPoint = s.RespawnPoint ?? Vector2.Zero;
			LastUpdateReceived = SyncTime.Now;
		}

		internal PlayerState(CelesteNetBinaryReader r) {
			pid = r.ReadPlayerID();
			CurrentMap = r.ReadAreaKey();
			CurrentRoom = r.ReadString();
			RespawnPoint = r.ReadVector2();
			LastUpdateReceived = SyncTime.Now;
		}

		public void SetPing(int tcp, int? udp) {
			Ping_TCP = tcp;
			Ping_UDP = udp ?? tcp;
		}

		public void ApplyUpdate(PlayerState newState) {
			RespawnPoint = newState.RespawnPoint;
			CurrentRoom = newState.CurrentRoom;
			pid = newState.pid;
		}

		public void SendUpdateImmediate() {
			if (!pid.Equals(PlayerID.MyID)) return;  // Safeguard against broadcasting others' statuses
			CNetComm.Instance.Send(new Data.DataPlayerState() {
				newState = this,
			}, false);
		}

		public void CheckSendUpdate() {
			if ((SyncTime.Now - LastUpdateReceived).TotalSeconds < idleUpdateTime) return;  // Enforce update frequency
			SendUpdateImmediate();
		}
	}

	public static class PlayerStateExtensions {

		public static PlayerState ReadPlayerState(this CelesteNetBinaryReader r) {
			return new PlayerState(r);
		}

		public static void Write(this CelesteNetBinaryWriter w, PlayerState s) {
			w.Write(s.pid);
			w.Write(s.CurrentMap);
			w.Write(s.CurrentRoom ?? "");
			w.Write(s.RespawnPoint);
		}
	}
}
