using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HellaUnsafe.Common
{
    // USAGE:
    // private static ReadOnlySpan<sbyte> Test2DArray_Data/*[8][5]*/ =>
    //    [
    //            4,      6,     24,      7,      5,
    //            0,      0,      2,      0,      0,
    //            12,     28,     41,     13,     -4,
    //            -9,     15,     42,     25,     14,
    //            1,     -2,     62,     41,     -9,
    //            -10,     37,     65,     -4,      3,
    //            -6,      4,     66,      7,     -8,
    //            16,     14,     38,     -3,     33,
    //    ];
    //
    // private static readonly Native2DArray<sbyte> Test2DArray = new Native2DArray<sbyte>(Test2DArray_Data, 8, 5);
    //
    // MAKE ABSOLUTELY SURE YOU CREATE A SINGLETON!
    // THE NATIVE2DARRAY CONSTRUCTOR LEAKS NATIVE HEAP MEMORY EVERY TIME!
    // It would be nice if we could just use embedded .text data as a fixed pointer in the binary
    // itself, but that is only supported under-the-hood for byte arrays because of platform endianness

    internal unsafe struct Native2DArray<T> where T : unmanaged
    {
        public T* Pointer;
        public int Height;
        public int Width;

        public Native2DArray(T[] preallocatedData, int height, int width)
        {
            int numElements = height * width;
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.AsSpan(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Height = height;
            Width = width;
        }

        public Native2DArray(ReadOnlySpan<T> preallocatedData, int height, int width)
        {
            int numElements = height * width;
            Pointer = (T*)Marshal.AllocHGlobal(numElements * sizeof(T));
            preallocatedData.Slice(0, numElements).CopyTo(new Span<T>(Pointer, numElements));
            Height = height;
            Width = width;
        }

        public Native2DArray(T* nativePtr, int height, int width)
        {
            Pointer = nativePtr;
            Height = height;
            Width = width;
        }

        public T* this[int y] => Row(y);

        public T* Row(int y)
        {
            return &Pointer[Width * y];
        }
    }
}
