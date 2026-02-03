using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.Module;
using Microsoft.Xna.Framework;
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

		/// <summary>
		/// Gets the local Player ID, in serialized string form. Note that if this is called before connecting
		/// to CelesteNet since starting the game, the display name could be incorrect causing mismatches.
		/// </summary>
		/// <returns>Serialized Player ID</returns>
		public static string GetPlayerID() => PlayerID.MyID.SerializedID;

		/// <summary>
		/// Generates a new CoopSessionID
		/// </summary>
		/// <returns></returns>
		public static string GenerateNewCoopSessionID() => CoopSessionID.GetNewID().SerializedID;

		/// <summary>
		/// Try to join a session.
		/// </summary>
		/// <param name="currentSession">The current Session</param>
		/// <param name="serializedPlayerIDs">The serialized form of every PlayerID in the session</param>
		/// <param name="id">The serialized form of the CoopSessionID to join</param>
		/// <param name="deathSyncMode">Enum value of the death sync mode to use. See DeathSyncMode enum for values.</param>
		/// <returns>Returns true if the session was joined successfully, false if the session was not joined</returns>
		public static bool TryJoinSession(Session currentSession, string[] serializedPlayerIDs, string id, int deathSyncMode) {
			Logger.Log(LogLevel.Debug, "Co-op Helper", $"Beginning {nameof(TryJoinSession)}...");
			// Basic sanity checks
			if (currentSession == null || serializedPlayerIDs == null || serializedPlayerIDs.Length < 2 || string.IsNullOrEmpty(id)) {
				Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to join co-op session from interop. Invalid arguments to {nameof(TryJoinSession)}. "
					+ $"Info: {currentSession?.ToString() ?? "null"} | {serializedPlayerIDs?.Length.ToString() ?? "null"} | {id ?? "null"} | {deathSyncMode}");
				return false;
			}

			// Parse out the inputs to internal form
			PlayerID[] idArr = new PlayerID[serializedPlayerIDs.Length];
			for (int i = 0; i < idArr.Length; i++) {
				PlayerID? deserialized = PlayerID.FromSerialized(serializedPlayerIDs[i]);
				if (deserialized == null) {
					Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to join co-op session from interop. Could not deserialize PlayerID: '{serializedPlayerIDs[i] ?? "null"}'");
					return false;
				}
				idArr[i] = deserialized.Value;
			}
			CoopSessionID? coopSessionID = CoopSessionID.FromSerialized(id);
			if (coopSessionID == null) {
				Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to join co-op session from interop. Could not deserialize CoopSessionID: '{id ?? "null"}'");
				return false;
			}
			if (!Enum.IsDefined(typeof(DeathSyncMode), deathSyncMode)) {
				Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to join co-op session from interop. deathsyncmode '{deathSyncMode} is not defined.");
				return false;
			}

			// Make the session!
			return CoopHelperModule.MakeSession(currentSession, idArr, id: coopSessionID.Value, deathMode: (DeathSyncMode)deathSyncMode);
		}

		/// <summary>
		/// Create a synced version of a vanilla entity from EntityData. Use the vanilla "name" property. For entities to sync:
		/// - They must be of the same type.
		/// - data.Level.Name must match on all clients.
		/// - data.ID must match on all clients.
		/// Note that some properties may be added to the EntityData object.
		/// </summary>
		/// <param name="data">The EntityData object to use</param>
		/// <param name="offset">The positional offset of the room the entity is added to</param>
		/// <returns>Returns the newly created Entity, or null if a synced version could not be created.</returns>
		public static Monocle.Entity MakeSyncedEntity(EntityData data, Vector2 offset) {
			return CoopHelperModule.Instance.CreateSyncedEntityFromVanillaData(data, offset);
		}

		/// <summary>
		/// Checks whether we are currently in a co-op session (for a co-op map; does not include co-op everywhere)
		/// </summary>
		/// <returns><see langword="true" /> if we are currently in a session, else <see langword="false"/></returns>
		public static bool IsInSession()
		{
			return CoopHelperModule.Session?.IsInCoopSession == true;
        }

        /// <summary>
        /// Checks whether we are currently using co-op everywhere
        /// </summary>
        /// <returns><see langword="true" /> if we are currently using co-op everywhere, else <see langword="false"/></returns>
        public static bool IsCoopEverywhereActive()
		{
			return CoopHelperModule.Session?.CoopEverywhere == true;
		}

		/// <summary>
		/// Gets the serialized IDs of the players in the session.
		/// Do not parse this yourself; use other interop functions to extract data about the player.
		/// </summary>
		/// <returns>An enumeration of the players in the session</returns>
		public static IEnumerable<string> GetPlayersInSession()
		{
			if (CoopHelperModule.Session?.IsInCoopSession != true) yield break;
			foreach (PlayerID playerId in CoopHelperModule.Session.SessionMembers) yield return playerId.SerializedID;
		}

		/// <summary>
		/// Extracts the CelesteNet ID for a player from the Co-op ID.
		/// Note that this will NOT get the most up-to-date ID if the serialized ID was fetched before the target player's last reconnection to the server.
		/// </summary>
		/// <param name="serializedPlayerId"></param>
		/// <returns>The CelesteNet ID of the player, or <see cref="uint.MaxValue" /> if the CelesteNet ID is unknown or does not exist.</returns>
		public static uint GetPlayerCnetId(string serializedPlayerId)
		{
			PlayerID? playerId = PlayerID.FromSerialized(serializedPlayerId);
			if (playerId == null)
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to retrieve player CelesteNet ID. Could not deserialize CoopSessionID: '{serializedPlayerId ?? "null"}'");
                return uint.MaxValue;
			}
			return playerId.Value.CNetID;
		}

		/// <summary>
		/// Retrieves the SID of the current map of the given player.
		/// If the given player or their current state is unknown, or they are in the overworld, this returns an empty string.
		/// </summary>
		/// <param name="serializedPlayerId">The serialized ID of the player</param>
		/// <returns>The SID of the map the player is in, if known, otherwise empty string</returns>
		public static string GetPlayerCurrentMap(string serializedPlayerId)
        {
            PlayerID? playerId = PlayerID.FromSerialized(serializedPlayerId);
            if (playerId == null)
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to retrieve player current room. Could not deserialize CoopSessionID: '{serializedPlayerId ?? "null"}'");
                return "";
            }
			PlayerState state = PlayerState.Get(playerId.Value);
            if (state == null)
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to retrieve player current room. Player state is not known: '{serializedPlayerId ?? "null"}'");
                return "";
            }
			return state.CurrentMap.SID;
        }

        /// <summary>
        /// Retrieves the current room of the given player.
        /// If the given player or their current state is unknown, or they are in the overworld, this returns an empty string.
        /// </summary>
        /// <param name="serializedPlayerId">The serialized ID of the player</param>
        /// <returns>The room the player is in, if known, otherwise empty string</returns>
        public static string GetPlayerCurrentRoom(string serializedPlayerId)
        {
            PlayerID? playerId = PlayerID.FromSerialized(serializedPlayerId);
            if (playerId == null)
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to retrieve player current room. Could not deserialize CoopSessionID: '{serializedPlayerId ?? "null"}'");
                return "";
            }
            PlayerState state = PlayerState.Get(playerId.Value);
            if (state == null)
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to retrieve player current room. Player state is not known: '{serializedPlayerId ?? "null"}'");
                return "";
            }
            return state.CurrentRoom;
        }
    }
}
