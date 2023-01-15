﻿using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Triggers {
	[CustomEntity("corkr900CoopHelper/SyncedDialogCutscene")]
	public class SyncedDialogCutsceneTrigger : Trigger, ISynchronizable {
		private string dialogEntry;
		private bool triggered;
		private EntityID id;
		private bool onlyOnce;
		private bool endLevel;
		private int deathCount;

		private int otherPlayersInTrigger = 0;
		Player player = null;
		WaitingForPlayersMessage message = null;

		private int PlayersNeeded {
			get {
				return (CoopHelperModule.Session?.IsInCoopSession == true) ?
					CoopHelperModule.Session.SessionMembers?.Count ?? 1 : 1;
			}
		}

		public SyncedDialogCutsceneTrigger(EntityData data, Vector2 offset, EntityID entId)
			: base(data, offset)
		{
			dialogEntry = data.Attr("dialogId");
			onlyOnce = data.Bool("onlyOnce", defaultValue: true);
			endLevel = data.Bool("endLevel");
			deathCount = data.Int("deathCount", -1);
			triggered = false;
			id = entId;
		}

		public override void OnEnter(Player player) {
			Session session = (Scene as Level).Session;
			if (!session.GetFlag("DoNotLoad" + id.ToString()) && (deathCount < 0 || SceneAs<Level>().Session.DeathsInCurrentLevel == deathCount)) {
				this.player = player;
				player.StateMachine.State = Player.StDummy;
				EntityStateTracker.PostUpdate(this);
				if (otherPlayersInTrigger + 1 >= PlayersNeeded) {
					BeginCutscene();
				}
				else {
					Scene.Add(message = new WaitingForPlayersMessage());
				}
			}
		}

		private void BeginCutscene() {
			if (triggered || player == null) {
				return;
			}
			triggered = true;
			message?.RemoveSelf();
			message = null;
			Scene.Add(new DialogCutscene(dialogEntry, player, endLevel));
			if (onlyOnce) {
				Session session2 = (Scene as Level).Session;
				session2.SetFlag("DoNotLoad" + id.ToString());
			}
			if (onlyOnce) {
				RemoveSelf();
			}
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			EntityStateTracker.AddListener(this);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public static int GetHeader() => 20;

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public static bool ParseState(CelesteNetBinaryReader r) {
			return r.ReadBoolean();
		}

		public void ApplyState(object state) {
			if (state is bool playerEntered && playerEntered) {
				otherPlayersInTrigger++;
				if (player != null && otherPlayersInTrigger + 1 >= PlayersNeeded) {
					BeginCutscene();
				}
			}
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(player != null);
		}

	}

	public class WaitingForPlayersMessage : Entity {
		public WaitingForPlayersMessage() {
			Tag = Tags.HUD;
		}
		public override void Render() {
			base.Render();
			string text = "Waiting for all players...";  // TODO tokenize
			Vector2 position = new Vector2(960, 540);
			Vector2 anchor = Vector2.One / 2f;
			ActiveFont.DrawOutline(text, position, anchor, Vector2.One, Color.White, 3, Color.Black);
		}
	}
}
