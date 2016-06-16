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

static const opus_int16 parityFrame1[] = { 7943, 7107, 6318, 5193, 4190, 3128, 2161, 1584, 1301, 1422, 2029, 2647, 3483, 4584, 5303, 5910, 6474,
6711, 6383, 5735, 5048, 4077, 3338, 2664, 2045, 1598, 1016, 343, -296, -932, -1899, -2773, -4008, -5405,
-6800, -8318, -9544, -10935, -12330, -13420, -14358, -15549, -16298, -16877, -17511, -17641, -18006, -17745, -17473, -17349, -16671,
-16326, -15276, -14206, -13368, -12084, -11207, -10391, -9840, -9089, -7844, -7030, -6180, -5396, -4720, -3797, -3546, -3188,
-2690, -2681, -2495, -2616, -2595, -2156, -1890, -1506, -971, -125, 1005, 2003};

static const opus_int16 parityFrame2[] = { 3191, 4391, 5702, 7045, 8030, 9306, 10374, 11124, 12085, 12969, 13990, 14803, 15408, 15740, 15796, 16015, 15850,
15528, 14930, 14054, 13476, 12807, 12317, 12120, 11697, 11137, 10494, 9918, 9223, 8177, 7033, 5839, 4492, 3451,
2447, 1682, 1391, 738, 514, 595, 677, 1018, 1304, 1486, 1753, 1806, 1683, 1909, 1845, 1428, 1120,
474, -249, -620, -1476, -2585, -3112, -4292, -5512, -6178, -7174, -8017, -8834, -9726, -10942, -11607, -12220, -13247,
-13576, -14209, -15011, -15139, -15416, -15410, -15117, -15121, -14877, -14685, -14321, -13869};

static const opus_int16 parityFrame3[] = { -13474, -12835, -12610, -12245, -11548, -10867, -9853, -8893, -7698, -6325, -4977, -3282, -1704, -324, 784, 1597, 2448,
2953, 3288, 3408, 3308, 3492, 3719, 4095, 4364, 4604, 4963, 5071, 5624, 6188, 6585, 7241, 7523, 8367,
9526, 10367, 11636, 12627, 13323, 13974, 14355, 14510, 14519, 14322, 13787, 13266, 12727, 12147, 11439, 10770, 10063,
9299, 8544, 7664, 6825, 5714, 4544, 3599, 2775, 1722, 691, -150, -808, -1084, -1539, -1646, -1604, -1857,
-1777, -1429, -930, -580, -258, -349, -305, 214, 266, 626, 688, 450};


void ParityTest()
{
	int error = 0;
	OpusEncoder* encoder = opus_encoder_create(16000, 1, OPUS_APPLICATION_RESTRICTED_LOWDELAY, &error);
	opus_encoder_ctl(encoder, OPUS_SET_BITRATE_REQUEST, 32 * 1024);
	opus_encoder_ctl(encoder, OPUS_SET_COMPLEXITY_REQUEST, 5);
	unsigned char outputBuffer[10000];
	memset(outputBuffer, 0, 10000 * sizeof(unsigned char));
	opus_encode(encoder, parityFrame1, 80, outputBuffer, 10000);
	opus_encode(encoder, parityFrame2, 80, outputBuffer, 10000);
	opus_encode(encoder, parityFrame3, 80, outputBuffer, 10000);
}

int main(int _argc, char **_argv)
{
	/*fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(99, 16));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(5, 16));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(322, 10));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(-2, 30));
	fprintf(stdout, "%lx\n", silk_INVERSE32_varQ(-123, 12));

	printf("2f 0x%x\n", (unsigned int)n_AR_Q14);*/

	ParityTest();

	return 0;

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
