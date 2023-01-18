using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
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
	}

	public static class EntityStateTracker {
		private static readonly long MaximumMessageSize = 3200;
		private static readonly int MaximumBufferRetention = 100;

		private static Queue<ISynchronizable> outgoing = new Queue<ISynchronizable>();
		private static LinkedList<Tuple<int, EntityID, object>> incoming = new LinkedList<Tuple<int, EntityID, object>>();
		private static Dictionary<int, SyncBehavior> behaviors = new Dictionary<int, SyncBehavior>();
		private static Dictionary<EntityID, ISynchronizable> listeners = new Dictionary<EntityID, ISynchronizable>();

		public static bool HasUpdates { get { lock (outgoing) { return outgoing.Count > 0 && !IgnorePendingUpdatesInUpdateCheck; } } }
		private static volatile bool IgnorePendingUpdatesInUpdateCheck = false;

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
						throw new InvalidOperationException("Co-op Helper: SyncBehavior musrt define a parser (" + t.Name + ")");
					}
					RegisterType(behav);
				}
			}
		}

		public static void AddListener(ISynchronizable ent) {
			EntityID id = ent.GetID();
			if (listeners.ContainsKey(id)) listeners[id] = ent;
			else listeners.Add(id, ent);
		}

		public static void RemoveListener(ISynchronizable ent) {
			EntityID id = ent.GetID();
			if (listeners.ContainsKey(id)) listeners.Remove(id);
		}

		public static void RemoveListener(EntityID id) {
			if (listeners.ContainsKey(id)) listeners.Remove(id);
		}

		public static void RegisterType(SyncBehavior behav) {
			if (behav.Header == 0) throw new InvalidOperationException("Co-op Helper: Types may not use 0 as their header");
			if (behaviors.ContainsKey(behav.Header)){
				if (behaviors[behav.Header].Equals(behav.Header)) return;
				throw new InvalidOperationException(string.Format("Co-op Helper: Multiple typed registered under the same header ({0})", behav.Header));
			}
			behaviors.Add(behav.Header, behav);
		}

		private static int GetHeader(ISynchronizable isy) {
			if (isy is ExternalSyncedEntity ese) return ese.Header;
			MethodInfo info = isy.GetType().GetMethod("GetSyncBehavior", BindingFlags.Static | BindingFlags.Public);
			return ((SyncBehavior)info.Invoke(null, null)).Header;
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
				while (outgoing.Count > 0 && w.BaseStream.Length < MaximumMessageSize) {
					ISynchronizable ob = outgoing.Dequeue();
					w.Write(GetHeader(ob));
					w.Write(ob.GetID());
					ob.WriteState(w);
				}
				w.Write(0);
				IgnorePendingUpdatesInUpdateCheck = false;
			}
		}

		internal static void ReceiveUpdates(CelesteNetBinaryReader r, bool isMySession) {
			lock (incoming) {
				do {
					int header = r.ReadInt32();
					if (header == 0) break;
					EntityID id = r.ReadEntityID();
					if (!behaviors.ContainsKey(header)) {
						throw new InvalidOperationException("Co-op Helper: Received header {0} does not have an associated parser");
					}
					object parsedState = behaviors[header].Parser(r);
					if (isMySession) {
						incoming.AddLast(new Tuple<int, EntityID, object>(header, id, parsedState));
					}
				} while (true);
			}
		}

		internal static void FlushIncoming() {
			lock (incoming) {
				Dictionary<EntityID, LinkedListNode<Tuple<int, EntityID, object>>> duplicateDict
					= new Dictionary<EntityID, LinkedListNode<Tuple<int, EntityID, object>>>();
				LinkedListNode<Tuple<int, EntityID, object>> node = incoming.First;
				while (node != null) {
					LinkedListNode<Tuple<int, EntityID, object>> next = node.Next;
					SyncBehavior behav = behaviors[node.Value.Item1];
					// Specific listening entities take priority
					if (listeners.ContainsKey(node.Value.Item2)) {
						listeners[node.Value.Item2].ApplyState(node.Value.Item3);
						incoming.Remove(node);
					}
					// If there's no listener but a static handler, try to use that
					else if (behav.StaticHandler?.Invoke(node.Value.Item2, node.Value.Item3) == true) {
						incoming.Remove(node);
					}
					else if (behav.DiscardIfNoListener) {
						incoming.Remove(node);
					}
					// Discard oldest duplicates if the type says to
					else if (behav.DiscardDuplicates) {
						if (duplicateDict.ContainsKey(node.Value.Item2)) {
							incoming.Remove(duplicateDict[node.Value.Item2]);
							duplicateDict[node.Value.Item2] = node;
						}
						else {
							duplicateDict.Add(node.Value.Item2, node);
						}
					}
					// TODO discard updates from rooms that nobody's in
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
						incoming.Clear();
						break;
					}
					LinkedListNode<Tuple<int, EntityID, object>> next = node.Next;
					SyncBehavior behav = behaviors[node.Value.Item1];
					if (!behav.Critical) {
						incoming.Remove(node);
					}
					node = next;
				}
			}
		}

		internal static void ClearBuffers() {
			outgoing?.Clear();
			incoming?.Clear();
			listeners?.Clear();
		}

		internal static void CheckRecurringUpdates() {
			foreach (ISynchronizable listener in listeners.Values) {
				if (listener.CheckRecurringUpdate()) PostUpdate(listener);
			}
		}

		public static void Write(this CelesteNetBinaryWriter w, EntityID id) {
			w.Write(id.Level);
			w.Write(id.ID);
		}

		public static EntityID ReadEntityID(this CelesteNetBinaryReader r) {
			string level = r.ReadString();
			int id = r.ReadInt32();
			return new EntityID(level, id);
		}
	}
}
