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
	/// <summary>
	/// ModInterop class to let other code mods use the entity synchronization framework
	/// </summary>
	[ModExportName("CoopHelper")]
	public static class ModInterop {

		/// <summary>
		/// Call this once for every synchronizable type in your mod to register the type. It must be called prior to joining a co-op session.
		/// </summary>
		/// <param name="header">Integer used to uniquely identify the type.
		/// This must be constant across all versions and across all instances of Everest.
		/// This cannot be the same as the header for any other type in any mod.</param>
		/// <param name="discardIfNoListener">If true, updates of this type will be immediately discarded if there is no listener for it</param>
		/// <param name="discardDuplicates">If true, if there are multiple updates for the same entity in the incoming queue then the older update will be discarded.
		/// Use this for classes that may send frequent updates and can recover from not receiving all of them.</param>
		/// <param name="critical">If true, updates will not be discarded in the event of the incoming queue becoming too large.</param>
		/// <param name="parser">Function to read the state object from the celestenet stream</param>
		/// <param name="staticHandler">This function will be called to handle an update if there are no matching listeners.
		/// It should return true if the message was handled, or false to leave it on the incoming queue. Pass null if the type doesn't have static handling.</param>
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

		/// <summary>
		/// Notify the sync system that an entity is listening for remote updates
		/// </summary>
		/// <param name="listener">The entity to receive updates</param>
		/// <param name="id">The unique ID of the entity</param>
		/// <param name="header">The integer header matching its registered type</param>
		/// <param name="doRecurringUpdate">Whether to periodically poll the entity for whether an update should be sent (USE SPARINGLY - poor performance)</param>
		public static void AddListener(object listener, EntityID id, int header, bool doRecurringUpdate) {
			EntityStateTracker.AddListener(new ExternalSyncedEntity(listener, id, header, doRecurringUpdate), doRecurringUpdate);
		}

		/// <summary>
		/// Notify the sync system that an entity is no longer listening for remote updates
		/// </summary>
		/// <param name="id">The unique ID of the entity</param>
		public static void RemoveListener(EntityID id) {
			EntityStateTracker.RemoveListener(id);
		}

		/// <summary>
		/// Notify the sync system that an entity needs to send an updated state
		/// </summary>
		/// <param name="id">The unique ID of the entity</param>
		public static void PostUpdate(EntityID id) {
			EntityStateTracker.PostUpdate(id);
		}
	}
}
