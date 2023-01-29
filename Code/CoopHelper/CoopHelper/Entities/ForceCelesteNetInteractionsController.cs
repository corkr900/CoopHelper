using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/ForceInteractionsController")]
	public class ForceCelesteNetInteractionsController : Entity {
		bool forceTo;
		public ForceCelesteNetInteractionsController(EntityData data, Vector2 offset) {
			forceTo = data.Bool("forceSettingTo", false);
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			CoopHelperModule.Session.ForceCNetInteractions = forceTo;
			RemoveSelf();
		}
	}
}
