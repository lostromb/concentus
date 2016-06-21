using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConcentusDemo
{
    public class AudioWorker
    {
        private const int BUFFER_LENGTH_MS = 200;

        private readonly string INPUT_FILE = @"Butterfly.raw";

        private volatile bool _running = false;

        // The NAudio waveout device
        private NAudioPlayer _player;

        // The input file stream
        private FileStream _inputFileStream;

        // The actual codec object
        private IOpusCodec _codec;

        // Codec params that can be modified by other threads
        private readonly Mutex _codecParamLock = new Mutex();
        private int _complexity = 5;
        private int _bitrate = 64;
        private double _frameSize = 20;
        private bool _codecParamChanged = true;
        
        public AudioWorker()
        {
            _codec = new ConcentusCodec();
            _player = new NAudioPlayer();
        }

        public void Run(object dummy)
        {
            // Amount to read from the file at a time (this equals 2ms)
            int readSize = 4 * 48000 / 1000;
            byte[] inputSamples = new byte[readSize];
            _running = true;
            _inputFileStream = new FileStream(INPUT_FILE, FileMode.Open);
            _player.Start();

            while (_running)
            {
                // Spin until the output buffer has some room
                while (_player.BufferSizeMs() > BUFFER_LENGTH_MS)
                {
                    Thread.Sleep(5);
                }

                // Check for updated parameters and send them to the codec if needed
                _codecParamLock.WaitOne();
                if (_codecParamChanged)
                {
                    _codec.SetBitrate(_bitrate);
                    _codec.SetComplexity(_complexity);
                    _codec.SetFrameSize(_frameSize);
                    _codecParamChanged = false;
                }
                _codecParamLock.ReleaseMutex();

                // Read from the input file
                if (_inputFileStream.Position >= _inputFileStream.Length - readSize)
                {
                    // Loop if necessary
                    _inputFileStream.Seek(0, SeekOrigin.Begin);
                }
                int bytesRead = _inputFileStream.Read(inputSamples, 0, readSize);
                AudioChunk inputPcm = new AudioChunk(inputSamples, 48000);

                // Run the opus encoder and decoder
                byte[] compressedFrame = _codec.Compress(inputPcm);
                if (compressedFrame != null && compressedFrame.Length > 0)
                {
                    AudioChunk decompressed = _codec.Decompress(compressedFrame);

                    // Pipe the output to the audio device
                    _player.QueueChunk(decompressed);
                }
            }
        }

        public void UpdateBitrate(int bitrate)
        {
            _codecParamLock.WaitOne();
            _bitrate = bitrate;
            _codecParamChanged = true;
            _codecParamLock.ReleaseMutex();
        }

        public void UpdateComplexity(int complexity)
        {
            _codecParamLock.WaitOne();
            _complexity = complexity;
            _codecParamChanged = true;
            _codecParamLock.ReleaseMutex();
        }

        public void UpdateFrameSize(double frameSize)
        {
            _codecParamLock.WaitOne();
            _frameSize = frameSize;
            _codecParamChanged = true;
            _codecParamLock.ReleaseMutex();
        }

        public CodecStatistics GetStatistics()
        {
            return _codec.GetStatistics();
        }

        public void Stop()
        {
            _running = false;
        }
    }
}
