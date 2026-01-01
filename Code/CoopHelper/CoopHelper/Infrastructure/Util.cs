using Celeste.Mod.CoopHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure {
	public static class Util {

		internal static string DLL { get { return CleanDLL(CoopHelperModule.Instance.Metadata); } }
		internal static string CleanDLL(EverestModuleMetadata meta) {
			string ret;
			if (string.IsNullOrEmpty(meta.DLL)) ret = meta.DLL;
			else if (string.IsNullOrEmpty(meta.PathDirectory)) ret = meta.DLL;
			else if (meta.PathDirectory.Length + 1 >= meta.DLL.Length) ret = meta.DLL;  // Probably impossible. But probably is not a promise.
			else ret = meta.DLL.Substring(meta.PathDirectory.Length + 1);
			return ret?.Replace('\\', '/');
		}

		internal static MapData GetMapDataForMode(GlobalAreaKey area) {
			if (!area.ExistsLocal) return null;
			return area.Data.Mode[(int)area.Mode].MapData;
		}

		internal static LevelSetStats GetSetStats(string levelSet) {
			if (string.IsNullOrEmpty(levelSet)) return null;
			return SaveData.Instance.GetLevelSetStatsFor(levelSet);
		}

		internal static ModContent GetModContent(GlobalAreaKey area) {
			string path = string.Format("Maps/{0}", area.Data.Mode[(int)area.Mode].Path);
			foreach (ModContent content in Everest.Content.Mods) {
				if (content.Map.ContainsKey(path)) return content;
			}
			return null;
		}

		internal static double TimeToSeconds(long ticks) {
			TimeSpan timeSpan = TimeSpan.FromTicks(ticks);
			return timeSpan.TotalSeconds;
		}

        internal static FileSystemModAsset FindMapAsset(string pathVirtual)
        {
            foreach (var mod in Everest.Content.Mods)
            {
                foreach (ModAsset asset in mod.List.Where(IsFileMapAsset))
                {
                    if (asset.PathVirtual == pathVirtual)
                    {
                        return asset as FileSystemModAsset;
                    }
                }
            }
            return null;
        }

        internal static bool IsFileMapAsset(ModAsset ma) =>
            ma is FileSystemModAsset &&
            ma.Type?.Equals(typeof(AssetTypeMap)) == true;

		internal static string GetFullFilePath(FileSystemModAsset asset) => Path.Combine(asset.Source.Path, asset.Path);
    }
}
