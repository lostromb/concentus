using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Common
{
    public static class celt_inner_prod
    {
        public static int celt_inner_prod_c(Pointer<short> x, Pointer<short> y, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[i], y[i]);
            return xy;
        }

        public static int celt_inner_prod_c(Pointer<int> x, Pointer<int> y, int N)
        {
            int i;
            int xy = 0;
            for (i = 0; i < N; i++)
                xy = Inlines.MAC16_16(xy, x[i], y[i]);
            return xy;
        }

        public static void dual_inner_prod_c(Pointer<short> x, Pointer<short> y01, Pointer<short> y02, int N, BoxedValue<int> xy1, BoxedValue<int> xy2)
        {
            int i;
            int xy01 = 0;
            int xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[i], y01[i]);
                xy02 = Inlines.MAC16_16(xy02, x[i], y02[i]);
            }
            xy1.Val = xy01;
            xy2.Val = xy02;
        }

        public static void dual_inner_prod_c(Pointer<int> x, Pointer<int> y01, Pointer<int> y02, int N, BoxedValue<int> xy1, BoxedValue<int> xy2)
        {
            int i;
            int xy01 = 0;
            int xy02 = 0;
            for (i = 0; i < N; i++)
            {
                xy01 = Inlines.MAC16_16(xy01, x[i], y01[i]);
                xy02 = Inlines.MAC16_16(xy02, x[i], y02[i]);
            }
            xy1.Val = xy01;
            xy2.Val = xy02;
        }
    }
}
