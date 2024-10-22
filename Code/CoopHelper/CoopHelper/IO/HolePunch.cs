using Celeste.Mod.CoopHelper.Infrastructure;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	public class HolePunch {

		// Establish UDP connection to shared server
		// Send server hole punch request
		// Server responds with other client's public and private endpoints (sent to both clients)
		// Send a connection establishment packet to other client's public and private endpoints
		// For each: if a response is received, verify nonce and mark as connected
		// If both endpoints connected, throw out the public and only use private

		public static void Request(PlayerID otherClientID) {
			// TODO
		}
	}

	public class HolePunchSession {

		private HolePunchConnection privConn;
		private HolePunchConnection pubConn;

		public bool Connected => ConnectedPrivate || ConnectedPublic;
		public bool ConnectedPrivate { get; private set; } = false;
		public bool ConnectedPublic { get; private set; } = false;

		public HolePunchSession(IPAddress pubAddr, int pubPort, IPAddress privAddr, int privPort) {
			privConn = new HolePunchConnection(privAddr, privPort, OnPrivStatChange, OnDataReceived);
			pubConn = new HolePunchConnection(pubAddr, pubPort, OnPubStatChange, OnDataReceived);
		}

		private void OnPrivStatChange(HolePunchConnection.ConnStatus old, HolePunchConnection.ConnStatus newStatus) {
			if (newStatus == HolePunchConnection.ConnStatus.Confirmed) {
				pubConn?.Close();
				ConnectedPrivate = true;
			}
			else {
				ConnectedPrivate = false;
			}
		}

		private void OnPubStatChange(HolePunchConnection.ConnStatus old, HolePunchConnection.ConnStatus newStatus) {
			if (newStatus == HolePunchConnection.ConnStatus.Confirmed) {
				ConnectedPublic = true;
			}
			else {
				ConnectedPublic = false;
			}
			// TODO
		}

		public void Send(HolePunchDatagram data) {
			if (ConnectedPrivate) {
				privConn.Send(data);
			}
			else if (ConnectedPublic) {
				pubConn.Send(data);
			}
		}

		private void OnDataReceived(HolePunchDatagram data) {
			// TODO
			throw new NotImplementedException();
		}
	}

	public class HolePunchConnection {

		public enum ConnStatus {
			Uninitialized = 0,
			Requested = 100,
			AwaitingConfirmation = 200,
			Confirmed = 300,
			Closed = 900,
			Error = 999,
		}


		public ConnStatus Status {
			get => _status;
			private set {
				if (_status == value) return;
                ConnStatus prevStat = _status;
				_status = value;
				statusChanged?.Invoke(prevStat, value);
			}
		}
		private ConnStatus _status = ConnStatus.Uninitialized;

		private Action<ConnStatus, ConnStatus> statusChanged;
		private Action<HolePunchDatagram> onDataReceived;
		private IPEndPoint endpoint;
		private UdpClient client;
		private Task connectionTask;

		public HolePunchConnection(IPAddress addr, int port, Action<ConnStatus, ConnStatus> statusChangeCallback, Action<HolePunchDatagram> onReceive) {
			statusChanged = statusChangeCallback;
			onDataReceived = onReceive;
			endpoint = new IPEndPoint(addr, port);
			connectionTask = new Task(MakeConnection);
		}

		private async void MakeConnection() {
			client = new UdpClient();
			//client.ReceiveAsync();
			HolePunchDatagram gram = HolePunchDatagram.FirstPunch;
			Task<int> req = client.SendAsync(gram.Payload, gram.Bytes, endpoint);
			Status = ConnStatus.Requested;
			int initres = await req;
			if (initres <= 0) {
				// TODO log something
				Status = ConnStatus.Error;
				Close();
				return;
			}
			Status = ConnStatus.AwaitingConfirmation;
			while (Status <= ConnStatus.Confirmed) {
				UdpReceiveResult confres = await client.ReceiveAsync();
				HolePunchDatagram data = HolePunchDatagram.Parse(confres);
				if (data == null) {
					// TODO log something
					Status = ConnStatus.Error;
					Close();
					return;
				}
				else if (data.IsInitialPunch) continue;
				else if (Status < ConnStatus.Confirmed) {
					if (data.IsConfirmation) {
						Status = ConnStatus.Confirmed;
					}
				}
				else {
					onDataReceived?.Invoke(data);
				}
			}
			
		}

		internal void Close() {
			client?.Close();
			if (Status < ConnStatus.Closed) Status = ConnStatus.Closed;
		}

		internal void Send(HolePunchDatagram data) {
			if (data == null || data.Bytes == 0) return;
			// TODO header
			// TODO use a send queue
			client.SendAsync(data.Payload, endpoint);
		}
	}

	public class HolePunchDatagram {
		public byte[] Payload {
			get => _payload ?? Array.Empty<byte>();
			set => AssignPayload(value);
		}
		// TODO data array (sans header)

		private byte[] _payload;

		public int Bytes => _payload?.Length ?? 0;

		public bool IsInitialPunch { get; private set; } = false;
		public bool IsConfirmation { get; private set; } = false;

		private void AssignPayload(byte[] value) {
			throw new NotImplementedException();
		}

		// /////////////////////////////////////////////////////////////////////////////

		public static HolePunchDatagram FirstPunch => new HolePunchDatagram() {
			Payload = new byte[16],
		};
		public static HolePunchDatagram Confirmation => new HolePunchDatagram() {
			Payload = new byte[12],
		};

		internal static HolePunchDatagram Parse(UdpReceiveResult confres) => Parse(confres.Buffer);
		internal static HolePunchDatagram Parse(byte[] buf) {
			throw new NotImplementedException();  // TODO
		}

	}
}
