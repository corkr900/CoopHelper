using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CoopHelper.IO;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public class CoopGameComponent : CelesteNetGameComponent {
		public CoopGameComponent(CelesteNetClientContext context, Game game)
			: base (context, game) {

		}

		public override void Tick() {
			CNetComm.Instance?.Tick();
		}
	}
}
