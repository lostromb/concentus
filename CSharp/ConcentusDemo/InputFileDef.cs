using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public class InputFileDef
    {
        public string FileName;
        public string Attribution;
        private FileStream _inputFileStream;

        // Amount to read from the file at a time (this equals 2ms)
        private const int readSize = 4 * 48000 / 1000;
        private readonly byte[] inputSamples = new byte[readSize];

        public InputFileDef(string fileName, string attribution)
        {
            FileName = fileName;
            Attribution = attribution;
        }

        public void Initialize()
        {
            _inputFileStream = new FileStream(FileName, FileMode.Open);
        }

        public AudioChunk ReadChunk()
        {
            // Read from the input file
            if (_inputFileStream.Position >= _inputFileStream.Length - readSize)
            {
                // Loop if necessary
                _inputFileStream.Seek(0, SeekOrigin.Begin);
            }
            int bytesRead = _inputFileStream.Read(inputSamples, 0, readSize);
            return new AudioChunk(inputSamples, 48000);
        }
    }
}
