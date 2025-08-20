#include "opus.h"
#include "opus_defines.h"
#include "opus_types.h"
#include "opus_private.h"
#include <stdio.h>
#include <stdlib.h>

int main()
{
    int param_bitrate = 96000;
    int param_channels = 2;
    int param_application = OPUS_APPLICATION_AUDIO;
    int param_signal = OPUS_SIGNAL_MUSIC;
    int param_sample_rate = 48000;
    int param_frame_size = OPUS_FRAMESIZE_20_MS;
    int param_complexity = 0;
    int param_force_mode = OPUS_AUTO;
    int param_use_dtx = 0;
    int param_use_vbr = 0;
    int param_use_contrained_vbr = 0;
    int error;
    OpusEncoder* encoder = opus_encoder_create(param_sample_rate, param_channels, param_application, &error);
    OpusDecoder* decoder = opus_decoder_create(param_sample_rate, param_channels, &error);
    opus_encoder_ctl(encoder, OPUS_SET_BITRATE_REQUEST, param_bitrate);
    opus_encoder_ctl(encoder, OPUS_SET_FORCE_MODE_REQUEST, param_force_mode);
    opus_encoder_ctl(encoder, OPUS_SET_SIGNAL_REQUEST, param_signal);
    opus_encoder_ctl(encoder, OPUS_SET_COMPLEXITY_REQUEST, param_complexity);
    opus_encoder_ctl(encoder, OPUS_SET_DTX_REQUEST, param_use_dtx);
    opus_encoder_ctl(encoder, OPUS_SET_VBR_REQUEST, param_use_vbr);
    opus_encoder_ctl(encoder, OPUS_SET_VBR_CONSTRAINT_REQUEST, param_use_contrained_vbr);

    const char* fileName = NULL;
    if (param_channels == 1)
    {
        switch (param_sample_rate)
        {
        case 8000:
            fileName = "D:\\Code\\concentus\\AudioData\\8Khz Mono.raw";
            break;
        case 12000:
            fileName = "D:\\Code\\concentus\\AudioData\\12Khz Mono.raw";
            break;
        case 16000:
            fileName = "D:\\Code\\concentus\\AudioData\\16Khz Mono.raw";
            break;
        case 24000:
            fileName = "D:\\Code\\concentus\\AudioData\\24Khz Mono.raw";
            break;
        case 48000:
            fileName = "D:\\Code\\concentus\\AudioData\\48Khz Mono.raw";
            break;
        }
    }
    else
    {
        switch (param_sample_rate)
        {
        case 8000:
            fileName = "D:\\Code\\concentus\\AudioData\\8Khz Stereo.raw";
            break;
        case 12000:
            fileName = "D:\\Code\\concentus\\AudioData\\12Khz Stereo.raw";
            break;
        case 16000:
            fileName = "D:\\Code\\concentus\\AudioData\\16Khz Stereo.raw";
            break;
        case 24000:
            fileName = "D:\\Code\\concentus\\AudioData\\24Khz Stereo.raw";
            break;
        case 48000:
            fileName = "D:\\Code\\concentus\\AudioData\\48Khz Stereo.raw";
            break;
        }
    }

    int packetSamplesPerChannel = -1;
    switch (param_frame_size)
    {
        case OPUS_FRAMESIZE_2_5_MS:
            packetSamplesPerChannel = ((param_sample_rate * 2) + (param_sample_rate >> 1)) / 1000;
            break;
        case OPUS_FRAMESIZE_5_MS:
            packetSamplesPerChannel = param_sample_rate * 5 / 1000;
            break;
        case OPUS_FRAMESIZE_10_MS:
            packetSamplesPerChannel = param_sample_rate * 10 / 1000;
            break;
        case OPUS_FRAMESIZE_20_MS:
            packetSamplesPerChannel = param_sample_rate * 20 / 1000;
            break;
        case OPUS_FRAMESIZE_40_MS:
            packetSamplesPerChannel = param_sample_rate * 40 / 1000;
            break;
        case OPUS_FRAMESIZE_60_MS:
            packetSamplesPerChannel = param_sample_rate * 60 / 1000;
            break;
        case OPUS_FRAMESIZE_80_MS:
            packetSamplesPerChannel = param_sample_rate * 80 / 1000;
            break;
        case OPUS_FRAMESIZE_100_MS:
            packetSamplesPerChannel = param_sample_rate * 100 / 1000;
            break;
        case OPUS_FRAMESIZE_120_MS:
            packetSamplesPerChannel = param_sample_rate * 120 / 1000;
            break;
    }

    opus_int inputBufLength = packetSamplesPerChannel * param_channels * sizeof(opus_int16);
    opus_uint8* inAudioByte = malloc(inputBufLength);
    opus_uint8 outPacket[1275];
    short* inAudioSamples = (short*)inAudioByte;
    FILE* fileIn = fopen(fileName, "rb");
    while (1)
    {
        int bytesRead = fread(inAudioByte, sizeof(opus_uint8), inputBufLength, fileIn);
        if (bytesRead < inputBufLength)
        {
            break;
        }

        int errorOrLength;
        errorOrLength = opus_encode(encoder, inAudioSamples, packetSamplesPerChannel, outPacket, 1275);
        printf_s("ENCODE: %i\r\n", errorOrLength);

        if (errorOrLength > 0)
        {
            errorOrLength = opus_decode(decoder, outPacket, errorOrLength, inAudioSamples, packetSamplesPerChannel, 0);
            printf_s("DECODE: %i\r\n", errorOrLength);
        }
    }

    free(inAudioByte);
    opus_encoder_destroy(encoder);
    opus_decoder_destroy(decoder);
}