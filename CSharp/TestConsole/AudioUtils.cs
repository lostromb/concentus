using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole
{
    public class AudioUtils
    {
        /// <summary>
        /// Sends an entire audio chunk through a compressor and returns the byte array output and encode params
        /// </summary>
        /// <param name="audio"></param>
        /// <param name="compressor"></param>
        /// <param name="encodeParams"></param>
        /// <returns></returns>
        internal static byte[] CompressAudioUsingStream(AudioChunk audio, IAudioCompressionStream compressor, out string encodeParams)
        {
            if (compressor == null)
            {
                encodeParams = "ERROR: NULL ENCODER";
                return new byte[0];
            }

            encodeParams = compressor.GetEncodeParams();
            byte[] body = compressor.Compress(audio);
            byte[] footer = compressor.Close();
            if (footer == null)
            {
                footer = new byte[0];
            }

            byte[] returnVal = new byte[body.Length + footer.Length];
            Array.Copy(body, 0, returnVal, 0, body.Length);
            Array.Copy(footer, 0, returnVal, body.Length, footer.Length);
            return returnVal;
        }

        /// <summary>
        /// Sends an encoded audio sample through a decompressor and returns the decoded audio
        /// </summary>
        /// <param name="input"></param>
        /// <param name="decompressor"></param>
        /// <returns></returns>
        internal static AudioChunk DecompressAudioUsingStream(byte[] input, IAudioDecompressionStream decompressor)
        {
            if (decompressor == null)
            {
                return null;
            }

            AudioChunk first = decompressor.Decompress(input);
            AudioChunk second = decompressor.Close();
            if (second != null && second.DataLength > 0)
            {
                first = first.Concatenate(second);
            }

            return first;
        }
    }
}
