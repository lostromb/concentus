using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace ConcentusDemo
{
    using NAudio.Wave.SampleProviders;
    using NAudio.CoreAudioApi;

    // based on http://mark-dot-net.blogspot.co.uk/2014/02/fire-and-forget-audio-playback-with.html
    // todo: use this pattern instead https://gist.github.com/markheath/8783999

    public class NAudioPlayer : IDisposable
    {
        private readonly IWavePlayer _outputDevice;
        private readonly MixingSampleProvider _mixer;
        private StreamedSampleProvider _activeStream;

        public NAudioPlayer()
        {
            _outputDevice = new WaveOutEvent();
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
            _mixer.ReadFully = true;
        }

        public void Start()
        {
            _outputDevice.Init(_mixer);
            _activeStream = new StreamedSampleProvider();
            _mixer.AddMixerInput(_activeStream);
            _outputDevice.Play();
        }

        public void Dispose()
        {
            _outputDevice.Dispose();
        }

        public void QueueChunk(AudioChunk chunk)
        {
            _activeStream.QueueChunk(chunk);
        }

        public int BufferSizeMs()
        {
            return _activeStream.BufferSizeMs();
        }

        private class StreamedSampleProvider : ISampleProvider
        {
            private AudioChunk _nextChunk = null;
            private Mutex _streamLock = new Mutex();
            private Queue<AudioChunk> _inputChunks = new Queue<AudioChunk>();

            private int _inCursor = 0;

            public StreamedSampleProvider()
            {
                WaveFormat = new WaveFormat(44100, 1);
            }

            public int BufferSizeMs()
            {
                double length = 0;
                _streamLock.WaitOne();
                foreach (var chunk in _inputChunks)
                {
                    length += chunk.Length.TotalMilliseconds;
                }
                _streamLock.ReleaseMutex();
                return (int)length;
            }

            public void QueueChunk(AudioChunk chunk)
            {
                _streamLock.WaitOne();
                short[] resampledData = Lanczos.Resample(chunk.Data, chunk.SampleRate, 44100);
                AudioChunk resampledChunk = new AudioChunk(resampledData, 44100);
                _inputChunks.Enqueue(resampledChunk);
                _streamLock.ReleaseMutex();
            }

            public bool Finished
            {
                get
                {
                    return false;
                }
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (_nextChunk == null)
                {
                    _streamLock.WaitOne();
                    if (_inputChunks.Count != 0)
                    {
                        _nextChunk = _inputChunks.Dequeue();
                        _streamLock.ReleaseMutex();
                    }
                    else
                    {
                        // Serious buffer underrun. In this case, just return silence instead of stuttering
                        _nextChunk = null;
                        for (int c = 0; c < count; c++)
                        {
                            buffer[c + offset] = 0.0f;
                        }

                        _streamLock.ReleaseMutex();
                        return count;
                    }
                }

                int samplesWritten = 0;
                short[] returnVal = new short[count];

                while (samplesWritten < count && _nextChunk != null)
                {
                    int remainingInThisChunk = _nextChunk.DataLength - _inCursor;
                    int remainingToWrite = (count - samplesWritten);
                    int chunkSize = Math.Min(remainingInThisChunk, remainingToWrite);
                    Array.Copy(_nextChunk.Data, _inCursor, returnVal, samplesWritten, chunkSize);
                    _inCursor += chunkSize;
                    samplesWritten += chunkSize;

                    if (_inCursor >= _nextChunk.DataLength)
                    {
                        _inCursor = 0;

                        _streamLock.WaitOne();
                        if (_inputChunks.Count != 0)
                        {
                            _nextChunk = _inputChunks.Dequeue();
                            _streamLock.ReleaseMutex();
                        }
                        else
                        {
                            _nextChunk = null;
                            for (int c = 0; c < count; c++)
                            {
                                buffer[c + offset] = 0.0f;
                            }

                            _streamLock.ReleaseMutex();
                            return count;
                        }
                    }
                }

                for (int c = 0; c < samplesWritten; c++)
                {
                    buffer[c + offset] = ((float)returnVal[c]) / ((float)short.MaxValue);
                }

                return samplesWritten;
            }

            public WaveFormat WaveFormat
            {
                get;
                private set;
            }
        }
    }
}
