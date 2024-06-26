﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.CoopHelper.Module {
	public class CoopHelperModuleSettings : EverestModuleSettings {
		public enum TimeServer {
			Windows,
			Pool,
			None,
		}

		[YamlIgnore]
		[SettingName("corkr900_CoopHelper_Setting_CoopEverywhere")]
		public bool CoopEverywhere { get; internal set; }
	}
}
