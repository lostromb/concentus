/* Copyright (c) 2011-2013 Xiph.Org Foundation
Written by Gregory Maxwell */
/*
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

- Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#ifdef HAVE_CONFIG_H
#include "config.h"
#endif

#include <stdio.h>
#include <stdlib.h>
#include <limits.h>
#include <stdint.h>
#include <math.h>
#include <string.h>
#include <time.h>
#if (!defined WIN32 && !defined _WIN32) || defined(__MINGW32__)
#include <unistd.h>
#else
#include <process.h>
#define getpid _getpid
#endif
#include "opus_multistream.h"
#include "opus.h"
#include "../src/opus_private.h"
#include "test_opus_common.h"
#include "../silk/fixed/main_FIX.h"
#include "../silk/API.h"

#define MAX_PACKET (1500)
#define SAMPLES (48000*30)
#define SSAMPLES (SAMPLES/3)
#define MAX_FRAME_SAMP (5760)

#define PI (3.141592653589793238462643f)

void generate_music(short *buf, opus_int32 len)
{
	opus_int32 a1, b1, a2, b2;
	opus_int32 c1, c2, d1, d2;
	opus_int32 i, j;
	a1 = b1 = a2 = b2 = 0;
	c1 = c2 = d1 = d2 = 0;
	j = 0;
	/*60ms silence*/
	//for(i=0;i<2880;i++)buf[i*2]=buf[i*2+1]=0;
	for (i = 0; i<len; i++)
	{
		opus_uint32 r;
		opus_int32 v1, v2;
		v1 = v2 = (((j*((j >> 12) ^ ((j >> 10 | j >> 12) & 26 & j >> 7))) & 128) + 128) << 15;
		r = fast_rand(); v1 += r & 65535; v1 -= r >> 16;
		r = fast_rand(); v2 += r & 65535; v2 -= r >> 16;
		b1 = v1 - a1 + ((b1 * 61 + 32) >> 6); a1 = v1;
		b2 = v2 - a2 + ((b2 * 61 + 32) >> 6); a2 = v2;
		c1 = (30 * (c1 + b1 + d1) + 32) >> 6; d1 = b1;
		c2 = (30 * (c2 + b2 + d2) + 32) >> 6; d2 = b2;
		v1 = (c1 + 128) >> 8;
		v2 = (c2 + 128) >> 8;
		buf[i * 2] = v1>32767 ? 32767 : (v1<-32768 ? -32768 : v1);
		buf[i * 2 + 1] = v2>32767 ? 32767 : (v2<-32768 ? -32768 : v2);
		if (i % 6 == 0)j++;
	}
}

#if 0
static int save_ctr = 0;
static void int_to_char(opus_uint32 i, unsigned char ch[4])
{
	ch[0] = i >> 24;
	ch[1] = (i >> 16) & 0xFF;
	ch[2] = (i >> 8) & 0xFF;
	ch[3] = i & 0xFF;
}

static OPUS_INLINE void save_packet(unsigned char* p, int len, opus_uint32 rng)
{
	FILE *fout;
	unsigned char int_field[4];
	char name[256];
	snprintf(name, 255, "test_opus_encode.%llu.%d.bit", (unsigned long long)iseed, save_ctr);
	fprintf(stdout, "writing %d byte packet to %s\n", len, name);
	fout = fopen(name, "wb+");
	if (fout == NULL)test_failed();
	int_to_char(len, int_field);
	fwrite(int_field, 1, 4, fout);
	int_to_char(rng, int_field);
	fwrite(int_field, 1, 4, fout);
	fwrite(p, 1, len, fout);
	fclose(fout);
	save_ctr++;
}
#endif

int run_test1(int no_fuzz)
{
	static const int fsizes[6] = { 960 * 3,960 * 2,120,240,480,960 };
	static const char *mstrings[3] = { "    LP","Hybrid","  MDCT" };
	unsigned char mapping[256] = { 0,1,255 };
	unsigned char db62[36];
	opus_int32 i;
	int rc, j, err;
	OpusEncoder *enc;
	OpusMSEncoder *MSenc;
	OpusDecoder *dec;
	OpusMSDecoder *MSdec;
	OpusMSDecoder *MSdec_err;
	OpusDecoder *dec_err[10];
	short *inbuf;
	short *outbuf;
	short *out2buf;
	opus_int32 bitrate_bps;
	unsigned char packet[MAX_PACKET + 257];
	opus_uint32 enc_final_range;
	opus_uint32 dec_final_range;
	int fswitch;
	int fsize;
	int count;

	/*FIXME: encoder api tests, fs!=48k, mono, VBR*/

	fprintf(stdout, "  Encode+Decode tests.\n");

	enc = opus_encoder_create(48000, 2, OPUS_APPLICATION_VOIP, &err);
	if (err != OPUS_OK || enc == NULL)test_failed();

	for (i = 0; i<2; i++)
	{
		int *ret_err;
		ret_err = i ? 0 : &err;
		MSenc = opus_multistream_encoder_create(8000, 2, 2, 0, mapping, OPUS_UNIMPLEMENTED, ret_err);
		if ((ret_err && *ret_err != OPUS_BAD_ARG) || MSenc != NULL)test_failed();

		MSenc = opus_multistream_encoder_create(8000, 0, 1, 0, mapping, OPUS_APPLICATION_VOIP, ret_err);
		if ((ret_err && *ret_err != OPUS_BAD_ARG) || MSenc != NULL)test_failed();

		MSenc = opus_multistream_encoder_create(44100, 2, 2, 0, mapping, OPUS_APPLICATION_VOIP, ret_err);
		if ((ret_err && *ret_err != OPUS_BAD_ARG) || MSenc != NULL)test_failed();

		MSenc = opus_multistream_encoder_create(8000, 2, 2, 3, mapping, OPUS_APPLICATION_VOIP, ret_err);
		if ((ret_err && *ret_err != OPUS_BAD_ARG) || MSenc != NULL)test_failed();

		MSenc = opus_multistream_encoder_create(8000, 2, -1, 0, mapping, OPUS_APPLICATION_VOIP, ret_err);
		if ((ret_err && *ret_err != OPUS_BAD_ARG) || MSenc != NULL)test_failed();

		MSenc = opus_multistream_encoder_create(8000, 256, 2, 0, mapping, OPUS_APPLICATION_VOIP, ret_err);
		if ((ret_err && *ret_err != OPUS_BAD_ARG) || MSenc != NULL)test_failed();
	}

	MSenc = opus_multistream_encoder_create(8000, 2, 2, 0, mapping, OPUS_APPLICATION_AUDIO, &err);
	if (err != OPUS_OK || MSenc == NULL)test_failed();

	/*Some multistream encoder API tests*/
	if (opus_multistream_encoder_ctl(MSenc, OPUS_GET_BITRATE(&i)) != OPUS_OK)test_failed();
	if (opus_multistream_encoder_ctl(MSenc, OPUS_GET_LSB_DEPTH(&i)) != OPUS_OK)test_failed();
	if (i<16)test_failed();

	{
		OpusEncoder *tmp_enc;
		if (opus_multistream_encoder_ctl(MSenc, OPUS_MULTISTREAM_GET_ENCODER_STATE(1, &tmp_enc)) != OPUS_OK)test_failed();
		if (opus_encoder_ctl(tmp_enc, OPUS_GET_LSB_DEPTH(&j)) != OPUS_OK)test_failed();
		if (i != j)test_failed();
		if (opus_multistream_encoder_ctl(MSenc, OPUS_MULTISTREAM_GET_ENCODER_STATE(2, &tmp_enc)) != OPUS_BAD_ARG)test_failed();
	}

	dec = opus_decoder_create(48000, 2, &err);
	if (err != OPUS_OK || dec == NULL)test_failed();

	MSdec = opus_multistream_decoder_create(48000, 2, 2, 0, mapping, &err);
	if (err != OPUS_OK || MSdec == NULL)test_failed();

	MSdec_err = opus_multistream_decoder_create(48000, 3, 2, 0, mapping, &err);
	if (err != OPUS_OK || MSdec_err == NULL)test_failed();

	dec_err[0] = (OpusDecoder *)malloc(opus_decoder_get_size(2));
	memcpy(dec_err[0], dec, opus_decoder_get_size(2));
	dec_err[1] = opus_decoder_create(48000, 1, &err);
	dec_err[2] = opus_decoder_create(24000, 2, &err);
	dec_err[3] = opus_decoder_create(24000, 1, &err);
	dec_err[4] = opus_decoder_create(16000, 2, &err);
	dec_err[5] = opus_decoder_create(16000, 1, &err);
	dec_err[6] = opus_decoder_create(12000, 2, &err);
	dec_err[7] = opus_decoder_create(12000, 1, &err);
	dec_err[8] = opus_decoder_create(8000, 2, &err);
	dec_err[9] = opus_decoder_create(8000, 1, &err);
	for (i = 0; i<10; i++)if (dec_err[i] == NULL)test_failed();

	{
		OpusEncoder *enccpy;
		/*The opus state structures contain no pointers and can be freely copied*/
		enccpy = (OpusEncoder *)malloc(opus_encoder_get_size(2));
		memcpy(enccpy, enc, opus_encoder_get_size(2));
		memset(enc, 255, opus_encoder_get_size(2));
		opus_encoder_destroy(enc);
		enc = enccpy;
	}

	inbuf = (short *)malloc(sizeof(short)*SAMPLES * 2);
	outbuf = (short *)malloc(sizeof(short)*SAMPLES * 2);
	out2buf = (short *)malloc(sizeof(short)*MAX_FRAME_SAMP * 3);
	if (inbuf == NULL || outbuf == NULL || out2buf == NULL)test_failed();

	generate_music(inbuf, SAMPLES);

	/*   FILE *foo;
	foo = fopen("foo.sw", "wb+");
	fwrite(inbuf, 1, SAMPLES*2*2, foo);
	fclose(foo);*/

	if (opus_encoder_ctl(enc, OPUS_SET_BANDWIDTH(OPUS_AUTO)) != OPUS_OK)test_failed();
	if (opus_encoder_ctl(enc, OPUS_SET_FORCE_MODE(-2)) != OPUS_BAD_ARG)test_failed();

	for (rc = 0; rc<3; rc++)
	{
		if (opus_encoder_ctl(enc, OPUS_SET_VBR(rc<2)) != OPUS_OK)test_failed();
		if (opus_encoder_ctl(enc, OPUS_SET_VBR_CONSTRAINT(rc == 1)) != OPUS_OK)test_failed();
		if (opus_encoder_ctl(enc, OPUS_SET_VBR_CONSTRAINT(rc == 1)) != OPUS_OK)test_failed();
		if (opus_encoder_ctl(enc, OPUS_SET_INBAND_FEC(rc == 0)) != OPUS_OK)test_failed();
		for (j = 0; j<13; j++)
		{
			int rate;
			/*int modes[13] = { 0,0,0,1,1,1,1,2,2,2,2,2,2 };
			int rates[13] = { 6000,12000,48000,16000,32000,48000,64000,512000,13000,24000,48000,64000,96000 };
			int frame[13] = { 960 * 2,960,480,960,960,960,480,960 * 3,960 * 3,960,480,240,120 };*/
			int modes[13] = { 2,0,0,1,1,1,1,2,2,2,2,2,2 };
			int rates[13] = { 96000,12000,48000,16000,32000,48000,64000,512000,13000,24000,48000,64000,96000 };
			int frame[13] = { 120,960,480,960,960,960,480,960 * 3,960 * 3,960,480,240,120 };
			rate = rates[j] + fast_rand() % rates[j];
			count = i = 0;
			do {
				int bw, len, out_samples, frame_size;
				frame_size = frame[j];
				if ((fast_rand() & 255) == 0)
				{
					if (opus_encoder_ctl(enc, OPUS_RESET_STATE) != OPUS_OK)test_failed();
					if (opus_decoder_ctl(dec, OPUS_RESET_STATE) != OPUS_OK)test_failed();
					if ((fast_rand() & 1) != 0)
					{
						if (opus_decoder_ctl(dec_err[fast_rand() & 1], OPUS_RESET_STATE) != OPUS_OK)test_failed();
					}
				}
				if ((fast_rand() & 127) == 0)
				{
					if (opus_decoder_ctl(dec_err[fast_rand() & 1], OPUS_RESET_STATE) != OPUS_OK)test_failed();
				}
				if (fast_rand() % 10 == 0) {
					int complex = fast_rand() % 11;
					if (opus_encoder_ctl(enc, OPUS_SET_COMPLEXITY(complex)) != OPUS_OK)test_failed();
				}
				if (fast_rand() % 50 == 0)opus_decoder_ctl(dec, OPUS_RESET_STATE);
				if (opus_encoder_ctl(enc, OPUS_SET_INBAND_FEC(rc == 0)) != OPUS_OK)test_failed();
				if (opus_encoder_ctl(enc, OPUS_SET_FORCE_MODE(MODE_SILK_ONLY + modes[j])) != OPUS_OK)test_failed();
				if (opus_encoder_ctl(enc, OPUS_SET_DTX(fast_rand() & 1)) != OPUS_OK)test_failed();
				if (opus_encoder_ctl(enc, OPUS_SET_BITRATE(rate)) != OPUS_OK)test_failed();
				if (opus_encoder_ctl(enc, OPUS_SET_FORCE_CHANNELS((rates[j] >= 64000 ? 2 : 1))) != OPUS_OK)test_failed();
				if (opus_encoder_ctl(enc, OPUS_SET_COMPLEXITY((count >> 2) % 11)) != OPUS_OK)test_failed();
				if (opus_encoder_ctl(enc, OPUS_SET_PACKET_LOSS_PERC((fast_rand() & 15)&(fast_rand() % 15))) != OPUS_OK)test_failed();
				bw = modes[j] == 0 ? OPUS_BANDWIDTH_NARROWBAND + (fast_rand() % 3) :
					modes[j] == 1 ? OPUS_BANDWIDTH_SUPERWIDEBAND + (fast_rand() & 1) :
					OPUS_BANDWIDTH_NARROWBAND + (fast_rand() % 5);
				if (modes[j] == 2 && bw == OPUS_BANDWIDTH_MEDIUMBAND)bw += 3;
				if (opus_encoder_ctl(enc, OPUS_SET_BANDWIDTH(bw)) != OPUS_OK)test_failed();
				len = opus_encode(enc, &inbuf[i << 1], frame_size, packet, MAX_PACKET);
				if (len<0 || len>MAX_PACKET)test_failed();
				if (opus_encoder_ctl(enc, OPUS_GET_FINAL_RANGE(&enc_final_range)) != OPUS_OK)test_failed();
				if ((fast_rand() & 3) == 0)
				{
					if (opus_packet_pad(packet, len, len + 1) != OPUS_OK)test_failed();
					len++;
				}
				if ((fast_rand() & 7) == 0)
				{
					if (opus_packet_pad(packet, len, len + 256) != OPUS_OK)test_failed();
					len += 256;
				}
				if ((fast_rand() & 3) == 0)
				{
					len = opus_packet_unpad(packet, len);
					if (len<1)test_failed();
				}
				out_samples = opus_decode(dec, packet, len, &outbuf[i << 1], MAX_FRAME_SAMP, 0);
				if (out_samples != frame_size)test_failed();
				if (opus_decoder_ctl(dec, OPUS_GET_FINAL_RANGE(&dec_final_range)) != OPUS_OK)test_failed();
				if (enc_final_range != dec_final_range)test_failed();
				/*LBRR decode*/
				out_samples = opus_decode(dec_err[0], packet, len, out2buf, frame_size, (fast_rand() & 3) != 0);
				if (out_samples != frame_size)test_failed();
				out_samples = opus_decode(dec_err[1], packet, (fast_rand() & 3) == 0 ? 0 : len, out2buf, MAX_FRAME_SAMP, (fast_rand() & 7) != 0);
				if (out_samples<120)test_failed();
				i += frame_size;
				count++;
			} while (i<(SSAMPLES - MAX_FRAME_SAMP));
			fprintf(stdout, "    Mode %s FB encode %s, %6d bps OK.\n", mstrings[modes[j]], rc == 0 ? " VBR" : rc == 1 ? "CVBR" : " CBR", rate);
		}
	}

	if (opus_encoder_ctl(enc, OPUS_SET_FORCE_MODE(OPUS_AUTO)) != OPUS_OK)test_failed();
	if (opus_encoder_ctl(enc, OPUS_SET_FORCE_CHANNELS(OPUS_AUTO)) != OPUS_OK)test_failed();
	if (opus_encoder_ctl(enc, OPUS_SET_INBAND_FEC(0)) != OPUS_OK)test_failed();
	if (opus_encoder_ctl(enc, OPUS_SET_DTX(0)) != OPUS_OK)test_failed();

	for (rc = 0; rc<3; rc++)
	{
		if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_VBR(rc<2)) != OPUS_OK)test_failed();
		if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_VBR_CONSTRAINT(rc == 1)) != OPUS_OK)test_failed();
		if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_VBR_CONSTRAINT(rc == 1)) != OPUS_OK)test_failed();
		if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_INBAND_FEC(rc == 0)) != OPUS_OK)test_failed();
		for (j = 0; j<16; j++)
		{
			int rate;
			int modes[16] = { 0,0,0,0,0,0,0,0,2,2,2,2,2,2,2,2 };
			int rates[16] = { 4000,12000,32000,8000,16000,32000,48000,88000,4000,12000,32000,8000,16000,32000,48000,88000 };
			int frame[16] = { 160 * 1,160,80,160,160,80,40,20,160 * 1,160,80,160,160,80,40,20 };
			if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_INBAND_FEC(rc == 0 && j == 1)) != OPUS_OK)test_failed();
			if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_FORCE_MODE(MODE_SILK_ONLY + modes[j])) != OPUS_OK)test_failed();
			rate = rates[j] + fast_rand() % rates[j];
			if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_DTX(fast_rand() & 1)) != OPUS_OK)test_failed();
			if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_BITRATE(rate)) != OPUS_OK)test_failed();
			count = i = 0;
			do {
				int pred, len, out_samples, frame_size, loss;
				if (opus_multistream_encoder_ctl(MSenc, OPUS_GET_PREDICTION_DISABLED(&pred)) != OPUS_OK)test_failed();
				if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_PREDICTION_DISABLED((int)(fast_rand() & 15)<(pred ? 11 : 4))) != OPUS_OK)test_failed();
				frame_size = frame[j];
				if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_COMPLEXITY((count >> 2) % 11)) != OPUS_OK)test_failed();
				if (opus_multistream_encoder_ctl(MSenc, OPUS_SET_PACKET_LOSS_PERC((fast_rand() & 15)&(fast_rand() % 15))) != OPUS_OK)test_failed();
				if ((fast_rand() & 255) == 0)
				{
					if (opus_multistream_encoder_ctl(MSenc, OPUS_RESET_STATE) != OPUS_OK)test_failed();
					if (opus_multistream_decoder_ctl(MSdec, OPUS_RESET_STATE) != OPUS_OK)test_failed();
					if ((fast_rand() & 3) != 0)
					{
						if (opus_multistream_decoder_ctl(MSdec_err, OPUS_RESET_STATE) != OPUS_OK)test_failed();
					}
				}
				if ((fast_rand() & 255) == 0)
				{
					if (opus_multistream_decoder_ctl(MSdec_err, OPUS_RESET_STATE) != OPUS_OK)test_failed();
				}
				len = opus_multistream_encode(MSenc, &inbuf[i << 1], frame_size, packet, MAX_PACKET);
				if (len<0 || len>MAX_PACKET)test_failed();
				if (opus_multistream_encoder_ctl(MSenc, OPUS_GET_FINAL_RANGE(&enc_final_range)) != OPUS_OK)test_failed();
				if ((fast_rand() & 3) == 0)
				{
					if (opus_multistream_packet_pad(packet, len, len + 1, 2) != OPUS_OK)test_failed();
					len++;
				}
				if ((fast_rand() & 7) == 0)
				{
					if (opus_multistream_packet_pad(packet, len, len + 256, 2) != OPUS_OK)test_failed();
					len += 256;
				}
				if ((fast_rand() & 3) == 0)
				{
					len = opus_multistream_packet_unpad(packet, len, 2);
					if (len<1)test_failed();
				}
				out_samples = opus_multistream_decode(MSdec, packet, len, out2buf, MAX_FRAME_SAMP, 0);
				if (out_samples != frame_size * 6)test_failed();
				if (opus_multistream_decoder_ctl(MSdec, OPUS_GET_FINAL_RANGE(&dec_final_range)) != OPUS_OK)test_failed();
				if (enc_final_range != dec_final_range)test_failed();
				/*LBRR decode*/
				loss = (fast_rand() & 63) == 0;
				out_samples = opus_multistream_decode(MSdec_err, packet, loss ? 0 : len, out2buf, frame_size * 6, (fast_rand() & 3) != 0);
				if (out_samples != (frame_size * 6))test_failed();
				i += frame_size;
				count++;
			} while (i<(SSAMPLES / 12 - MAX_FRAME_SAMP));
			fprintf(stdout, "    Mode %s NB dual-mono MS encode %s, %6d bps OK.\n", mstrings[modes[j]], rc == 0 ? " VBR" : rc == 1 ? "CVBR" : " CBR", rate);
		}
	}

	bitrate_bps = 512000;
	fsize = fast_rand() % 31;
	fswitch = 100;

	debruijn2(6, db62);
	count = i = 0;
	do {
		unsigned char toc;
		const unsigned char *frames[48];
		short size[48];
		int payload_offset;
		opus_uint32 dec_final_range2;
		int jj, dec2;
		int len, out_samples;
		int frame_size = fsizes[db62[fsize]];
		opus_int32 offset = i % (SAMPLES - MAX_FRAME_SAMP);

		opus_encoder_ctl(enc, OPUS_SET_BITRATE(bitrate_bps));

		len = opus_encode(enc, &inbuf[offset << 1], frame_size, packet, MAX_PACKET);
		if (len<0 || len>MAX_PACKET)test_failed();
		count++;

		opus_encoder_ctl(enc, OPUS_GET_FINAL_RANGE(&enc_final_range));

		out_samples = opus_decode(dec, packet, len, &outbuf[offset << 1], MAX_FRAME_SAMP, 0);
		if (out_samples != frame_size)test_failed();

		opus_decoder_ctl(dec, OPUS_GET_FINAL_RANGE(&dec_final_range));

		/* compare final range encoder rng values of encoder and decoder */
		if (dec_final_range != enc_final_range)test_failed();

		/* We fuzz the packet, but take care not to only corrupt the payload
		Corrupted headers are tested elsewhere and we need to actually run
		the decoders in order to compare them. */
		if (opus_packet_parse(packet, len, &toc, frames, size, &payload_offset) <= 0)test_failed();
		if ((fast_rand() & 1023) == 0)len = 0;
		for (j = (frames[0] - packet); j<len; j++)for (jj = 0; jj<8; jj++)packet[j] ^= ((!no_fuzz) && ((fast_rand() & 1023) == 0)) << jj;
		out_samples = opus_decode(dec_err[0], len>0 ? packet : NULL, len, out2buf, MAX_FRAME_SAMP, 0);
		if (out_samples<0 || out_samples>MAX_FRAME_SAMP)test_failed();
		if ((len>0 && out_samples != frame_size))test_failed(); /*FIXME use lastframe*/

		opus_decoder_ctl(dec_err[0], OPUS_GET_FINAL_RANGE(&dec_final_range));

		/*randomly select one of the decoders to compare with*/
		dec2 = fast_rand() % 9 + 1;
		out_samples = opus_decode(dec_err[dec2], len>0 ? packet : NULL, len, out2buf, MAX_FRAME_SAMP, 0);
		if (out_samples<0 || out_samples>MAX_FRAME_SAMP)test_failed(); /*FIXME, use factor, lastframe for loss*/

		opus_decoder_ctl(dec_err[dec2], OPUS_GET_FINAL_RANGE(&dec_final_range2));
		if (len>0 && dec_final_range != dec_final_range2)test_failed();

		fswitch--;
		if (fswitch<1)
		{
			int new_size;
			fsize = (fsize + 1) % 36;
			new_size = fsizes[db62[fsize]];
			if (new_size == 960 || new_size == 480)fswitch = 2880 / new_size*(fast_rand() % 19 + 1);
			else fswitch = (fast_rand() % (2880 / new_size)) + 1;
		}
		bitrate_bps = ((fast_rand() % 508000 + 4000) + bitrate_bps) >> 1;
		i += frame_size;
	} while (i<SAMPLES * 4);
	fprintf(stdout, "    All framesize pairs switching encode, %d frames OK.\n", count);

	if (opus_encoder_ctl(enc, OPUS_RESET_STATE) != OPUS_OK)test_failed();
	opus_encoder_destroy(enc);
	if (opus_multistream_encoder_ctl(MSenc, OPUS_RESET_STATE) != OPUS_OK)test_failed();
	opus_multistream_encoder_destroy(MSenc);
	if (opus_decoder_ctl(dec, OPUS_RESET_STATE) != OPUS_OK)test_failed();
	opus_decoder_destroy(dec);
	if (opus_multistream_decoder_ctl(MSdec, OPUS_RESET_STATE) != OPUS_OK)test_failed();
	opus_multistream_decoder_destroy(MSdec);
	opus_multistream_decoder_destroy(MSdec_err);
	for (i = 0; i<10; i++)opus_decoder_destroy(dec_err[i]);
	free(inbuf);
	free(outbuf);
	free(out2buf);
	return 0;
}

typedef struct {
	silk_decoder_state          channel_state[DECODER_NUM_CHANNELS];
	stereo_dec_state                sStereo;
	opus_int                         nChannelsAPI;
	opus_int                         nChannelsInternal;
	opus_int                         prev_decode_only_middle;
} silk_decoder;

void silk_encode()
{
	opus_int16 samples[324] =
	{ 4522, 4188, 2984, 3264, 4813, 5527,
		4007, 1472, -522, -1561, -1794, -1466,
		-693, 51, -463, -2173, -3494, -3394,
		-2548, -2024, -1871, -1615, -789, 775,
		2233, 3624, 4811, 4779, 3149, 447,
		-2112, -3456, -3967, -4589, -5189, -5017,
		-3443, -1140, 2331, 7637, 14121, 18202,
		17864, 13800, 9378, 6530, 3893, -516,
		-5096, -7583, -8249, -8551, -9340, -9202,
		-7535, -5734, -6061, -7597, -7657, -5131,
		-1916, 827, 3005, 5416, 6830, 6346,
		4075, 1700, -153, -2363, -5717, -8960,
		-10241, -9042, -6776, -5145, -4357, -3989,
		-3378, -3279, -3244, -2446, -849, 594,
		966, 987, 2420, 5570, 8435, 7951,
		3851, -1199, -4880, -6659, -7930, -8861,
		-8346, -6352, -4052, -2203, -613, 2198,
		5243, 6500, 5103, 3217, 2739, 3710,
		4557, 5240, 6488, 7824, 7279, 3973,
		-29, -2077, -2067, -2212, -3784, -4775,
		-3326, -813, 1011, 2330, 4173, 5691,
		5170, 2250, -122, 404, 3291, 5317,
		5424, 4605, 3947, 3482, 2225, 144,
		-1785, -3698, -5939, -8105, -8694, -6921,
		-3585, -1046, -414, -592, -474, 48,
		160, -443, -1062, -1497, -2569, -3673,
		-3719, -1914, 182, 1022, 323, -278,
		107, 1880, 5243, 10568, 15963, 18443,
		16906, 13103, 9739, 7637, 5146, 1263,
		-2634, -5080, -6271, -7124, -7497, -6658,
		-4810, -4290, -5904, -7622, -7085, -4787,
		-1736, 728, 3147, 5250, 5440, 3530,
		1292, -159, -1360, -3432, -6204, -7735,
		-6945, -4601, -2837, -2406, -2749, -3301,
		-3989, -4400, -3646, -1271, 1492, 2955,
		2965, 2946, 4351, 6722, 7293, 4309,
		-1040, -5803, -8091, -8338, -7903, -7222,
		-5677, -4184, -3794, -4204, -3711, -1560,
		743, 1365, 516, 619, 2847, 6309,
		9005, 10165, 9789, 7683, 3535, -1041,
		-3639, -3194, -1698, -1685, -2928, -2872,
		-1225, 298, 465, 482, 763, 608,
		-868, -2149, -921, 3137, 7242, 8863,
		7700, 5319, 2818, 412, -1870, -3515,
		-4275, -4825, -5599, -5753, -3922, -653,
		1958, 2086, 33, -1863, -2341, -1822,
		-1357, -939, -166, 284, -298, -1083,
		-792, 477, 875, -546, -1879, -645,
		4076, 10008, 15052, 17353, 16914, 14356,
		11391, 8363, 5843, 3576, 1587, -524,
		-2559, -4184, -4784, -4836, -4676, -5762,
		-7611, -8805, -7796, -5140, -1781, 871,
		2568, 3593, 3564, 2127, -234, -1920,
		-2336, -2815, 0, 0, 0, 0 };

	silk_encoder encoder;
	memset(&encoder, 0, sizeof(silk_encoder));
	encoder.nChannelsAPI = 1;

	silk_EncControlStruct control;
	memset(&control, 0, sizeof(silk_EncControlStruct));

	int error = silk_InitEncoder(&encoder, 0, &control);

	control.nChannelsAPI = 1;
	control.nChannelsInternal = 1;
	control.API_sampleRate = 16000;
	control.maxInternalSampleRate = 16000;
	control.minInternalSampleRate = 8000;
	control.desiredInternalSampleRate = 16000;
	control.payloadSize_ms = 20;
	control.bitRate = 25000;
	control.complexity = 9;
	control.useCBR = 1;
	control.maxBits = 8000;

	unsigned char outbuf[1000];
	memset(&outbuf, 0, 1000);

	ec_enc entropyCoder;
	memset(&entropyCoder, 0, sizeof(ec_enc));
	ec_enc_init(&entropyCoder, outbuf, 1000);

	int bytesOut = control.maxBits / 8;
	error = silk_Encode(&encoder, &control, samples, 320, &entropyCoder, &bytesOut, 0);

	// Now decode that frame again
	ec_enc_done(&entropyCoder);

	silk_decoder decoder;
	decoder.nChannelsAPI = 1;

	silk_DecControlStruct decodeControl;

	error = silk_InitDecoder(&decoder);
	decodeControl.nChannelsAPI = 1;
	decodeControl.nChannelsInternal = 1;
	decodeControl.internalSampleRate = 16000;
	decodeControl.API_sampleRate = 16000;
	decodeControl.payloadSize_ms = 20;

	ec_ctx entropyDecoder;
	ec_dec_init(&entropyDecoder, outbuf, 1000);

	short samplesOut[1000];
	int nSamplesOut = 0;

	silk_Decode(&decoder, &decodeControl, 0, 1, &entropyDecoder, samplesOut, &nSamplesOut, 0);

	fprintf(stdout, "%d\n", nSamplesOut);
	for (int c = 0; c < nSamplesOut; c++)
	{
		fprintf(stdout, "%d,\n", samplesOut[c]);
	}
}

int main(int _argc, char **_argv)
{
	/*fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(99, 16));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(5, 16));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(322, 10));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(-2, 30));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(-123, 12));

	fprintf(stdout, "2f 0x%x\n", (unsigned int)n_AR_Q14);*/

	//silk_encode();

	//return 0;

	const char * oversion;
	const char * env_seed;
	int env_used;

	if (_argc>2)
	{
		fprintf(stderr, "Usage: %s [<seed>]\n", _argv[0]);
		return 1;
	}

	env_used = 0;
	env_seed = getenv("SEED");
	if (_argc>1)iseed = atoi(_argv[1]);
	else if (env_seed)
	{
		iseed = atoi(env_seed);
		env_used = 1;
	}
	else iseed = (opus_uint32)time(NULL) ^ ((getpid() & 65535) << 16);

	iseed = 13371337;

	Rw = Rz = iseed;

	oversion = opus_get_version_string();
	if (!oversion)test_failed();
	fprintf(stderr, "Testing %s encoder. Random seed: %u (%.4X)\n", oversion, iseed, fast_rand() % 65535);
	if (env_used)fprintf(stderr, "  Random seed set from the environment (SEED=%s).\n", env_seed);

	/*Setting TEST_OPUS_NOFUZZ tells the tool not to send garbage data
	into the decoders. This is helpful because garbage data
	may cause the decoders to clip, which angers CLANG IOC.*/
	run_test1(getenv("TEST_OPUS_NOFUZZ") != NULL);

	fprintf(stderr, "Tests completed successfully.\n");

	return 0;
}
