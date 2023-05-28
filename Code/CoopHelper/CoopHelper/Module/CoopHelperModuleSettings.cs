using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Module {
	public class CoopHelperModuleSettings : EverestModuleSettings {
		public enum TimeServer {
			Windows,
			Pool,
			None,
		}

		[SettingName("corkr900_CoopHelper_Setting_TimeServer")]
		//[SettingSubText("corkr900_CoopHelper_Setting_TimeServer_Subtext")]
		public TimeServer NSTPTimeServer { get; set; } = TimeServer.Windows;

		[SettingName("corkr900_CoopHelper_Setting_DisplayName")]
		//[SettingSubText("corkr900_CoopHelper_Setting_DisplayName_Subtext")]
		[SettingInGame(false)]
		[SettingMinLength(0)]
		[SettingMaxLength(20)]
		public string DisplayName { get; set; } = "";

		[SettingName("corkr900_CoopHelper_Setting_CoopEverywhere")]
		public bool CoopEverywhere { get; internal set; }
	}
}
