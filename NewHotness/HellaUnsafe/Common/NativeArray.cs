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

        public static T* AllocateGlobal<T>(ReadOnlySpan<T> managedArray) where T : unmanaged
        {
            T* pointer = (T*)Marshal.AllocHGlobal(managedArray.Length * sizeof(T));
            managedArray.CopyTo(new Span<T>(pointer, managedArray.Length));
            return pointer;
        }
    }
}
