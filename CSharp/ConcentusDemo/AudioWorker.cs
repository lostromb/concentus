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

        private readonly InputFileDef[] InputFiles = new InputFileDef[]
        {
            new InputFileDef("Blunderbuss.raw", "\"Blunderbuss\" by Henrik José, Creative Commons license"),
            new InputFileDef("Spacecut.raw", "\"Spacecut\" by Ercola/Fairlight, Creative Commons license"),
            new InputFileDef("Jurgen.raw", "\"Jurgen: A Comedy of Justice\". Librivox recording"),
        };

        private volatile bool _running = false;

        // The NAudio waveout device
        private NAudioPlayer _player;

        // The actual codec objects
        private IOpusCodec _currentCodec;
        private ConcentusCodec _concentus;
        private NativeOpusCodec _opus;

        // The currently playing file
        private InputFileDef _currentMusic;

        // Codec params that can be modified by other threads
        private readonly Mutex _codecParamLock = new Mutex();
        private int _complexity = 5;
        private int _bitrate = 64;
        private double _frameSize = 20;
        private bool _codecParamChanged = true;
        
        public AudioWorker()
        {
            _concentus = new ConcentusCodec();
            _opus = new NativeOpusCodec();
            _currentCodec = _concentus;
            _player = new NAudioPlayer();
            _currentMusic = InputFiles[0];
        }

        public void Run(object dummy)
        {
            foreach (var inputStream in InputFiles)
            {
                inputStream.Initialize();
            }
            
            _running = true;
            
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
                    _currentCodec.SetBitrate(_bitrate);
                    _currentCodec.SetComplexity(_complexity);
                    _currentCodec.SetFrameSize(_frameSize);
                    _codecParamChanged = false;
                }

                AudioChunk inputPcm = _currentMusic.ReadChunk();
                _codecParamLock.ReleaseMutex();

                // Run the opus encoder and decoder
                byte[] compressedFrame = _currentCodec.Compress(inputPcm);
                if (compressedFrame != null && compressedFrame.Length > 0)
                {
                    AudioChunk decompressed = _currentCodec.Decompress(compressedFrame);

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

        public void UpdateCodec(int codec)
        {
            _codecParamLock.WaitOne();
            if (codec == 0)
                _currentCodec = _concentus;
            else
                _currentCodec = _opus;
            _codecParamChanged = true;
            _codecParamLock.ReleaseMutex();
        }

        public InputFileDef UpdateInputFile(int newFileIndex)
        {
            if (newFileIndex < InputFiles.Length && newFileIndex >= 0)
            {
                _codecParamLock.WaitOne();
                _currentMusic = InputFiles[newFileIndex];
                _codecParamLock.ReleaseMutex();
            }

            return _currentMusic;
        }

        public CodecStatistics GetStatistics()
        {
            return _currentCodec.GetStatistics();
        }

        public void Stop()
        {
            _running = false;
        }
    }
}
