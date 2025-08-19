/* Copyright (c) 2022 Amazon */
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

using static HellaUnsafe.Celt.Arch;
using static HellaUnsafe.Common.CRuntime;
using static HellaUnsafe.Opus.OpusDefines;
using static HellaUnsafe.Opus.OpusPrivate;
using static HellaUnsafe.Silk.SigProcFIX;

namespace HellaUnsafe.Opus
{
    internal static unsafe class Extensions
    {
        /* Given an extension payload, advance data to the next extension and return the
           length of the remaining extensions. */
        internal static unsafe int skip_extension(in byte** data, int len, int* header_size)
        {
            int id, L;
            if (len == 0)
                return 0;
            id = **data >> 1;
            L = **data & 1;
            if (id == 0 && L == 1)
            {
                *header_size = 1;
                if (len < 1)
                    return -1;
                (*data)++;
                len--;
                return len;
            }
            else if (id > 0 && id < 32)
            {
                if (len < 1 + L)
                    return -1;
                *data += 1 + L;
                len -= 1 + L;
                *header_size = 1;
                return len;
            }
            else
            {
                if (L == 0)
                {
                    *data += len;
                    *header_size = 1;
                    return 0;
                }
                else
                {
                    int bytes = 0;
                    *header_size = 1;
                    do
                    {
                        (*data)++;
                        len--;
                        if (len == 0)
                            return -1;
                        bytes += **data;
                        (*header_size)++;
                    } while (**data == 255);
                    (*data)++;
                    len--;
                    if (bytes <= len)
                    {
                        len -= bytes;
                        *data += bytes;
                    }
                    else
                    {
                        return -1;
                    }
                    return len;
                }
            }
        }

        /* Count the number of extensions, excluding real padding and separators. */
        internal static unsafe int opus_packet_extensions_count(in byte* data, int len)
        {
            int curr_len;
            int count = 0;
            byte* curr_data = data;

            celt_assert(len >= 0);
            celt_assert(data != null || len == 0);

            curr_len = len;
            while (curr_len > 0)
            {
                int id;
                int header_size;
                id = *curr_data >> 1;
                curr_len = skip_extension(&curr_data, curr_len, &header_size);
                if (curr_len < 0)
                    return OPUS_INVALID_PACKET;
                if (id > 1)
                    count++;
            }
            return count;
        }

        /* Extract extensions from Opus padding (excluding real padding and separators) */
        internal static unsafe int opus_packet_extensions_parse(in byte* data, int len, opus_extension_data* extensions, int* nb_extensions)
        {
            byte* curr_data;
            int curr_len;
            int curr_frame = 0;
            int count = 0;

            celt_assert(len >= 0);
            celt_assert(data != null || len == 0);
            celt_assert(nb_extensions != null);
            celt_assert(extensions != null || *nb_extensions == 0);

            curr_data = data;
            curr_len = len;
            while (curr_len > 0)
            {
                int id;
                int header_size;
                opus_extension_data curr_ext = default;
                id = *curr_data >> 1;
                if (id > 1)
                {
                    curr_ext.id = id;
                    curr_ext.frame = curr_frame;
                    curr_ext.data = curr_data;
                }
                else if (id == 1)
                {
                    int L = *curr_data & 1;
                    if (L == 0)
                        curr_frame++;
                    else
                    {
                        if (curr_len >= 2)
                            curr_frame += curr_data[1];
                        /* Else we're at the end and it doesn't matter. */
                    }
                    if (curr_frame >= 48)
                    {
                        *nb_extensions = count;
                        return OPUS_INVALID_PACKET;
                    }
                }
                curr_len = skip_extension(&curr_data, curr_len, &header_size);
                /* printf("curr_len = %d, header_size = %d\n", curr_len, header_size); */
                if (curr_len < 0)
                {
                    *nb_extensions = count;
                    return OPUS_INVALID_PACKET;
                }
                celt_assert(curr_data - data == len - curr_len);
                if (id > 1)
                {
                    if (count == *nb_extensions)
                    {
                        return OPUS_BUFFER_TOO_SMALL;
                    }
                    curr_ext.len = (int)(curr_data - curr_ext.data - header_size);
                    curr_ext.data += header_size;
                    extensions[count++] = curr_ext;
                }
            }
            celt_assert(curr_len == 0);
            *nb_extensions = count;
            return OPUS_OK;
        }

        internal static unsafe int opus_packet_extensions_generate(byte* data, int len, in opus_extension_data* extensions, int nb_extensions, int pad)
        {
            int max_frame = 0;
            int i;
            int frame;
            int curr_frame = 0;
            int pos = 0;
            int written = 0;

            celt_assert(len >= 0);

            for (i = 0; i < nb_extensions; i++)
            {
                max_frame = IMAX(max_frame, extensions[i].frame);
                if (extensions[i].id < 2 || extensions[i].id > 127)
                    return OPUS_BAD_ARG;
            }
            if (max_frame >= 48) return OPUS_BAD_ARG;
            for (frame = 0; frame <= max_frame; frame++)
            {
                for (i = 0; i < nb_extensions; i++)
                {
                    if (extensions[i].frame == frame)
                    {
                        /* Insert separator when needed. */
                        if (frame != curr_frame)
                        {
                            int diff = frame - curr_frame;
                            if (len - pos < 2)
                                return OPUS_BUFFER_TOO_SMALL;
                            if (diff == 1)
                            {
                                if (data != null) data[pos] = 0x02;
                                pos++;
                            }
                            else
                            {
                                if (data != null) data[pos] = 0x03;
                                pos++;
                                if (data != null) data[pos] = (byte)diff;
                                pos++;
                            }
                            curr_frame = frame;
                        }
                        if (extensions[i].id < 32)
                        {
                            if (extensions[i].len < 0 || extensions[i].len > 1)
                                return OPUS_BAD_ARG;
                            if (len - pos < extensions[i].len + 1)
                                return OPUS_BUFFER_TOO_SMALL;
                            if (data != null) data[pos] = (byte)((extensions[i].id << 1) + extensions[i].len);
                            pos++;
                            if (extensions[i].len > 0)
                            {
                                if (data != null) data[pos] = extensions[i].data[0];
                                pos++;
                            }
                        }
                        else
                        {
                            int last;
                            int length_bytes;
                            if (extensions[i].len < 0)
                                return OPUS_BAD_ARG;
                            last = BOOL2INT(written == nb_extensions - 1);
                            length_bytes = 1 + extensions[i].len / 255;
                            if (last != 0)
                                length_bytes = 0;
                            if (len - pos < 1 + length_bytes + extensions[i].len)
                                return OPUS_BUFFER_TOO_SMALL;
                            if (data != null) data[pos] = (byte)((extensions[i].id << 1) + BOOL2INT(last == 0));
                            pos++;
                            if (last == 0)
                            {
                                int j;
                                for (j = 0; j < extensions[i].len / 255; j++)
                                {
                                    if (data != null) data[pos] = 255;
                                    pos++;
                                }
                                if (data != null) data[pos] = (byte)(extensions[i].len % 255);
                                pos++;
                            }
                            if (data != null) OPUS_COPY(&data[pos], extensions[i].data, extensions[i].len);
                            pos += extensions[i].len;
                        }
                        written++;
                    }
                }
            }
            /* If we need to pad, just prepend 0x01 bytes. Even better would be to fill the
               end with zeros, but that requires checking that turning the last extesion into
               an L=1 case still fits. */
            if (pad != 0 && pos < len)
            {
                int padding = len - pos;
                if (data != null)
                {
                    OPUS_MOVE(data + padding, data, pos);
                    for (i = 0; i < padding; i++)
                        data[i] = 0x01;
                }
                pos += padding;
            }
            return pos;
        }
    }
}
