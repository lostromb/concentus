package opus

const MAX_NEURONS = 100

func tansig_approx(x float32) float32 {
	var y, dy float32
	sign := float32(1)
	if !(x < 8) {
		return 1
	}
	if !(x > -8) {
		return -1
	}
	if x < 0 {
		x = -x
		sign = -1
	}
	i := int(0.5 + 25*x)
	x -= 0.04 * float32(i)
	y = tansig_table[i]
	dy = 1 - y*y
	y = y + x*dy*(1-y*x)
	return sign * y
}

func mlp_process(m *MLPState, input []float32, output []float32) {
	var hidden [MAX_NEURONS]float32
	W := m.weights
	W_ptr := 0

	for j := 0; j < m.topo[1]; j++ {
		sum := W[W_ptr]
		W_ptr++
		for k := 0; k < m.topo[0]; k++ {
			sum += input[k] * W[W_ptr]
			W_ptr++
		}
		hidden[j] = tansig_approx(sum)
	}

	for j := 0; j < m.topo[2]; j++ {
		sum := W[W_ptr]
		W_ptr++
		for k := 0; k < m.topo[1]; k++ {
			sum += hidden[k] * W[W_ptr]
			W_ptr++
		}
		output[j] = tansig_approx(sum)
	}
}
