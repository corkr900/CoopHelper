﻿using Celeste.Mod.CelesteNet;
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
using System.Runtime.CompilerServices;
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

		public delegate void OnReceiveBundledEntityUpdateHandler(DataBundledEntityUpdate data);
		public static event OnReceiveBundledEntityUpdateHandler OnReceiveBundledEntityUpdate;

		public delegate void OnReceiveSessionJoinRequestHandler(DataSessionJoinRequest data);
		public static event OnReceiveSessionJoinRequestHandler OnReceiveSessionJoinRequest;

		public delegate void OnReceiveSessionJoinResponseHandler(DataSessionJoinResponse data);
		public static event OnReceiveSessionJoinResponseHandler OnReceiveSessionJoinResponse;

		public delegate void OnReceiveSessionJoinFinalizeHandler(DataSessionJoinFinalize data);
		public static event OnReceiveSessionJoinFinalizeHandler OnReceiveSessionJoinFinalize;

		public delegate void OnReceivePlayerStateHandler(Data.DataPlayerState data);
		public static event OnReceivePlayerStateHandler OnReceivePlayerState;

		public delegate void OnReceiveConnectionInfoHandler(DataConnectionInfo data);
		public static event OnReceiveConnectionInfoHandler OnReceiveConnectionInfo;

		

		#endregion

		#region Local State Information

		public CelesteNetClientContext CnetContext { get { return CelesteNetClientModule.Instance?.Context; } }

		public CelesteNetClient CnetClient { get { return CelesteNetClientModule.Instance?.Client; } }
		public bool IsConnected { get { return CnetClient?.Con?.IsConnected ?? false; } }
		public uint? CnetID { get { return IsConnected ? (uint?)CnetClient?.PlayerInfo?.ID : null; } }
		public long MaxPacketSize { get { return CnetClient?.Con is CelesteNetTCPUDPConnection connection ? (connection.ConnectionSettings?.MaxPacketSize ?? 2048) : 2048; } }

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

		public static ulong SentMsgs { get; private set; } = 0;
		public static ulong ReceivedMsgs { get; private set; } = 0;

		private static object ReceivedMessagesCounterLock = new object();

		#endregion

		#region Setup

		public CNetComm(Game game)
			: base(game) {
			Instance = this;
			Disposed += OnComponentDisposed;
			CelesteNetClientContext.OnStart += OnCNetClientContextStart;
			CelesteNetClientContext.OnDispose += OnCNetClientContextDispose;
			OnReceivePlayerState += PlayerState.OnPlayerStateReceived;
			OnReceiveConnectionInfo += PlayerState.OnConnectionDataReceived;
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
			PlayerState.Mine?.ConnectedToCnet();
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
			foreach (Action act in queue) {
				act();
			}
			base.Update(gameTime);
		}

		#endregion

		#region Entry Points

		/// <summary>
		/// Send a packet immediately
		/// </summary>
		/// <typeparam name="T">DataType</typeparam>
		/// <param name="data">Packet object</param>
		/// <param name="sendToSelf">If true, handlers on this client will also fire for this message</param>
		internal void Send<T>(T data, bool sendToSelf) where T : DataType<T> {
			if (!CanSendMessages) {
				return;
			}
			try {
				if (sendToSelf) CnetClient.SendAndHandle(data);
				else CnetClient.Send(data);
				if (!(data is Data.DataPlayerState)) ++SentMsgs;
			}
			catch(Exception e) {
				// The only way I know of for this to happen is a well-timed connection blorp but just in case
				Logger.Log(LogLevel.Error, "Co-op Helper", $"Exception was handled in CoopHelper.IO.CNetComm.Send<{typeof(T).Name}>");
				Logger.LogDetailed(LogLevel.Error, "Co-op Helper", e.Message);
			}
		}

		/// <summary>
		/// This function is called once per CelesteNet tick.
		/// This is the primary kicking-off point for network-y stuff
		/// </summary>
		/// <param name="counter">This parameter counts up once for each tick that occurs since starting the game</param>
		internal void Tick(ulong counter) {
			if (CoopHelperModule.Session?.InSessionIncludingEverywhere == true
				&& EntityStateTracker.HasUpdates)
			{
				EntityStateTracker.NotifyInitiatingOutgoingMessage();
				Send(new DataBundledEntityUpdate(), false);
			}
			EntityStateTracker.FlushIncoming();
			// Don't bother checking recurring updates during screen transitions;
			// They're either just created or about to be destroyed
			bool currentlyScreenTransitioning = (Engine.Scene as Level)?.Transitioning ?? false;
			if (!currentlyScreenTransitioning) {
				EntityStateTracker.CheckRecurringUpdates();
			}
			// Some things don't need to happen very often, so only do them every X ticks
			if (counter % 30 == 0) {
				PlayerState.PurgeStale();
				PlayerState.Mine.CheckSendHeartbeat();
			}
		}

		#endregion

		#region Message Handlers

		public void Handle(CelesteNetConnection con, DataConnectionInfo data) {
			if (data.Player == null) data.Player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveConnectionInfo?.Invoke(data));
		}

		public void Handle(CelesteNetConnection con, Data.DataPlayerState data) {
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceivePlayerState?.Invoke(data));
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Handled packet: {data.GetTypeID(con.Data)}");
		}

		public void Handle(CelesteNetConnection con, DataSessionJoinRequest data) {
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinRequest?.Invoke(data));
			lock (ReceivedMessagesCounterLock) {
				++ReceivedMsgs;
			}
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Handled packet: {data.GetTypeID(con.Data)}");
		}

		public void Handle(CelesteNetConnection con, DataSessionJoinResponse data) {
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinResponse?.Invoke(data));
			lock (ReceivedMessagesCounterLock) {
				++ReceivedMsgs;
			}
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Handled packet: {data.GetTypeID(con.Data)}");
		}

		public void Handle(CelesteNetConnection con, DataSessionJoinFinalize data) {
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveSessionJoinFinalize?.Invoke(data));
			lock (ReceivedMessagesCounterLock) {
				++ReceivedMsgs;
			}
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Handled packet: {data.GetTypeID(con.Data)}");
		}

		public void Handle(CelesteNetConnection con, DataBundledEntityUpdate data) {
			if (CoopHelperModule.Session?.InSessionIncludingEverywhere != true) return;
			if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
			updateQueue.Enqueue(() => OnReceiveBundledEntityUpdate?.Invoke(data));
			lock (ReceivedMessagesCounterLock) {
				++ReceivedMsgs;
			}
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Handled packet: {data.GetTypeID(con.Data)}");
		}

		#endregion
	}
}
