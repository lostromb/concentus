package opus

var pred_coef = []int{29440, 26112, 21248, 16384}
var beta_coef = []int{30147, 22282, 12124, 6554}

var beta_intra = 4915
var small_energy_icdf = []int16{2, 1, 0}

func loss_distortion(eBands [][]int, oldEBands [][]int, start int, end int, len int, C int) int {
	dist := 0
	for c := 0; c < C; c++ {
		for i := start; i < end; i++ {
			d := int((eBands[c][i] >> 3) - (oldEBands[c][i] >> 3))
			dist += d * d
		}
	}
	minDist := dist >> (2*CeltConstants.DB_SHIFT - 6)
	if minDist < 200 {
		return minDist
	}
	return 200
}

func quant_coarse_energy_impl(m *CeltMode, start int, end int, eBands [][]int, oldEBands [][]int, budget int, tell int, prob_model []int16, error [][]int, enc *EntropyCoder, C int, LM int, intra int, max_decay int, lfe int) int {
	var i, c int
	badness := 0
	prev := [2]int{0, 0}
	var coef int
	var beta int

	if tell+3 <= budget {
		enc.enc_bit_logp(intra, 3)
	}

	if intra != 0 {
		coef = 0
		beta = beta_intra
	} else {
		beta = beta_coef[LM]
		coef = pred_coef[LM]
	}

	for i = start; i < end; i++ {
		c = 0
		for c < C {
			var bits_left int
			var qi, qi0 int
			var q int
			var x int
			var f, tmp int
			var oldE int
			var decay_bound int
			x = eBands[c][i]
			oldE = MAX16Int(-int(0.5+9.0*float32(int(1)<<CeltConstants.DB_SHIFT)), oldEBands[c][i])

			f = SHL32(EXTEND32Int(x), 7) - PSHR32(MULT16_16(coef, oldE), 8) - prev[c]
			qi = (f + (int)(0.5+0.5*float32(int(1)<<(CeltConstants.DB_SHIFT+7)))) >> (CeltConstants.DB_SHIFT + 7)
			decay_bound = int(EXTRACT16(MAX32(-((int)(0.5 + 28.0*float32(int(1)<<CeltConstants.DB_SHIFT))), SUB32(oldEBands[c][i], max_decay))))
			if qi < 0 && x < decay_bound {
				qi += int(SHR16Int(SUB16Int(decay_bound, x), CeltConstants.DB_SHIFT))
				if qi > 0 {
					qi = 0
				}
			}
			qi0 = qi
			tell = enc.tell()
			bits_left = budget - tell - 3*C*(end-i)
			if i != start && bits_left < 30 {
				if bits_left < 24 {
					qi = IMIN(1, qi)
				}
				if bits_left < 16 {
					qi = IMAX(-1, qi)
				}
			}
			if lfe != 0 && i >= 2 {
				qi = IMIN(qi, 0)
			}
			if budget-tell >= 15 {
				pi := 2 * IMIN(i, 20)

				boxed_qi := BoxedValueInt{qi}
				Laplace.ec_laplace_encode(enc, &boxed_qi, int64(prob_model[pi])<<7, int(prob_model[pi+1])<<6)
				qi = boxed_qi.Val
			} else if budget-tell >= 2 {
				qi = IMAX(-1, IMIN(qi, 1))
				sign := 0
				if qi < 0 {
					sign = 1
				}
				enc.enc_icdf((2*qi)^(-sign), small_energy_icdf, 2)
			} else if budget-tell >= 1 {
				qi = IMIN(0, qi)
				enc.enc_bit_logp(-qi, 1)
			} else {
				qi = -1
			}
			error[c][i] = PSHR32(f, 7) - SHL16Int(qi, CeltConstants.DB_SHIFT)
			badness += abs(qi0 - qi)
			q = SHL32(qi, CeltConstants.DB_SHIFT)

			tmp = PSHR32(MULT16_16(coef, oldE), 8) + prev[c] + SHL32(q, 7)
			tmp = MAX32(-int(0.5+28.0*float32(int(1)<<(CeltConstants.DB_SHIFT+7))), tmp)
			oldEBands[c][i] = PSHR32(tmp, 7)
			prev[c] = prev[c] + SHL32(q, 7) - MULT16_16(beta, PSHR32(q, 8))
			c++
		}
	}
	if lfe != 0 {
		return 0
	}
	return badness
}
func quant_coarse_energy(m *CeltMode, start int, end int, effEnd int, eBands [][]int, oldEBands [][]int, budget int, error [][]int, enc *EntropyCoder, C int, LM int, nbAvailableBytes int, force_intra int, delayedIntra *BoxedValueInt, two_pass int, loss_rate int, lfe int) {

	intra := boolToInt(force_intra != 0 || (two_pass == 0 && delayedIntra.Val > 2*C*(end-start) && nbAvailableBytes > (end-start)*C))

	intra_bias := (budget * delayedIntra.Val * loss_rate) / (C * 512)
	new_distortion := loss_distortion(eBands, oldEBands, start, effEnd, m.nbEBands, C)

	tell := enc.tell()
	if tell+3 > budget {
		two_pass = 0
		intra = 0
	}

	max_decay := 16 << CeltConstants.DB_SHIFT
	if end-start > 10 {
		if max_decay > (nbAvailableBytes << (CeltConstants.DB_SHIFT - 3)) {
			max_decay = nbAvailableBytes << (CeltConstants.DB_SHIFT - 3)
		}
	}
	if lfe != 0 {
		max_decay = 3 << CeltConstants.DB_SHIFT
	}
	enc_start_state := EntropyCoder{}
	enc_start_state.Assign(enc)

	oldEBands_intra := make([][]int, C)
	error_intra := make([][]int, C)
	for c := 0; c < C; c++ {
		oldEBands_intra[c] = make([]int, m.nbEBands)
		error_intra[c] = make([]int, m.nbEBands)
		copy(oldEBands_intra[c], oldEBands[c])
	}

	badness1 := 0
	if two_pass != 0 || intra != 0 {
		badness1 = quant_coarse_energy_impl(m, start, end, eBands, oldEBands_intra, budget, tell, CeltTables.E_prob_model[LM][1], error_intra, enc, C, LM, 1, max_decay, lfe)
	}

	if intra == 0 {

		enc_intra_state := EntropyCoder{}
		enc_intra_state.Assign(enc)

		tell_intra := enc.tell_frac()
		nstart_bytes := enc_start_state.range_bytes()
		nintra_bytes := enc_intra_state.range_bytes()
		intra_buf := nstart_bytes
		save_bytes := nintra_bytes - nstart_bytes
		var intra_bits []byte
		if save_bytes > 0 {
			intra_bits = make([]byte, save_bytes)
			copy(intra_bits, enc_intra_state.get_buffer()[intra_buf:intra_buf+save_bytes])
		}

		enc.Assign(&enc_start_state)
		badness2 := quant_coarse_energy_impl(m, start, end, eBands, oldEBands, budget, tell, CeltTables.E_prob_model[LM][0], error, enc, C, LM, 0, max_decay, lfe)

		if two_pass != 0 && (badness1 < badness2 || (badness1 == badness2 && enc.tell_frac()+intra_bias > tell_intra)) {
			enc.Assign(&enc_intra_state)
			if save_bytes > 0 {
				enc.write_buffer(intra_bits, 0, intra_buf, int(save_bytes))
			}
			for c := 0; c < C; c++ {
				copy(oldEBands[c], oldEBands_intra[c])
				copy(error[c], error_intra[c])
			}
			intra = 1
		}
	} else {
		for c := 0; c < C; c++ {
			copy(oldEBands[c], oldEBands_intra[c])
			copy(error[c], error_intra[c])
		}
	}

	if intra != 0 {
		delayedIntra.Val = new_distortion
	} else {
		delayedIntra.Val = ADD32(MULT16_32_Q15Int(MULT16_16_Q15Int(pred_coef[LM], pred_coef[LM]), delayedIntra.Val),
			new_distortion)
	}
}

func quant_fine_energy(m *CeltMode, start int, end int, oldEBands [][]int, error [][]int, fine_quant []int, enc *EntropyCoder, C int) {
	for i := start; i < end; i++ {
		frac := 1 << fine_quant[i]
		if fine_quant[i] <= 0 {
			continue
		}
		for c := 0; c < C; c++ {
			q2 := (error[c][i] + (1 << (CeltConstants.DB_SHIFT - 1))) >> (CeltConstants.DB_SHIFT - fine_quant[i])
			if q2 > frac-1 {
				q2 = frac - 1
			}
			if q2 < 0 {
				q2 = 0
			}
			enc.enc_bits(int64(q2), fine_quant[i])
			offset := ((q2 << CeltConstants.DB_SHIFT) + (1 << (CeltConstants.DB_SHIFT - 1))) >> fine_quant[i]
			offset -= 1 << (CeltConstants.DB_SHIFT - 1)
			oldEBands[c][i] += int(offset)
			error[c][i] -= offset
		}
	}
}

func quant_energy_finalise(m *CeltMode, start int, end int, oldEBands [][]int, error [][]int, fine_quant []int, fine_priority []int, bits_left int, enc *EntropyCoder, C int) {
	for prio := 0; prio < 2; prio++ {
		for i := start; i < end && bits_left >= C; i++ {
			if fine_quant[i] >= CeltConstants.MAX_FINE_BITS || fine_priority[i] != prio {
				continue
			}
			for c := 0; c < C; c++ {
				q2 := 0
				if error[c][i] >= 0 {
					q2 = 1
				}
				enc.enc_bits(int64(q2), 1)
				offset := (q2<<CeltConstants.DB_SHIFT - (1 << (CeltConstants.DB_SHIFT - 1))) >> (fine_quant[i] + 1)
				oldEBands[c][i] += int(offset)
				bits_left--
			}
		}
	}
}

func unquant_coarse_energy(m *CeltMode, start int, end int, oldEBands []int, intra int, dec *EntropyCoder, C int, LM int) {
	prob_model := e_prob_model[LM][intra]
	var i, c int
	var prev = []int{0, 0}
	var coef int
	var beta int
	var budget int
	var tell int

	if intra != 0 {
		coef = 0
		beta = beta_intra
	} else {
		beta = beta_coef[LM]
		coef = pred_coef[LM]
	}

	budget = dec.storage * 8

	/* Decode at a fixed coarse resolution */
	for i = start; i < end; i++ {
		c = 0
		for {
			var qi int
			var q int
			var tmp int
			/* It would be better to express this invariant as a
			   test on C at function entry, but that isn't enough
			   to make the static analyzer happy. */
			OpusAssert(c < 2)
			tell = dec.tell()
			if budget-tell >= 15 {
				var pi int
				pi = 2 * IMIN(i, 20)
				qi = Laplace.ec_laplace_decode(dec,
					int64(prob_model[pi])<<7, int(prob_model[pi+1])<<6)
			} else if budget-tell >= 2 {
				qi = dec.dec_icdf(small_energy_icdf, 2)
				qi = (qi >> 1) ^ -(qi & 1)
			} else if budget-tell >= 1 {
				qi = 0 - dec.dec_bit_logp(1)
			} else {
				qi = -1
			}
			q = SHL32(qi, CeltConstants.DB_SHIFT) // opus bug: useless extend32

			oldEBands[i+c*m.nbEBands] = MAX16Int(int(0-(0.5+(9.0)*float64(int(1)<<(CeltConstants.DB_SHIFT)))), oldEBands[i+c*m.nbEBands])
			tmp = PSHR32(MULT16_16(coef, oldEBands[i+c*m.nbEBands]), 8) + prev[c] + SHL32(q, 7)
			tmp = MAX32(-int(0.5+(28.0)*float64(int(1)<<(CeltConstants.DB_SHIFT+7))), tmp)
			oldEBands[i+c*m.nbEBands] = PSHR32(tmp, 7)
			prev[c] = prev[c] + SHL32(q, 7) - MULT16_16(beta, PSHR32(q, 8))
			c++
			if c < C {
				continue
			}
			break
		}
	}
}

func unquant_fine_energy(m *CeltMode, start int, end int, oldEBands []int, fine_quant []int, dec *EntropyCoder, C int) {
	var i, c int
	/* Decode finer resolution */
	for i = start; i < end; i++ {
		if fine_quant[i] <= 0 {
			continue
		}
		c = 0
		for {
			var q2 int
			var offset int
			q2 = dec.dec_bits(fine_quant[i])
			offset = SUB16Int((SHR32(SHL32(q2, CeltConstants.DB_SHIFT)+int(0.5+(.5)*float64(int(1)<<(CeltConstants.DB_SHIFT))), fine_quant[i])), int(0.5+(.5)*float64(int(1)<<(CeltConstants.DB_SHIFT))))
			oldEBands[i+c*m.nbEBands] += offset
			c++
			if c < C {
				continue
			}
			break
		}
	}
}

func unquant_energy_finalise(m *CeltMode, start int, end int, oldEBands []int, fine_quant []int, fine_priority []int, bits_left int, dec *EntropyCoder, C int) {
	for prio := 0; prio < 2; prio++ {
		for i := start; i < end && bits_left >= C; i++ {
			if fine_quant[i] >= CeltConstants.MAX_FINE_BITS || fine_priority[i] != prio {
				continue
			}
			for c := 0; c < C; c++ {
				q2 := dec.dec_bits(1)
				offset := (q2<<CeltConstants.DB_SHIFT - (1 << (CeltConstants.DB_SHIFT - 1))) >> (fine_quant[i] + 1)
				index := i + c*m.nbEBands
				oldEBands[index] += (offset)
				bits_left--
			}
		}
	}
}

func amp2Log2(m *CeltMode, effEnd int, end int, bandE [][]int, bandLogE [][]int, C int) {
	for c := 0; c < C; c++ {
		for i := 0; i < effEnd; i++ {
			bandLogE[c][i] = int(celt_log2(int(bandE[c][i])<<2) - int(CeltTables.EMeans[i])<<6)
		}
		for i := effEnd; i < end; i++ {
			bandLogE[c][i] = -(14 << CeltConstants.DB_SHIFT)
		}
	}
}

func amp2Log2Ptr(m *CeltMode, effEnd int, end int, bandE []int, bandLogE []int, bandLogEPtr int, C int) {
	for c := 0; c < C; c++ {
		for i := 0; i < effEnd; i++ {
			bandLogE[bandLogEPtr+c*m.nbEBands+i] = celt_log2(SHL32(int(bandE[i+c*m.nbEBands]), 2)) - SHL16Int(int(CeltTables.EMeans[i]), 6)
		}
		for i := effEnd; i < end; i++ {
			bandLogE[bandLogEPtr+c*m.nbEBands+i] = -(14 << CeltConstants.DB_SHIFT)
		}
	}
}
