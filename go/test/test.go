package main

import (
	"crypto/md5"
	"encoding/binary"
	"encoding/hex"
	"fmt"
	"io"
	"net/http"
	_ "net/http/pprof"
	"os"
	"time"

	"github.com/lostromb/concentus/go/comm"
	"github.com/lostromb/concentus/go/opus"
)

func main() {
	go func() {
		http.ListenAndServe("localhost:6060", nil)
	}()
	test3()
}
func test() {
	//Avoid panic
	defer func() {
		if r := recover(); r != nil {
			fmt.Errorf("decode panic: %v", r)
		}
	}()
	encoder, err := opus.NewOpusEncoder(48000, 2, opus.OPUS_APPLICATION_AUDIO)
	encoder.SetBitrate(96000)
	encoder.SetForceMode(opus.MODE_CELT_ONLY)
	encoder.SetSignalType(opus.OPUS_SIGNAL_MUSIC)
	encoder.SetComplexity(0)

	decoder, err := opus.NewOpusDecoder(48000, 2)
	fileIn, err := os.Open("48Khz Stereo.raw")
	if err != nil {
		panic(err)
	}
	defer fileIn.Close()

	var packetSamples = 960
	inBuf := make([]byte, packetSamples*2*2)
	data_packet := make([]byte, 1275)
	start := time.Now().UnixNano()
	i := 0
	for {
		_, err := io.ReadFull(fileIn, inBuf)
		if err != nil {
			break
		}
		if i > 1000 {
			break
		}
		pcm, _ := BytesToShorts(inBuf, 0, len(inBuf))

		fmt.Printf("imput md5:%s\r\n", ByteSliceToMD5(inBuf))
		if i == 0 {
			comm.Debug = false
		} else {
			comm.Debug = false

		}
		bytesEncoded, err := encoder.Encode(pcm, 0, packetSamples, data_packet, 0, 1275)

		fmt.Printf("i:%d bytesEncoded:%d data_packet:%s\r\n", i, bytesEncoded, ByteSliceToMD5(data_packet))
		_, err = decoder.Decode(data_packet, 0, bytesEncoded, pcm, 0, packetSamples, false)

		if err == nil {
			fmt.Printf("pcm:%s\r\n", IntSliceToMD5(pcm))
		}
		i++
	}
	elapsed := time.Duration(time.Now().UnixNano() - start)
	fmt.Printf("Time was: %+v ms\n", float64(elapsed)/1e6)
}
func test1() {

	encoder, err := opus.NewOpusEncoder(16000, 1, opus.OPUS_APPLICATION_AUDIO)
	encoder.SetBitrate(96000)
	encoder.SetForceMode(opus.MODE_SILK_ONLY)
	encoder.SetSignalType(opus.OPUS_SIGNAL_MUSIC)
	encoder.SetComplexity(0)

	decoder, err := opus.NewOpusDecoder(16000, 1)
	fileIn, err := os.Open("16Khz Mono.raw")
	if err != nil {
		panic(err)
	}
	defer fileIn.Close()

	var packetSamples = 960
	inBuf := make([]byte, packetSamples*2)
	data_packet := make([]byte, 1275)
	start := time.Now().UnixNano()
	i := 0
	for {
		_, err := io.ReadFull(fileIn, inBuf)
		if err != nil {
			break
		}
		if i > 1000 {
			break
		}

		pcm, _ := BytesToShorts(inBuf, 0, len(inBuf))

		fmt.Printf("imput md5:%s\r\n", ByteSliceToMD5(inBuf))
		if i == 20 {
			//opus.Debug = false
		} else {
			//opus.Debug = false

		}
		bytesEncoded, err := encoder.Encode(pcm, 0, packetSamples, data_packet, 0, 1275)

		fmt.Printf("i:%d bytesEncoded:%d data_packet:%s\r\n", i, bytesEncoded, ByteSliceToMD5(data_packet))
		_, err = decoder.Decode(data_packet, 0, bytesEncoded, pcm, 0, packetSamples, false)

		if err == nil {
			fmt.Printf("pcm:%s\r\n", IntSliceToMD5(pcm))
		}
		i++
	}
	elapsed := time.Duration(time.Now().UnixNano() - start)
	fmt.Printf("Time was: %+v ms\n", float64(elapsed)/1e6)

}
func ByteSliceToMD5(slice []byte) string {
	hasher := md5.New()
	hasher.Write(slice)
	hash := hasher.Sum(nil)
	return hex.EncodeToString(hash)
}

func IntSliceToMD5(slice []int16) string {
	hasher := md5.New()
	buf := make([]byte, 2) // 用于每个整数的缓冲区

	for _, num := range slice {
		// 将int转换为uint32（保留位模式）
		u := uint16(num)
		binary.BigEndian.PutUint16(buf, u)
		hasher.Write(buf)
	}

	hash := hasher.Sum(nil)
	return hex.EncodeToString(hash)
}

func BytesToShorts(input []byte, offset, length int) ([]int16, error) {
	// 1. 输入验证
	totalBytes := offset + length
	if totalBytes > len(input) {
		return nil, fmt.Errorf("offset + length exceeds input length (%d > %d)", totalBytes, len(input))
	}
	if length%2 != 0 {
		return nil, fmt.Errorf("length must be multiple of 2, got %d", length)
	}
	if offset < 0 {
		return nil, fmt.Errorf("offset cannot be negative")
	}

	// 2. 创建结果数组 (Java中的short对应Go的int16)
	processedValues := make([]int16, length/2)

	// 3. 按照Java原始算法逐字节处理
	for c := 0; c < len(processedValues); c++ {
		// 计算字节位置 - 与Java完全一致
		posLow := (c * 2) + offset
		posHigh := (c * 2) + 1 + offset

		// 低字节 (无符号处理)
		a := int16(input[posLow] & 0xFF)

		// 高字节 (带符号扩展)
		b := int16(input[posHigh]) << 8

		// 组合值 (保留位运算)
		processedValues[c] = a | b
	}

	return processedValues, nil
}

func test3() {

	fileIn, err := os.Open("48Khz Stereo.raw")
	if err != nil {
		panic(err)
	}
	defer fileIn.Close()

	var packetSamples = 960
	inBuf := make([]byte, packetSamples*2*2)
	data_packet := make([]int16, packetSamples*2)

	i := 0
	for {

		_, err := io.ReadFull(fileIn, inBuf)
		if err != nil {
			break
		}
		if i > 1 {
			break
		}
		pcm, _ := BytesToShorts(inBuf, 0, len(inBuf))

		fmt.Printf("pcm:%s\r\n", IntSliceToMD5(pcm))
		fmt.Printf("pcm:%+v\r\n", (pcm))

		var resampler = comm.NewSpeexResampler(2, 48000, 88200, 10)

		var inputLen = len(pcm)
		var outLen = len(data_packet)
		resampler.ProcessShort(0, pcm, 0, &inputLen, data_packet, 0, &outLen)
		fmt.Printf("outLen:%d data_packet:%s \r\n", outLen, IntSliceToMD5(data_packet))
		fmt.Printf("data_packet:%+v\r\n", (data_packet))
		i++
	}

}
