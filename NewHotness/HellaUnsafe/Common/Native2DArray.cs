using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HellaUnsafe.Common
{
    internal unsafe struct Native2DArray<T> where T : unmanaged
    {
        public T* Pointer;
        public int Width;
        public int Height;

        public Native2DArray(T[] preallocatedData, int width, int height)
        {
            int numElements = width * height;
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.AsSpan(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Width = width;
            Height = height;
        }

        public Native2DArray(T* nativePtr, int width, int height)
        {
            Pointer = nativePtr;
            Width = width;
            Height = height;
        }

        public T* this[int y] => Row(y);

        public T* Row(int y)
        {
            return &Pointer[Width * y];
        }
    }
}
