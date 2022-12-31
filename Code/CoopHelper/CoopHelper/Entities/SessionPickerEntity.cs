﻿using Celeste;
using Celeste.Mod.CoopHelper.Module;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SessionPicker")]
	public class SessionPickerEntity : Entity {
		private Sprite sprite;
		private int PlayersNeeded = 2;

		private SessionPickerHUD hud;
		private Player player;


		public SessionPickerEntity(EntityData data, Vector2 offset) : base(data.Position + offset) {
			Position = data.Position + offset;
			Add(sprite = GFX.SpriteBank.Create("corkr900_CoopHelper_SessionPicker"));
			sprite.Play("idle");
			sprite.Position = new Vector2(-8, -16);
			Add(new TalkComponent(
				new Rectangle(-16, -16, 32, 32),
				new Vector2(0, -16),
				Open
			) { PlayerMustBeFacing = false });

		}

		public void Open(Player player) {
			if (hud != null) return;  // Already open
			hud = new SessionPickerHUD(PlayersNeeded, Close);
			Scene.Add(hud);
			player.StateMachine.State = Player.StDummy;
			this.player = player;
			Audio.Play("event:/ui/game/pause");
		}

		public void Close(SessionPickerHUDCloseArgs args) {
			if (hud == null) return;  // Already closed
			player.StateMachine.State = Player.StNormal;
			Scene.Remove(hud);
			hud = null;
			Audio.Play("event:/ui/game/unpause");
			CoopHelperModuleSession coopSes = CoopHelperModule.Session;
			Session currentSession = SceneAs<Level>()?.Session;
			if (coopSes?.IsInCoopSession == true && currentSession != null) {
				int role = coopSes.SessionRole;
				currentSession.SetFlag("CoopHelper_InSession", true);
				for (int i = 0; i < PlayersNeeded; i++) {
					currentSession.SetFlag("CoopHelper_SessionRole_" + i, i == role);
				}
			}
			else {
				currentSession.SetFlag("CoopHelper_InSession", false);
				for (int i = 0; i < PlayersNeeded; i++) {
					currentSession.SetFlag("CoopHelper_SessionRole_" + i, false);
				}
			}
		}
	}
}
