using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/GroupButton")]
	public class GroupButton : Entity, ISynchronizable {
		private EntityID id;
		private string direction = "Right";
		private GroupButtonDetector trigger;
		private bool complete;
		private Sprite button;
		private Dictionary<PlayerID, Sprite> indicators = new Dictionary<PlayerID, Sprite>();
		private string flagToSet;
		private Player player;
		private List<PlayerID> otherPlayerStanding = new List<PlayerID>();

		public GroupButton(EntityData data, Vector2 offset) : base(data.Position + offset) {
			id = new EntityID(data.Level.Name, data.ID);
			direction = data.Attr("direction", "Right");
			flagToSet = data.Attr("flag", "");
			trigger = new GroupButtonDetector(new EntityData() {
				Width = 28,
				Height = 6,
				Position = data.Position + new Vector2(-14, -6),
			}, offset);
			trigger.OnPlayerStand = (Player p) => {
				player = p;
				CheckComplete();
				EntityStateTracker.PostUpdate(this);
				UpdateSprites();
			};
			trigger.OnPlayerLeave = () => {
				player = null;
				EntityStateTracker.PostUpdate(this);
				UpdateSprites();
			};
			Add(button = GFX.SpriteBank.Create("corkr900_CoopHelper_GroupSwitch"));
			button.Position = new Vector2(-16f, -8f);
			Depth = (Depths.FGTerrain + Depths.FGDecals) / 2;
		}

		private void UpdateSprites() {
			button.Play(player == null ? "idle" : "pressed");
			if (CoopHelperModule.Session?.SessionMembers == null) {
				foreach (KeyValuePair<PlayerID, Sprite> kvp in indicators) {
					kvp.Value.Play("off");
				}
			}
			else {
				foreach (PlayerID p in CoopHelperModule.Session?.SessionMembers) {
					bool isOn = complete || (p.Equals(PlayerID.MyID) ? player != null : otherPlayerStanding.Contains(p));
					if (indicators.ContainsKey(p)) {
						indicators[p]?.Play(isOn ? "on" : "off");
					}
				}
			}
		}

		private void CheckComplete() {
			if (complete) return;
			if (player == null) return;
			if (CoopHelperModule.Session?.IsInCoopSession == true
				&& CoopHelperModule.Session?.SessionMembers != null
				&& (otherPlayerStanding.Count >= CoopHelperModule.Session.SessionMembers.Count - 1
					|| SceneAs<Level>()?.Session?.GetFlag("CoopHelper_Debug") == true)) {
				MarkComplete();
			}
		}

		private void MarkComplete() {
			Session sess = SceneAs<Level>()?.Session;
			if (sess == null) return;
			complete = true;
			sess.SetFlag(flagToSet);
			Vector2 newPt = SceneAs<Level>().GetSpawnPoint(Position);
			if (!sess.RespawnPoint.HasValue || sess.RespawnPoint.Value != newPt) {
				sess.HitCheckpoint = true;
				sess.RespawnPoint = newPt;
				sess.UpdateLevelStartDashes();
			}
		}

		public override void Added(Scene scene) {
			base.Added(scene);

			// Indicators
			int count = CoopHelperModule.Session?.SessionMembers?.Count ?? 0;
			for (int i = 0; i < count; i++) {
				PlayerID playerID = CoopHelperModule.Session.SessionMembers[i];
				if (!indicators.ContainsKey(playerID)) {
					Sprite indicator = GFX.SpriteBank.Create("corkr900_CoopHelper_GroupSwitchIndicator");
					indicator.Position = new Vector2(8f * i - 4f * count, 0f);
					Add(indicator);
					indicators.Add(playerID, indicator);
				}
			}

			// Detection trigger
			scene.Add(trigger);

			// Misc
			EntityStateTracker.AddListener(this, false);
			if ((scene as Level)?.Session?.GetFlag(flagToSet) == true) {
				complete = true;
				UpdateSprites();
			}
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			trigger.RemoveSelf();
			EntityStateTracker.RemoveListener(this);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 14,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = true,
			DiscardDuplicates = false,
			Critical = false,
		};

		public static GroupButtonState ParseState(CelesteNetBinaryReader r) {
			GroupButtonState s = new GroupButtonState();
			s.playerIsStanding = r.ReadBoolean();
			s.player = r.ReadPlayerID();
			s.complete = r.ReadBoolean();
			return s;
		}

		public void ApplyState(object state) {
			if (state is GroupButtonState st && !complete) {
				if (st.complete) {
					MarkComplete();
				}
				else if (st.playerIsStanding) {
					otherPlayerStanding.Remove(st.player);
				}
				else if (!otherPlayerStanding.Contains(st.player)) {
					otherPlayerStanding.Add(st.player);
					CheckComplete();
				}
			}
			UpdateSprites();
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(player == null);
			w.Write(PlayerID.MyID);
			w.Write(complete);
		}

		public bool CheckRecurringUpdate() => false;
	}

	public class GroupButtonState {
		public bool playerIsStanding;
		public PlayerID player;
		public bool complete;
	}

	public class GroupButtonDetector : Trigger {
		private Player player;
		private bool playerIsStanding;
		public Action<Player> OnPlayerStand;
		public Action OnPlayerLeave;

		public GroupButtonDetector(EntityData data, Vector2 offset) : base(data, offset) {
			playerIsStanding = false;
		}

		public override void OnEnter(Player player) {
			base.OnEnter(player);
			this.player = player;
		}

		public override void OnLeave(Player player) {
			base.OnLeave(player);
			this.player = null;
		}

		public override void Update() {
			base.Update();
			bool prev = playerIsStanding;
			playerIsStanding = player != null && player.OnGround();
			if (prev != playerIsStanding) {
				if (playerIsStanding) {
					OnPlayerStand?.Invoke(player);
				}
				else {
					OnPlayerLeave?.Invoke();
				}
			}
		}
	}
}
