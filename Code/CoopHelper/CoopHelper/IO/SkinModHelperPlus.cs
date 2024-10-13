using Monocle;
using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	[ModImportName("SkinModHelperPlus")]
	public class SkinModHelperPlus {

		public static bool IsAvailable => SessionSet_GeneralSkin != null;

		public static Action<string> SessionSet_PlayerSkin;
		public static Action<string> SessionSet_SilhouetteSkin;
		public static Action<string, bool?> SessionSet_GeneralSkin;
		public static Action<Sprite, Sprite, bool> CopyColorGrades;
	}
}
