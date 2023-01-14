using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
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

		public static readonly string ProtocolVersion = "0_0_1";

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

		private static IDetour hook_Strawberry_orig_OnCollect;

		private static IDetour hook_CrushBlock_AttackSequence;

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
				typeof(CoopHelperModule).GetMethod("OnStrawberryCollect"));

			// IL Hooks
			MethodInfo m = typeof(CrushBlock).GetMethod("AttackSequence", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget();
			hook_CrushBlock_AttackSequence = new ILHook(m, (il) => ILKevinCollide(m.DeclaringType.GetField("<>4__this"), il));

			On.Celeste.Key.RegisterUsed += OnKeyRegisterUsed;
			On.Celeste.Level.LoadLevel += OnLevelLoad;
			On.Celeste.Player.OnTransition += OnPlayerTransition;
			On.Celeste.Cassette.OnPlayer += OnCasetteOnPlayer;
			On.Celeste.DashBlock.Break_Vector2_Vector2_bool_bool += OnDashBlockBreak;
			On.Celeste.LockBlock.UnlockRoutine += OnLockBlockUnlockRoutine;
			On.Celeste.FallingBlock.PlayerFallCheck += OnFallingBlockPlayerCheck;
			On.Celeste.CoreModeToggle.OnPlayer += OnCoreModeTogglePlayer;
			On.Celeste.TempleCrackedBlock.Break += OnTempleCrackedBlockBreak;
			On.Celeste.ChangeRespawnTrigger.OnEnter += OnChangeRespawnTriggerEnter;

			Everest.Events.Player.OnSpawn += OnSpawn;
			Everest.Events.Player.OnDie += OnDie;
			Everest.Events.Level.OnExit += onLevelExit;
			Everest.Events.Level.OnEnter += OnLevelEnter;
		}

		public override void Unload() {
			Celeste.Instance.Components.Remove(Comm);
			Comm = null;

			// Manual Hooks
			hook_Strawberry_orig_OnCollect?.Dispose();
			hook_Strawberry_orig_OnCollect = null;

			// IL Hooks
			hook_CrushBlock_AttackSequence?.Dispose();
			hook_CrushBlock_AttackSequence = null;

			On.Celeste.Key.RegisterUsed -= OnKeyRegisterUsed;
			On.Celeste.Level.LoadLevel -= OnLevelLoad;
			On.Celeste.Player.OnTransition -= OnPlayerTransition;
			On.Celeste.Cassette.OnPlayer -= OnCasetteOnPlayer;
			On.Celeste.DashBlock.Break_Vector2_Vector2_bool_bool -= OnDashBlockBreak;
			On.Celeste.LockBlock.UnlockRoutine -= OnLockBlockUnlockRoutine;
			On.Celeste.FallingBlock.PlayerFallCheck -= OnFallingBlockPlayerCheck;
			On.Celeste.CoreModeToggle.OnPlayer -= OnCoreModeTogglePlayer;
			On.Celeste.TempleCrackedBlock.Break -= OnTempleCrackedBlockBreak;
			On.Celeste.ChangeRespawnTrigger.OnEnter -= OnChangeRespawnTriggerEnter;

			Everest.Events.Player.OnSpawn -= OnSpawn;
			Everest.Events.Player.OnDie -= OnDie;
			Everest.Events.Level.OnExit -= onLevelExit;
			Everest.Events.Level.OnEnter -= OnLevelEnter;
		}

		#endregion

		#region Session Information

		public delegate void OnSessionInfoChangedHandler();
		public static event OnSessionInfoChangedHandler OnSessionInfoChanged;

		public static void NotifySessionChanged() {
			OnSessionInfoChanged?.Invoke();
		}

		public void ChangeSessionInfo(CoopSessionID id, PlayerID[] players) {
			if (Session == null) return;

			int myRole = -1;
			for (int i = 0; i < players.Length; i++) {
				if (players[i].Equals(PlayerID.MyID)) {
					myRole = i;
				}
			}
			if (myRole < 0) return;  // I'm not in this one

			Session.IsInCoopSession = true;
			Session.SessionID = id;
			Session.SessionRole = myRole;
			Session.SessionMembers = new List<PlayerID>(players);

			NotifySessionChanged();
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
			//if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/game/06_reflection/crushblock_impact")))
			//{
			//	cursor.Emit(OpCodes.Ldarg_0);  // load state machine instance
			//	cursor.Emit(OpCodes.Ldfld, crushBlockLdfldValue);  // load coroutine object instance
			//	cursor.EmitDelegate<Action<CrushBlock>>((CrushBlock blk) => {
			//		if (blk is SyncedKevin kevin) {
			//			kevin.OnReturnBegin();
			//		}
			//	});
			//}
		}

		#endregion

		#region Hooked Code + Event Handlers

		public static void OnStrawberryCollect(Action<Strawberry> orig, Strawberry self) {
			orig(self);
			Player player = self.SceneAs<Level>().Tracker.GetEntity<Player>();
			player?.Get<SessionSynchronizer>()?.StrawberryCollected(self.ID);
		}

		private void OnSpawn(Player pl) {
			if (pl.Get<SessionSynchronizer>() == null) {
				pl.Add(new SessionSynchronizer(pl, true, false));
			}
		}

		private void OnDie(Player pl) {
			pl.Get<SessionSynchronizer>()?.PlayerDied();
		}

		private void OnPlayerTransition(On.Celeste.Player.orig_OnTransition orig, Player self) {
			orig(self);
			Session s = self.SceneAs<Level>()?.Session;
			if (s == null) return;
			PlayerState.Mine.CurrentRoom = s.Level;
			PlayerState.Mine.RespawnPoint = s.RespawnPoint ?? Vector2.Zero;
			PlayerState.Mine.SendUpdateImmediate();
		}

		private void OnLevelEnter(Session session, bool fromSaveData) {
			EntityStateTracker.ClearBuffers();
		}

		private void onLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
			// If we're restarting, another update is close behind so don't bother updating
			if (mode != LevelExit.Mode.Restart) {
				PlayerState.Mine.CurrentMap = GlobalAreaKey.Overworld;
				PlayerState.Mine.CurrentRoom = "";
				PlayerState.Mine.RespawnPoint = Vector2.Zero;
				PlayerState.Mine.SendUpdateImmediate();
			}
			EntityStateTracker.ClearBuffers();
		}

		private void OnLevelLoad(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
			orig(self, playerIntro, isFromLoader);
			if (isFromLoader && playerIntro != Player.IntroTypes.Transition) {
				PlayerState.Mine.CurrentMap = new GlobalAreaKey(self.Session.Area);
				PlayerState.Mine.CurrentRoom = self.Session.Level;
				PlayerState.Mine.RespawnPoint = self.Session.RespawnPoint ?? Vector2.Zero;
				PlayerState.Mine.SendUpdateImmediate();
			}
		}

		private void OnChangeRespawnTriggerEnter(On.Celeste.ChangeRespawnTrigger.orig_OnEnter orig, ChangeRespawnTrigger self, Player player) {
			orig(self, player);
			Session s = player.SceneAs<Level>().Session;
			if (s.RespawnPoint != null && s.RespawnPoint != PlayerState.Mine.RespawnPoint) {
				PlayerState.Mine.RespawnPoint = s.RespawnPoint.Value;
				PlayerState.Mine.SendUpdateImmediate();
			}
		}

		private bool OnFallingBlockPlayerCheck(On.Celeste.FallingBlock.orig_PlayerFallCheck orig, FallingBlock self) {
			bool res = orig(self);
			if (res && !self.Triggered && self is SyncedFallingBlock sfb) {
				self.Triggered = true;
				EntityStateTracker.PostUpdate(sfb);
			}
			return res;
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
			bool alreadyCollected = new DynamicData(self).Get<bool>("collected");
			orig(self, player);
			if (!alreadyCollected) {
				player.Get<SessionSynchronizer>()?.CassetteCollected();
			}
		}

		private void OnTempleCrackedBlockBreak(On.Celeste.TempleCrackedBlock.orig_Break orig, TempleCrackedBlock self, Vector2 from) {
			orig(self, from);
			if (self is SyncedTempleCrackedBlock synced) {
				synced.OnBreak(from);
			}
		}

		#endregion
	}
}
