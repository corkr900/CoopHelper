using Celeste.Mod.CoopHelper.Data;
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Module;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure
{
    public class MapSync
    {

        public static void TryDoSync(Level level)
        {
            if (CoopHelperModule.Settings.MapSync != CoopHelperModuleSettings.MapSyncMode.Send) return;
            if (CNetComm.Instance?.HasPendingOutgoingChunks ?? true)
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", "Map sync skipped due to pending outgoing chunks.");
                return;
            }

            string assetPathVirtual = $"Maps/{level.Session.MapData.Filename}";
            var asset = Util.FindMapAsset(assetPathVirtual);
            if (asset != null)
            {
                Logger.Log(LogLevel.Debug, "Co-op Helper", $"Syncing map at virtual path '{assetPathVirtual} (size {asset.Data.Length})'");
                CNetComm.Instance?.Send(new DataMapSync()
                {
                    VirtualPath = assetPathVirtual,
                    MapBinary = asset.Data,
                }, false);
            }
        }

        public static void OnReceiveMapSync(DataMapSync data)
        {
            if (CoopHelperModule.Settings.MapSync != CoopHelperModuleSettings.MapSyncMode.Receive)
            {
                Logger.Log(LogLevel.Debug, "Co-op Helper", "Received map sync but map sync receiving is disabled in settings.");
                return;
            }
            if (data.MapBinary?.Length > 0)
            {
                ApplyMapSync(data.VirtualPath, data.MapBinary);
            }
        }

        public static void ApplyMapSync(string virtualPath, byte[] fileData)
        {
            var asset = Util.FindMapAsset(virtualPath);
            if (asset == null)
            {
                Logger.Log(LogLevel.Info, "Co-op Helper", $"Received map sync for unknown map at virtual path '{virtualPath}'");
                return;
            }
            string fullFilePath = Util.GetFullFilePath(asset);
            try
            {
                File.WriteAllBytes(fullFilePath, fileData);
                Logger.Log(LogLevel.Info, "Co-op Helper", $"Applied map sync for map at virtual path '{virtualPath}' to local path '{fullFilePath}'");
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, "Co-op Helper", $"Failed to apply map sync for map at virtual path '{virtualPath}' to local path '{fullFilePath}': {e}");
            }
        }

    }
}
