/*
Copyright (c) 2006-2011 Skype Limited. All Rights Reserved

	Ported to Java by Logan Stromberg

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions
	are met:

	- Redistributions of source code must retain the above copyright
	notice, this list of conditions and the following disclaimer.

	- Redistributions in binary form must reproduce the above copyright
	notice, this list of conditions and the following disclaimer in the
	documentation and/or other materials provided with the distribution.

	- Neither the name of Internet Society, IETF or IETF Trust, nor the
	names of specific contributors, may be used to endorse or promote
	products derived from this software without specific prior written
	permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
	``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
	LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
	A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
	OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
	EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
	PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
	PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
	LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
	NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
	SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
package opus

///
/// <summary>
/// Struct for Packet Loss Concealment
/// </summary>
///
type PLCStruct struct {
	pitchL_Q8         int
	LTPCoef_Q14       []int16
	prevLPC_Q12       []int16
	last_frame_lost   int
	rand_seed         int
	randScale_Q14     int16
	conc_energy       int
	conc_energy_shift int
	prevLTP_scale_Q14 int16
	prevGain_Q16      []int
	fs_kHz            int
	nb_subfr          int
	subfr_length      int
}

func NewPLCStruct() *PLCStruct {
	obj := &PLCStruct{}
	obj.LTPCoef_Q14 = make([]int16, SilkConstants.LTP_ORDER)
	obj.prevLPC_Q12 = make([]int16, SilkConstants.MAX_LPC_ORDER)
	obj.prevGain_Q16 = make([]int, 2)
	return obj
}
func (p *PLCStruct) Reset() {
	p.pitchL_Q8 = 0
	MemSetLen(p.LTPCoef_Q14, 0, SilkConstants.LTP_ORDER)
	MemSetLen(p.prevLPC_Q12, 0, SilkConstants.MAX_LPC_ORDER)
	p.last_frame_lost = 0
	p.rand_seed = 0
	p.randScale_Q14 = 0
	p.conc_energy = 0
	p.conc_energy_shift = 0
	p.prevLTP_scale_Q14 = 0
	MemSetLen(p.prevGain_Q16, 0, 2)
	p.fs_kHz = 0
	p.nb_subfr = 0
	p.subfr_length = 0
}
