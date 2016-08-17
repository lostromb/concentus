using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    public class OpusDataBuffer
    {
        private sbyte[] _data;

        public OpusDataBuffer(sbyte[] data)
        {
            _data = data;
        }

        public sbyte GetByte(int idx)
        {
            return _data[idx];
        }

        public void SetByte(int idx, sbyte val)
        {
            _data[idx] = val;
        }

        public void CopyFrom(sbyte[] source, int sourceOffset, int destOffset, int length)
        {
            for (int c = 0; c < length; c++)
            {
                _data[c + destOffset] = source[c + sourceOffset];
            }
        }

        public void CopyTo(int sourceOffset, sbyte[] dest, int destOffset, int length)
        {
            for (int c = 0; c < length; c++)
            {
                dest[c + destOffset] = _data[c + sourceOffset];
            }
        }

        public void MemMove(int source, int dest, int length)
        {
            if (source == dest || length == 0)
                return;

            // Do regions overlap?
            if (source + length > dest || dest + length > source)
            {
                // Take extra precautions
                if (dest < source)
                {
                    // Copy forwards
                    for (int c = 0; c < length; c++)
                    {
                        _data[c + dest] = _data[c + source];
                    }
                }
                else
                {
                    // Copy backwards
                    for (int c = length - 1; c >= 0; c--)
                    {
                        _data[c + dest] = _data[c + source];
                    }
                }
            }
            else
            {
                // Memory regions cannot overlap; just do a fast copy
                Array.Copy(_data, source, _data, dest, length);
            }
        }

        public void MemSet(sbyte value, int offset, int length)
        {
            for (int c = 0; c < length; c++)
            {
                _data[c + offset] = value;
            }
        }

        public int Length
        {
            get
            {
                return _data.Length;
            }
        }

        public bool BufferEquals(sbyte[] other)
        {
            return other == _data;
        }
    }
}
