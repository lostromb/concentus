package opus

import (
	"math"
)

func silk_VQ_WMat_EC(ind *BoxedValueByte, rate_dist_Q14 *BoxedValueInt, gain_Q7 *BoxedValueInt, in_Q14 []int16, in_Q14_ptr int, W_Q18 []int, W_Q18_ptr int, cb_Q7 [][]int8, cb_gain_Q7 []int16, cl_Q5 []int16, mu_Q9 int, max_gain_Q7 int, L int) {
	var k, gain_tmp_Q7 int
	var cb_row_Q7 []int8
	var cb_row_Q7_ptr = 0
	diff_Q14 := make([]int16, 5)
	var sum1_Q14, sum2_Q16 int

	/* Loop over codebook */
	rate_dist_Q14.Val = math.MaxInt32

	for k = 0; k < L; k++ {
		/* Go to next cbk vector */

		cb_row_Q7 = cb_Q7[cb_row_Q7_ptr]
		cb_row_Q7_ptr++
		gain_tmp_Q7 = int(cb_gain_Q7[k])

		diff_Q14[0] = int16(int(in_Q14[in_Q14_ptr]) - silk_LSHIFT(int(cb_row_Q7[0]), 7))
		diff_Q14[1] = int16(int(in_Q14[in_Q14_ptr+1]) - silk_LSHIFT(int(cb_row_Q7[1]), 7))
		diff_Q14[2] = int16(int(in_Q14[in_Q14_ptr+2]) - silk_LSHIFT(int(cb_row_Q7[2]), 7))
		diff_Q14[3] = int16(int(in_Q14[in_Q14_ptr+3]) - silk_LSHIFT(int(cb_row_Q7[3]), 7))
		diff_Q14[4] = int16(int(in_Q14[in_Q14_ptr+4]) - silk_LSHIFT(int(cb_row_Q7[4]), 7))

		/* Weighted rate */
		sum1_Q14 = silk_SMULBB(mu_Q9, int(cl_Q5[k]))

		/* Penalty for too large gain */
		sum1_Q14 = silk_ADD_LSHIFT32(sum1_Q14, silk_max(silk_SUB32(gain_tmp_Q7, max_gain_Q7), 0), 10)

		OpusAssert(sum1_Q14 >= 0)

		/* first row of W_Q18 */
		sum2_Q16 = silk_SMULWB(W_Q18[W_Q18_ptr+1], int(diff_Q14[1]))
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+2], int(diff_Q14[2]))
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+3], int(diff_Q14[3]))
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+4], int(diff_Q14[4]))
		sum2_Q16 = silk_LSHIFT(sum2_Q16, 1)
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr], int(diff_Q14[0]))

		sum1_Q14 = silk_SMLAWB(sum1_Q14, sum2_Q16, int(diff_Q14[0]))

		/* second row of W_Q18 */
		sum2_Q16 = silk_SMULWB(W_Q18[W_Q18_ptr+7], int(diff_Q14[2]))
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+8], int(diff_Q14[3]))
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+9], int(diff_Q14[4]))
		sum2_Q16 = silk_LSHIFT(sum2_Q16, 1)
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+6], int(diff_Q14[1]))
		sum1_Q14 = silk_SMLAWB(sum1_Q14, sum2_Q16, int(diff_Q14[1]))

		/* third row of W_Q18 */
		sum2_Q16 = silk_SMULWB(W_Q18[W_Q18_ptr+13], int(diff_Q14[3]))
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+14], int(diff_Q14[4]))
		sum2_Q16 = silk_LSHIFT(sum2_Q16, 1)
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+12], int(diff_Q14[2]))
		sum1_Q14 = silk_SMLAWB(sum1_Q14, sum2_Q16, int(diff_Q14[2]))

		/* fourth row of W_Q18 */
		sum2_Q16 = silk_SMULWB(W_Q18[W_Q18_ptr+19], int(diff_Q14[4]))
		sum2_Q16 = silk_LSHIFT(sum2_Q16, 1)
		sum2_Q16 = silk_SMLAWB(sum2_Q16, W_Q18[W_Q18_ptr+18], int(diff_Q14[3]))
		sum1_Q14 = silk_SMLAWB(sum1_Q14, sum2_Q16, int(diff_Q14[3]))

		/* last row of W_Q18 */
		sum2_Q16 = silk_SMULWB(W_Q18[W_Q18_ptr+24], int(diff_Q14[4]))
		sum1_Q14 = silk_SMLAWB(sum1_Q14, sum2_Q16, int(diff_Q14[4]))

		OpusAssert(sum1_Q14 >= 0)

		/* find best */
		if sum1_Q14 < rate_dist_Q14.Val {
			rate_dist_Q14.Val = sum1_Q14
			ind.Val = int8(k)
			gain_Q7.Val = gain_tmp_Q7
		}
	}

}
