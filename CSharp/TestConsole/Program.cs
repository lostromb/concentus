using Concentus.Common.CPlusPlus;
using Concentus.Silk;
using Concentus.Silk.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Concentus.Common;
using System.IO;
using Durandal.Common.Audio;
using Concentus.Opus.Enums;
using Concentus;
using Concentus.Structs;

namespace TestConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ConcentusCodec concentus = new ConcentusCodec();
            concentus.Initialize();
            OpusCodec opus = new OpusCodec();
            opus.Initialize();
            FileStream inputStream = new FileStream(@"Henrik Jose - Blunderbuss.wav", FileMode.Open);
            FileStream outputStream = new FileStream(@"Concentus.wav", FileMode.Create);
            BinaryReader reader = new BinaryReader(inputStream);
            IAudioCompressionStream compressor = opus.CreateCompressionStream(48000);
            IAudioDecompressionStream decompressor = concentus.CreateDecompressionStream(compressor.GetEncodeParams());

            Stopwatch timer = new Stopwatch();
            timer.Start();
            short[] inBuf = new short[320];
            try
            {
                byte[] wavHeader = reader.ReadBytes(48);
                outputStream.Write(wavHeader, 0, 48);
                while (true)
                {
                    for (int c = 0; c < inBuf.Length; c++)
                    {
                        inBuf[c] = reader.ReadInt16();
                    }

                    AudioChunk audio = new AudioChunk(inBuf, 48000);
                    byte[] compressed = compressor.Compress(audio);

                    if (compressed != null && compressed.Length > 0)
                    {
                        AudioChunk decompressed = decompressor.Decompress(compressed);
                        byte[] decompressedData = decompressed.GetDataAsBytes();
                        outputStream.Write(decompressedData, 0, decompressedData.Length);
                        outputStream.Flush();
                    }
                }
            }
            catch (EndOfStreamException)
            {
                inputStream.Close();
                outputStream.Close();
            }
            Console.WriteLine(timer.ElapsedMilliseconds);
        }
    }
}
