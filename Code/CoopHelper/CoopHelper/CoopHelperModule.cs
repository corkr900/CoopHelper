using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper {
	public class CoopHelperModule : EverestModule {

		public static readonly string ProtocolVersion = "0_0_1";

		#region Setup and Static Stuff

		public static CoopHelperModule Instance { get; private set; }
		public static string AssemblyVersion {
			get {
				if (string.IsNullOrEmpty(_version)) {
					System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
					System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
					_version = fvi.FileVersion;
				}
				return _version;
			}
		}
		private static string _version = null;

		public override Type SettingsType => typeof(CoopHelperModuleSettings);
		public static CoopHelperModuleSettings Settings => (CoopHelperModuleSettings)Instance._Settings;
		public override Type SaveDataType => typeof(CoopHelperModuleSaveData);
		public static CoopHelperModuleSaveData SaveData => (CoopHelperModuleSaveData)Instance._SaveData;
		public override Type SessionType => typeof(CoopHelperModuleSession);
		public static CoopHelperModuleSession Session => (CoopHelperModuleSession)Instance._Session;
		public CoopHelperModule() {
			Instance = this;
		}

		#endregion

		#region Startup

		public override void Load() {

		}

		public override void Unload() {

		}

		#endregion
	}

	public class CoopHelperModuleSettings : EverestModuleSettings {

	}

	public class CoopHelperModuleSaveData : EverestModuleSaveData {

	}

	public class CoopHelperModuleSession : EverestModuleSession {

	}
}
