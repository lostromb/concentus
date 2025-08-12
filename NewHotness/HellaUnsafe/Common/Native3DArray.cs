using System;
using System.Collections.Generic;
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

        public Native3DArray(T[] preallocatedData, int depth, int height, int width)
        {
            int numElements = width * height * depth;
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.AsSpan(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native3DArray(ReadOnlySpan<T> preallocatedData, int depth, int height, int width)
        {
            int numElements = width * height * depth;
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.Slice(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native3DArray(T* nativePtr, int depth, int height, int width)
        {
            Pointer = nativePtr;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native2DArray<T> this[int z] => Layer(z);

        public Native2DArray<T> Layer(int z)
        {
            return new Native2DArray<T>(&Pointer[Height * Width * z], Height, Width);
        }

        public T* Row(int z, int y)
        {
            return &Pointer[(Height * Width * z) + (Width * y)];
        }
    }
}
