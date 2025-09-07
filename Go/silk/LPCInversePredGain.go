// Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
// Ported to Java by Logan Stromberg

// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:

// - Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.

// - Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.

// - Neither the name of Internet Society, IETF or IETF Trust, nor the
// names of specific contributors, may be used to endorse or promote
// products derived from this software without specific prior written
// permission.

// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// “AS IS” AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
// OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
package silk

import (
	"math"

	"github.com/lostromb/concentus/go/comm/arrayUtil"
)

const QA24 = 24

func LPC_inverse_pred_gain_QA(A_QA [][]int, order int) int {
	A_LIMIT := int(math.Trunc(0.99975*float64(int(1)<<QA24) + 0.5))

	var k, n, mult2Q int
	var invGain_Q30, rc_Q31, rc_mult1_Q30, rc_mult2, tmp_QA int
	currentRowIndex := order & 1

	invGain_Q30 = 1 << 30
	for k = order - 1; k > 0; k-- {
		if A_QA[currentRowIndex][k] > A_LIMIT || A_QA[currentRowIndex][k] < -A_LIMIT {
			return 0
		}

		rc_Q31 = 0 - inlines.Silk_LSHIFT(A_QA[currentRowIndex][k], 31-QA24)

		rc_mult1_Q30 = (1 << 30) - inlines.Silk_SMMUL(rc_Q31, rc_Q31)
		inlines.OpusAssert(rc_mult1_Q30 > (1 << 15))
		inlines.OpusAssert(rc_mult1_Q30 <= (1 << 30))

		mult2Q = 32 - inlines.Silk_CLZ32(inlines.Silk_abs(rc_mult1_Q30))
		rc_mult2 = inlines.Silk_INVERSE32_varQ(rc_mult1_Q30, mult2Q+30)

		invGain_Q30 = inlines.Silk_LSHIFT(inlines.Silk_SMMUL(invGain_Q30, rc_mult1_Q30), 2)
		inlines.OpusAssert(invGain_Q30 >= 0)
		inlines.OpusAssert(invGain_Q30 <= (1 << 30))

		nextRowIndex := k & 1
		for n = 0; n < k; n++ {
			tmp_QA = A_QA[currentRowIndex][n] - inlines.MUL32_FRAC_Q(A_QA[currentRowIndex][k-n-1], rc_Q31, 31)
			A_QA[nextRowIndex][n] = inlines.MUL32_FRAC_Q(tmp_QA, rc_mult2, mult2Q)
		}

		currentRowIndex = nextRowIndex
	}

	if A_QA[currentRowIndex][0] > A_LIMIT || A_QA[currentRowIndex][0] < -A_LIMIT {
		return 0
	}

	rc_Q31 = 0 - inlines.Silk_LSHIFT(A_QA[currentRowIndex][0], 31-QA24)
	rc_mult1_Q30 = (1 << 30) - inlines.Silk_SMMUL(rc_Q31, rc_Q31)

	invGain_Q30 = inlines.Silk_LSHIFT(inlines.Silk_SMMUL(invGain_Q30, rc_mult1_Q30), 2)
	inlines.OpusAssert(invGain_Q30 >= 0)
	inlines.OpusAssert(invGain_Q30 <= 1<<30)

	return invGain_Q30
}

func silk_LPC_inverse_pred_gain(A_Q12 []int16, order int) int {
	//var Atmp_QA [2][SILK_MAX_ORDER_LPC]int
	var DC_resp int
	Atmp_QA := arrayUtil.InitTwoDimensionalArrayInt(2, SilkConstants.SILK_MAX_ORDER_LPC)
	currentRowIndex := order & 1
	for k := 0; k < order; k++ {
		DC_resp += int(A_Q12[k])
		Atmp_QA[currentRowIndex][k] = inlines.Silk_LSHIFT32(int(A_Q12[k]), QA24-12)
	}
	if DC_resp >= 4096 {
		return 0
	}
	return LPC_inverse_pred_gain_QA(Atmp_QA, order)
}

func silk_LPC_inverse_pred_gain_Q24(A_Q24 []int, order int) int {
	Atmp_QA := arrayUtil.InitTwoDimensionalArrayInt(2, SilkConstants.SILK_MAX_ORDER_LPC)
	currentRowIndex := order & 1
	for k := 0; k < order; k++ {
		Atmp_QA[currentRowIndex][k] = inlines.Silk_RSHIFT32(A_Q24[k], 24-QA24)
	}
	return LPC_inverse_pred_gain_QA(Atmp_QA, order)
}
