package opus

func InitTwoDimensionalArrayInt(x, y int) [][]int {
	arr := make([][]int, x)
	for i := range arr {
		arr[i] = make([]int, y)
	}
	return arr
}

func InitTwoDimensionalArrayFloat(x, y int) [][]float32 {
	arr := make([][]float32, x)
	for i := range arr {
		arr[i] = make([]float32, y)
	}
	return arr
}

func InitTwoDimensionalArrayShort(x, y int) [][]int16 {
	arr := make([][]int16, x)
	for i := range arr {
		arr[i] = make([]int16, y)
	}
	return arr
}

func InitTwoDimensionalArrayByte(x, y int) [][]int8 {
	arr := make([][]int8, x)
	for i := range arr {
		arr[i] = make([]int8, y)
	}
	return arr
}

func InitThreeDimensionalArrayByte(x, y, z int) [][][]byte {
	arr := make([][][]byte, x)
	for i := range arr {
		arr[i] = make([][]byte, y)
		for j := range arr[i] {
			arr[i][j] = make([]byte, z)
		}
	}
	return arr
}

func MemSet[T any](array []T, value T) {
	for i := range array {
		array[i] = value
	}
}

func MemSetLen[T any](array []T, value T, length int) {
	if length > len(array) {
		length = len(array)
	}
	for i := 0; i < length; i++ {
		array[i] = value
	}
}

func MemSetWithOffset[T any](array []T, value T, offset, length int) {
	end := offset + length
	if end > len(array) {
		end = len(array)
	}
	if offset < 0 || offset >= len(array) {
		return
	}
	for i := offset; i < end; i++ {
		array[i] = value
	}
}

func MemMove[T any](array []T, src_idx, dst_idx, length int) {
	copy(array[dst_idx:dst_idx+length], array[src_idx:src_idx+length])
}
