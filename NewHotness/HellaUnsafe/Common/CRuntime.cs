/* Copyright (c) 2024 Logan Stromberg

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

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace HellaUnsafe.Common
{
    internal static class CRuntime
    {
        [Conditional("DEBUG")]
        internal static void ASSERT(bool condition)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                throw new Exception("Assertion failed");
            }

            //Debug.Assert(condition);
        }

        [Conditional("DEBUG")]
        internal static void ASSERT(bool condition, string message)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                throw new Exception(message);
            }

            //Debug.Assert(condition);
        }

        internal static sbyte abs(sbyte t)
        {
            if (t < 0)
            {
                return (sbyte)(0 - t);
            }

            return t;
        }

        internal static int BOOL2INT(bool x)
        {
            return x ? 1 : 0;
        }

        /// <summary>
        /// Gets an unmanaged pointer to a span. THIS IS DANGEROUS FOR OBVIOUS REASONS.
        /// Intended for use with stackalloc spans where original code uses pointers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static unsafe T* SpanToPointerDangerous<T>(Span<T> input) where T : unmanaged
        {
            return (T*)Unsafe.AsPointer(ref input[0]);
        }

        /// <summary>
        /// Given a span of native integers, cast that into an array of pointers of the specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static unsafe T** SpanToPointerOfPointersDangerous<T>(Span<nint> input) where T : unmanaged
        {
            return (T**)Unsafe.AsPointer(ref input[0]);
        }

        internal static unsafe void OPUS_CLEAR(byte* dst, int elements)
        {
            new Span<byte>(dst, elements).Fill(0);
        }

        internal static unsafe void OPUS_CLEAR(int* dst, int elements)
        {
            new Span<int>(dst, elements).Fill(0);
        }

        internal static unsafe void OPUS_CLEAR(float* dst, int elements)
        {
            new Span<float>(dst, elements).Fill(0);
        }

        internal static unsafe void OPUS_CLEAR(byte* dst, uint elements)
        {
            new Span<byte>(dst, (int)elements).Fill(0);
        }

        internal static unsafe void OPUS_COPY(byte* dst, byte* src, int elements)
        {
            new Span<byte>(src, elements).CopyTo(new Span<byte>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(short* dst, short* src, int elements)
        {
            new Span<short>(src, elements).CopyTo(new Span<short>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(float* dst, float* src, int elements)
        {
            new Span<float>(src, elements).CopyTo(new Span<float>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(float* dst, Span<float> src, int elements)
        {
            src.Slice(0, elements).CopyTo(new Span<float>(dst, elements));
        }

        internal static unsafe void OPUS_COPY(byte* dst, byte* src, uint elements)
        {
            new Span<byte>(src, (int)elements).CopyTo(new Span<byte>(dst, (int)elements));
        }

        internal static unsafe void OPUS_MOVE(byte* dst, byte* src, int elements)
        {
            new Span<byte>(src, elements).CopyTo(new Span<byte>(dst, elements));
        }

        internal static unsafe void OPUS_MOVE(float* dst, float* src, int elements)
        {
            new Span<float>(src, elements).CopyTo(new Span<float>(dst, elements));
        }

        internal static unsafe void OPUS_MOVE(byte* dst, byte* src, uint elements)
        {
            new Span<byte>(src, (int)elements).CopyTo(new Span<byte>(dst, (int)elements));
        }

        internal static unsafe void silk_memset(sbyte* dst, sbyte src, int bytes)
        {
            new Span<sbyte>(dst, bytes).Fill(src);
        }

        internal static unsafe void silk_memset(byte* dst, byte src, int bytes)
        {
            new Span<byte>(dst, bytes).Fill(src);
        }

        internal static unsafe void silk_memset(short* dst, short src, int bytes)
        {
            new Span<short>(dst, bytes / sizeof(short)).Fill(src);
        }

        internal static unsafe void silk_memset(int* dst, int src, int bytes)
        {
            new Span<int>(dst, bytes / sizeof(int)).Fill(src);
        }

        internal static unsafe void silk_memset(float* dst, float src, int bytes)
        {
            new Span<float>(dst, bytes / sizeof(float)).Fill(src);
        }

        internal static unsafe void silk_memset(double* dst, double src, int bytes)
        {
            new Span<double>(dst, bytes / sizeof(double)).Fill(src);
        }

        internal static unsafe void silk_memcpy(byte* dst, byte* src, int bytes)
        {
            new Span<byte>(src, bytes / sizeof(byte)).CopyTo(new Span<byte>(dst, bytes / sizeof(byte)));
        }

        internal static unsafe void silk_memcpy(sbyte* dst, sbyte* src, int bytes)
        {
            new Span<sbyte>(src, bytes / sizeof(sbyte)).CopyTo(new Span<sbyte>(dst, bytes / sizeof(sbyte)));
        }

        internal static unsafe void silk_memcpy(int* dst, int* src, int bytes)
        {
            new Span<int>(src, bytes / sizeof(int)).CopyTo(new Span<int>(dst, bytes / sizeof(int)));
        }

        internal static unsafe void silk_memcpy(short* dst, short* src, int bytes)
        {
            new Span<short>(src, bytes / sizeof(short)).CopyTo(new Span<short>(dst, bytes / sizeof(short)));
        }

        internal static unsafe void silk_memcpy(float* dst, float* src, int bytes)
        {
            new Span<float>(src, bytes / sizeof(float)).CopyTo(new Span<float>(dst, bytes / sizeof(float)));
        }

        internal static unsafe void silk_memcpy(double* dst, double* src, int bytes)
        {
            new Span<double>(src, bytes / sizeof(double)).CopyTo(new Span<double>(dst, bytes / sizeof(double)));
        }

        internal static unsafe void silk_memmove(float* dst, float* src, int bytes)
        {
            new Span<float>(src, bytes / sizeof(float)).CopyTo(new Span<float>(dst, bytes / sizeof(float)));
        }

        internal static unsafe void silk_memmove(short* dst, short* src, int bytes)
        {
            new Span<short>(src, bytes / sizeof(short)).CopyTo(new Span<short>(dst, bytes / sizeof(short)));
        }

        internal static unsafe void silk_memmove(int* dst, int* src, int bytes)
        {
            new Span<int>(src, bytes / sizeof(int)).CopyTo(new Span<int>(dst, bytes / sizeof(int)));
        }

        internal static bool opus_likely(bool input)
        {
            return input;
        }

        internal static unsafe T** AllocateGlobalPointerArray<T>(int elements) where T : unmanaged
        {
            IntPtr dest = Marshal.AllocHGlobal(elements * sizeof(IntPtr));
            Unsafe.InitBlock((void*)dest, 0, (uint)(elements * sizeof(IntPtr)));
            return (T**)dest;
        }

        public static unsafe T** AllocateGlobalPointerArray<T>(int length, T*[] managedArray) where T : unmanaged
        {
            Debug.Assert(length == managedArray.Length);
            T** pointer = (T**)Marshal.AllocHGlobal(managedArray.Length * sizeof(T*));
            for (int c = 0; c < managedArray.Length; c++)
            {
                Debug.Assert(managedArray[c] != default(T*)); // Sanity check to make sure null pointers don't reach initialization
                pointer[c] = managedArray[c];
            }

            return pointer;
        }

        /// <summary>
        /// Allocates an array on the unmanaged heap with the specified type, length, and input data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static unsafe T* AllocateGlobalArray<T>(T[] input) where T: unmanaged
        {
            fixed (T* src = input)
            {
                IntPtr dest = Marshal.AllocHGlobal(input.Length * sizeof(T));
                Unsafe.CopyBlock((void*)dest, (void*)src, (uint)(input.Length * sizeof(T)));
                return (T*)dest;
            }
        }

        /// <summary>
        /// Allocates an array on the unmanaged heap with the specified type, length, and input data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static unsafe T* AllocateGlobalArray<T>(int expectedLength, T[] input) where T : unmanaged
        {
            Debug.Assert(expectedLength == input.Length);
            fixed (T* src = input)
            {
                IntPtr dest = Marshal.AllocHGlobal(input.Length * sizeof(T));
                Unsafe.CopyBlock((void*)dest, (void*)src, (uint)(input.Length * sizeof(T)));
                return (T*)dest;
            }
        }

        public static unsafe T* AllocateGlobalArray<T>(ReadOnlySpan<T> managedArray) where T : unmanaged
        {
            T* pointer = (T*)Marshal.AllocHGlobal(managedArray.Length * sizeof(T));
            managedArray.CopyTo(new Span<T>(pointer, managedArray.Length));
            return pointer;
        }

        internal static unsafe T* AllocateGlobalArrayDWordAligned<T>(T[] managedArray) where T : unmanaged
        {
            T* pointer = (T*)NativeMemory.AlignedAlloc((nuint)(managedArray.Length * sizeof(T)), 4);
            managedArray.CopyTo(new Span<T>(pointer, managedArray.Length));
            return pointer;
        }

        /// <summary>
        /// Initializes a new unmanaged struct of type T on the unmanaged heap initialized to zero.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal static unsafe T* AllocGlobalStructZeroed<T>() where T : unmanaged
        {
            int size = sizeof(T);
            IntPtr dest = Marshal.AllocHGlobal(size);
            Unsafe.InitBlock((void*)dest, 0, (uint)size);
            return (T*)dest;
        }

        /// <summary>
        /// Initializes a new unmanaged struct of type T on the unmanaged heap
        /// with initial value equal to a copy of the given input
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static unsafe T* AllocGlobalStructInit<T>(T input) where T : unmanaged
        {
            IntPtr dest = Marshal.AllocHGlobal(sizeof(T));
            Unsafe.Copy((void*)dest, ref input);
            return (T*)dest;
        }

        /// <summary>
        /// Initializes a new unmanaged struct of type T on the unmanaged heap
        /// with initial value equal to the result of a value producer function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static unsafe T* AllocGlobalStructInit<T>(Func<T> input) where T : unmanaged
        {
            T createdObj = input();
            IntPtr dest = Marshal.AllocHGlobal(sizeof(T));
            Unsafe.Copy((void*)dest, ref createdObj);
            return (T*)dest;
        }

        //internal static unsafe ref T Array2DElementRef<T>(T* flatArray, int x, int y, int dimY) where T : unmanaged
        //{
        //    return ref flatArray[(y * dimY) + x];
        //}

        //internal static unsafe T Array2DElement<T>(T* flatArray, int x, int y, int dimY) where T : unmanaged
        //{
        //    return flatArray[(y * dimY) + x];
        //}

        //internal static unsafe T* Array2DRow<T>(T* flatArray, int x, int dimY) where T : unmanaged
        //{
        //    return flatArray + (x * dimY);
        //}
    }
}
