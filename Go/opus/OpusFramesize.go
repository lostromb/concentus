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
“AS IS” AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
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

type OpusFramesize int

const (
	// Error state
	OPUS_FRAMESIZE_UNKNOWN OpusFramesize = iota
	// Select frame size from the argument (default)
	OPUS_FRAMESIZE_ARG
	// Use 2.5 ms frames
	OPUS_FRAMESIZE_2_5_MS
	// Use 5 ms frames
	OPUS_FRAMESIZE_5_MS
	// Use 10 ms frames
	OPUS_FRAMESIZE_10_MS
	// Use 20 ms frames
	OPUS_FRAMESIZE_20_MS
	// Use 40 ms frames
	OPUS_FRAMESIZE_40_MS
	// Use 60 ms frames
	OPUS_FRAMESIZE_60_MS
	// Do not use - not fully implemented. Optimize the frame size dynamically.
	OPUS_FRAMESIZE_VARIABLE
)

type opusFramesizeHelpers struct{}

var OpusFramesizeHelpers = &opusFramesizeHelpers{}

func (o *opusFramesizeHelpers) GetOrdinal(size OpusFramesize) int {
	switch size {
	case OPUS_FRAMESIZE_ARG:
		return 1
	case OPUS_FRAMESIZE_2_5_MS:
		return 2
	case OPUS_FRAMESIZE_5_MS:
		return 3
	case OPUS_FRAMESIZE_10_MS:
		return 4
	case OPUS_FRAMESIZE_20_MS:
		return 5
	case OPUS_FRAMESIZE_40_MS:
		return 6
	case OPUS_FRAMESIZE_60_MS:
		return 7
	case OPUS_FRAMESIZE_VARIABLE:
		return 8
	}
	return -1
}
