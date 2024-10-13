using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.IO {
	public class SkinModHelper {

		private static MethodInfo m_UpdateSkin;

		public static bool IsAvailable => m_UpdateSkin != null;

		static SkinModHelper() {
			Type t_SkinModHelperModule = Type.GetType("SkinModHelper.Module.SkinModHelperModule,SkinModHelper");
			m_UpdateSkin = t_SkinModHelperModule?.GetMethod("UpdateSkin");
		}

		public static void ApplySkin(string skinID) {
			if (!IsAvailable) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "Could not change skin with SkinModHelper: SkinModHelper is not available.");
				return;
			}
			try {
				m_UpdateSkin?.Invoke(null, new object[] { skinID });
			}
			catch (Exception) {
				Logger.Log(LogLevel.Error, "Co-op Helper", "Could not change skin: skin \"" + skinID + "\" is not defined.");
				m_UpdateSkin?.Invoke(null, new object[] { "Default" });
			}
		}
	}
}
