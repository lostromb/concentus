using Concentus.Common;
using Concentus.Common.CPlusPlus;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus
{
    /// <summary>
    /// multi-layer perceptron processor
    /// </summary>
    public static class mlp
    {
        private const int MAX_NEURONS = 100;

        public static float tansig_approx(float x)
        {
            int i;
            float y, dy;
            float sign = 1;
            /* Tests are reversed to catch NaNs */
            if (!(x < 8))
                return 1;
            if (!(x > -8))
                return -1;
            if (x < 0)
            {
                x = -x;
                sign = -1;
            }
            i = (int)Math.Floor(.5f + 25 * x);
            x -= .04f * i;
            y = Tables.tansig_table[i];
            dy = 1 - y * y;
            y = y + x * dy * (1 - y * x);
            return sign * y;
        }

        public static void mlp_process(MLP m, Pointer<float> input, Pointer<float> output)
        {
            int j;
            float[] hidden = new float[MAX_NEURONS];
            Pointer<float> W = m.weights;

            /* Copy to tmp_in */

            for (j = 0; j < m.topo[1]; j++)
            {
                int k;
                float sum = W[0];
                W = W.Point(1);
                for (k = 0; k < m.topo[0]; k++)
                {
                    sum = sum + input[k] * W[0];
                    W = W.Point(1);
                }
                hidden[j] = tansig_approx(sum);
            }

            for (j = 0; j < m.topo[2]; j++)
            {
                int k;
                float sum = W[0];
                W = W.Point(1);
                for (k = 0; k < m.topo[1]; k++)
                {
                    sum = sum + hidden[k] * W[0];
                    W = W.Point(1);
                }
                output[j] = tansig_approx(sum);
            }
        }
    }
}
