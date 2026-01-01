using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using FMOD;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.ModInterop;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste.Mod.CoopHelper {
	public class CoopHelperModule : EverestModule {

		public static readonly string ProtocolVersion = "1_0_5";

		#region Setup and Static Stuff

		public static CoopHelperModule Instance { get; private set; }
		public static string AssemblyVersion {
			get {
				if (string.IsNullOrEmpty(_version)) {
					System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
					System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
					_version = fvi.FileVersion;
				}
				return _version;
			}
		}
		private static string _version = null;

		public override Type SettingsType => typeof(CoopHelperModuleSettings);
		public static CoopHelperModuleSettings Settings => (CoopHelperModuleSettings)Instance._Settings;
		public override Type SaveDataType => typeof(CoopHelperModuleSaveData);
		public static CoopHelperModuleSaveData SaveData => (CoopHelperModuleSaveData)Instance._SaveData;
		public override Type SessionType => typeof(CoopHelperModuleSession);
		public static CoopHelperModuleSession Session => (CoopHelperModuleSession)Instance._Session;

		private CNetComm Comm;

		private static Hook hook_Strawberry_orig_OnCollect;
		private static Hook hook_CelesteNetClientSettings_Interactions_get;
		private static Hook hook_Player_orig_Die;
		private static Hook hook_SpikeInfo_OnPlayer;
		private static Hook hook_Level_orig_LoadLevel;

		private static ILHook hook_CrushBlock_AttackSequence;

		public CoopHelperModule() {
			Instance = this;
		}

		#endregion

		#region Startup

		public override void Load() {
			Celeste.Instance.Components.Add(Comm = new CNetComm(Celeste.Instance));

			// Manual Hooks
			hook_Strawberry_orig_OnCollect = new Hook(
				typeof(Strawberry).GetMethod("orig_OnCollect", BindingFlags.Public | BindingFlags.Instance),
				OnStrawberryCollect);
			hook_CelesteNetClientSettings_Interactions_get = new Hook(
				typeof(CelesteNetClientSettings).GetProperty("Interactions").GetMethod,
				OnCelesteNetClientSettingsInteractionsGet);
			hook_Player_orig_Die = new Hook(
				typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance),
				OnPlayerDie);
			hook_SpikeInfo_OnPlayer = new Hook(
				typeof(TriggerSpikes.SpikeInfo).GetMethod("OnPlayer", BindingFlags.Public | BindingFlags.Instance),
				OnSpikeInfoOnPlayer);
			hook_Level_orig_LoadLevel = new Hook(
				typeof(Level).GetMethod("orig_LoadLevel", BindingFlags.Public | BindingFlags.Instance),
				OnLevelOrigLoadLevel);

			// IL Hooks
			MethodInfo m = typeof(CrushBlock).GetMethod("AttackSequence", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget();
			hook_CrushBlock_AttackSequence = new ILHook(m, (il) => ILKevinCollide(m.DeclaringType.GetField("<>4__this"), il));

			On.Celeste.Key.RegisterUsed += OnKeyRegisterUsed;
			On.Celeste.Level.LoadLevel += OnLevelLoad;
			On.Celeste.Player.OnTransition += OnPlayerTransition;
			On.Celeste.Spring.ctor_EntityData_Vector2_Orientations += OnSpringCtor;
			On.Celeste.Booster.PlayerReleased += OnBoosterPlayerReleased;
			On.Celeste.Booster.PlayerBoosted += OnBoosterPlayerBoosted;
			On.Celeste.HeartGem.RegisterAsCollected += OnHeartCollected;
			On.Celeste.Platform.StartShaking += OnPlatformStartShaking;
			On.Celeste.Cassette.OnPlayer += OnCasetteOnPlayer;
			On.Celeste.MoveBlock.MoveCheck += OnMoveBlockMoveCheck;
			On.Celeste.DashBlock.Break_Vector2_Vector2_bool_bool += OnDashBlockBreak;
			On.Celeste.LockBlock.UnlockRoutine += OnLockBlockUnlockRoutine;
			On.Celeste.LevelLoader.StartLevel += OnLevelLoaderStart;
			On.Celeste.FallingBlock.PlayerFallCheck += OnFallingBlockPlayerCheck;
			On.Celeste.AscendManager.Routine += OnAscendManagerRoutine;
			On.Celeste.CoreModeToggle.OnPlayer += OnCoreModeTogglePlayer;
			On.Celeste.TempleCrackedBlock.Break += OnTempleCrackedBlockBreak;
			On.Celeste.ClutterAbsorbEffect.Added += OnClutterAbsorbEffectAdded;
			On.Celeste.ChangeRespawnTrigger.OnEnter += OnChangeRespawnTriggerEnter;

			Everest.Events.Player.OnSpawn += OnSpawn;
			Everest.Events.Level.OnExit += onLevelExit;
			Everest.Events.Level.OnLoadEntity += OnLevelLoadEntity;
			Everest.Events.AssetReload.OnReloadLevel += OnReloadLevel;

			typeof(ModInterop).ModInterop();
			typeof(SkinModHelperPlus).ModInterop();
		}

		public override void Unload() {
			Celeste.Instance.Components.Remove(Comm);
			Comm = null;

			// Manual Hooks
			hook_Strawberry_orig_OnCollect?.Dispose();
			hook_Strawberry_orig_OnCollect = null;
			hook_CelesteNetClientSettings_Interactions_get?.Dispose();
			hook_CelesteNetClientSettings_Interactions_get = null;
			hook_Player_orig_Die?.Dispose();
			hook_Player_orig_Die = null;
			hook_SpikeInfo_OnPlayer?.Dispose();
			hook_SpikeInfo_OnPlayer = null;
			hook_Level_orig_LoadLevel?.Dispose();
			hook_Level_orig_LoadLevel = null;

			// IL Hooks
			hook_CrushBlock_AttackSequence?.Dispose();
			hook_CrushBlock_AttackSequence = null;

			On.Celeste.Key.RegisterUsed -= OnKeyRegisterUsed;
			On.Celeste.Level.LoadLevel -= OnLevelLoad;
			On.Celeste.Player.OnTransition -= OnPlayerTransition;
			On.Celeste.Spring.ctor_EntityData_Vector2_Orientations -= OnSpringCtor;
			On.Celeste.Booster.PlayerReleased -= OnBoosterPlayerReleased;
			On.Celeste.Booster.PlayerBoosted -= OnBoosterPlayerBoosted;
			On.Celeste.HeartGem.RegisterAsCollected -= OnHeartCollected;
			On.Celeste.Platform.StartShaking -= OnPlatformStartShaking;
			On.Celeste.Cassette.OnPlayer -= OnCasetteOnPlayer;
			On.Celeste.MoveBlock.MoveCheck -= OnMoveBlockMoveCheck;
			On.Celeste.DashBlock.Break_Vector2_Vector2_bool_bool -= OnDashBlockBreak;
			On.Celeste.LockBlock.UnlockRoutine -= OnLockBlockUnlockRoutine;
			On.Celeste.LevelLoader.StartLevel -= OnLevelLoaderStart;
			On.Celeste.FallingBlock.PlayerFallCheck -= OnFallingBlockPlayerCheck;
			On.Celeste.AscendManager.Routine -= OnAscendManagerRoutine;
			On.Celeste.CoreModeToggle.OnPlayer -= OnCoreModeTogglePlayer;
			On.Celeste.TempleCrackedBlock.Break -= OnTempleCrackedBlockBreak;
			On.Celeste.ClutterAbsorbEffect.Added -= OnClutterAbsorbEffectAdded;
			On.Celeste.ChangeRespawnTrigger.OnEnter -= OnChangeRespawnTriggerEnter;

            Everest.Events.Player.OnSpawn -= OnSpawn;
			Everest.Events.Level.OnExit -= onLevelExit;
			Everest.Events.Level.OnLoadEntity -= OnLevelLoadEntity;
            Everest.Events.AssetReload.OnReloadLevel -= OnReloadLevel;
        }

        #endregion

        #region Session Information

        public delegate void OnSessionInfoChangedHandler();
		public static event OnSessionInfoChangedHandler OnSessionInfoChanged;

		internal CoopHelperModuleSession CachedSession = null;

		public static void NotifySessionChanged() {
			OnSessionInfoChanged?.Invoke();
		}

		internal static bool MakeSession(
			Session currentSession,
			PlayerID[] players,
			CoopSessionID? id = null,
			int? dashes = null,
			DeathSyncMode deathMode = DeathSyncMode.SameRoomOnly,
			string ability = "",
			string skin = "")
		{
			// Basic checks...
			if (Session == null) {
				Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to make co-op session. Session is null.");
				return false;
			}
			int myRole = -1;
			for (int i = 0; i < (players?.Length ?? 0); i++) {
				if (players[i].Equals(PlayerID.MyID)) {
					myRole = i;
				}
			}
			if (myRole < 0) {
				Logger.Log(LogLevel.Warn, "Co-op Helper", $"Failed to make co-op session. Local player is not in the session.");
				return false;
			}


			// Set up basic session data and flags
			if (id == null) id = CoopSessionID.GetNewID();
			currentSession.SetFlag("CoopHelper_InSession", true);
			for (int i = 0; i < players.Length; i++) {
				currentSession.SetFlag("CoopHelper_SessionRole_" + i, i == myRole);
			}

			// Dash count
			if (dashes != null) currentSession.Inventory.Dashes = dashes.Value;

			// Handle abilities
			if (ability == "grapple") {
				Type t_JackalModule = Type.GetType("Celeste.Mod.JackalHelper.JackalModule,JackalHelper");
				if (t_JackalModule != null) {
					PropertyInfo p_Session = t_JackalModule.GetProperty("Session");
					EverestModuleSession JackalSession = p_Session?.GetValue(null) as EverestModuleSession;
					DynamicData dd = DynamicData.For(JackalSession);
					dd.Set("hasGrapple", true);
				}
			}

			// Handle skins
			if (!string.IsNullOrEmpty(skin)) {
				// If using SMH+
				if (SkinModHelperPlus.IsAvailable) {
					Logger.Log(LogLevel.Verbose, "Co-op Helper", $"Changing skin to '{skin}' using SkinModHelperPlus");
					SkinModHelperPlus.SessionSet_GeneralSkin(skin, true);
				}
				// If using classic SMH
				else if (SkinModHelper.IsAvailable) {
					Logger.Log(LogLevel.Verbose, "Co-op Helper", $"Changing skin to '{skin}' using SkinModHelper");
					SkinModHelper.ApplySkin(skin);
				}
				else {
					Logger.Log(LogLevel.Warn, "Co-op Helper", "Could not change skin: neither SkinModHelper nor SMH+ is installed.");
				}
			}

			// Write to the Session and broadcast the update
			Session.IsInCoopSession = true;
			Session.SessionID = id.Value;
			Session.SessionRole = myRole;
			Session.SessionMembers = new List<PlayerID>(players);
			Session.DeathSync = deathMode;
			Session.DashCount = dashes ?? currentSession.Inventory.Dashes;
			Session.Skin = skin;
			Session.Ability = ability;

			NotifySessionChanged();
			return true;
		}

		private void CacheSession() {
			CachedSession = Session;
		}

		private void TryRestoreCachedSession(Session currentSession) {
			if (CachedSession == null) return;
			MakeSession(currentSession,
				CachedSession.SessionMembers?.ToArray(),
				CachedSession.SessionID,
				CachedSession.DashCount,
				CachedSession.DeathSync,
				CachedSession.Ability,
				CachedSession.Skin);
			CachedSession = null;
		}

		#endregion

		#region IL Hooks

		private void ILKevinCollide(FieldInfo crushBlockLdfldValue, ILContext il) {
			ILCursor cursor = new ILCursor(il);
			if (cursor.TryGotoNext(instr => instr.MatchCallvirt(typeof(Entity).GetMethod("RemoveSelf")))
				&& cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdloc(2)))
			{
				cursor.Emit(OpCodes.Ldloc_1);  // load instance
				cursor.EmitDelegate<Func<bool, CrushBlock, bool>>((bool collided, CrushBlock blk) => {
					if (blk is SyncedKevin kev) {
						if (!kev.Attacking && !collided) collided = true;
						else if (collided) kev.OnReturnBegin();
					}
					return collided;
				});
			}
		}

		#endregion

		#region Hooked Code + Event Handlers

		private bool OnLevelLoadEntity(Level level, LevelData levelData, Vector2 offset, EntityData data) {
			if (!Session.CoopEverywhere) return false;
			Entity e = CreateSyncedEntityFromVanillaData(data, offset);
			if (e == null) return false;
			level.Add(e);
			return true;
		}
		internal Entity CreateSyncedEntityFromVanillaData(EntityData data, Vector2 offset) {
			data.Values ??= new();
			switch(data.Name) {
				default:
					return null;

				case "refill":
					return new SyncedRefill(data, offset);

				case "zipMover":
					return new SyncedZipMover(data, offset);

				case "fallingBlock":
					return new SyncedFallingBlock(data, offset);

				case "crumbleBlock":
					return new SyncedCrumblePlatform(data, offset);

				case "touchSwitch":
					return new SyncedTouchSwitch(data, offset);

				case "key":
					return new SyncedKey(data, offset);

				case "lockBlock":
					return new SyncedLockBlock(data, offset);

				case "booster":
					return new SyncedBooster(data, offset);

				case "moveBlock":
					return new SyncedMoveBlock(data, offset);

				case "switchBlock":
				case "swapBlock":
					return new SyncedSwapBlock(data, offset);

				case "dashSwitchH":
					data.Values["side"] = data.Bool("leftSide", false) ? "Left" : "Right";
					goto case "dashSwitch";
				case "dashSwitchV":
					data.Values["side"] = data.Bool("ceiling", false) ? "Up" : "Down";
					goto case "dashSwitch";
				case "dashSwitch":
					return new SyncedDashSwitch(data, offset);

				case "templeCrackedBlock":
					return new SyncedTempleCrackedBlock(data, offset);

				case "coreModeToggle":
					return new SyncedCoreModeToggle(data, offset);

				case "eyebomb":
					return new SyncedPuffer(data, offset);

				case "lightningBlock":
					return new SyncedLightningBreakerBox(data, offset);

				case "crushBlock":
					return new SyncedKevin(data, offset);

				case "dashBlock":
					return new SyncedDashBlock(data, offset);

				case "colorSwitch":
					return new SyncedClutterSwitch(data, offset);

				case "bounceBlock":
					data.Values["coreMode"] = data.Bool("notCoreMode") ? "Cold" : "None";
					return new SyncedBounceBlock(data, offset);

				case "cloud":
					return new SyncedCloud(data, offset);

				case "triggerSpikesUp":
					data.Name = "corkr900CoopHelper/TriggerSpikesUp";
					goto case "%triggerSpikes";
				case "triggerSpikesDown":
					data.Name = "corkr900CoopHelper/TriggerSpikesDown";
					goto case "%triggerSpikes";
				case "triggerSpikesRight":
					data.Name = "corkr900CoopHelper/TriggerSpikesRight";
					goto case "%triggerSpikes";
				case "triggerSpikesLeft":
					data.Name = "corkr900CoopHelper/TriggerSpikesLeft";
					goto case "%triggerSpikes";
				case "%triggerSpikes":
					return new SyncedTriggerSpikes(data, offset);

				//case "seeker":
				//	level.Add(new SyncedSeeker(data, offset));
				//	return true;
			}
		}

		public static void OnLevelOrigLoadLevel(Action<Level, Player.IntroTypes, bool> orig, Level self, Player.IntroTypes intro, bool isFromLoader) {
			orig(self, intro, isFromLoader);
			if (intro != Player.IntroTypes.Transition) {
				Player pl = self.Tracker?.GetEntity<Player>();
				if (pl != null && Session?.SyncedKeys != null) {
					foreach (EntityID id in Session.SyncedKeys) {
						self.Add(new SyncedKey(pl, id));
					}
				}
			}
		}

		public static void OnStrawberryCollect(Action<Strawberry> orig, Strawberry self) {
			orig(self);
			if (!self.Golden) {
				Player player = self.SceneAs<Level>().Tracker.GetEntity<Player>();
				player?.Get<SessionSynchronizer>()?.StrawberryCollected(self.ID, self.Position);
			}
		}

		public static bool OnCelesteNetClientSettingsInteractionsGet(Func<CelesteNetClientSettings, bool> orig, CelesteNetClientSettings self) {
			if (Session?.ForceCNetInteractions != null) {
				return Session.ForceCNetInteractions ?? false;
			}
			return orig(self);
		}

		private void OnSpawn(Player pl) {
			if (pl.Get<SessionSynchronizer>() == null) {
				pl.Add(new SessionSynchronizer(pl, true, false));
			}
		}

		public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats) {
			if (Session?.IsInCoopSession ?? false) {
				// check whether we'll *actually* die first...
				Session session = self.level.Session;
				bool flag = !ifInvincible && global::Celeste.SaveData.Instance.Assists.Invincible;
				if (!self.Dead && !flag && self.StateMachine.State != Player.StReflectionFall) {
					// Cache off session data to restore after golden death
					bool hasGolden = self.Leader?.Followers?.Any((Follower f) => f.Entity is Strawberry strawb && strawb.Golden) ?? false;
					if (hasGolden) {
						Instance.CacheSession();
					}
					// Send sync info
					self.Get<SessionSynchronizer>()?.PlayerDied(hasGolden);
				}
			}
			// Now actually do the thing
			return orig(self, direction, ifInvincible, registerStats);
		}

		private void OnPlayerTransition(On.Celeste.Player.orig_OnTransition orig, Player self) {
			orig(self);
			Session s = self.SceneAs<Level>()?.Session;
			if (s == null) return;
			PlayerState.Mine.EnterRoom(s.Level, s.RespawnPoint ?? Vector2.Zero);
			PlayerState.Mine.SendUpdateImmediate();
		}

		private void OnLevelLoaderStart(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self) {
			EntityStateTracker.ClearBuffers();
			orig(self);
			TryRestoreCachedSession(self.session);
			PlayerState.Mine.EnterMap(self.Level.Session.Area, self.Level.Session.Level);
			PlayerState.Mine.UpdateRespawn(self.Level.Session.RespawnPoint ?? Vector2.Zero);
			PlayerState.Mine.SendUpdateImmediate();
		}

		private void onLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
			// If we're restarting, another update is close behind so don't bother updating
			if (mode != LevelExit.Mode.Restart) {
				PlayerState.Mine.EnterOverworld();
				PlayerState.Mine.SendUpdateImmediate();
				EntityStateTracker.ClearBuffers();
			}
		}

		private void OnLevelLoad(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
			orig(self, playerIntro, isFromLoader);
			if (isFromLoader && playerIntro != Player.IntroTypes.Transition) {
				PlayerState.Mine.UpdateRespawn(self.Session.RespawnPoint ?? Vector2.Zero);
				PlayerState.Mine.SendUpdateImmediate();
			}
		}

		private void OnChangeRespawnTriggerEnter(On.Celeste.ChangeRespawnTrigger.orig_OnEnter orig, ChangeRespawnTrigger self, Player player) {
			orig(self, player);
			Session s = player.SceneAs<Level>().Session;
			if (s.RespawnPoint != null && s.RespawnPoint != PlayerState.Mine.RespawnPoint) {
				PlayerState.Mine.UpdateRespawn(s.RespawnPoint.Value);
				PlayerState.Mine.SendUpdateImmediate();
			}
		}

		private bool OnFallingBlockPlayerCheck(On.Celeste.FallingBlock.orig_PlayerFallCheck orig, FallingBlock self) {
			bool res = orig(self);
			if (res && !self.Triggered && self is SyncedFallingBlock sfb) {
				sfb.Triggered = true;
				EntityStateTracker.PostUpdate(sfb);
			}
			return res;
		}

		private void OnPlatformStartShaking(On.Celeste.Platform.orig_StartShaking orig, Platform self, float time) {
			orig(self, time);
			if (self is SyncedFallingBlock sfb && !sfb.Triggered) {
				sfb.Triggered = true;
				EntityStateTracker.PostUpdate(sfb);
			}
		}

		private void OnCoreModeTogglePlayer(On.Celeste.CoreModeToggle.orig_OnPlayer orig, CoreModeToggle self, Player player) {
			if (self is SyncedCoreModeToggle synced) {
				Session.CoreModes before = self.SceneAs<Level>().CoreMode;
				orig(self, player);
				Session.CoreModes after = self.SceneAs<Level>().CoreMode;
				if (after != before) {
					EntityStateTracker.PostUpdate(synced);
				}
			}
			else orig(self, player);
		}

		private void OnKeyRegisterUsed(On.Celeste.Key.orig_RegisterUsed orig, Key self) {
			orig(self);
			if (self is SyncedKey key) {
				key.OnRegisterUsed();
			}
		}

		private IEnumerator OnLockBlockUnlockRoutine(On.Celeste.LockBlock.orig_UnlockRoutine orig, LockBlock self, Follower fol) {
			if (self is SyncedLockBlock slb) {
				yield return new SwapImmediately(slb.UnlockRoutineOverride(fol.EntityAs<Key>()));
			}
			else yield return new SwapImmediately(orig(self, fol));
		}

		private void OnDashBlockBreak(On.Celeste.DashBlock.orig_Break_Vector2_Vector2_bool_bool orig, DashBlock self, Vector2 from, Vector2 direction, bool playSound, bool playDebrisSound) {
			orig(self, from, direction, playSound, playDebrisSound);
			if (self is SyncedDashBlock sdb) {
				sdb.OnBreak();
			}
		}

		private void OnCasetteOnPlayer(On.Celeste.Cassette.orig_OnPlayer orig, Cassette self, Player player) {
			bool alreadyCollected = self.collected;
			orig(self, player);
			if (!alreadyCollected) {
				player.Get<SessionSynchronizer>()?.CassetteCollected();
			}
		}

		private void OnHeartCollected(On.Celeste.HeartGem.orig_RegisterAsCollected orig, HeartGem self, Level level, string poem) {
			orig(self, level, poem);
			AreaKey area = level.Session.Area;
			bool isCompleteArea = self.IsCompleteArea(area.Mode != 0 || area.ID == 9);
			string poemID = AreaData.Get(level).Mode[(int)area.Mode].PoemID;
			level.Tracker.GetEntity<Player>()?.Get<SessionSynchronizer>()?.HeartCollected(isCompleteArea, poemID);
		}

		private void OnTempleCrackedBlockBreak(On.Celeste.TempleCrackedBlock.orig_Break orig, TempleCrackedBlock self, Vector2 from) {
			orig(self, from);
			if (self is SyncedTempleCrackedBlock synced) {
				synced.OnBreak(from);
			}
		}

		private void OnClutterAbsorbEffectAdded(On.Celeste.ClutterAbsorbEffect.orig_Added orig, ClutterAbsorbEffect self, Scene scene) {
			orig(self, scene);
			// I don't want to forever answer questions about why its crashing
			// So I'm doing this to prevent a crash when there are no
			// cabinets in the room you're in when clutter is cleared
			Level level = scene as Level;
			if (level != null && level.Tracker.GetEntity<ClutterCabinet>() == null) {
				List<ClutterCabinet> cabinets = self.cabinets;

				ClutterCabinet cab = new ClutterCabinet(new Vector2(level.Bounds.Left - 32, level.Bounds.Top - 32));
				level.Add(cab);
				cabinets.Add(cab);

				cab = new ClutterCabinet(new Vector2(level.Bounds.Left - 32, level.Bounds.Bottom + 32));
				level.Add(cab);
				cabinets.Add(cab);

				cab = new ClutterCabinet(new Vector2(level.Bounds.Right + 32, level.Bounds.Top - 32));
				level.Add(cab);
				cabinets.Add(cab);

				cab = new ClutterCabinet(new Vector2(level.Bounds.Right + 32, level.Bounds.Bottom + 32));
				level.Add(cab);
				cabinets.Add(cab);
			}
		}

		private void OnSpringCtor(On.Celeste.Spring.orig_ctor_EntityData_Vector2_Orientations orig, Spring self, EntityData data, Vector2 offset, Spring.Orientations orientation) {
			orig(self, data, offset, orientation);
			self.Add(new SyncedPufferCollider((SyncedPuffer p) => {
				if (p.HitSpring(self)) {
					self.BounceAnimate();
				}
			}));
		}

		private IEnumerator OnAscendManagerRoutine(On.Celeste.AscendManager.orig_Routine orig, AscendManager self) {
			if (self is SyncedSummitBackgroundManager synced) {
				yield return new SwapImmediately(synced.RoutineOverride());
			}
			else {
				yield return new SwapImmediately(orig(self));
			}
		}

		private bool OnMoveBlockMoveCheck(On.Celeste.MoveBlock.orig_MoveCheck orig, MoveBlock self, Vector2 speed) {
			bool result = orig(self, speed);
			if (self is SyncedMoveBlock smb) {
				smb.LastMoveCheckResult = result;
			}
			return result;
		}

		private void OnBoosterPlayerReleased(On.Celeste.Booster.orig_PlayerReleased orig, Booster self) {
			orig(self);
			if (self is SyncedBooster sb) {
				sb.OnPlayerReleased();
			}
		}

		private void OnBoosterPlayerBoosted(On.Celeste.Booster.orig_PlayerBoosted orig, Booster self, Player player, Vector2 direction) {
			orig(self, player, direction);
			if (self is SyncedBooster sb) {
				sb.OnPlayerBoosted();
			}
		}

		public delegate bool orig_SpikeInfoOnPlayer(ref TriggerSpikes.SpikeInfo self, Player player, Vector2 outwards);
		public static bool OnSpikeInfoOnPlayer(orig_SpikeInfoOnPlayer orig, ref TriggerSpikes.SpikeInfo self, Player player, Vector2 outwards) {
			bool ret = orig(ref self, player, outwards);

			if (self.Parent is SyncedTriggerSpikes sts) {
				sts.OnTriggered();
			}
			return ret;
		}

		private void OnReloadLevel(Level level)
        {
			MapSync.TryDoSync(level);
        }

        #endregion
    }
}
