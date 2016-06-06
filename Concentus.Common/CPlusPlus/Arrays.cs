using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common.CPlusPlus
{
    public static class Arrays
    {
        public static T[][] InitTwoDimensionalArray<T>(int x, int y)
        {
            T[][] returnVal = new T[x][];
            for (int c = 0; c < x; c++)
            {
                returnVal[c] = new T[y];
            }
            return returnVal;
        }

        public static Pointer<Pointer<T>> InitTwoDimensionalArrayPointer<T>(int x, int y)
        {
            Pointer<Pointer<T>> returnVal = Pointer.Malloc<Pointer<T>>(x);
            for (int c = 0; c < x; c++)
            {
                returnVal[c] = Pointer.Malloc<T>(y);
            }
            return returnVal;
        }

        public static T[][][] InitThreeDimensionalArray<T>(int x, int y, int z)
        {
            T[][][] returnVal = new T[x][][];
            for (int c = 0; c < x; c++)
            {
                returnVal[c] = new T[y][];
                for (int a = 0; a < y; a++)
                {
                    returnVal[c][a] = new T[z];
                }
            }
            return returnVal;
        }

        public static Pointer<Pointer<Pointer<T>>> InitThreeDimensionalArrayPointer<T>(int x, int y, int z)
        {
            Pointer<Pointer<Pointer<T>>> returnVal = Pointer.Malloc<Pointer<Pointer<T>>>(x);
            for (int c = 0; c < x; c++)
            {
                returnVal[c] = Pointer.Malloc<Pointer<T>>(y);
                for (int a = 0; a < y; a++)
                {
                    returnVal[c][a] = Pointer.Malloc<T>(z);
                }
            }
            return returnVal;
        }

        public static T[] CloneArray<T>(T[] inArray)
        {
            T[] returnVal = new T[inArray.Length];
            Array.Copy(inArray, returnVal, inArray.Length);
            return returnVal;
        }

        public static T[][] CloneArray<T>(T[][] inArray)
        {
            T[][] returnVal = new T[inArray.Length][];
            for (int c = 0; c < inArray.Length; c++)
            {
                returnVal[c] = new T[inArray[c].Length];
                Array.Copy(inArray[c], returnVal[c], inArray[c].Length);
            }
            return returnVal;
        }

        public static T[][][] CloneArray<T>(T[][][] inArray)
        {
            T[][][] returnVal = new T[inArray.Length][][];
            for (int c = 0; c < inArray.Length; c++)
            {
                returnVal[c] = new T[inArray[c].Length][];
                for (int a = 0; a < inArray[c].Length; a++)
                {
                    returnVal[c][a] = new T[inArray[c][a].Length];
                    Array.Copy(inArray[c][a], returnVal[c][a], inArray[c][a].Length);
                }
            }
            return returnVal;
        }

        public static Pointer<T> CloneArray<T>(Pointer<T> inArray, int arrayLength)
        {
            Pointer<T> returnVal = Pointer.Malloc<T>(arrayLength);
            inArray.MemCopyTo(returnVal, arrayLength);
            return returnVal;
        }

        //FIXME: For the most part this method is used to zero-out arrays, which is usually already done by the runtime.
        public static void MemSet<T>(T[] array, T value)
        {
            for (int c = 0; c < array.Length; c++)
            {
                array[c] = value;
            }
        }

        public static void MemSet<T>(T[] array, T value, int length)
        {
            for (int c = 0; c < length; c++)
            {
                array[c] = value;
            }
        }
    }
}
