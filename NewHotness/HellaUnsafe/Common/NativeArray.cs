using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace HellaUnsafe.Common
{
    internal static unsafe class NativeArray
    {
        public static T* AllocateGlobal<T>(T[] managedArray) where T : unmanaged
        {
            return AllocateGlobal<T>(managedArray.AsSpan());
        }

        /// <summary>
        /// Special case for array of pointers.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="managedArray"></param>
        /// <returns></returns>
        public static T** AllocateGlobal<T>(T*[] managedArray) where T : unmanaged
        {
            T** pointer = (T**)Marshal.AllocHGlobal(managedArray.Length * sizeof(T*));
            for (int c = 0; c < managedArray.Length; c++)
            {
                pointer[c] = managedArray[c];
            }

            return pointer;
        }

        public static T* AllocateGlobal<T>(ReadOnlySpan<T> managedArray) where T : unmanaged
        {
            T* pointer = (T*)Marshal.AllocHGlobal(managedArray.Length * sizeof(T));
            managedArray.CopyTo(new Span<T>(pointer, managedArray.Length));
            return pointer;
        }

        public static T* AllocateGlobalDWordAligned<T>(T[] managedArray) where T : unmanaged
        {
            T* pointer = (T*)NativeMemory.AlignedAlloc((nuint)(managedArray.Length * sizeof(T)), 4);
            managedArray.CopyTo(new Span<T>(pointer, managedArray.Length));
            return pointer;
        }
    }
}
