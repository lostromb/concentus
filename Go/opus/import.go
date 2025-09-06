package opus

import (
	"github.com/dosgo/concentus/go/celt"
	"github.com/dosgo/concentus/go/comm"
	"github.com/dosgo/concentus/go/silk"
)

var inlines = comm.Inlines{}
var kernels = comm.Kernels{}
var SilkConstants = silk.SilkConstants
var SilkTables = silk.SilkTables

var TuningParameters = silk.TuningParameters

var SilkError = silk.SilkError

var OpusFramesizeHelpers = celt.OpusFramesizeHelpers
