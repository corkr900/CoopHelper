using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	[ModImportName("SkinModHelperPlus")]
	public class SkinModHelperPlus {
		public static Func<string, bool> SetSessionSkin;
	}
}
