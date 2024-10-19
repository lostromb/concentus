using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HellaUnsafe.Common
{
    internal unsafe struct Native3DArray<T> where T : unmanaged
    {
        public T* Pointer;
        public int Width;
        public int Height;
        public int Depth;

        public Native3DArray(T[] preallocatedData, int width, int height, int depth)
        {
            int numElements = width * height * depth;
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.AsSpan(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public Native2DArray<T> this[int z] => Layer(z);

        public Native2DArray<T> Layer(int z)
        {
            return new Native2DArray<T>(&Pointer[Width * Height * z], Width, Height);
        }

        public T* Row(int y, int z)
        {
            return &Pointer[(Width * Height * z) + (Width * y)];
        }
    }
}
