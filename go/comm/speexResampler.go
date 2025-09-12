package comm

import (
	"fmt"
	"math"
	"sync"
)

const (
	fixedStackAlloc = 8192
)

// SpeexResampler 实现任意比率的音频重采样
type SpeexResampler struct {
	mu sync.Mutex

	inRate        int
	outRate       int
	numRate       int
	denRate       int
	quality       int
	nbChannels    int
	filtLen       int
	memAllocSize  int
	bufferSize    int
	intAdvance    int
	fracAdvance   int
	cutoff        float64
	oversample    int
	initialised   int
	started       int
	lastSample    []int
	sampFracNum   []int
	magicSamples  []int
	mem           []float64
	sincTable     []float64
	sincTableLen  int
	resampler_ptr func(channel_index int, input []float64, input_ptr int, in_len *int, output []float64, output_ptr int, out_len *int) int

	inStride  int
	outStride int
}

// NewSpeexResampler 创建新的重采样器
func NewSpeexResampler(nbChannels, inRate, outRate, quality int) *SpeexResampler {
	return NewSpeexResamplerFractional(nbChannels, inRate, outRate, inRate, outRate, quality)
}

// NewSpeexResamplerFractional 创建支持分数比率的重采样器
func NewSpeexResamplerFractional(nbChannels, ratioNum, ratioDen, inRate, outRate, quality int) *SpeexResampler {
	if quality > 10 || quality < 0 {
		panic("quality must be between 0 and 10")
	}

	r := &SpeexResampler{
		nbChannels:   nbChannels,
		quality:      -1,
		bufferSize:   160,
		inStride:     1,
		outStride:    1,
		lastSample:   make([]int, nbChannels),
		magicSamples: make([]int, nbChannels),
		sampFracNum:  make([]int, nbChannels),
	}

	r.quality = quality
	r.SetRateFraction(ratioNum, ratioDen, inRate, outRate)
	r.updateFilter()
	r.initialised = 1
	return r
}

// 质量映射表
type qualityMapping struct {
	baseLength          int
	oversample          int
	downsampleBandwidth float64
	upsampleBandwidth   float64
	windowFunc          *funcDef
}

type funcDef struct {
	table      []float64
	oversample int
}

var qualityMap = []qualityMapping{
	{8, 4, 0.830, 0.860, &funcDef{kaiser6Table, 32}},
	{16, 4, 0.850, 0.880, &funcDef{kaiser6Table, 32}},
	{32, 4, 0.882, 0.910, &funcDef{kaiser6Table, 32}},
	{48, 8, 0.895, 0.917, &funcDef{kaiser8Table, 32}},
	{64, 8, 0.921, 0.940, &funcDef{kaiser8Table, 32}},
	{80, 16, 0.922, 0.940, &funcDef{kaiser10Table, 32}},
	{96, 16, 0.940, 0.945, &funcDef{kaiser10Table, 32}},
	{128, 16, 0.950, 0.950, &funcDef{kaiser10Table, 32}},
	{160, 16, 0.960, 0.960, &funcDef{kaiser10Table, 32}},
	{192, 32, 0.968, 0.968, &funcDef{kaiser12Table, 64}},
	{256, 32, 0.975, 0.975, &funcDef{kaiser12Table, 64}},
}

var (
	kaiser12Table = []float64{
		0.99859849, 1.00000000, 0.99859849, 0.99440475, 0.98745105, 0.97779076,
		0.96549770, 0.95066529, 0.93340547, 0.91384741, 0.89213598, 0.86843014,
		0.84290116, 0.81573067, 0.78710866, 0.75723148, 0.72629970, 0.69451601,
		0.66208321, 0.62920216, 0.59606986, 0.56287762, 0.52980938, 0.49704014,
		0.46473455, 0.43304576, 0.40211431, 0.37206735, 0.34301800, 0.31506490,
		0.28829195, 0.26276832, 0.23854851, 0.21567274, 0.19416736, 0.17404546,
		0.15530766, 0.13794294, 0.12192957, 0.10723616, 0.09382272, 0.08164178,
		0.07063950, 0.06075685, 0.05193064, 0.04409466, 0.03718069, 0.03111947,
		0.02584161, 0.02127838, 0.01736250, 0.01402878, 0.01121463, 0.00886058,
		0.00691064, 0.00531256, 0.00401805, 0.00298291, 0.00216702, 0.00153438,
		0.00105297, 0.00069463, 0.00043489, 0.00025272, 0.00013031, 0.0000527734,
		0.00001000, 0.00000000,
	}

	kaiser10Table = []float64{
		0.99537781, 1.00000000, 0.99537781, 0.98162644, 0.95908712, 0.92831446,
		0.89005583, 0.84522401, 0.79486424, 0.74011713, 0.68217934, 0.62226347,
		0.56155915, 0.50119680, 0.44221549, 0.38553619, 0.33194107, 0.28205962,
		0.23636152, 0.19515633, 0.15859932, 0.12670280, 0.09935205, 0.07632451,
		0.05731132, 0.04193980, 0.02979584, 0.02044510, 0.01345224, 0.00839739,
		0.00488951, 0.00257636, 0.00115101, 0.00035515, 0.00000000, 0.00000000,
	}

	kaiser8Table = []float64{
		0.99635258, 1.00000000, 0.99635258, 0.98548012, 0.96759014, 0.94302200,
		0.91223751, 0.87580811, 0.83439927, 0.78875245, 0.73966538, 0.68797126,
		0.63451750, 0.58014482, 0.52566725, 0.47185369, 0.41941150, 0.36897272,
		0.32108304, 0.27619388, 0.23465776, 0.19672670, 0.16255380, 0.13219758,
		0.10562887, 0.08273982, 0.06335451, 0.04724088, 0.03412321, 0.02369490,
		0.01563093, 0.00959968, 0.00527363, 0.00233883, 0.00050000, 0.00000000,
	}

	kaiser6Table = []float64{
		0.99733006, 1.00000000, 0.99733006, 0.98935595, 0.97618418, 0.95799003,
		0.93501423, 0.90755855, 0.87598009, 0.84068475, 0.80211977, 0.76076565,
		0.71712752, 0.67172623, 0.62508937, 0.57774224, 0.53019925, 0.48295561,
		0.43647969, 0.39120616, 0.34752997, 0.30580127, 0.26632152, 0.22934058,
		0.19505503, 0.16360756, 0.13508755, 0.10953262, 0.08693120, 0.06722600,
		0.05031820, 0.03607231, 0.02432151, 0.01487334, 0.00752000, 0.00000000,
	}
)

func computeFunc(x float64, f *funcDef) float64 {
	var y, frac float64
	var interp0, interp1, interp2, interp3 float64
	var ind int
	y = x * float64(f.oversample)
	ind = int(math.Floor(y))
	frac = (y - float64(ind))
	/* CSE with handle the repeated powers */
	interp3 = -0.1666666667*frac + 0.1666666667*(frac*frac*frac)
	interp2 = frac + 0.5*(frac*frac) - 0.5*(frac*frac*frac)
	/*interp[2] = 1.f - 0.5f*frac - frac*frac + 0.5f*frac*frac*frac;*/
	interp0 = -0.3333333333*frac + 0.5*(frac*frac) - 0.1666666667*(frac*frac*frac)
	/* Just to make sure we don't have rounding problems */
	interp1 = 1.0 - interp3 - interp2 - interp0

	/*sum = frac*accum[1] + (1-frac)*accum[2];*/
	return interp0*f.table[ind] + interp1*f.table[ind+1] + interp2*f.table[ind+2] + interp3*f.table[ind+3]

}

func sinc(cutoff, x float64, N int, window *funcDef) float64 {
	var xx = x * cutoff
	if math.Abs(x) < 1e-6 {
		return cutoff

	} else if math.Abs(x) > 0.5*float64(N) {
		return 0
	}
	/*FIXME: Can it really be any slower than this? */
	return float64(cutoff * math.Sin(math.Pi*xx) / (math.Pi * xx) * computeFunc(math.Abs(2.0*x/float64(N)), window))
}

func cubicCoef(frac float64, interp []float64) {
	interp[0] = -0.16667*frac + 0.16667*frac*frac*frac
	interp[1] = frac + 0.5*frac*frac - 0.5*frac*frac*frac
	/*interp[2] = 1.f - 0.5f*frac - frac*frac + 0.5f*frac*frac*frac;*/
	interp[3] = -0.33333*frac + 0.5*frac*frac - 0.16667*frac*frac*frac
	/* Just to make sure we don't have rounding problems */
	interp[2] = 1.0 - interp[0] - interp[1] - interp[3]
}

func (r *SpeexResampler) resamplerBasicDirectSingle(channel_index int, input []float64, input_ptr int, in_len *int, output []float64, output_ptr int, out_len *int) int {
	var N = r.filtLen
	var out_sample = 0
	var last_sample = r.lastSample[channel_index]
	var samp_frac_num = r.sampFracNum[channel_index]

	var sum float64

	for !(last_sample >= *in_len || out_sample >= *out_len) {
		var sinct = samp_frac_num * N
		var iptr = input_ptr + last_sample

		var j int
		sum = 0
		for j = 0; j < N; j++ {
			sum += r.sincTable[sinct+j] * input[iptr+j]
		}

		output[output_ptr+(r.outStride*out_sample)] = sum
		out_sample++
		last_sample += r.intAdvance
		samp_frac_num += r.fracAdvance
		if samp_frac_num >= r.denRate {
			samp_frac_num -= r.denRate
			last_sample++
		}
	}

	r.lastSample[channel_index] = last_sample
	r.sampFracNum[channel_index] = samp_frac_num
	return out_sample
}

func (r *SpeexResampler) resamplerBasicInterpolateSingle(channel_index int, input []float64, input_ptr int, in_len *int, output []float64, output_ptr int, out_len *int) int {
	var N = r.filtLen
	var out_sample = 0
	var last_sample = r.lastSample[channel_index]
	var samp_frac_num = r.sampFracNum[channel_index]
	var sum float64
	interp := make([]float64, 4)
	accum := make([]float64, 4)

	for !(last_sample >= *in_len || out_sample >= *out_len) {
		var iptr = input_ptr + last_sample

		var offset = samp_frac_num * r.oversample / r.denRate
		var frac = (float64((samp_frac_num * r.oversample) % r.denRate)) / float64(r.denRate)

		var j int
		accum[0] = 0
		accum[1] = 0
		accum[2] = 0
		accum[3] = 0

		for j = 0; j < N; j++ {

			var curr_in = input[iptr+j]
			accum[0] += curr_in * r.sincTable[4+(j+1)*r.oversample-offset-2]
			accum[1] += curr_in * r.sincTable[4+(j+1)*r.oversample-offset-1]
			accum[2] += curr_in * r.sincTable[4+(j+1)*r.oversample-offset]
			accum[3] += curr_in * r.sincTable[4+(j+1)*r.oversample-offset+1]
		}

		cubicCoef(frac, interp)
		sum = (interp[0] * accum[0]) +
			(interp[1] * accum[1]) +
			(interp[2] * accum[2]) +
			(interp[3] * accum[3])

		output[output_ptr+(r.outStride*out_sample)] = sum
		out_sample++
		last_sample += r.intAdvance
		samp_frac_num += r.fracAdvance
		if samp_frac_num >= r.denRate {

			samp_frac_num -= r.denRate
			last_sample++
		}
	}

	r.lastSample[channel_index] = last_sample
	r.sampFracNum[channel_index] = samp_frac_num
	return out_sample
}

func (r *SpeexResampler) updateFilter() {
	r.mu.Lock()
	defer r.mu.Unlock()

	var old_length int

	old_length = r.filtLen
	r.oversample = qualityMap[r.quality].oversample
	r.filtLen = qualityMap[r.quality].baseLength
	if r.numRate > r.denRate {

		/* down-sampling */
		r.cutoff = qualityMap[r.quality].downsampleBandwidth * float64(r.denRate) / float64(r.numRate)
		/* FIXME: divide the numerator and denominator by a certain amount if they're too large */
		r.filtLen = r.filtLen * r.numRate / r.denRate
		/* Round up to make sure we have a multiple of 8 */
		r.filtLen = ((r.filtLen - 1) & (^0x7)) + 8
		if 2*r.denRate < r.numRate {
			r.oversample >>= 1
		}
		if 4*r.denRate < r.numRate {
			r.oversample >>= 1
		}
		if 8*r.denRate < r.numRate {
			r.oversample >>= 1
		}
		if 16*r.denRate < r.numRate {
			r.oversample >>= 1
		}
		if r.oversample < 1 {
			r.oversample = 1
		}
	} else {
		/* up-sampling */
		r.cutoff = qualityMap[r.quality].upsampleBandwidth
	}

	if r.denRate <= 16*(r.oversample+8) {
		var i int
		if r.sincTable == nil {
			r.sincTable = make([]float64, r.filtLen*r.denRate)
		} else if r.sincTableLen < r.filtLen*r.denRate {
			r.sincTable = make([]float64, r.filtLen*r.denRate)
			r.sincTableLen = r.filtLen * r.denRate
		}
		for i = 0; i < r.denRate; i++ {
			var j int
			for j = 0; j < r.filtLen; j++ {
				r.sincTable[i*r.filtLen+j] = sinc(r.cutoff, float64(float64(j-r.filtLen/2+1)-(float64(i))/float64(r.denRate)), r.filtLen, qualityMap[r.quality].windowFunc)
			}
		}

		r.resampler_ptr = r.resamplerBasicDirectSingle
		fmt.Printf("resamplerBasicDirectSingle\r\n")

		/*fprintf (stderr, "resampler uses direct sinc table and normalised cutoff %f\n", cutoff);*/
	} else {
		var i int
		if r.sincTable == nil {
			r.sincTable = make([]float64, r.filtLen*r.oversample+8)
		} else if r.sincTableLen < r.filtLen*r.oversample+8 {
			r.sincTable = make([]float64, r.filtLen*r.oversample+8)
			r.sincTableLen = r.filtLen*r.oversample + 8
		}
		for i = -4; i < (int)(r.oversample*r.filtLen+4); i++ {
			r.sincTable[i+4] = sinc(r.cutoff, float64(i/r.oversample-r.filtLen/2), r.filtLen, qualityMap[r.quality].windowFunc)
		}
		r.resampler_ptr = r.resamplerBasicInterpolateSingle
		/*fprintf (stderr, "resampler uses interpolated sinc table and normalised cutoff %f\n", cutoff);*/
	}
	r.intAdvance = r.numRate / r.denRate
	r.fracAdvance = r.numRate % r.denRate

	/* Here's the place where we update the filter memory to take into account
	   the change in filter length. It's probably the messiest part of the code
	   due to handling of lots of corner cases. */
	if r.mem == nil {

		r.memAllocSize = r.filtLen - 1 + r.bufferSize
		r.mem = make([]float64, r.nbChannels*r.memAllocSize)
		for i = 0; i < r.nbChannels*r.memAllocSize; i++ {
			r.mem[i] = 0
		}
		/*speex_warning("init filter");*/
	} else if r.started == 0 {
		var i int
		r.memAllocSize = r.filtLen - 1 + r.bufferSize
		r.mem = make([]float64, r.nbChannels*r.memAllocSize)
		for i = 0; i < r.nbChannels*r.memAllocSize; i++ {
			r.mem[i] = 0
		}
		/*speex_warning("reinit filter");*/
	} else if r.filtLen > old_length {
		var i int
		/* Increase the filter length */
		/*speex_warning("increase filter size");*/
		var old_alloc_size = r.memAllocSize
		if (r.filtLen - 1 + r.bufferSize) > r.memAllocSize {
			r.memAllocSize = r.filtLen - 1 + r.bufferSize
			r.mem = make([]float64, r.nbChannels*r.memAllocSize)
		}
		for i = r.nbChannels - 1; i >= 0; i-- {
			var j int
			var olen = old_length
			/*if (st.magic_samples[i])*/
			{
				/* Try and remove the magic samples as if nothing had happened */

				/* FIXME: This is wrong but for now we need it to avoid going over the array bounds */
				olen = old_length + 2*r.magicSamples[i]
				for j = old_length - 2 + r.magicSamples[i]; j >= 0; j-- {
					r.mem[i*r.memAllocSize+j+r.magicSamples[i]] = r.mem[i*old_alloc_size+j]
				}
				for j = 0; j < r.magicSamples[i]; j++ {
					r.mem[i*r.memAllocSize+j] = 0
				}
				r.magicSamples[i] = 0
			}
			if r.filtLen > olen {
				/* If the new filter length is still bigger than the "augmented" length */
				/* Copy data going backward */
				for j = 0; j < olen-1; j++ {
					r.mem[i*r.memAllocSize+(r.filtLen-2-j)] = r.mem[i*r.memAllocSize+(olen-2-j)]
				}
				/* Then put zeros for lack of anything better */
				for ; j < r.filtLen-1; j++ {
					r.mem[i*r.memAllocSize+(r.filtLen-2-j)] = 0
				}
				/* Adjust last_sample */
				r.lastSample[i] += (r.filtLen - olen) / 2
			} else {
				/* Put back some of the magic! */
				r.magicSamples[i] = (olen - r.filtLen) / 2
				for j = 0; j < r.filtLen-1+r.magicSamples[i]; j++ {
					r.mem[i*r.memAllocSize+j] = r.mem[i*r.memAllocSize+j+r.magicSamples[i]]
				}
			}
		}
	} else if r.filtLen < old_length {
		var i int
		/* Reduce filter length, this a bit tricky. We need to store some of the memory as "magic"
		   samples so they can be used directly as input the next time(s) */
		for i = 0; i < r.nbChannels; i++ {
			var j int
			var old_magic = r.magicSamples[i]
			r.magicSamples[i] = (old_length - r.filtLen) / 2
			/* We must copy some of the memory that's no longer used */
			/* Copy data going backward */
			for j = 0; j < r.filtLen-1+r.magicSamples[i]+old_magic; j++ {
				r.mem[i*r.memAllocSize+j] = r.mem[i*r.memAllocSize+j+r.magicSamples[i]]
			}
			r.magicSamples[i] += old_magic
		}
	}
}

func (r *SpeexResampler) speexResamplerMagic(channelIndex int, output []float64, output_ptr *int, outLen int) int {
	var tmp_in_len = r.magicSamples[channelIndex]
	var mem_ptr = channelIndex * r.memAllocSize
	var N = r.filtLen

	r.speexResamplerProcessNative(channelIndex, &tmp_in_len, output, *output_ptr, &outLen)

	r.magicSamples[channelIndex] -= tmp_in_len

	/* If we couldn't process all "magic" input samples, save the rest for next time */
	if r.magicSamples[channelIndex] != 0 {
		var i int
		for i = mem_ptr; i < r.magicSamples[channelIndex]+mem_ptr; i++ {

			r.mem[N-1+i] = r.mem[N-1+i+tmp_in_len]
		}
	}

	*output_ptr += outLen * r.outStride
	return outLen
}

func (r *SpeexResampler) speexResamplerProcessNative(channel_index int, in_len *int, output []float64, output_ptr int, out_len *int) {
	var j = 0
	var N = r.filtLen
	var out_sample = 0
	var mem_ptr = channel_index * r.memAllocSize
	var ilen int

	r.started = 1

	/* Call the right resampler through the function ptr */
	out_sample = r.resampler_ptr(channel_index, r.mem, mem_ptr, in_len, output, output_ptr, out_len)

	if r.lastSample[channel_index] < *in_len {
		*in_len = r.lastSample[channel_index]
	}
	*out_len = out_sample
	r.lastSample[channel_index] -= *in_len

	ilen = *in_len

	for j = mem_ptr; j < N-1+mem_ptr; j++ {
		r.mem[j] = r.mem[j+ilen]
	}
}

// 公共API
func (r *SpeexResampler) ProcessFloat(channel_index int, input []float64, input_ptr int, in_len *int, output []float64, output_ptr int, out_len *int) {
	r.mu.Lock()
	defer r.mu.Unlock()

	var j int
	var ilen = *in_len
	var olen = *out_len
	var x = channel_index * r.memAllocSize
	var filt_offs = r.filtLen - 1
	var xlen = r.memAllocSize - filt_offs
	var istride = r.inStride

	if r.magicSamples[channel_index] != 0 {
		olen -= r.speexResamplerMagic(channel_index, output, &output_ptr, olen)
	}

	if r.magicSamples[channel_index] == 0 {
		for ilen != 0 && olen != 0 {
			var ichunk = ilen
			if ilen > xlen {
				ichunk = xlen
			}
			//   var ichunk = (ilen > xlen) ? xlen : ilen;
			var ochunk = olen

			if input != nil {
				for j = 0; j < ichunk; j++ {
					r.mem[x+j+filt_offs] = input[input_ptr+j*istride]
				}
			} else {
				for j = 0; j < ichunk; j++ {
					r.mem[x+j+filt_offs] = 0
				}
			}

			r.speexResamplerProcessNative(channel_index, &ichunk, output, output_ptr, &ochunk)
			ilen -= ichunk
			olen -= ochunk
			output_ptr += ochunk * r.outStride
			if input != nil {
				input_ptr += ichunk * istride
			}
		}
	}

	*in_len -= ilen
	*out_len -= olen
}

func (r *SpeexResampler) ProcessShort(channelIndex int, input []int16, input_ptr int, inLen *int, output []int16, output_ptr int, outLen *int) {
	r.mu.Lock()
	defer r.mu.Unlock()

	var j int
	var istride_save = r.inStride
	var ostride_save = r.outStride
	var ilen = *inLen
	var olen = *outLen
	var x = channelIndex * r.memAllocSize
	var xlen = r.memAllocSize - (r.filtLen - 1)
	var ylen = fixedStackAlloc
	if olen < fixedStackAlloc {
		ylen = olen
	}

	var ystack = make([]float64, ylen)

	r.outStride = 1

	for ilen != 0 && olen != 0 {
		var y = 0
		var ichunk = ilen
		if ilen > xlen {
			ichunk = xlen
		}
		var ochunk = olen
		if olen > ylen {
			ochunk = ylen
		}
		//var ichunk = (ilen > xlen) ? xlen : ilen;
		// var ochunk = (olen > ylen) ? ylen : olen;
		var omagic = 0

		if r.magicSamples[channelIndex] != 0 {
			omagic = r.speexResamplerMagic(channelIndex, ystack, &y, ochunk)
			ochunk -= omagic
			olen -= omagic
		}
		if r.magicSamples[channelIndex] == 0 {
			if input != nil {
				for j = 0; j < ichunk; j++ {
					r.mem[x+j+r.filtLen-1] = float64(input[input_ptr+j*istride_save])
				}
			} else {
				for j = 0; j < ichunk; j++ {
					r.mem[x+j+r.filtLen-1] = 0
				}
			}
			r.speexResamplerProcessNative(channelIndex, &ichunk, ystack, y, &ochunk)
		} else {
			ichunk = 0
			ochunk = 0
		}
		for j = 0; j < ochunk+omagic; j++ {
			output[output_ptr+j*ostride_save] = FLOAT2INT(ystack[j])
		}

		ilen -= ichunk
		olen -= ochunk
		output_ptr += ((ochunk + omagic) * ostride_save)
		if input != nil {
			input_ptr += ichunk * istride_save
		}
	}

	r.outStride = ostride_save

	*inLen -= ilen
	*outLen -= olen
}
func FLOAT2INT(x float64) int16 {
	if x < math.MinInt16 {
		return math.MinInt16
	} else {

		if x > math.MaxInt16 {
			return math.MaxInt16

		} else {
			return int16(x)
		}
	}

}

func (r *SpeexResampler) ProcessInterleavedFloat(input []float64, in_len *int, output []float64, out_len *int) {
	var i int
	var istride_save, ostride_save int
	var bak_out_len = *out_len
	var bak_in_len = *in_len
	istride_save = r.inStride
	ostride_save = r.outStride
	r.inStride = r.nbChannels
	r.outStride = r.nbChannels
	for i = 0; i < r.nbChannels; i++ {
		*out_len = bak_out_len
		*in_len = bak_in_len
		if input != nil {
			r.ProcessFloat(i, input, i, in_len, output, i, out_len)
		} else {
			r.ProcessFloat(i, nil, 0, in_len, output, i, out_len)
		}
	}
	r.inStride = istride_save
	r.outStride = ostride_save
}

func (r *SpeexResampler) ProcessInterleavedShort(input []int16, in_len *int, output []int16, out_len *int) {
	var i int
	var istride_save, ostride_save int
	var bak_out_len = *out_len
	var bak_in_len = *in_len
	istride_save = r.inStride
	ostride_save = r.outStride
	r.inStride = r.nbChannels
	r.outStride = r.nbChannels
	for i = 0; i < r.nbChannels; i++ {

		*out_len = bak_out_len
		*in_len = bak_in_len
		if input != nil {
			r.ProcessShort(i, input, i, in_len, output, i, out_len)
		} else {
			r.ProcessShort(i, nil, 0, in_len, output, i, out_len)
		}
	}
	r.inStride = istride_save
	r.outStride = ostride_save
}

func (r *SpeexResampler) SkipZeroes() {
	for i := 0; i < r.nbChannels; i++ {
		r.lastSample[i] = r.filtLen / 2
	}
}

func (r *SpeexResampler) ResetMem() {
	for i := 0; i < r.nbChannels; i++ {
		r.lastSample[i] = 0
		r.magicSamples[i] = 0
		r.sampFracNum[i] = 0
	}
	for i := range r.mem {
		r.mem[i] = 0
	}
}

// Getters and Setters
func (r *SpeexResampler) SetRates(inRate, outRate int) {
	r.SetRateFraction(inRate, outRate, inRate, outRate)
}

func (r *SpeexResampler) GetRates() (int, int) {
	return r.inRate, r.outRate
}

func (r *SpeexResampler) SetRateFraction(ratio_num int, ratio_den int, in_rate int, out_rate int) {
	var fact int
	var old_den int
	var i int
	if r.inRate == in_rate && r.outRate == out_rate && r.numRate == ratio_num && r.denRate == ratio_den {
		return
	}

	old_den = r.denRate
	r.inRate = in_rate
	r.outRate = out_rate
	r.numRate = ratio_num
	r.denRate = ratio_den
	/* FIXME: This is terribly inefficient, but who cares (at least for now)? */
	for fact = 2; fact <= inlines.IMIN(r.numRate, r.denRate); fact++ {
		for (r.numRate%fact == 0) && (r.denRate%fact == 0) {
			r.numRate /= fact
			r.denRate /= fact
		}
	}

	if old_den > 0 {

		for i = 0; i < r.nbChannels; i++ {

			r.sampFracNum[i] = r.sampFracNum[i] * r.denRate / old_den
			/* Safety net */
			if r.sampFracNum[i] >= r.denRate {
				r.sampFracNum[i] = r.denRate - 1
			}
		}
	}

	if r.initialised != 0 {
		r.updateFilter()
	}
}

func (r *SpeexResampler) GetRateFraction() (int, int) {
	return r.numRate, r.denRate
}

func (r *SpeexResampler) GetQuality() int {
	return r.quality
}

func (r *SpeexResampler) SetInputStride(stride int) {
	r.inStride = stride
}

func (r *SpeexResampler) SetOutputStride(stride int) {
	r.outStride = stride
}

func (r *SpeexResampler) InputLatency() int {
	return r.filtLen / 2
}

func (r *SpeexResampler) OutputLatency() int {
	return ((r.filtLen/2)*r.denRate + (r.numRate >> 1)) / r.numRate
}
