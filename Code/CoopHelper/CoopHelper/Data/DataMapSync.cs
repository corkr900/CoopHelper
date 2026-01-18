using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Data
{
    public class DataMapSync : DataCoopBase<DataMapSync>
    {
        public string VirtualPath { get; set; }
        public byte[] MapBinary { get; set; }


        static DataMapSync()
        {
            DataID = "CoopHelper_MapSync_" + CoopHelperModule.ProtocolVersion;
        }

        public override DataFlags DataFlags => DataFlags.None;

        protected override int MaxChunksPerPacket => 200;

        public override void FixupMeta(DataContext ctx)
        {
            player = Get<MetaPlayerPrivateState>(ctx);
        }

        public override MetaType[] GenerateMeta(DataContext ctx)
        {
            return new MetaType[] { new MetaPlayerPrivateState(player) };
        }

        protected override void Write(MemoryStream w)
        {
            base.Write(w);
            if (CNetComm.Instance.CurrentChannelIsMain || CNetComm.Instance.CurrentChannelIsPublic)
            {
                Engine.Commands.Log("DataMapSync being sent on a public CelesteNet channel; to reduce network traffic, this is not allowed.");
                Logger.Log(LogLevel.Warn, "Co-op Helper", $"DataMapSync being sent on a public CelesteNet channel; to reduce network traffic, this is not allowed.");
                w.Write(0);
                return;
            }
            int len = MapBinary?.Length ?? 0;
            w.Write(len);
            if (len > 0) {
                w.Write(VirtualPath);
                w.Write(MapBinary);
            }
        }

        protected override void Read(MemoryStream r)
        {
            base.Read(r);
            int length = r.ReadInt32();
            if (length > 0)
            {
                VirtualPath = r.ReadString();
                MapBinary = new byte[length];
                int bytesRead = r.Read(MapBinary, 0, length);
            }
        }
    }
}
