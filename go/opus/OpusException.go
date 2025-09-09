package opus

type OpusException struct {
	_message         string
	_opus_error_code int
}

func OpusException1(message string) *OpusException {
	return OpusException2(message, 1)
}

func OpusException2(message string, opus_error_code int) *OpusException {
	fullMessage := message + ": " + opus_strerror(opus_error_code)
	return &OpusException{
		_message:         fullMessage,
		_opus_error_code: opus_error_code,
	}
}

func (e *OpusException) Error() string {
	return e._message
}

func (e *OpusException) getMessage() string {
	return e._message
}
