using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.IO;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {

	public class SyncBehavior {
		/// <summary>
		/// REQUIRED: integer header that tells the deserializer how to handle it.
		/// This may not match the header for any other type and must be the same
		/// on all computers using the same version of Co-op Helper.
		/// </summary>
		public int Header;
		/// <summary>
		/// REQUIRED: Function to read the state object from the celestenet stream
		/// </summary>
		public Func<CelesteNetBinaryReader, object> Parser;
		/// <summary>
		/// Optional: This function will be called to handle an update if there are no matching listeners.
		/// It should return true if the message can be / is handled, or false to leave it on the incoming queue
		/// </summary>
		public Func<EntityID, object, bool> StaticHandler;
		/// <summary>
		/// If true, updates of this type will be immediately discarded if there is no listener for it
		/// </summary>
		public bool DiscardIfNoListener;
		/// <summary>
		/// If true, if there are multiple updates for the same entity in the incoming queue then the older update will be discarded.
		/// Use this for classes that may send frequent updates and can recover from not receiving all of them.
		/// </summary>
		public bool DiscardDuplicates;
		/// <summary>
		/// If true, these updates will not be discarded in the event of the incoming queue becoming too large.
		/// </summary>
		public bool Critical;
		/// <summary>
		/// Updates will not be applied if there's no Player in the scene.
		/// Updates ignored due to this are handled as if there was no listener.
		/// </summary>
		public bool ApplyStateRequiresPlayer;
	}

	public static class EntityStateTracker {
		private static readonly int MaximumBufferRetention = 100;
		private static readonly int EndOfTransmissionHeader = 0;

		private static Queue<ISynchronizable> outgoing = new Queue<ISynchronizable>();
		private static LinkedList<Tuple<int, EntityID, object>> incoming = new LinkedList<Tuple<int, EntityID, object>>();
		private static Dictionary<int, SyncBehavior> behaviors = new Dictionary<int, SyncBehavior>();
		private static Dictionary<EntityID, ISynchronizable> listeners = new Dictionary<EntityID, ISynchronizable>();
		private static Dictionary<EntityID, ISynchronizable> listenersWithRecurringUpdate = new Dictionary<EntityID, ISynchronizable>();

		public static bool HasUpdates { get { lock (outgoing) { return outgoing.Count > 0 && !IgnorePendingUpdatesInUpdateCheck; } } }
		private static volatile bool IgnorePendingUpdatesInUpdateCheck = false;

		public static int CurrentListeners { get { return listeners.Count; } }
		public static int ProcessedUpdates { get; set; } = 0;
		public static int DiscardedUpdates { get; set; } = 0;
		public static int SentUpdates { get; set; } = 0;

		static EntityStateTracker() {
			Assembly assembly = Assembly.GetExecutingAssembly();
			foreach(Type t in assembly.DefinedTypes) {
				if (!t.IsClass) continue;
				if (t.Equals(typeof(ExternalSyncedEntity))) continue;
				bool isISync = t.GetInterfaces().Any(itf => itf == typeof(ISynchronizable));
				if (isISync) {
					MethodInfo getSyncBehavior = t.GetMethod("GetSyncBehavior", BindingFlags.Static | BindingFlags.Public);
					if (getSyncBehavior == null
						|| !getSyncBehavior.IsStatic
						|| !getSyncBehavior.ReturnType.Equals(typeof(SyncBehavior))
						|| getSyncBehavior.GetParameters().Length > 0)
					{
						throw new InvalidOperationException("Co-op Helper: Types implementing ISynchronizable must define a static GetSyncBehavior method returning a SyncBehavior (" + t.Name + ")");
					}
					SyncBehavior behav = (SyncBehavior)getSyncBehavior.Invoke(null, null);
					if (behav.Parser == null) {
						throw new InvalidOperationException("Co-op Helper: SyncBehavior must define a parser (" + t.Name + ")");
					}
					RegisterType(behav);
				}
			}
		}

		public static void AddListener(ISynchronizable ent, bool doRecurringUpdate) {
			EntityID id = ent.GetID();
			if (listeners.ContainsKey(id)) listeners[id] = ent;
			else listeners.Add(id, ent);
			if (doRecurringUpdate) {
				if (listenersWithRecurringUpdate.ContainsKey(id)) listeners[id] = ent;
				else listenersWithRecurringUpdate.Add(id, ent);
			}
		}

		public static void RemoveListener(ISynchronizable ent) {
			EntityID id = ent.GetID();
			if (listeners.ContainsKey(id)) listeners.Remove(id);
			if (listenersWithRecurringUpdate.ContainsKey(id)) listenersWithRecurringUpdate.Remove(id);
		}

		public static void RemoveListener(EntityID id) {
			if (listeners.ContainsKey(id)) listeners.Remove(id);
			if (listenersWithRecurringUpdate.ContainsKey(id)) listenersWithRecurringUpdate.Remove(id);
		}

		public static void RegisterType(SyncBehavior behav) {
			if (behav.Header == EndOfTransmissionHeader) throw new InvalidOperationException(string.Format("Co-op Helper: Types may not use {0} as their header", EndOfTransmissionHeader));
			if (behaviors.ContainsKey(behav.Header)){
				if (behaviors[behav.Header].Equals(behav.Header)) return;
				throw new InvalidOperationException(string.Format("Co-op Helper: Multiple typed registered under the same header ({0})", behav.Header));
			}
			behaviors.Add(behav.Header, behav);
		}

		private static SyncBehavior GetBehavior(ISynchronizable isy) {
			if (isy is ExternalSyncedEntity ese) return null;
			MethodInfo info = isy.GetType().GetMethod("GetSyncBehavior", BindingFlags.Static | BindingFlags.Public);
			return info?.Invoke(null, null) as SyncBehavior;
		}

		private static int GetHeader(ISynchronizable isy) {
			return isy is ExternalSyncedEntity ese ? ese.Header : GetBehavior(isy).Header;
		}

		public static void PostUpdate(EntityID id) {
			if (listeners.ContainsKey(id)) {
				PostUpdate(listeners[id]);
			}
		}

		public static void PostUpdate(ISynchronizable entity) {
			lock (outgoing) {
				if (!outgoing.Contains(entity, new SynchronizableComparer())) {
					outgoing.Enqueue(entity);
				}
				IgnorePendingUpdatesInUpdateCheck = false;
			}
		}

		internal static void NotifyInitiatingOutgoingMessage() {
			IgnorePendingUpdatesInUpdateCheck = true;
		}

		internal static void FlushOutgoing(CelesteNetBinaryWriter w) {
			lock (outgoing) {
				long streamPositionAtStart = w.BaseStream.Position;
				long maxPacketSize = CNetComm.Instance?.MaxPacketSize ?? 2048;
				long stopThreshold = (long)(maxPacketSize * 0.8);
				int before = outgoing.Count;
				while (outgoing.Count > 0 && w.BaseStream.Position - streamPositionAtStart < stopThreshold) {
					ISynchronizable ob = outgoing.Dequeue();
					w.Write(GetHeader(ob));
					w.Write(ob.GetID());
					long sizeHeaderPosition = w.BaseStream.Position;
					w.Write((long)0);  // placeholder... we'll overwrite it later
					// serialize the update and measure the serialized size
					long posBefore = w.BaseStream.Position;
					ob.WriteState(w);
					long posAfter = w.BaseStream.Position;
					// go back and overwrite the size header with the measured size
					w.BaseStream.Seek(sizeHeaderPosition, System.IO.SeekOrigin.Begin);
					w.Write(posAfter - posBefore);
					// return to the correct position to continue with next subpacket
					w.BaseStream.Seek(posAfter, System.IO.SeekOrigin.Begin);
				}
				int after = outgoing.Count;
				SentUpdates += before - after;
				if (after > 0) {
					Logger.Log(LogLevel.Info, "Co-op Helper", string.Format("Outgoing update buffer exceeds packet size limit; deferring {0} updates", after));
				}
				w.Write(EndOfTransmissionHeader);
				IgnorePendingUpdatesInUpdateCheck = false;
			}
		}

		internal static void ReceiveUpdates(CelesteNetBinaryReader r, bool isMySession) {
			lock (incoming) {
				try {
					do {
						int header = r.ReadInt32();
						if (header == EndOfTransmissionHeader) break;
						EntityID id = r.ReadEntityID();
						long size = r.ReadInt64();
						if (isMySession && behaviors.ContainsKey(header)) {
							object parsedState = behaviors[header].Parser(r);
							incoming.AddLast(new Tuple<int, EntityID, object>(header, id, parsedState));
						}
						else {
							r.BaseStream.Seek(size, System.IO.SeekOrigin.Current);
						}
					} while (true);
				}
				catch(Exception e) {
					Logger.Log(LogLevel.Error, "Co-op Helper", "Error occurred deserializing entity update packet. Aborting.");
					throw e;
				}
			}
		}

		internal static void FlushIncoming() {
			Level level = Engine.Scene as Level;
			if (level == null) return;
			if (level.Transitioning) return;  // don't process incoming updates during screen transition or if the scene isn't a Level
			bool playerPresent = level.Tracker?.GetEntity<Player>() != null;
			lock (incoming) {
				Dictionary<EntityID, LinkedListNode<Tuple<int, EntityID, object>>> duplicateDict
					= new Dictionary<EntityID, LinkedListNode<Tuple<int, EntityID, object>>>();
				LinkedListNode<Tuple<int, EntityID, object>> node = incoming.First;
				while (node != null) {
					LinkedListNode<Tuple<int, EntityID, object>> next = node.Next;
					SyncBehavior behav = behaviors[node.Value.Item1];
					bool playerCheck = playerPresent || !behav.ApplyStateRequiresPlayer;
					// Specific listening entities take priority
					if (playerCheck && listeners.ContainsKey(node.Value.Item2)) {
						listeners[node.Value.Item2].ApplyState(node.Value.Item3);
						incoming.Remove(node);
						++ProcessedUpdates;
					}
					// If there's no listener but a static handler, try to use that
					else if (behav.StaticHandler?.Invoke(node.Value.Item2, node.Value.Item3) == true) {
						incoming.Remove(node);
						++ProcessedUpdates;
					}
					// Discard unlistened updates if the behavior says to
					else if (behav.DiscardIfNoListener) {
						incoming.Remove(node);
						++DiscardedUpdates;
					}
					// Discard oldest duplicates if the type says to
					else if (behav.DiscardDuplicates) {
						if (duplicateDict.ContainsKey(node.Value.Item2)) {
							incoming.Remove(duplicateDict[node.Value.Item2]);
							duplicateDict[node.Value.Item2] = node;
							++DiscardedUpdates;
						}
						else {
							duplicateDict.Add(node.Value.Item2, node);
						}
					}
					node = next;
				}

				// Prevent the incoming buffer from growing indefinitely with updates noone's listening to
				// Discard oldest non-critical updates until the incoming buffer is down to the max retention
				node = incoming.First;
				if (incoming.Count > MaximumBufferRetention) {
					Logger.Log(LogLevel.Info, "Co-op Helper", "Incoming message buffer is too big; discarding non-critical updates");
				}
				while (incoming.Count > MaximumBufferRetention) {
					if (node == null) {
						Logger.Log(LogLevel.Error, "Co-op Helper", "Incoming message queue contains too many critical updates. Purging all incoming updates.");
						Engine.Commands.Log("Co-op Helper: Incoming message queue contains too many critical updates. Purging all incoming updates.");
						DiscardedUpdates += incoming.Count;
						incoming.Clear();
						break;
					}
					LinkedListNode<Tuple<int, EntityID, object>> next = node.Next;
					SyncBehavior behav = behaviors[node.Value.Item1];
					if (!behav.Critical) {
						incoming.Remove(node);
						++DiscardedUpdates;
					}
					node = next;
				}
			}
		}

		internal static void ClearBuffers() {
			outgoing?.Clear();
			incoming?.Clear();
			listeners?.Clear();
			listenersWithRecurringUpdate?.Clear();
		}

		internal static void CheckRecurringUpdates() {
			foreach (ISynchronizable listener in listenersWithRecurringUpdate.Values) {
				if (listener.CheckRecurringUpdate()) PostUpdate(listener);
			}
		}

		public static void Write(this CelesteNetBinaryWriter w, EntityID id) {
			w.Write(id.Level ?? "");
			w.Write(id.ID);
		}

		public static EntityID ReadEntityID(this CelesteNetBinaryReader r) {
			string level = r.ReadString();
			int id = r.ReadInt32();
			return new EntityID(level, id);
		}
	}
}
