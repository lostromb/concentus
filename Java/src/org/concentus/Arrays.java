/* Copyright (c) 2016 Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

package org.concentus;

import java.lang.reflect.Array;

class Arrays<T>
{
    static int[][] InitTwoDimensionalArrayInt(int x, int y)
    {
        int[][] returnVal = new int[x][];
        for (int c = 0; c < x; c++)
        {
            returnVal[c] = new int[y];
        }
        return returnVal;
    }
    
    static float[][] InitTwoDimensionalArrayFloat(int x, int y)
    {
        float[][] returnVal = new float[x][];
        for (int c = 0; c < x; c++)
        {
            returnVal[c] = new float[y];
        }
        return returnVal;
    }
    
    static short[][] InitTwoDimensionalArrayShort(int x, int y)
    {
        short[][] returnVal = new short[x][];
        for (int c = 0; c < x; c++)
        {
            returnVal[c] = new short[y];
        }
        return returnVal;
    }
    
    static byte[][] InitTwoDimensionalArrayByte(int x, int y)
    {
        byte[][] returnVal = new byte[x][];
        for (int c = 0; c < x; c++)
        {
            returnVal[c] = new byte[y];
        }
        return returnVal;
    }
    
    static byte[][][] InitThreeDimensionalArrayByte(int x, int y, int z)
    {
        byte[][][] returnVal = new byte[x][][];
        for (int c = 0; c < x; c++)
        {
            returnVal[c] = new byte[y][];
            for (int a = 0; a < y; a++)
            {
                returnVal[c][a] = new byte[z];
            }
        }
        return returnVal;
    }
    
    static void MemSet(byte[] array, byte value)
    {
        for (int c = 0; c < array.length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSet(short[] array, short value)
    {
        for (int c = 0; c < array.length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSet(int[] array, int value)
    {
        for (int c = 0; c < array.length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSet(float[] array, float value)
    {
        for (int c = 0; c < array.length; c++)
        {
            array[c] = value;
        }
    }

    static void MemSet(byte[] array, byte value, int length)
    {
        for (int c = 0; c < length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSet(short[] array, short value, int length)
    {
        for (int c = 0; c < length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSet(int[] array, int value, int length)
    {
        for (int c = 0; c < length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSet(float[] array, float value, int length)
    {
        for (int c = 0; c < length; c++)
        {
            array[c] = value;
        }
    }

    static void MemSetWithOffset(byte[] array, byte value, int offset, int length)
    {
        for (int c = offset; c < offset + length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSetWithOffset(short[] array, short value, int offset, int length)
    {
        for (int c = offset; c < offset + length; c++)
        {
            array[c] = value;
        }
    }
    
    static void MemSetWithOffset(int[] array, int value, int offset, int length)
    {
        for (int c = offset; c < offset + length; c++)
        {
            array[c] = value;
        }
    }
    
    // Hooray for generic programming in Java

    static void MemMove(byte[] array, int src_idx, int dst_idx, int length)
    {
        if (src_idx == dst_idx || length == 0)
            return;

        // Do regions overlap?
        if (src_idx + length > dst_idx || dst_idx + length > src_idx)
        {
            // Take extra precautions
            if (dst_idx < src_idx)
            {
                // Copy forwards
                for (int c = 0; c < length; c++)
                {
                    array[c + dst_idx] = array[c + src_idx];
                }
            }
            else
            {
                // Copy backwards
                for (int c = length - 1; c >= 0; c--)
                {
                    array[c + dst_idx] = array[c + src_idx];
                }
            }
        }
        else
        {
            // Memory regions cannot overlap; just do a fast copy
            System.arraycopy(array, src_idx, array, dst_idx, length);
        }
    }
    
    static void MemMove(short[] array, int src_idx, int dst_idx, int length)
    {
        if (src_idx == dst_idx || length == 0)
            return;

        // Do regions overlap?
        if (src_idx + length > dst_idx || dst_idx + length > src_idx)
        {
            // Take extra precautions
            if (dst_idx < src_idx)
            {
                // Copy forwards
                for (int c = 0; c < length; c++)
                {
                    array[c + dst_idx] = array[c + src_idx];
                }
            }
            else
            {
                // Copy backwards
                for (int c = length - 1; c >= 0; c--)
                {
                    array[c + dst_idx] = array[c + src_idx];
                }
            }
        }
        else
        {
            // Memory regions cannot overlap; just do a fast copy
            System.arraycopy(array, src_idx, array, dst_idx, length);
        }
    }
    
    static void MemMove(int[] array, int src_idx, int dst_idx, int length)
    {
        if (src_idx == dst_idx || length == 0)
            return;

        // Do regions overlap?
        if (src_idx + length > dst_idx || dst_idx + length > src_idx)
        {
            // Take extra precautions
            if (dst_idx < src_idx)
            {
                // Copy forwards
                for (int c = 0; c < length; c++)
                {
                    array[c + dst_idx] = array[c + src_idx];
                }
            }
            else
            {
                // Copy backwards
                for (int c = length - 1; c >= 0; c--)
                {
                    array[c + dst_idx] = array[c + src_idx];
                }
            }
        }
        else
        {
            // Memory regions cannot overlap; just do a fast copy
            System.arraycopy(array, src_idx, array, dst_idx, length);
        }
    }
}
