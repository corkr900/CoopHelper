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
        internal static void Load()
        {
            CNetComm.OnReceiveMapSync += OnReceiveMapSync;
        }

        internal static void Unload()
        {
            CNetComm.OnReceiveMapSync -= OnReceiveMapSync;
        }

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
                Logger.Log(LogLevel.Info, "Co-op Helper", $"Syncing map at virtual path '{assetPathVirtual} (size {asset.Data.Length})'");
                CNetComm.Instance?.Send(new DataMapSync()
                {
                    VirtualPath = assetPathVirtual,
                    MapBinary = asset.Data,
                }, false);
            }
        }

        public static void OnReceiveMapSync(DataMapSync data)
        {
            Logger.Log(LogLevel.Info, "Co-op Helper", $"Received map sync...");
            if (CoopHelperModule.Settings.MapSync != CoopHelperModuleSettings.MapSyncMode.Receive)
            {
                Logger.Log(LogLevel.Info, "Co-op Helper", "Received map sync but map sync receiving is disabled in settings.");
                return;
            }
            if (data.MapBinary?.Length > 0)
            {
                ApplyMapSync(data.VirtualPath, data.MapBinary);
            }
            else
            {
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"Received DataMapSync with empty binary :thinkeline: virtual path '{data.VirtualPath}'");
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
