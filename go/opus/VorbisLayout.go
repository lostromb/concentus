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
type VorbisLayout struct {
	nb_streams          int
	nb_coupled_streams int
	mapping            []int16
}

/* Index is nb_channel-1*/
var vorbis_mappings = []VorbisLayout{
	{1, 0, []int16{0}},                          /* 1: mono */
	{1, 1, []int16{0, 1}},                       /* 2: stereo */
	{2, 1, []int16{0, 2, 1}},                    /* 3: 1-d surround */
	{2, 2, []int16{0, 1, 2, 3}},                 /* 4: quadraphonic surround */
	{3, 2, []int16{0, 4, 1, 2, 3}},             /* 5: 5-channel surround */
	{4, 2, []int16{0, 4, 1, 2, 3, 5}},           /* 6: 5.1 surround */
	{4, 3, []int16{0, 4, 1, 2, 3, 5, 6}},       /* 7: 6.1 surround */
	{5, 3, []int16{0, 6, 1, 2, 3, 4, 5, 7}},    /* 8: 7.1 surround */
}