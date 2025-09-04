// Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
// Ported to Java by Logan Stromberg
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//
// - Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
//
// - Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// - Neither the name of Internet Society, IETF or IETF Trust, nor the
// names of specific contributors, may be used to endorse or promote
// products derived from this software without specific prior written
// permission.
//
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
package opus

func silk_sum_sqr_shift5(energy *BoxedValueInt, shift *BoxedValueInt, x []int16, x_ptr int, _len int) {
	var i int
	var shft int32
	var nrg_tmp, nrg int32

	nrg = 0
	shft = 0
	_len--

	for i = 0; i < _len; i += 2 {
		nrg = (silk_SMLABB_ovflw(int32(nrg), int32(x[x_ptr+i]), int32(x[x_ptr+i])))
		nrg = (silk_SMLABB_ovflw(int32(nrg), int32(x[x_ptr+i+1]), int32(x[x_ptr+i+1])))
		if nrg < 0 {
			/* Scale down */
			nrg = int32(silk_RSHIFT_uint(int64(nrg), 2))
			shft = 2
			i += 2
			break
		}
	}

	for ; i < _len; i += 2 {
		nrg_tmp = int32(silk_SMULBB(int(x[x_ptr+i]), int(x[x_ptr+i])))
		nrg_tmp = silk_SMLABB_ovflw(nrg_tmp, int32(x[x_ptr+i+1]), int32(x[x_ptr+i+1]))
		nrg = int32(silk_ADD_RSHIFT_uint(int64(nrg), int64(nrg_tmp), int(shft)))

		if nrg < 0 {
			/* Scale down */
			nrg = int32(silk_RSHIFT_uint(int64(nrg), 2))
			shft += 2
		}
	}

	if i == _len {
		/* One sample left to process */
		nrg_tmp = int32(silk_SMULBB(int(x[x_ptr+i]), int(x[x_ptr+i])))
		nrg = int32(silk_ADD_RSHIFT_uint(int64(nrg), int64(nrg_tmp), int(shft)))

	}

	/* Make sure to have at least one extra leading zero (two leading zeros in total) */
	if (int(nrg) & 0xC0000000) != 0 {
		nrg = int32(silk_RSHIFT_uint(int64(nrg), 2))
		shft += 2
	}

	/* Output arguments */
	shift.Val = int(shft)
	energy.Val = int(nrg)
}

func silk_sum_sqr_shift4(energy *BoxedValueInt, shift *BoxedValueInt, x []int16, len int) {

	var i, shft int
	var nrg_tmp, nrg int

	nrg = 0
	shft = 0
	len--

	for i = 0; i < len; i += 2 {
		nrg = int(int32(silk_SMLABB_ovflw(int32(nrg), int32(x[i]), int32(x[i]))))
		nrg = int(int32(silk_SMLABB_ovflw(int32(nrg), int32(x[i+1]), int32(x[i+1]))))
		if nrg < 0 {
			/* Scale down */
			nrg = int(int32(silk_RSHIFT_uint(int64(nrg), 2)))
			shft = 2
			i += 2
			break
		}
	}

	for ; i < len; i += 2 {
		nrg_tmp = int(int32(silk_SMULBB(int(x[i]), int(x[i]))))
		nrg_tmp = int(int32(silk_SMLABB_ovflw(int32(nrg_tmp), int32(x[i+1]), int32(x[i+1]))))
		nrg = int(int32(silk_ADD_RSHIFT_uint(int64(nrg), int64(nrg_tmp), shft)))
		if nrg < 0 {
			/* Scale down */
			nrg = int(silk_RSHIFT_uint(int64(nrg), 2))
			shft += 2
		}
	}

	if i == len {
		/* One sample left to process */
		nrg_tmp = int(int32(silk_SMULBB(int(x[i]), int(x[i]))))
		nrg = int(int32(silk_ADD_RSHIFT_uint(int64(nrg), int64(nrg_tmp), shft)))
	}

	/* Make sure to have at least one extra leading zero (two leading zeros in total) */
	if (nrg & 0xC0000000) != 0 {
		nrg = int(int32(silk_RSHIFT_uint(int64(nrg), 2)))
		shft += 2
	}

	/* Output arguments */
	shift.Val = shft
	energy.Val = int(nrg)
}
