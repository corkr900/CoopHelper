using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	[ModExportName("CoopHelper")]
	public static class ModInterop {

		public static void RegisterSyncedType(int header, bool discardIfNoListener, bool discardDuplicates, bool critical, Func<CelesteNetBinaryReader, object> parser, Func<EntityID, object, bool> staticHandler = null) {
			EntityStateTracker.RegisterType(new SyncBehavior() {
				Header = header,
				DiscardIfNoListener = discardIfNoListener,
				DiscardDuplicates = discardDuplicates,
				Critical = critical,
				Parser = parser,
				StaticHandler = staticHandler,
			});
		}

		public static void AddListener(object listener, EntityID id, int header) {
			EntityStateTracker.AddListener(new ExternalSyncedEntity(listener, id, header));
		}

		public static void RemoveListener(EntityID id) {
			EntityStateTracker.RemoveListener(id);
		}

		public static void PostUpdate(EntityID id) {
			EntityStateTracker.PostUpdate(id);
		}
	}
}
