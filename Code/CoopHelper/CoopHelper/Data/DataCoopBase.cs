using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.IO;
using Monocle;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MonoMod.InlineRT.MonoModRule;

namespace Celeste.Mod.CoopHelper.Data
{
    /// <summary>
    /// A class representing complex or large packets. Contains functionality to split into multiple
    /// packets if there is too much data for cnet to transmit in one packet. Encoding is as follows:
    /// 1. (PlayerID) Sender player ID
    /// 2. (int32) Chunk count
    ///     - If chunk count == 0, skip to #7
    ///     - If chunk count == 1, skip to #5
    /// 3. (int32) Chunk index
    /// 4. (unsigned int32) Packet ID
    ///     - Packet ID is unique per sender and will be the same for all chunks in a packet
    /// 5. (int32) Data Length
    ///     - Defines the amount of data that follows, in bytes
    /// 6. (byte[DataLength]) The serialized data
    /// 7. (optional, varies) Nonstandard data handled by the individual packet types
	/// </summary>
    abstract public class DataCoopBase<T> : DataType<T> where T: DataCoopBase<T>, new() {

        private static uint PacketCounter = 0;
        private static uint NewPacketID() => ++PacketCounter;

        public DataPlayerInfo player;
        public PlayerID playerID;
        public uint packetID;
        public int chunkNumber;
        public int chunksInPacket;
        public byte[] data = null;

        protected virtual int MaxChunksPerPacket => 20;
        protected virtual bool UseBoundRef => false;

        public DataCoopBase()
        {
            playerID = PlayerID.MyID;
            packetID = NewPacketID();
        }

        public override MetaType[] GenerateMeta(DataContext ctx)
        {
            if (UseBoundRef)
            {
                return new MetaType[] {
                    new MetaPlayerPrivateState(player),
                    new MetaBoundRef(DataType<DataPlayerInfo>.DataID, player?.ID ?? uint.MaxValue, true)
                };
            }
            else return new MetaType[] { new MetaPlayerPrivateState(player) };
        }

        public override void FixupMeta(DataContext ctx)
        {
            player = Get<MetaPlayerPrivateState>(ctx);
            if (UseBoundRef) Get<MetaBoundRef>(ctx).ID = player?.ID ?? uint.MaxValue;
        }

        /// <summary>
        /// Reads formatted data from the cnet stream
        /// </summary>
        protected override void Read(CelesteNetBinaryReader reader)
        {
            playerID = reader.ReadPlayerID();
            chunksInPacket = reader.ReadInt32();
            if (chunksInPacket == 0)
            {
                chunkNumber = 0;
                packetID = 0;
                data = Array.Empty<byte>();
                return;
            }
            if (chunksInPacket == 1)
            {
                chunkNumber = 0;
                packetID = 0;
                int length = reader.ReadInt32();
                data = reader.ReadBytes(length);
                MemoryStream ms = new MemoryStream();
                ms.Write(data);
                ms.Position = 0;
                Read(ms);
            }
            else
            {
                chunkNumber = reader.ReadInt32();
                packetID = reader.ReadUInt32();
                int length = reader.ReadInt32();
                data = reader.ReadBytes(length);
            }
        }

        /// <summary>
        /// Serializes the data and writes the data to the cnet stream. If this is a starter object, data is serialized to binary
        /// and queued for sending; otherwise, pre-encoded data is sent.
        /// </summary>
        /// <param name="writer"></param>
        protected override void Write(CelesteNetBinaryWriter writer)
        {
            writer.Write(playerID);
            if (data == null)
            {
                if (!SerializeAndQueue())
                {
                    chunksInPacket = 0;  // Indicate no data being sent
                }
            }
            SendData(writer);
        }

        /// <summary>
        /// Serializes the data and writes the first chunk to the cnet stream. If the data is larger than
        /// one chunk, subsequent chunks are enqueued.
        /// </summary>
        private bool SerializeAndQueue()
        {
            using MemoryStream s = new MemoryStream();
            Write(s);
            long totalSize = s.Length;
            if (totalSize <= 0)
            {
                return true;  // Nothing to send, but a valid packet. Might be using nonstandard content rather than the chunkable data buffer.
            }

            s.Position = 0;
            long maxChunkSize = CNetComm.Instance.MaxPacketChunkSize;
            List<byte[]> chunks = new();
            while (s.Position < totalSize)
            {
                long chunkSize = Math.Min(totalSize - s.Position, maxChunkSize);
                byte[] chunk = new byte[chunkSize];
                int read = s.Read(chunk, 0, chunk.Length);
                if (read <= 0) break;  // Shouldn't happen, but just in case
                if (read < chunkSize)
                {  // Shouldn't happen, but just in case
                    byte[] newBuf = new byte[read];
                    Array.Copy(chunk, newBuf, read);
                    chunk = newBuf;
                }
                chunks.Add(chunk);
            }

            chunksInPacket = chunks.Count;
            Engine.Commands.Log($"Chunked packet of type {DataID} ({totalSize} bytes) into {chunksInPacket} chunks. Limit per chunk is {maxChunkSize} bytes.");
            if (chunksInPacket > MaxChunksPerPacket)
            {
                Logger.Log(LogLevel.Error, "Co-op Helper", $"Tried to send {DataID} packet with {chunksInPacket} chunks; maximum allowed is {MaxChunksPerPacket}. Data will not be sent.");
                return false; // Too many chunks; don't send anything
            }
            else if (chunksInPacket > 1)
            {
                Logger.Log(LogLevel.Info, "Co-op Helper", $"Chunking large packet of type {DataID} into {chunksInPacket} chunks. Packet length {totalSize}, limit is {maxChunkSize}");
            }
            data = chunks[0];
            chunkNumber = 0;
            for (int i = 1; i < chunksInPacket; i++)
            {
                CNetComm.Instance.EnqueueSubsequentChunk(new T()
                {
                    packetID = packetID,
                    chunksInPacket = chunksInPacket,
                    chunkNumber = i,
                    data = chunks[i],
                });
            }
            return true; // Successfully queued
        }

        /// <summary>
        /// Writes the data to the cnet stream
        /// </summary>
        /// <param name="writer"></param>
        private void SendData(CelesteNetBinaryWriter writer)
        {
            writer.Write(chunksInPacket);
            if (chunksInPacket <= 0)
            {
                return;
            }
            if (chunksInPacket > 1)
            {
                writer.Write(chunkNumber);
                writer.Write(packetID);
            }
            writer.Write(data?.Length ?? 0);
            writer.Write(data);
        }

        /// <summary>
        /// Composes several chunks into a single stream and then calls the virtual deserialization method
        /// </summary>
        /// <typeparam name="TChunk">The type of the chunk array. We expect this to be the same as the class's type parameter, but the compiler wants it to be separate</typeparam>
        /// <param name="arr">An array of DataCoopBase<TChunk>s to compose and deserialize</param>
        internal void Compose<TChunk>(DataCoopBase<TChunk>[] arr) where TChunk : DataCoopBase<TChunk>, new()
        {
            // We expect arr[0] == this, but that's not really important. just trust the process
            MemoryStream ms = new MemoryStream();
            foreach (DataCoopBase<TChunk> chunk in arr)
            {
                ms.Write(chunk.data);
            }
            ms.Position = 0;
            Read(ms);
        }

        /// <summary>
        /// Write to a memorystream buffer. Overriding this instead of Write(CelesteNetBinaryWriter)
        /// allows h2h to chunk the packet if it's too large
        /// </summary>
        /// <param name="w">The memory stream to write to</param>
        protected virtual void Write(MemoryStream w) { }

        /// <summary>
        /// Write to a memorystream buffer. Overriding this instead of Read(CelesteNetBinaryReader)
        /// allows h2h to chunk the packet if it's too large
        /// </summary>
        /// <param name="r">The memory stream to read from</param>
        protected virtual void Read(MemoryStream r) { }

    }
}
