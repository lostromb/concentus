package opus

const ALLOC_STEPS = 6

var LOG2_FRAC_TABLE = [25]byte{
	0,
	8, 13,
	16, 19, 21, 23,
	24, 26, 27, 28, 29, 30, 31, 32,
	32, 33, 34, 34, 35, 36, 36, 37, 37,
}

func get_pulses(i int) int {
	if i < 8 {
		return i
	}
	return (8 + (i & 7)) << ((i >> 3) - 1)
}

func bits2pulses(m *CeltMode, band int, LM int, bits int) int {
	LM++
	cache := m.cache.bits
	cache_ptr := int(m.cache.index[LM*m.nbEBands+band])

	lo := 0
	hi := int(cache[cache_ptr])
	bits--
	for i := 0; i < CeltConstants.LOG_MAX_PSEUDO; i++ {
		mid := (lo + hi + 1) >> 1
		if int(cache[cache_ptr+mid]) >= bits {
			hi = mid
		} else {
			lo = mid
		}
	}
	var lowVal int
	if lo == 0 {
		lowVal = -1
	} else {
		lowVal = int(cache[cache_ptr+lo])
	}
	if bits-lowVal <= int(cache[cache_ptr+hi])-bits {
		return lo
	}
	return hi
}

func pulses2bits(m *CeltMode, band int, LM int, pulses int) int {
	LM++
	if pulses == 0 {
		return 0
	}
	return int(m.cache.bits[int(m.cache.index[LM*m.nbEBands+band])+pulses] + 1)
}
func interp_bits2pulses(m *CeltMode, start int, end int, skip_start int, bits1 []int, bits2 []int, thresh []int, cap []int, total int, _balance *BoxedValueInt, skip_rsv int, intensity *BoxedValueInt, intensity_rsv int, dual_stereo *BoxedValueInt, dual_stereo_rsv int, bits []int, ebits []int, fine_priority []int, C int, LM int, ec *EntropyCoder, encode int, prev int, signalBandwidth int) int {
	var psum int
	var lo, hi int
	var i, j int
	var logM int
	var stereo int
	codedBands := -1
	var alloc_floor int
	var left, percoeff int
	var done int
	var balance int
	alloc_floor = C << BITRES
	stereo = 0
	if C > 1 {
		stereo = 1
	}

	logM = LM << BITRES
	lo = 0
	hi = 1 << ALLOC_STEPS
	for i = 0; i < ALLOC_STEPS; i++ {
		mid := (lo + hi) >> 1
		psum = 0
		done = 0
		for j = end - 1; j >= start; j-- {
			tmp := bits1[j] + (mid * bits2[j] >> ALLOC_STEPS)
			if tmp >= thresh[j] || done != 0 {
				done = 1
				psum += IMIN(tmp, cap[j])
			} else if tmp >= alloc_floor {
				psum += alloc_floor
			}
		}
		if psum > total {
			hi = mid
		} else {
			lo = mid
		}
	}
	psum = 0
	done = 0
	for j = end - 1; j >= start; j-- {
		tmp := bits1[j] + (lo * bits2[j] >> ALLOC_STEPS)
		if tmp < thresh[j] && done == 0 {
			if tmp >= alloc_floor {
				tmp = alloc_floor
			} else {
				tmp = 0
			}
		} else {
			done = 1
		}
		tmp = IMIN(tmp, cap[j])
		bits[j] = tmp
		psum += tmp
	}

	codedBands = end
	for {
		var band_width int
		var band_bits int
		var rem int
		j = codedBands - 1
		if j <= skip_start {
			total += skip_rsv
			break
		}

		left = total - psum
		percoeff = celt_udiv(left, int(m.eBands[codedBands]-m.eBands[start]))
		left -= int(m.eBands[codedBands]-m.eBands[start]) * percoeff
		rem = IMAX(left-int(m.eBands[j]-m.eBands[start]), 0)
		band_width = int(m.eBands[codedBands] - m.eBands[j])
		band_bits = bits[j] + percoeff*band_width + rem
		if band_bits >= IMAX(thresh[j], alloc_floor+(1<<BITRES)) {
			if encode != 0 {
				if codedBands <= start+2 || (band_bits > ((func() int {
					if j < prev {
						return 7
					}
					return 9
				}())*band_width<<LM<<BITRES)>>4 && j <= signalBandwidth) {
					ec.enc_bit_logp(1, 1)
					break
				}
				ec.enc_bit_logp(0, 1)
			} else if ec.dec_bit_logp(1) != 0 {
				break
			}
			psum += 1 << BITRES
			band_bits -= 1 << BITRES
		}
		psum -= bits[j] + intensity_rsv
		if intensity_rsv > 0 {
			intensity_rsv = int(LOG2_FRAC_TABLE[j-start])
		}
		psum += intensity_rsv
		if band_bits >= alloc_floor {
			psum += alloc_floor
			bits[j] = alloc_floor
		} else {
			bits[j] = 0
		}
		codedBands--
	}

	OpusAssert(codedBands > start)
	if intensity_rsv > 0 {
		if encode != 0 {
			if intensity.Val > codedBands {
				intensity.Val = codedBands
			}
			ec.enc_uint(int64(intensity.Val-start), int64(codedBands+1-start))
		} else {
			intensity.Val = start + int(ec.dec_uint(int64(codedBands+1-start)))
		}
	} else {
		intensity.Val = 0
	}

	if intensity.Val <= start {
		total += dual_stereo_rsv
		dual_stereo_rsv = 0
	}
	if dual_stereo_rsv > 0 {
		if encode != 0 {
			ec.enc_bit_logp(dual_stereo.Val, 1)
		} else {
			dual_stereo.Val = ec.dec_bit_logp(1)
		}
	} else {
		dual_stereo.Val = 0
	}

	left = total - psum
	percoeff = celt_udiv(left, int(m.eBands[codedBands]-m.eBands[start]))
	left -= int(m.eBands[codedBands]-m.eBands[start]) * percoeff
	for j = start; j < codedBands; j++ {
		bits[j] += percoeff * int(m.eBands[j+1]-m.eBands[j])
	}
	for j = start; j < codedBands; j++ {
		tmp := IMIN(left, int(m.eBands[j+1]-m.eBands[j]))
		bits[j] += tmp
		left -= tmp
	}

	balance = 0
	for j = start; j < codedBands; j++ {
		var N0, N, den int
		var offset int
		var NClogN int
		var excess, bit int

		OpusAssert(bits[j] >= 0)
		N0 = int(m.eBands[j+1] - m.eBands[j])
		N = N0 << LM
		bit = bits[j] + balance

		if N > 1 {
			excess = MAX32(bit-cap[j], 0)
			bits[j] = bit - excess

			den = C * N
			if C == 2 && N > 2 && dual_stereo.Val == 0 && j < intensity.Val {
				den++
			}

			NClogN = den * (int(m.logN[j]) + logM)

			offset = (NClogN >> 1) - den*CeltConstants.FINE_OFFSET

			if N == 2 {
				offset += den << BITRES >> 2
			}

			if bits[j]+offset < den*2<<BITRES {
				offset += NClogN >> 2
			} else if bits[j]+offset < den*3<<BITRES {
				offset += NClogN >> 3
			}

			ebits[j] = IMAX(0, bits[j]+offset+(den<<(BITRES-1)))
			ebits[j] = celt_udiv(ebits[j], den) >> BITRES

			if C*ebits[j] > (bits[j] >> BITRES) {
				ebits[j] = bits[j] >> stereo >> BITRES
			}

			ebits[j] = IMIN(ebits[j], CeltConstants.MAX_FINE_BITS)

			if ebits[j]*(den<<BITRES) >= bits[j]+offset {
				fine_priority[j] = 1
			} else {
				fine_priority[j] = 0
			}

			bits[j] -= C * ebits[j] << BITRES

		} else {
			excess = MAX32(0, bit-(C<<BITRES))
			bits[j] = bit - excess
			ebits[j] = 0
			fine_priority[j] = 1
		}

		if excess > 0 {
			extra_fine := IMIN(excess>>(stereo+BITRES), CeltConstants.MAX_FINE_BITS-ebits[j])
			ebits[j] += extra_fine
			extra_bits := extra_fine * C << BITRES
			if extra_bits >= excess-balance {
				fine_priority[j] = 1
			} else {
				fine_priority[j] = 0
			}
			excess -= extra_bits
		}
		balance = excess

		OpusAssert(bits[j] >= 0)
		OpusAssert(ebits[j] >= 0)
	}
	_balance.Val = balance

	for ; j < end; j++ {
		ebits[j] = bits[j] >> stereo >> BITRES
		OpusAssert(C*ebits[j]<<BITRES == bits[j])
		bits[j] = 0
		if ebits[j] < 1 {
			fine_priority[j] = 1
		} else {
			fine_priority[j] = 0
		}
	}

	return codedBands
}

func compute_allocation(m *CeltMode, start int, end int, offsets []int, cap []int, alloc_trim int, intensity *BoxedValueInt, dual_stereo *BoxedValueInt, total int, balance *BoxedValueInt, pulses []int, ebits []int, fine_priority []int, C int, LM int, ec *EntropyCoder, encode int, prev int, signalBandwidth int) int {
	total = IMAX(total, 0)
	len := m.nbEBands
	skip_start := start
	skip_rsv := 0
	if total >= 1<<BITRES {
		skip_rsv = 1 << BITRES
	}
	total -= skip_rsv
	intensity_rsv := 0
	dual_stereo_rsv := 0
	if C == 2 {
		intensity_rsv = int(LOG2_FRAC_TABLE[end-start])
		if intensity_rsv > total {
			intensity_rsv = 0
		} else {
			total -= intensity_rsv
			if total >= 1<<BITRES {
				dual_stereo_rsv = 1 << BITRES
			}
			total -= dual_stereo_rsv
		}
	}

	bits1 := make([]int, len)
	bits2 := make([]int, len)
	thresh := make([]int, len)
	trim_offset := make([]int, len)

	for j := start; j < end; j++ {
		thresh[j] = IMAX(C<<BITRES, int(3*(m.eBands[j+1]-m.eBands[j])<<LM<<BITRES)>>4)
		trim_offset[j] = C * int(m.eBands[j+1]-m.eBands[j]) * (alloc_trim - 5 - LM) * (end - j - 1) * (1 << (LM + BITRES)) >> 6
		if (m.eBands[j+1]-m.eBands[j])<<LM == 1 {
			trim_offset[j] -= C << BITRES
		}
	}
	lo := 1
	hi := m.nbAllocVectors - 1
	for lo <= hi {
		done := 0
		psum := 0
		mid := (lo + hi) >> 1
		for j := end - 1; j >= start; j-- {
			N := int(m.eBands[j+1] - m.eBands[j])

			bitsj := int(C*N) * int(m.allocVectors[mid*len+j]) << LM >> 2

			if bitsj > 0 {
				bitsj = IMAX(0, bitsj+trim_offset[j])
			}
			bitsj += offsets[j]
			if bitsj >= thresh[j] || done != 0 {
				done = 1
				psum += IMIN(bitsj, cap[j])
			} else if bitsj >= C<<BITRES {
				psum += C << BITRES
			}
		}
		if psum > total {
			hi = mid - 1
		} else {
			lo = mid + 1
		}
	}
	hi = lo
	lo = hi - 1
	for j := start; j < end; j++ {
		N := int(m.eBands[j+1] - m.eBands[j])
		bits1j := C * N * int(m.allocVectors[lo*len+j]) << LM >> 2
		bits2j := cap[j]
		if hi < m.nbAllocVectors {
			bits2j = C * N * int(m.allocVectors[hi*len+j]) << LM >> 2
		}
		if bits1j > 0 {
			bits1j = IMAX(0, bits1j+trim_offset[j])
		}
		if bits2j > 0 {
			bits2j = IMAX(0, bits2j+trim_offset[j])
		}
		if lo > 0 {
			bits1j += offsets[j]
		}
		bits2j += offsets[j]
		if offsets[j] > 0 {
			skip_start = j
		}
		bits2j = IMAX(0, bits2j-bits1j)
		bits1[j] = bits1j
		bits2[j] = bits2j
	}

	codedBands := interp_bits2pulses(m, start, end, skip_start, bits1, bits2, thresh, cap, total, balance, skip_rsv, intensity, intensity_rsv, dual_stereo, dual_stereo_rsv, pulses, ebits, fine_priority, C, LM, ec, encode, prev, signalBandwidth)
	return codedBands
}
