using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.Client.Components;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Infrastructure;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	public class CNetComm : GameComponent {
		public static CNetComm Instance { get; private set; }

		#region Events

		public delegate void OnConnectedHandler(CelesteNetClientContext cxt);
		public static event OnConnectedHandler OnConnected;

		public delegate void OnDisonnectedHandler(CelesteNetConnection con);
		public static event OnDisonnectedHandler OnDisconnected;

		public delegate void OnReceiveSessionJoinAvailableHandler(DataSessionJoinAvailable data);
		public static event OnReceiveSessionJoinAvailableHandler OnReceiveSessionJoinAvailable;

		public delegate void OnReceiveSessionJoinRequestHandler(DataSessionJoinRequest data);
		public static event OnReceiveSessionJoinRequestHandler OnReceiveSessionJoinRequest;

		public delegate void OnReceiveSessionJoinResponseHandler(DataSessionJoinResponse data);
		public static event OnReceiveSessionJoinResponseHandler OnReceiveSessionJoinResponse;

		public delegate void OnReceiveSessionJoinFinalizeHandler(DataSessionJoinFinalize data);
		public static event OnReceiveSessionJoinFinalizeHandler OnReceiveSessionJoinFinalize;

		#endregion

		#region Current State Information

		public CelesteNetClientContext CnetContext { get { return CelesteNetClientModule.Instance?.Context; } }

		public CelesteNetClient CnetClient { get { return CelesteNetClientModule.Instance?.Client; } }
		public bool IsConnected { get { return CnetClient?.Con?.IsConnected ?? false; } }
		public uint? CnetID { get { return IsConnected ? (uint?)CnetClient?.PlayerInfo?.ID : null; } }

		public DataChannelList.Channel CurrentChannel {
			get {
				KeyValuePair<Type, CelesteNetGameComponent> listComp = CnetContext.Components.FirstOrDefault((KeyValuePair<Type, CelesteNetGameComponent> kvp) => {
					return kvp.Key == typeof(CelesteNetPlayerListComponent);
				});
				if (listComp.Equals(default(KeyValuePair<Type, CelesteNetGameComponent>))) return null;
				CelesteNetPlayerListComponent comp = listComp.Value as CelesteNetPlayerListComponent;
				DataChannelList.Channel[] list = comp.Channels?.List;
				return list?.FirstOrDefault(c => c.Players.Contains(CnetClient.PlayerInfo.ID));
			}
		}
		public bool CurrentChannelIsMain {
			get {
				return CurrentChannel?.Name?.ToLower() == "main";
			}
		}

		public bool CanSendMessages {
			get {
				return IsConnected /*&& !CurrentChannelIsMain*/;
			}
		}

		private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();
		public static ulong msgCount { get; private set; } = 0;

		#endregion

		#region Setup

		public CNetComm(Game game)
			: base(game) {
			Instance = this;
			Disposed += OnComponentDisposed;
			CelesteNetClientContext.OnStart += OnCNetClientContextStart;
			CelesteNetClientContext.OnDispose += OnCNetClientContextDispose;
		}

		private void OnComponentDisposed(object sender, EventArgs args) {
			CelesteNetClientContext.OnStart -= OnCNetClientContextStart;
			CelesteNetClientContext.OnDispose -= OnCNetClientContextDispose;
		}

		#endregion

		#region Hooks + Events

		private void OnCNetClientContextStart(CelesteNetClientContext cxt) {
			CnetClient.Data.RegisterHandlersIn(this);
			CnetClient.Con.OnDisconnect += OnDisconnect;
			updateQueue.Enqueue(() => OnConnected?.Invoke(cxt));
		}

		private void OnCNetClientContextDispose(CelesteNetClientContext cxt) {
			// CnetClient is null here
		}

		private void OnDisconnect(CelesteNetConnection con) {
			updateQueue.Enqueue(() => OnDisconnected?.Invoke(con));
		}

		public override void Update(GameTime gameTime) {
			ConcurrentQueue<Action> queue = updateQueue;
			updateQueue = new ConcurrentQueue<Action>();
			foreach (Action act in queue) act();

			base.Update(gameTime);
		}


		#endregion

		internal void Send<T>(T data, bool sendToSelf) where T : DataType<T> {
			if (!CanSendMessages) {
				return;
			}
			try {
				if (sendToSelf) CnetClient.SendAndHandle(data);
				else CnetClient.Send(data);
				Engine.Commands.Log("Sent - " + typeof(DataType<T>).GetField("DataID").GetValue(data));
				++msgCount;
			}
			catch(Exception e) {
				// a well-timed connection blorp might theoretically get us here
				// but we can ignore it because the connection already blorped
			}
		}

		internal void Tick() {
			if (EntityStateTracker.HasUpdates) {
				Send(new DataBundledEntityUpdate(), false);
			}
		}

		#region Message Handlers

		public void Handle(CelesteNetConnection con, DataSessionJoinAvailable data) {
			Engine.Commands.Log("Received - " + typeof(DataType<DataSessionJoinAvailable>).GetField("DataID").GetValue(data));
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinAvailable?.Invoke(data));
		}

		public void Handle(CelesteNetConnection con, DataSessionJoinRequest data) {
			Engine.Commands.Log("Received - " + typeof(DataType<DataSessionJoinRequest>).GetField("DataID").GetValue(data));
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinRequest?.Invoke(data));
		}

		public void Handle(CelesteNetConnection con, DataSessionJoinResponse data) {
			Engine.Commands.Log("Received - " + typeof(DataType<DataSessionJoinResponse>).GetField("DataID").GetValue(data));
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinResponse?.Invoke(data));
		}

		public void Handle(CelesteNetConnection con, DataSessionJoinFinalize data) {
			Engine.Commands.Log("Received - " + typeof(DataType<DataSessionJoinFinalize>).GetField("DataID").GetValue(data));
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinFinalize?.Invoke(data));
		}

		#endregion
	}
}
