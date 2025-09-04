package opus

/* Copyright (c) 2007-2008 CSIRO
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

type laplace struct{}

var Laplace laplace

const (
	LAPLACE_LOG_MINP = 0
	LAPLACE_MINP     = 1 << LAPLACE_LOG_MINP
	LAPLACE_NMIN     = 16
)

func (l *laplace) ec_laplace_get_freq1(fs0 int64, decay int) int64 {
	ft := CapToUInt32(32768 - LAPLACE_MINP*(2*LAPLACE_NMIN) - fs0)
	return (CapToUInt32(ft*(16384-int64(decay))) >> 15)
}

func (l *laplace) ec_laplace_encode(enc *EntropyCoder, value *BoxedValueInt, fs int64, decay int) {
	var fl int64
	val := int64(value.Val)
	fl = 0
	if val != 0 {
		var s int64
		var i int64
		s = 0
		if val < 0 {
			s = -1
		}
		val = (val + s) ^ s
		fl = fs
		fs = l.ec_laplace_get_freq1(fs, decay)

		/* Search the decaying part of the PDF.*/
		for i = 1; fs > 0 && i < val; i++ {
			fs *= 2
			fl = CapToUInt32(fl + fs + 2*LAPLACE_MINP)
			fs = CapToUInt32(fs * int64(decay) >> 15)
		}

		/* Everything beyond that has probability LAPLACE_MINP. */
		if fs == 0 {
			var di int64
			var ndi_max int64
			ndi_max = int64(32768-fl+LAPLACE_MINP-1) >> LAPLACE_LOG_MINP
			ndi_max = (ndi_max - s) >> 1
			di = IMINLong((val)-i, ndi_max-1)
			fl = CapToUInt32(fl + int64(2*di+1+s)*LAPLACE_MINP)
			fs = IMINLong(LAPLACE_MINP, 32768-fl)
			value.Val = int((i + di + s) ^ s)
		} else {
			fs += LAPLACE_MINP
			fl = fl + CapToUInt32(fs&^int64(s))
		}
		OpusAssert(fl+fs <= 32768)
		OpusAssert(fs > 0)
	}

	enc.encode_bin(fl, (fl + fs), 15)
}

func (l *laplace) ec_laplace_decode(dec *EntropyCoder, fs int64, decay int) int {
	val := 0
	fm := dec.decode_bin(15)
	fl := int64(0)

	if fm >= int64(fs) {
		val++
		fl = fs
		fs = l.ec_laplace_get_freq1(fs, decay) + LAPLACE_MINP
		for fs > LAPLACE_MINP && fm >= int64(fl+2*fs) {
			fs *= 2
			fl = CapToUInt32(fl + fs)
			fs = CapToUInt32((fs-2*LAPLACE_MINP)*int64(decay))>>15 + LAPLACE_MINP
			val++
		}
		if fs <= LAPLACE_MINP {
			di := int(fm-int64(fl)) >> (LAPLACE_LOG_MINP + 1)
			val += di
			fl = CapToUInt32(fl + CapToUInt32(2*int64(di)*LAPLACE_MINP))
		}
		if fm < int64(fl+fs) {
			val = -val
		} else {
			fl = CapToUInt32(fl + fs)
		}
	}

	OpusAssert(fl < 32768)
	OpusAssert(fs > 0)
	OpusAssert(int64(fl) <= fm)
	OpusAssert(fm < int64(IMIN(int(fl+fs), 32768)))

	dec.dec_update(int64(fl), int64(IMIN(int(fl+fs), 32768)), 32768)
	return val
}
