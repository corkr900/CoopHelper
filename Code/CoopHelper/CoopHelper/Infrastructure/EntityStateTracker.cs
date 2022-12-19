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
	public static class EntityStateTracker {
		private static readonly long MaximumMessageSize = 3200;
		private static Queue<ISynchronizable> outgoingUpdates = new Queue<ISynchronizable>();
		private static Dictionary<EntityID, ISynchronizable> listeners = new Dictionary<EntityID, ISynchronizable>();
		private static Dictionary<int, MethodInfo> parsers = new Dictionary<int, MethodInfo>();

		public static bool HasUpdates { get { return outgoingUpdates.Count > 0; } }

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

		public static void DoAnything() {
			int y = 1;
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
			if (!outgoingUpdates.Contains(entity, new SynchronizableComparer())) {
				outgoingUpdates.Enqueue(entity);
			}
		}

		internal static void FlushUpdates(CelesteNetBinaryWriter w) {
			while (outgoingUpdates.Count > 0 && w.BaseStream.Length < MaximumMessageSize) {
				ISynchronizable ob = outgoingUpdates.Dequeue();
				w.Write(GetHeader(ob));
				w.Write(ob.GetID());
				ob.WriteState(w);
			}
			w.Write(0);
		}

		internal static void ReceiveUpdates(CelesteNetBinaryReader r) {
			do {
				int header = r.ReadInt32();
				if (header == 0) break;
				EntityID id = r.ReadEntityID();
				object parsedState = parsers[header].Invoke(null, new object[] { r });
				if (listeners.ContainsKey(id)) {
					listeners[id].ApplyState(parsedState);
				}
				else {
					// TODO queue up entity updates when player isn't listening for them
				}
			} while (true);
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
