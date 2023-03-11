using Celeste;
using Celeste.Mod.CoopHelper.Module;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SessionPicker")]
	public class SessionPickerEntity : Entity {
		private Sprite sprite;
		private int PlayersNeeded = 2;
		private bool removeIfSessionExists;
		private SessionPickerHUD hud;
		private Player player;
		private TalkComponent talkComponent;
		private string[] forceSkin = null;

		public SessionPickerEntity(EntityData data, Vector2 offset) : base(data.Position + offset) {
			Position = data.Position + offset;
			removeIfSessionExists = data.Bool("removeIfSessionExists", true);
			Add(sprite = GFX.SpriteBank.Create("corkr900_CoopHelper_SessionPicker"));
			sprite.Play("idle");
			sprite.Position = new Vector2(-8, -16);
			Add(talkComponent = new TalkComponent(
				new Rectangle(-16, -16, 32, 32),
				new Vector2(0, -16),
				Open
			) { PlayerMustBeFacing = false });
			string skinArg = data.Attr("skins", null);
			if (!string.IsNullOrEmpty(skinArg)) {
				forceSkin = skinArg.Split(',');
				for (int i = 0; i < forceSkin.Length; i++) {
					forceSkin[i] = forceSkin[i].Trim();
				}
			}
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			if (!CheckRemove()) {
				CoopHelperModule.OnSessionInfoChanged += SessionInfoChanged;
			}
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			CoopHelperModule.OnSessionInfoChanged -= SessionInfoChanged;
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			CoopHelperModule.OnSessionInfoChanged -= SessionInfoChanged;
		}

		private void SessionInfoChanged() {
			CheckRemove();
		}

		private bool CheckRemove() {
			if (removeIfSessionExists && CoopHelperModule.Session?.IsInCoopSession == true) {
				if (talkComponent != null) {
					talkComponent.Enabled = false;
				}
				if (hud != null) {
					hud.CloseSelf();
					hud = null;
				}
				RemoveSelf();
				return true;
			}
			return false;
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
				if (forceSkin != null && forceSkin.Length > 0) {
					string newSkin = forceSkin[role % forceSkin.Length];
					// TODO replace this with a ModInterop call when it's available
					Type t_SkinModHelperModule = Type.GetType("SkinModHelper.Module.SkinModHelperModule,SkinModHelper");
					if (t_SkinModHelperModule != null) {
						MethodInfo m_UpdateSkin = t_SkinModHelperModule?.GetMethod("UpdateSkin");
						try {
							m_UpdateSkin?.Invoke(null, new object[] { newSkin });
						}
						catch(Exception e) {
							Logger.Log(LogLevel.Error, "Co-op Helper + SkinModHelper", "Could not change skin: skin \"" + newSkin + "\" is not defined.");
							m_UpdateSkin?.Invoke(null, new object[] { "Default" });
						}
					}
					else {
						Logger.Log(LogLevel.Info, "Co-op Helper + SkinModHelper", "Could not change skin: SkinModHelper is not installed.");
					}
				}
			}
			else {
				currentSession.SetFlag("CoopHelper_InSession", false);
				for (int i = 0; i < PlayersNeeded; i++) {
					currentSession.SetFlag("CoopHelper_SessionRole_" + i, false);
				}
			}
			CheckRemove();
		}
	}
}
