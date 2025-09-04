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

func silk_insertion_sort_increasing(a []int, idx []int, L int, K int) {
	var value int
	var i, j int

	if !(K > 0) {
		panic("OpusAssert failed: K > 0")
	}
	if !(L > 0) {
		panic("OpusAssert failed: L > 0")
	}
	if !(L >= K) {
		panic("OpusAssert failed: L >= K")
	}

	for i = 0; i < K; i++ {
		idx[i] = i
	}

	for i = 1; i < K; i++ {
		value = a[i]
		j = i - 1
		for j >= 0 && value < a[j] {
			a[j+1] = a[j]
			idx[j+1] = idx[j]
			j--
		}
		a[j+1] = value
		idx[j+1] = i
	}

	for i = K; i < L; i++ {
		value = a[i]
		if value < a[K-1] {
			j = K - 2
			for j >= 0 && value < a[j] {
				a[j+1] = a[j]
				idx[j+1] = idx[j]
				j--
			}
			a[j+1] = value
			idx[j+1] = i
		}
	}
}

func silk_insertion_sort_increasing_all_values_int16(a []int16, L int) {
	var value int16
	var i, j int

	if !(L > 0) {
		panic("OpusAssert failed: L > 0")
	}

	for i = 1; i < L; i++ {
		value = a[i]
		j = i - 1
		for j >= 0 && value < a[j] {
			a[j+1] = a[j]
			j--
		}
		a[j+1] = value
	}
}

func silk_insertion_sort_decreasing_int16(a []int16, idx []int, L int, K int) {
	var i, j int
	var value int16

	if !(K > 0) {
		panic("OpusAssert failed: K > 0")
	}
	if !(L > 0) {
		panic("OpusAssert failed: L > 0")
	}
	if !(L >= K) {
		panic("OpusAssert failed: L >= K")
	}

	for i = 0; i < K; i++ {
		idx[i] = i
	}

	for i = 1; i < K; i++ {
		value = a[i]
		j = i - 1
		for j >= 0 && value > a[j] {
			a[j+1] = a[j]
			idx[j+1] = idx[j]
			j--
		}
		a[j+1] = value
		idx[j+1] = i
	}

	for i = K; i < L; i++ {
		value = a[i]
		if value > a[K-1] {
			j = K - 2
			for j >= 0 && value > a[j] {
				a[j+1] = a[j]
				idx[j+1] = idx[j]
				j--
			}
			a[j+1] = value
			idx[j+1] = i
		}
	}
}
