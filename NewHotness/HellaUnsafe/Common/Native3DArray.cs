using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HellaUnsafe.Common
{
    internal unsafe struct Native3DArray<T> where T : unmanaged
    {
        public T* Pointer;
        public int Width; // Third index (most granular)
        public int Height; // Second index
        public int Depth; // First index (least granular)

        public Native3DArray(int depth, int height, int width, T[] preallocatedData)
        {
            int numElements = width * height * depth;
            Debug.Assert(preallocatedData.Length == numElements);
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.AsSpan(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native3DArray(int depth, int height, int width, ReadOnlySpan<T> preallocatedData)
        {
            int numElements = width * height * depth;
            Debug.Assert(preallocatedData.Length == numElements);
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.Slice(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native3DArray(int depth, int height, int width, T* nativePtr)
        {
            Pointer = nativePtr;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native2DArray<T> this[int z] => Layer(z);

        public Native2DArray<T> Layer(int z)
        {
            return new Native2DArray<T>(Height, Width , &Pointer[Height * Width * z]);
        }

        public T* Row(int z, int y)
        {
            return &Pointer[(Height * Width * z) + (Width * y)];
        }
    }
}
