package comm

var Debug = false

func ConvertByteToInt8(data []byte) []int8 {
	result := make([]int8, len(data))
	for i, b := range data {
		result[i] = int8(b)
	}
	return result
}
