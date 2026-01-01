using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.CoopHelper.Module {
	public class CoopHelperModuleSettings : EverestModuleSettings {

		public enum TimeServer {
			Windows = 0,
			Pool = 1,
			None = 2,
		}

		public enum MapSyncMode
		{
			Off = 0,
			Send = 1,
			Receive = 2,
		}

        [YamlIgnore]
		[SettingName("corkr900_CoopHelper_Setting_CoopEverywhere")]
		public bool CoopEverywhere { get; internal set; }

		[YamlIgnore]
        [SettingName("corkr900_CoopHelper_Setting_MapSync")]
		[SettingSubText("corkr900_CoopHelper_Setting_MapSync_Subtext")]
        public MapSyncMode MapSync { get; internal set; } = MapSyncMode.Off;
    }
}
