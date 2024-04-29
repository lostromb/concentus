using Concentus;
using Concentus.Celt;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestOpusEncode
{
    public class Program
    {
        private const int MAX_PACKET = 1500;
        private const int SAMPLES = 48000 * 30;
        private const int SSAMPLES = (SAMPLES / 3);
        private const int MAX_FRAME_SAMP = 5760;

        private static uint iseed;

        internal static void deb2_impl(Pointer<byte> _t, BoxedValue<Pointer<byte>> _p, int _k, int _x, int _y)
        {
            int i;
            if (_x > 2)
            {
                if (_y < 3)
                {
                    for (i = 0; i < _y; i++)
                    {
                        // *(--*_p)=_t[i+1];
                        _p.Val = _p.Val.Point(-1); // fixme: aaaaagggg
                        _p.Val[0] = _t[i + 1];
                    }
                }
            }
            else {
                _t[_x] = _t[_x - _y];
                deb2_impl(_t, _p, _k, _x + 1, _y);
                for (i = _t[_x - _y] + 1; i < _k; i++)
                {
                    _t[_x] = (byte)i;
                    deb2_impl(_t, _p, _k, _x + 1, _x);
                }
            }
        }

        /*Generates a De Bruijn sequence (k,2) with length k^2*/
        internal static void debruijn2(int _k, Pointer<byte> _res)
        {
            BoxedValue<Pointer<byte>> p;
            Pointer<byte> t;
            t = Pointer.Malloc<byte>(_k * 2);
            t.MemSet(0, _k * 2);
            p = new BoxedValue<Pointer<byte>>(_res.Point(_k * _k));
            deb2_impl(t, p, _k, 1, 1);
        }

        /*MWC RNG of George Marsaglia*/
        private static uint Rz, Rw;
        internal static uint fast_rand()
        {
            Rz = 36969 * (Rz & 65535) + (Rz >> 16);
            Rw = 18000 * (Rw & 65535) + (Rw >> 16);
            return (Rz << 16) + Rw;
        }

        internal static void generate_music(Pointer<short> buf, int len)
        {
            int a1, b1, a2, b2;
            int c1, c2, d1, d2;
            int i, j;
            a1 = b1 = a2 = b2 = 0;
            c1 = c2 = d1 = d2 = 0;
            j = 0;
            /*60ms silence*/
            //for(i=0;i<2880;i++)buf[i*2]=buf[i*2+1]=0;
            for (i = 0; i < len; i++)
            {
                uint r;
                int v1, v2;
                v1 = v2 = (((j * ((j >> 12) ^ ((j >> 10 | j >> 12) & 26 & j >> 7))) & 128) + 128) << 15;
                r = fast_rand();
                v1 += (int)r & 65535;
                v1 -= (int)r >> 16;
                r = fast_rand();
                v2 += (int)r & 65535;
                v2 -= (int)r >> 16;
                b1 = v1 - a1 + ((b1 * 61 + 32) >> 6); a1 = v1;
                b2 = v2 - a2 + ((b2 * 61 + 32) >> 6); a2 = v2;
                c1 = (30 * (c1 + b1 + d1) + 32) >> 6; d1 = b1;
                c2 = (30 * (c2 + b2 + d2) + 32) >> 6; d2 = b2;
                v1 = (c1 + 128) >> 8;
                v2 = (c2 + 128) >> 8;
                buf[i * 2] = (short)(v1 > 32767 ? 32767 : (v1 < -32768 ? -32768 : v1));
                buf[i * 2 + 1] = (short)(v2 > 32767 ? 32767 : (v2 < -32768 ? -32768 : v2));
                if (i % 6 == 0) j++;
            }
        }

        internal static void test_failed()
        {
            Console.WriteLine("Test FAILED!");
            if (Debugger.IsAttached)
            {
                throw new Exception();
            }
        }

        internal static readonly int[] fsizes = { 960 * 3, 960 * 2, 120, 240, 480, 960 };
        internal static readonly string[] mstrings = { "    LP", "Hybrid", "  MDCT" };

        internal static int run_test1(bool no_fuzz)
        {
            byte[] mapping/*[256]*/ = { 0, 1, 255 };
            byte[] db62 = new byte[36];
            int i;
            int rc, j;
            BoxedValueInt err = new BoxedValueInt();
            OpusEncoder enc;
            OpusDecoder dec;
            OpusDecoder[] dec_err = new OpusDecoder[10];
            Pointer<short> inbuf;
            Pointer<short> outbuf;
            Pointer<short> out2buf;
            //int bitrate_bps;
            Pointer<byte> packet = Pointer.Malloc<byte>(MAX_PACKET + 257);
            uint enc_final_range;
            uint dec_final_range;
            //int fswitch;
            //int fsize;
            int count;
            /*FIXME: encoder api tests, fs!=48k, mono, VBR*/

            Console.WriteLine("  Encode+Decode tests.");

            enc = new OpusEncoder(48000, 2, OpusApplication.OPUS_APPLICATION_VOIP);
            if (err.Val != OpusError.OPUS_OK || enc == null) test_failed();

            dec = new OpusDecoder(48000, 2);
            if (err.Val != OpusError.OPUS_OK || dec == null) test_failed();

            // fixme: this tests assign() performed on a decoder struct, which doesn't exist
            //dec_err[0] = (OpusDecoder*)malloc(OpusDecoder_get_size(2));
            //memcpy(dec_err[0], dec, OpusDecoder_get_size(2));
            dec_err[0] = new OpusDecoder(48000, 2);
            dec_err[1] = new OpusDecoder(48000, 1);
            dec_err[2] = new OpusDecoder(24000, 2);
            dec_err[3] = new OpusDecoder(24000, 1);
            dec_err[4] = new OpusDecoder(16000, 2);
            dec_err[5] = new OpusDecoder(16000, 1);
            dec_err[6] = new OpusDecoder(12000, 2);
            dec_err[7] = new OpusDecoder(12000, 1);
            dec_err[8] = new OpusDecoder(8000, 2);
            dec_err[9] = new OpusDecoder(8000, 1);
            for (i = 1; i < 10; i++) if (dec_err[i] == null) test_failed();

            //{
            //    OpusEncoder* enccpy;
            //    /*The opus state structures contain no pointers and can be freely copied*/
            //    enccpy = (OpusEncoder*)malloc(opus_encoder_get_size(2));
            //    memcpy(enccpy, enc, opus_encoder_get_size(2));
            //    memset(enc, 255, opus_encoder_get_size(2));
            //    opus_encoder_destroy(enc);
            //    enc = enccpy;
            //}

            inbuf = Pointer.Malloc<short>(SAMPLES * 2);
            outbuf = Pointer.Malloc<short>(SAMPLES * 2);
            out2buf = Pointer.Malloc<short>(MAX_FRAME_SAMP * 3);
            if (inbuf == null || outbuf == null || out2buf == null) test_failed();

            generate_music(inbuf, SAMPLES);

            ///*   FILE *foo;
            //foo = fopen("foo.sw", "wb+");
            //fwrite(inbuf, 1, SAMPLES*2*2, foo);
            //fclose(foo);*/

            enc.Bandwidth = (OpusBandwidth.OPUS_BANDWIDTH_AUTO);

            for (rc = 0; rc < 3; rc++)
            {
                enc.UseVBR = (rc < 2);
                enc.UseConstrainedVBR = (rc == 1);
                enc.UseInbandFEC = (rc == 0);

                int[] modes = { 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2 };
                int[] rates = { 6000, 12000, 48000, 16000, 32000, 48000, 64000, 512000, 13000, 24000, 48000, 64000, 96000 };
                int[] frame = { 960 * 2, 960, 480, 960, 960, 960, 480, 960 * 3, 960 * 3, 960, 480, 240, 120 };

                for (j = 0; j < modes.Length; j++)
                {
                    int rate;
                    rate = rates[j] + (int)fast_rand() % rates[j];
                    count = i = 0;
                    do
                    {
                        OpusBandwidth bw;
                        int len, out_samples, frame_size;
                        frame_size = frame[j];
                        if ((fast_rand() & 255) == 0)
                        {
                            enc.ResetState();
                            dec.ResetState();

                            if ((fast_rand() & 1) != 0)
                            {
                                dec_err[fast_rand() & 1].ResetState();
                            }
                        }

                        if ((fast_rand() & 127) == 0)
                        {
                            dec_err[fast_rand() & 1].ResetState();
                        }

                        if (fast_rand() % 10 == 0)
                        {
                            int complex = (int)(fast_rand() % 11);
                            enc.Complexity = (complex);
                        }

                        if (fast_rand() % 50 == 0)
                        {
                            dec.ResetState();
                        }
                        
                        enc.UseInbandFEC = (rc == 0);
                        enc.ForceMode = (OpusMode.MODE_SILK_ONLY + modes[j]);
                        enc.UseDTX = ((fast_rand() & 1) != 0);
                        enc.Bitrate = (rate);
                        enc.ForceChannels = (rates[j] >= 64000 ? 2 : 1);
                        enc.Complexity = ((count >> 2) % 11);
                        enc.PacketLossPercent = ((int)((fast_rand() & 15) & (fast_rand() % 15)));

                        bw = modes[j] == 0 ? OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND + (int)(fast_rand() % 3) :
                            modes[j] == 1 ? OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND + (int)(fast_rand() & 1) :
                            OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND + (int)(fast_rand() % 5);

                        if (modes[j] == 2 && bw == OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND)
                            bw += 3;
                        enc.Bandwidth = (bw);
                        len = enc.Encode(inbuf.Data, i << 1, frame_size, packet.Data, 0, MAX_PACKET);
                        if (len < 0 || len > MAX_PACKET) test_failed();
                        enc_final_range = enc.FinalRange;
                        if ((fast_rand() & 3) == 0)
                        {
                            if (OpusRepacketizer.PadPacket(packet.Data, packet.Offset, len, len + 1) != OpusError.OPUS_OK) test_failed();
                            len++;
                        }
                        if ((fast_rand() & 7) == 0)
                        {
                            if (OpusRepacketizer.PadPacket(packet.Data, packet.Offset, len, len + 256) != OpusError.OPUS_OK) test_failed();
                            len += 256;
                        }
                        if ((fast_rand() & 3) == 0)
                        {
                            len = OpusRepacketizer.UnpadPacket(packet.Data, packet.Offset, len);
                            if (len < 1) test_failed();
                        }
                        out_samples = dec.Decode(packet.Data, 0, len, outbuf.Data, i << 1, MAX_FRAME_SAMP, false);
                        if (out_samples != frame_size) test_failed();
                        dec_final_range = dec.FinalRange;
                        if (enc_final_range != dec_final_range) test_failed();
                        /*LBRR decode*/
                        out_samples = dec_err[0].Decode(packet.Data, 0, len, out2buf.Data, 0, frame_size, ((int)fast_rand() & 3) != 0);
                        if (out_samples != frame_size) test_failed();
                        out_samples = dec_err[1].Decode(packet.Data, 0, (fast_rand() & 3) == 0 ? 0 : len, out2buf.Data, 0, /*MAX_FRAME_SAMP*/ frame_size, ((int)fast_rand() & 7) != 0);
                        if (out_samples < 120) test_failed();
                        i += frame_size;
                        count++;
                    } while (i < (SSAMPLES - MAX_FRAME_SAMP));
                    Console.WriteLine("    Mode {0} FB encode {1}, {2} bps OK.", mstrings[modes[j]], rc == 0 ? " VBR" : rc == 1 ? "CVBR" : " CBR", rate);
                }
            }

            //if (opus_encoder_ctl(enc, OPUS_RESET_STATE) != OpusError.OPUS_OK) test_failed();
            //opus_encoder_destroy(enc);
            //if (opus_multistream_encoder_ctl(MSenc, OPUS_RESET_STATE) != OpusError.OPUS_OK) test_failed();
            //opus_multistream_encoder_destroy(MSenc);
            //if (OpusDecoder_ctl(dec, OPUS_RESET_STATE) != OpusError.OPUS_OK) test_failed();
            //OpusDecoder_destroy(dec);
            //if (opus_multistream_decoder_ctl(MSdec, OPUS_RESET_STATE) != OpusError.OPUS_OK) test_failed();

            return 0;
        }

        internal static int run_test2(bool no_fuzz)
        {
            byte[] mapping/*[256]*/ = { 0, 1, 255 };
            byte[] db62 = new byte[36];
            int i;
            int rc, j;
            BoxedValueInt err = new BoxedValueInt(0);
            OpusMSEncoder MSenc;
            OpusMSDecoder MSdec;
            OpusMSDecoder MSdec_err;
            OpusDecoder[] dec_err = new OpusDecoder[10];
            short[] inbuf;
            short[] out2buf;
            byte[] packet = new byte[MAX_PACKET + 257];
            uint enc_final_range;
            uint dec_final_range;
            int count;

            inbuf = new short[SAMPLES * 2];
            out2buf = new short[MAX_FRAME_SAMP * 3];
            if (inbuf == null || out2buf == null) test_failed();
            
            generate_music(inbuf.GetPointer(), SAMPLES);

            for (i = 0; i < 2; i++)
            {
                try
                {
                    MSenc = OpusMSEncoder.Create(8000, 2, 2, 0, mapping, OpusApplication.OPUS_APPLICATION_UNIMPLEMENTED); test_failed();
                }
                catch (ArgumentException) { }
                try
                {
                    MSenc = OpusMSEncoder.Create(8000, 0, 1, 0, mapping, OpusApplication.OPUS_APPLICATION_VOIP); test_failed();
                }
                catch (ArgumentException) { }
                try
                {
                    MSenc = OpusMSEncoder.Create(44100, 2, 2, 0, mapping, OpusApplication.OPUS_APPLICATION_VOIP); test_failed();
                }
                catch (ArgumentException) { }
                try
                {
                    MSenc = OpusMSEncoder.Create(8000, 2, 2, 3, mapping, OpusApplication.OPUS_APPLICATION_VOIP); test_failed();
                }
                catch (ArgumentException) { }
                try
                {
                    MSenc = OpusMSEncoder.Create(8000, 2, -1, 0, mapping, OpusApplication.OPUS_APPLICATION_VOIP); test_failed();
                }
                catch (ArgumentException) { }
                try
                {
                    MSenc = OpusMSEncoder.Create(8000, 256, 2, 0, mapping, OpusApplication.OPUS_APPLICATION_VOIP); test_failed();
                }
                catch (ArgumentException) { }
            }

            MSenc = OpusMSEncoder.Create(8000, 2, 2, 0, mapping, OpusApplication.OPUS_APPLICATION_AUDIO);
            if (err.Val != OpusError.OPUS_OK || MSenc == null) test_failed();

            MSdec = new OpusMSDecoder(48000, 2, 2, 0, mapping);
            if (err.Val != OpusError.OPUS_OK || MSdec == null) test_failed();

            MSdec_err = new OpusMSDecoder(48000, 3, 2, 0, mapping);
            if (err.Val != OpusError.OPUS_OK || MSdec_err == null) test_failed();

            /*Some multistream encoder API tests*/
            i = MSenc.Bitrate;
            i = MSenc.LSBDepth;
            if (i < 16) test_failed();

            {
                IOpusEncoder tmp_enc;
                tmp_enc = MSenc.GetMultistreamEncoderState(1);
                if (tmp_enc == null) test_failed();
                j = tmp_enc.LSBDepth;
                if (i != j) test_failed();
                try
                {
                    MSenc.GetMultistreamEncoderState(2);
                    test_failed();
                }
                catch (ArgumentException) { }
            }

            OpusMode[] modes = { OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_SILK_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY, OpusMode.MODE_CELT_ONLY };
            int[] rates = { 4000, 12000, 32000, 8000, 16000, 32000, 48000, 88000, 4000, 12000, 32000, 8000, 16000, 32000, 48000, 88000 };
            int[] frame = { 160 * 1, 160, 80, 160, 160, 80, 40, 20, 160 * 1, 160, 80, 160, 160, 80, 40, 20 };

            for (rc = 0; rc < 3; rc++)
            {
                MSenc.UseVBR = (rc < 2);
                MSenc.UseConstrainedVBR = (rc == 1);
                MSenc.UseInbandFEC = (rc == 0);
                for (j = 0; j < 16; j++)
                {
                    int rate;
                    MSenc.UseInbandFEC = (rc == 0 && j == 1);
                    MSenc.ForceMode = (modes[j]);
                    rate = rates[j] + ((int)fast_rand() % rates[j]);
                    MSenc.UseDTX = ((fast_rand() & 1U) != 0);
                    MSenc.Bitrate = (rate);
                    count = i = 0;
                    do
                    {
                        int len, out_samples, frame_size;
                        bool loss;
                        bool pred = MSenc.PredictionDisabled;
                        MSenc.PredictionDisabled = ((int)(fast_rand() & 15) < (pred ? 11 : 4));
                        frame_size = frame[j];
                        MSenc.Complexity = ((count >> 2) % 11);
                        MSenc.PacketLossPercent = (((int)fast_rand() & 15) & ((int)fast_rand() % 15));
                        if ((fast_rand() & 255) == 0)
                        {
                            MSenc.ResetState();
                            MSdec.ResetState();
                            if ((fast_rand() & 3) != 0)
                            {
                                MSdec_err.ResetState();
                            }
                        }
                        if ((fast_rand() & 255) == 0)
                        {
                            MSdec_err.ResetState();
                        }
                        len = MSenc.EncodeMultistream(inbuf, i << 1, frame_size, packet, 0, MAX_PACKET);
                        if (len < 0 || len > MAX_PACKET) test_failed();
                        enc_final_range = MSenc.FinalRange;
                        if ((fast_rand() & 3) == 0)
                        {
                            if (OpusRepacketizer.PadMultistreamPacket(packet, 0, len, len + 1, 2) != OpusError.OPUS_OK) test_failed();
                            len++;
                        }
                        if ((fast_rand() & 7) == 0)
                        {
                            if (OpusRepacketizer.PadMultistreamPacket(packet, 0, len, len + 256, 2) != OpusError.OPUS_OK) test_failed();
                            len += 256;
                        }
                        //if ((fast_rand() & 3) == 0)
                        //{
                        //    len = Repacketizer.opus_multistream_packet_unpad(packet, len, 2);
                        //    if (len < 1) test_failed();
                        //}
                        out_samples = MSdec.DecodeMultistream(packet, 0, len, out2buf, 0, MAX_FRAME_SAMP, false);
                        if (out_samples != frame_size * 6) test_failed();
                        dec_final_range = MSdec.FinalRange;
                        if (enc_final_range != dec_final_range) test_failed();
                        /*LBRR decode*/
                        loss = (fast_rand() & 63) == 0;
                        out_samples = MSdec_err.DecodeMultistream(packet, 0, loss ? 0 : len, out2buf, 0, frame_size * 6, (fast_rand() & 3) != 0);
                        if (out_samples != (frame_size * 6)) test_failed();
                        i += frame_size;
                        count++;
                    } while (i < (SSAMPLES / 12 - MAX_FRAME_SAMP));
                    Console.WriteLine("    Mode {0} NB dual-mono MS encode {1}, {2} bps OK.", mstrings[(int)modes[j] - (int)OpusMode.MODE_SILK_ONLY], rc == 0 ? " VBR" : rc == 1 ? "CVBR" : " CBR", rate);
                }
            }

            return 0;
        }

        internal static int run_test3(bool no_fuzz)
        {
            //bitrate_bps = 512000;
            //fsize = fast_rand() % 31;
            //fswitch = 100;

            //debruijn2(6, db62);
            //count = i = 0;
            //do
            //{
            //    unsigned char toc;
            //    const unsigned char* frames[48];
            //    short size[48];
            //    int payload_offset;
            //    opus_uint32 dec_final_range2;
            //    int jj, dec2;
            //    int len, out_samples;
            //    int frame_size = fsizes[db62[fsize]];
            //    opus_int32 offset = i % (SAMPLES - MAX_FRAME_SAMP);

            //    opus_encoder_ctl(enc, OPUS_SET_BITRATE(bitrate_bps));

            //    len = opus_encode(enc, &inbuf[offset << 1], frame_size, packet, MAX_PACKET);
            //    if (len < 0 || len > MAX_PACKET) test_failed();
            //    count++;

            //    opus_encoder_ctl(enc, OPUS_GET_FINAL_RANGE(&enc_final_range));

            //    out_samples = opus_decode(dec, packet, len, &outbuf[offset << 1], MAX_FRAME_SAMP, 0);
            //    if (out_samples != frame_size) test_failed();

            //    OpusDecoder_ctl(dec, OPUS_GET_FINAL_RANGE(&dec_final_range));

            //    /* compare final range encoder rng values of encoder and decoder */
            //    if (dec_final_range != enc_final_range) test_failed();

            //    /* We fuzz the packet, but take care not to only corrupt the payload
            //    Corrupted headers are tested elsewhere and we need to actually run
            //    the decoders in order to compare them. */
            //    if (opus_packet_parse(packet, len, &toc, frames, size, &payload_offset) <= 0) test_failed();
            //    if ((fast_rand() & 1023) == 0) len = 0;
            //    for (j = (frames[0] - packet); j < len; j++) for (jj = 0; jj < 8; jj++) packet[j] ^= ((!no_fuzz) && ((fast_rand() & 1023) == 0)) << jj;
            //    out_samples = opus_decode(dec_err[0], len > 0 ? packet : null, len, out2buf, MAX_FRAME_SAMP, 0);
            //    if (out_samples < 0 || out_samples > MAX_FRAME_SAMP) test_failed();
            //    if ((len > 0 && out_samples != frame_size)) test_failed(); /*FIXME use lastframe*/

            //    OpusDecoder_ctl(dec_err[0], OPUS_GET_FINAL_RANGE(&dec_final_range));

            //    /*randomly select one of the decoders to compare with*/
            //    dec2 = fast_rand() % 9 + 1;
            //    out_samples = opus_decode(dec_err[dec2], len > 0 ? packet : null, len, out2buf, MAX_FRAME_SAMP, 0);
            //    if (out_samples < 0 || out_samples > MAX_FRAME_SAMP) test_failed(); /*FIXME, use factor, lastframe for loss*/

            //    OpusDecoder_ctl(dec_err[dec2], OPUS_GET_FINAL_RANGE(&dec_final_range2));
            //    if (len > 0 && dec_final_range != dec_final_range2) test_failed();

            //    fswitch--;
            //    if (fswitch < 1)
            //    {
            //        int new_size;
            //        fsize = (fsize + 1) % 36;
            //        new_size = fsizes[db62[fsize]];
            //        if (new_size == 960 || new_size == 480) fswitch = 2880 / new_size * (fast_rand() % 19 + 1);
            //        else fswitch = (fast_rand() % (2880 / new_size)) + 1;
            //    }
            //    bitrate_bps = ((fast_rand() % 508000 + 4000) + bitrate_bps) >> 1;
            //    i += frame_size;
            //} while (i < SAMPLES * 4);
            //fprintf(stdout, "    All framesize pairs switching encode, %d frames OK.\n", count);

            return 0;
        }

        internal static void Main(string[] args)
        {
            iseed = (uint)new Random().Next();

            Rw = Rz = iseed;

            string oversion = CodecHelpers.GetVersionString();

            Console.WriteLine("Testing {0} encoder. Random seed: {1}", oversion, iseed);
            run_test1(true);
            run_test2(true);
            run_test3(true);

            Console.WriteLine("Tests completed successfully.");
        }
    }
}
