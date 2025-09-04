// Copyright (c) 2007-2008 CSIRO
// Copyright (c) 2007-2011 Xiph.Org Foundation
// Originally written by Jean-Marc Valin, Gregory Maxwell, Koen Vos,
// Timothy B. Terriberry, and the Opus open-source contributors
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

const (
	OPUS_BANDWIDTH_UNKNOWN int = iota
	OPUS_BANDWIDTH_AUTO
	OPUS_BANDWIDTH_NARROWBAND
	OPUS_BANDWIDTH_MEDIUMBAND
	OPUS_BANDWIDTH_WIDEBAND
	OPUS_BANDWIDTH_SUPERWIDEBAND
	OPUS_BANDWIDTH_FULLBAND
)

func OpusBandwidthHelpers_GetOrdinal(bw int) int {
	switch bw {
	case OPUS_BANDWIDTH_NARROWBAND:
		return 1
	case OPUS_BANDWIDTH_MEDIUMBAND:
		return 2
	case OPUS_BANDWIDTH_WIDEBAND:
		return 3
	case OPUS_BANDWIDTH_SUPERWIDEBAND:
		return 4
	case OPUS_BANDWIDTH_FULLBAND:
		return 5
	}
	return -1
}

func OpusBandwidthHelpers_GetBandwidth(ordinal int) int {
	switch ordinal {
	case 1:
		return OPUS_BANDWIDTH_NARROWBAND
	case 2:
		return OPUS_BANDWIDTH_MEDIUMBAND
	case 3:
		return OPUS_BANDWIDTH_WIDEBAND
	case 4:
		return OPUS_BANDWIDTH_SUPERWIDEBAND
	case 5:
		return OPUS_BANDWIDTH_FULLBAND
	}
	return OPUS_BANDWIDTH_AUTO
}

func SUBTRACT(a int, b int) int {
	return OpusBandwidthHelpers_GetBandwidth(OpusBandwidthHelpers_GetOrdinal(a) - b)
}

func OpusBandwidthHelpers_MIN(a int, b int) int {
	if OpusBandwidthHelpers_GetOrdinal(a) < OpusBandwidthHelpers_GetOrdinal(b) {
		return a
	}
	return b
}

func OpusBandwidthHelpers_MAX(a int, b int) int {
	if OpusBandwidthHelpers_GetOrdinal(a) > OpusBandwidthHelpers_GetOrdinal(b) {
		return a
	}
	return b
}
