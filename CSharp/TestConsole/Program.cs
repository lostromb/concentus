using System;
using System.Diagnostics;
using System.IO;
using Concentus.Common;

namespace ConcentusDemo
{
    public class Program
    {
        private static int functionA(int val)
        {
            return Inlines.EC_CLZ((uint)val);
        }

        private static int functionB(int val)
        {
            return Inlines.EC_CLZ((uint)val);
        }

        internal static void Main(string[] args)
        {
            for (int c = 0; c < 1000000; c += 10000)
            {
                Console.WriteLine("{0}\t{1}", functionA(c), functionB(c));
            }
            
            Stopwatch timerz = new Stopwatch();
            timerz.Start();
            for (int c = 0; c < 1000000; c++)
            {
                functionA(c);
            }
            timerz.Stop();
            Console.WriteLine(timerz.ElapsedTicks);
            timerz.Restart();
            for (int c = 0; c < 1000000; c++)
            {
                functionB(c);
            }
            timerz.Stop();
            Console.WriteLine(timerz.ElapsedTicks);
            return;

            int quality = 12;
            ConcentusCodec concentus = new ConcentusCodec(quality);
            concentus.Initialize();
            OpusCodec opus = new OpusCodec(quality);
            opus.Initialize();
            FileStream inputStream = new FileStream(@"Henrik Jose - Blunderbuss.wav", FileMode.Open);
            FileStream outputStream = new FileStream(@"Concentus.wav", FileMode.Create);
            BinaryReader reader = new BinaryReader(inputStream);
            IAudioCompressionStream compressor = concentus.CreateCompressionStream(48000);
            IAudioDecompressionStream decompressor = concentus.CreateDecompressionStream(compressor.GetEncodeParams());

            int inputTimeMs = 0;

            Stopwatch timer = new Stopwatch();
            timer.Start();
            short[] inBuf = new short[2880];
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

                    inputTimeMs += 60;

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
