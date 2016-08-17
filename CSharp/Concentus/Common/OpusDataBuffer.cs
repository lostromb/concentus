using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    public static class DataBufferExtensions
    {
        public static sbyte GetByte(this sbyte[] _data, int idx)
        {
            return _data[idx];
        }

        public static void SetByte(this sbyte[] _data, int idx, sbyte val)
        {
            _data[idx] = val;
        }

        public static void CopyFrom(this sbyte[] _data, sbyte[] source, int sourceOffset, int destOffset, int length)
        {
            for (int c = 0; c < length; c++)
            {
                _data[c + destOffset] = source[c + sourceOffset];
            }
        }

        public static void CopyTo(this sbyte[] _data, int sourceOffset, sbyte[] dest, int destOffset, int length)
        {
            for (int c = 0; c < length; c++)
            {
                dest[c + destOffset] = _data[c + sourceOffset];
            }
        }

        public static void MemMove(this sbyte[] _data, int source, int dest, int length)
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

        public static void MemSet(this sbyte[] _data, sbyte value, int offset, int length)
        {
            for (int c = 0; c < length; c++)
            {
                _data[c + offset] = value;
            }
        }
    }
}
