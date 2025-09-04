/*
 * Copyright (c) 2006-2011 Skype Limited. All Rights Reserved
 * Ported to Java by Logan Stromberg
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * - Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above copyright
 * notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 *
 * - Neither the name of Internet Society, IETF or IETF Trust, nor the
 * names of specific contributors, may be used to endorse or promote
 * products derived from this software without specific prior written
 * permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
 * OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
package opus

type CNGState struct {
	CNG_exc_buf_Q14   []int
	CNG_smth_NLSF_Q15 []int16
	CNG_synth_state   []int
	CNG_smth_Gain_Q16 int
	rand_seed         int
	fs_kHz            int
}

func NewCNGState() *CNGState {
	return &CNGState{CNG_exc_buf_Q14: make([]int, SilkConstants.MAX_FRAME_LENGTH), CNG_smth_NLSF_Q15: make([]int16, SilkConstants.MAX_LPC_ORDER), CNG_synth_state: make([]int, SilkConstants.MAX_LPC_ORDER)}
}
func (s *CNGState) Reset() {
	MemSetLen(s.CNG_exc_buf_Q14, 0, SilkConstants.MAX_FRAME_LENGTH)
	MemSetLen(s.CNG_smth_NLSF_Q15, 0, SilkConstants.MAX_LPC_ORDER)
	MemSetLen(s.CNG_synth_state, 0, SilkConstants.MAX_LPC_ORDER)
	s.CNG_smth_Gain_Q16 = 0
	s.rand_seed = 0
	s.fs_kHz = 0
}
