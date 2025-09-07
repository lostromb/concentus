package opus

import (
	"github.com/lostromb/concentus/go/celt"
	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/silk"
)

var inlines = comm.Inlines{}
var kernels = comm.Kernels{}
var SilkConstants = silk.SilkConstants
var SilkTables = silk.SilkTables

var TuningParameters = silk.TuningParameters

var SilkError = silk.SilkError

var OpusFramesizeHelpers = celt.OpusFramesizeHelpers
