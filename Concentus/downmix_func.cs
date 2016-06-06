using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    public static class downmix_func_def
    {
        /// <summary>
        /// fixme: refactor this out
        /// </summary>
        /// <typeparam name="T">The type of signal being handled (either short or float)</typeparam>
        /// <param name="_x"></param>
        /// <param name="sub"></param>
        /// <param name="subframe"></param>
        /// <param name="offset"></param>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="C"></param>
        public delegate void downmix_func<T>(Pointer<T> _x, Pointer<int> sub, int subframe, int offset, int c1, int c2, int C);
    }
}
