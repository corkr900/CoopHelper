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
	// TODO separate critical and noncritical updates
	// TODO leave incoming updates buffered during death animation
	public static class EntityStateTracker {
		private static readonly long MaximumMessageSize = 3200;
		private static readonly int MaximumBufferRetention = 100;

		private static Queue<ISynchronizable> outgoing = new Queue<ISynchronizable>();
		private static LinkedList<Tuple<EntityID, object>> incoming = new LinkedList<Tuple<EntityID, object>>();
		private static Dictionary<int, MethodInfo> parsers = new Dictionary<int, MethodInfo>();
		private static Dictionary<EntityID, ISynchronizable> listeners = new Dictionary<EntityID, ISynchronizable>();

		public static bool HasUpdates { get { lock (outgoing) { return outgoing.Count > 0 && !IgnorePendingUpdatesInUpdateCheck; } } }
		private static volatile bool IgnorePendingUpdatesInUpdateCheck = false;

		static EntityStateTracker() {
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
			foreach(Type t in assembly.DefinedTypes) {
				if (!t.IsClass) continue;
				bool isISync = t.GetInterfaces().Any(itf => itf == typeof(ISynchronizable));
				if (isISync) {
					MethodInfo getHeader = t.GetMethod("GetHeader", BindingFlags.Static | BindingFlags.Public);
					if (getHeader == null || !getHeader.ReturnType.Equals(typeof(int))) {
						throw new InvalidOperationException("Co-op Helper: Types implementing ISynchronizable must define a static GetHeader method returning an int (" + t.Name + ")");
					}
					MethodInfo parse = t.GetMethod("ParseState", BindingFlags.Static | BindingFlags.Public);
					if (parse == null) {
						throw new InvalidOperationException("Co-op Helper: Types implementing ISynchronizable must define a static ParseState method returning the Generic type of the interface (" + t.Name + ")");
					}
					RegisterType((int)getHeader.Invoke(null, null), parse);
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

		public static void RegisterType(int header, MethodInfo parser) {
			if (header == 0) throw new InvalidOperationException("Co-op Helper: Types may not use 0 as their header");
			if (parsers.ContainsKey(header)){
				if (parsers[header].Equals(parser)) return;
				throw new InvalidOperationException("Co-op Helper: Multiple typed registered under the same header");
			}
			parsers.Add(header, parser);
		}

		private static int GetHeader(ISynchronizable isy) {
			MethodInfo getHeader = isy.GetType().GetMethod("GetHeader", BindingFlags.Static | BindingFlags.Public);
			if (getHeader == null || !getHeader.ReturnType.Equals(typeof(int))) {
				throw new InvalidOperationException("Co-op Helper: Types implementing ISynchronizable must define a static GetHeader method returning an int");
			}
			return (int)getHeader.Invoke(null, null);
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

		internal static void ReceiveUpdates(CelesteNetBinaryReader r) {
			lock (incoming) {
				do {
					int header = r.ReadInt32();
					if (header == 0) break;
					EntityID id = r.ReadEntityID();
					object parsedState = parsers[header].Invoke(null, new object[] { r });
					incoming.AddLast(new Tuple<EntityID, object>(id, parsedState));
				} while (true);
			}
		}

		internal static void FlushIncoming() {
			lock (incoming) {
				LinkedListNode<Tuple<EntityID, object>> node = incoming.First;
				while (node != null) {
					LinkedListNode<Tuple<EntityID, object>> next = node.Next;
					if (listeners.ContainsKey(node.Value.Item1)) {
						listeners[node.Value.Item1].ApplyState(node.Value.Item2);
						incoming.Remove(node);
					}
					// TODO discard updates from rooms that nobody's in
					node = next;
				}
				// Prevent the incoming buffer from growing indefinitely with updates noone's listening to
				while (incoming.Count > MaximumBufferRetention) {
					incoming.RemoveFirst();
				}
			}
		}

		internal static void ClearBuffers() {
			outgoing?.Clear();
			incoming?.Clear();
			listeners?.Clear();
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
