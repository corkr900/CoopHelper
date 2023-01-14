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
		private static LinkedList<Tuple<int, EntityID, object>> incoming = new LinkedList<Tuple<int, EntityID, object>>();
		private static Dictionary<int, MethodInfo> parsers = new Dictionary<int, MethodInfo>();
		private static Dictionary<int, MethodInfo> staticHandlers = new Dictionary<int, MethodInfo>();
		private static Dictionary<EntityID, ISynchronizable> listeners = new Dictionary<EntityID, ISynchronizable>();

		public static bool HasUpdates { get { lock (outgoing) { return outgoing.Count > 0 && !IgnorePendingUpdatesInUpdateCheck; } } }
		private static volatile bool IgnorePendingUpdatesInUpdateCheck = false;

		static EntityStateTracker() {
			Assembly assembly = Assembly.GetExecutingAssembly();
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
					MethodInfo staticHandler = t.GetMethod("StaticHandler", BindingFlags.Static | BindingFlags.Public);
					if (staticHandler != null) {
						ParameterInfo[] paramInfo = staticHandler.GetParameters();
						if (!staticHandler.ReturnType.Equals(typeof(bool))
							|| paramInfo.Length != 1
							|| !paramInfo[0].ParameterType.Equals(typeof(object)))
						{
							throw new InvalidOperationException("Co-op Helper: StaticHandler function on ISynchronizable must have a object parameter and return a bool (" + t.Name + ")");
						}
					}
					RegisterType((int)getHeader.Invoke(null, null), parse, staticHandler);
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

		public static void RegisterType(int header, MethodInfo parser, MethodInfo staticHandler) {
			if (header == 0) throw new InvalidOperationException("Co-op Helper: Types may not use 0 as their header");
			if (parsers.ContainsKey(header)){
				if (parsers[header].Equals(parser)) return;
				throw new InvalidOperationException("Co-op Helper: Multiple typed registered under the same header");
			}
			parsers.Add(header, parser);
			if (staticHandler != null) {
				staticHandlers.Add(header, staticHandler);
			}
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

		internal static void ReceiveUpdates(CelesteNetBinaryReader r, bool isMySession) {
			lock (incoming) {
				do {
					int header = r.ReadInt32();
					if (header == 0) break;
					EntityID id = r.ReadEntityID();
					object parsedState = parsers[header].Invoke(null, new object[] { r });
					if (isMySession) {
						incoming.AddLast(new Tuple<int, EntityID, object>(header, id, parsedState));
					}
				} while (true);
			}
		}

		internal static void FlushIncoming() {
			lock (incoming) {
				LinkedListNode<Tuple<int, EntityID, object>> node = incoming.First;
				while (node != null) {
					LinkedListNode<Tuple<int, EntityID, object>> next = node.Next;
					// Specific listening entities take priority
					if (listeners.ContainsKey(node.Value.Item2)) {
						listeners[node.Value.Item2].ApplyState(node.Value.Item3);
						incoming.Remove(node);
					}
					// If there's no listener but a static handler, use that
					else if (staticHandlers.ContainsKey(node.Value.Item1)) {
						if ((bool)staticHandlers[node.Value.Item1].Invoke(null, new object[] { node.Value.Item3 })) {
							incoming.Remove(node);
						}
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
