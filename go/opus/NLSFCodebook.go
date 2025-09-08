package opus

type NLSFCodebook struct {
	nVectors            int16
	order               int16
	quantStepSize_Q16   int16
	invQuantStepSize_Q6 int16
	CB1_NLSF_Q8         []int16
	CB1_iCDF            []int16
	pred_Q8             []int16
	ec_sel              []int16
	ec_iCDF             []int16
	ec_Rates_Q5         []int16
	deltaMin_Q15        []int16
}

func (this *NLSFCodebook) Reset() {
	this.nVectors = 0
	this.order = 0
	this.quantStepSize_Q16 = 0
	this.invQuantStepSize_Q6 = 0
	this.CB1_NLSF_Q8 = nil
	this.CB1_iCDF = nil
	this.pred_Q8 = nil
	this.ec_sel = nil
	this.ec_iCDF = nil
	this.ec_Rates_Q5 = nil
	this.deltaMin_Q15 = nil
}
