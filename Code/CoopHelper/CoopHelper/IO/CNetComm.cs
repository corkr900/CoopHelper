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

        public delegate void OnReceiveMapSyncHandler(DataMapSync data);
        public static event OnReceiveMapSyncHandler OnReceiveMapSync;



        #endregion

        #region Local State Information

        public CelesteNetClientContext CnetContext { get { return CelesteNetClientModule.Instance?.Context; } }

		public CelesteNetClient CnetClient { get { return CelesteNetClientModule.Instance?.Client; } }
		public bool IsConnected { get { return CnetClient?.Con?.IsConnected ?? false; } }
		public uint? CnetID { get { return IsConnected ? (uint?)CnetClient?.PlayerInfo?.ID : null; } }
		public long MaxPacketSize { get { return CnetClient?.Con is CelesteNetTCPUDPConnection connection ? (connection.ConnectionSettings?.MaxPacketSize ?? 2048) : 2048; } }
        /// <summary>
        /// IDK exactly how much overhead i need to leave, but 256 bytes should be plenty for whatever headers cnet adds.
        /// The co-op helper format's header is at most 25 bytes plus the length of the sender's name
        /// </summary>
        public long MaxPacketChunkSize => MaxPacketSize - 25L - (PlayerID.LastKnownName?.Length ?? 100) - 256L;

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
		public bool CurrentChannelIsPublic
        {
            get
            {
                return CurrentChannel?.Name?.StartsWith("!") != true;
            }
        }
        public bool CanSendMessages {
			get {
				return IsConnected /*&& !CurrentChannelIsMain*/;
			}
		}
		public bool HasPendingOutgoingChunks => !outgoingExtraPacketChunks.IsEmpty;
		public bool HasPendingIncomingChunks => incomingExtraPacketChunks.Count > 0;

        #endregion

        #region Queues and Counters

        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();
        private ConcurrentQueue<DataType> outgoingExtraPacketChunks = new();
        private List<DataType> incomingExtraPacketChunks = new();
        private static object incomingChunksLock = new();

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
            outgoingExtraPacketChunks.Clear();
            incomingExtraPacketChunks.Clear();
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

        internal void EnqueueSubsequentChunk<T>(DataCoopBase<T> data) where T : DataCoopBase<T>, new()
        {
            outgoingExtraPacketChunks.Enqueue(data);
        }

        /// <summary>
        /// This function is called once per CelesteNet tick.
        /// This is the primary kicking-off point for network-y stuff
        /// </summary>
        /// <param name="counter">This parameter counts up once for each tick that occurs since starting the game</param>
        internal void Tick(ulong counter) {
            // Send any queued up extra packet chunks from previous large packets
            if (outgoingExtraPacketChunks.TryDequeue(out DataType chunk))
            {
                // Not calling SendAndHandle because the first one already has all the data when sending to ourself
                CnetClient.Send(chunk);
            }
			// Send the pending updates
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

        private T PreHandle<T>(T data) where T : DataCoopBase<T>, new()
        {
            if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
            if (data.chunksInPacket <= 1)
            {  // Packet does not have subsequent chunks and can be processed immediately
                return data;
            }

            // record the incoming chunk and then check if we have all of them
            lock (incomingChunksLock)
            {
                incomingExtraPacketChunks.Add(data);
                T[] arr = new T[data.chunksInPacket];
                Type t = data.GetType();
                // Sort the chunks for this packet into an array
                foreach (T chunk in incomingExtraPacketChunks)
                {
                    if (chunk.playerID.Equals(data.playerID) && chunk.packetID == data.packetID)
                    {
                        if (!chunk.GetType().Equals(t))
                        {
                            Logger.Log(LogLevel.Error, "Co-op Helper", $"Received packets of different types from the same sender with the same ID.");
                            throw new InvalidOperationException($"Co-op Helper: Received packets of different types from the same sender with the same ID.");
                        }
                        arr[chunk.chunkNumber] = chunk;
                    }
                }
                // Check whether we've received all the chunks
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] == null) return null;  // Don't have all chunks yet
                }
                // Compose the chunks into the same object and return it to be sent to the correct event
                for (int i = 0; i < arr.Length; i++)
                {
                    incomingExtraPacketChunks.Remove(arr[i]);
                }
                arr[0].Compose(arr);
                return arr[0];
            }
        }

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

        public void Handle(CelesteNetConnection con, DataMapSync data)
        {
            DataMapSync packet = PreHandle(data);
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Received chunk {data.chunkNumber + 1} of {data.chunksInPacket} for packet of type {data.GetTypeID(con.Data)}");
            if (packet == null) return;  // Waiting on more chunks
            updateQueue.Enqueue(() => 
				OnReceiveMapSync?.Invoke(packet)
			);
            lock (ReceivedMessagesCounterLock)
            {
                ++ReceivedMsgs;
            }
            Logger.Log(LogLevel.Debug, "Co-op Helper", $"Handled packet: {data.GetTypeID(con.Data)}");
        }

        #endregion
    }
}
