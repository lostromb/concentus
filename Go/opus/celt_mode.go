/*
Copyright (c) 2007-2008 CSIRO

	Copyright (c) 2007-2011 Xiph.Org Foundation
	Originally written by Jean-Marc Valin, Gregory Maxwell, Koen Vos,
	Timothy B. Terriberry, and the Opus open-source contributors
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

type CeltMode struct {
	Fs             int
	overlap        int
	nbEBands       int
	effEBands      int
	preemph        []int
	eBands         []int16
	maxLM          int
	nbShortMdcts   int
	shortMdctSize  int
	nbAllocVectors int
	allocVectors   []int16
	logN           []int16
	window         []int
	mdct           *MDCTLookup
	cache          *PulseCache
}

var mode48000_960_120 *CeltMode = &CeltMode{
	Fs:             48000,
	overlap:        120,
	nbEBands:       21,
	effEBands:      21,
	preemph:        []int{27853, 0, 4096, 8192},
	eBands:         CeltTables.Eband5ms,
	maxLM:          3,
	nbShortMdcts:   8,
	shortMdctSize:  120,
	nbAllocVectors: 11,
	allocVectors:   CeltTables.Band_allocation,
	logN:           CeltTables.LogN400,
	window:         CeltTables.Window120,
	mdct: &MDCTLookup{
		n:        1920,
		maxshift: 3,
		kfft: [4]*FFTState{
			CeltTables.Fft_state48000_960_0,
			CeltTables.Fft_state48000_960_1,
			CeltTables.Fft_state48000_960_2,
			CeltTables.Fft_state48000_960_3,
		},
		trig: CeltTables.Mdct_twiddles960,
	},
	cache: &PulseCache{
		size:  392,
		index: CeltTables.Cache_index50,
		bits:  CeltTables.Cache_bits50,
		caps:  CeltTables.Cache_caps50,
	},
}
