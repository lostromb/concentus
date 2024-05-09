package org.concentus.console;

import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import org.concentus.*;
import org.gagravarr.ogg.*;
import org.gagravarr.opus.*;

/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
/**
 *
 * @author lostromb
 */
public class Program {

    /**
     * @param args the command line arguments
     */
    public static void main(String[] args) {

        try {
            FileInputStream fileIn = new FileInputStream("F:\\concentus\\AudioData\\48Khz Stereo.raw");
            OpusEncoder encoder = new OpusEncoder(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.setBitrate(96000);
            encoder.setSignalType(OpusSignal.OPUS_SIGNAL_MUSIC);
            encoder.setComplexity(10);
            
            int frameSizeMilliseconds = 20;
            int encoderSampleRate = 48000;
            FileOutputStream fileOut = new FileOutputStream("F:\\concentus\\AudioData\\48Khz Stereo.opus");
            OpusInfo info = new OpusInfo();
            info.setNumChannels(2);
            info.setSampleRate(encoderSampleRate);
            OpusTags tags = new OpusTags();
            //tags.setVendor("Concentus");
            //tags.addComment("title", "A test!");
            OpusFile file = new OpusFile(fileOut, info, tags);
            int packetSamples = encoderSampleRate * frameSizeMilliseconds / 1000;
            byte[] inBuf = new byte[packetSamples * 2 * 2];
            byte[] data_packet = new byte[1275];
            long start = System.currentTimeMillis();
            long granulePos = 0;
            while (fileIn.available() >= inBuf.length) {
                int bytesRead = fileIn.read(inBuf, 0, inBuf.length);
                short[] pcm = BytesToShorts(inBuf, 0, inBuf.length);
                int bytesEncoded = encoder.encode(pcm, 0, packetSamples, data_packet, 0, 1275);
                byte[] packet = new byte[bytesEncoded];
                System.arraycopy(data_packet, 0, packet, 0, bytesEncoded);
                OpusAudioData data = new OpusAudioData(packet);
                // The ogg library should be handling granule positions automatically but for some reason it doesn't.
                // BUT when we do this, it ends up writing only a single packet per ogg page which has an absurd
                // level of container overhead. File a bug on vorbis-java or find a new ogg library...
                granulePos += (48000 * frameSizeMilliseconds / 1000);
                data.setGranulePosition(granulePos);
                file.writeAudioData(data);
            }
            file.close();
            
            long end = System.currentTimeMillis();
            System.out.println("Time was " + (end - start) + "ms");
            fileIn.close();
            //fileOut.close();
            System.out.println("Done!");
        } catch (IOException e) {
            System.out.println(e.getMessage());
        } catch (OpusException e) {
            System.out.println(e.getMessage());
        }
    }
    
    public static void test()
    {
        try {
            FileInputStream fileIn = new FileInputStream("C:\\Users\\lostromb\\Documents\\Visual Studio 2015\\Projects\\Concentus-git\\AudioData\\48Khz Stereo.raw");
            OpusEncoder encoder = new OpusEncoder(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.setBitrate(96000);
            encoder.setForceMode(OpusMode.MODE_CELT_ONLY);
            encoder.setSignalType(OpusSignal.OPUS_SIGNAL_MUSIC);
            encoder.setComplexity(0);
            
            OpusDecoder decoder = new OpusDecoder(48000, 2);

            FileOutputStream fileOut = new FileOutputStream("C:\\Users\\lostromb\\Documents\\Visual Studio 2015\\Projects\\Concentus-git\\AudioData\\out_j.raw");
            int packetSamples = 960;
            byte[] inBuf = new byte[packetSamples * 2 * 2];
            byte[] data_packet = new byte[1275];
            long start = System.currentTimeMillis();
            while (fileIn.available() >= inBuf.length) {
                int bytesRead = fileIn.read(inBuf, 0, inBuf.length);
                short[] pcm = BytesToShorts(inBuf, 0, inBuf.length);
                int bytesEncoded = encoder.encode(pcm, 0, packetSamples, data_packet, 0, 1275);
                //System.out.println(bytesEncoded + " bytes encoded");

                int samplesDecoded = decoder.decode(data_packet, 0, bytesEncoded, pcm, 0, packetSamples, false);
                //System.out.println(samplesDecoded + " samples decoded");
                byte[] bytesOut = ShortsToBytes(pcm);
                fileOut.write(bytesOut, 0, bytesOut.length);
            }
            
            long end = System.currentTimeMillis();
            System.out.println("Time was " + (end - start) + "ms");
            fileIn.close();
            fileOut.close();
            System.out.println("Done!");
        } catch (IOException e) {
            System.out.println(e.getMessage());
        } catch (OpusException e) {
            System.out.println(e.getMessage());
        }
    }

    /// <summary>
    /// Converts interleaved byte samples (such as what you get from a capture device)
    /// into linear short samples (that are much easier to work with)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static short[] BytesToShorts(byte[] input) {
        return BytesToShorts(input, 0, input.length);
    }

    /// <summary>
    /// Converts interleaved byte samples (such as what you get from a capture device)
    /// into linear short samples (that are much easier to work with)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static short[] BytesToShorts(byte[] input, int offset, int length) {
        short[] processedValues = new short[length / 2];
        for (int c = 0; c < processedValues.length; c++) {
            short a = (short) (((int) input[(c * 2) + offset]) & 0xFF);
            short b = (short) (((int) input[(c * 2) + 1 + offset]) << 8);
            processedValues[c] = (short) (a | b);
        }

        return processedValues;
    }

    /// <summary>
    /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static byte[] ShortsToBytes(short[] input) {
        return ShortsToBytes(input, 0, input.length);
    }

    /// <summary>
    /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static byte[] ShortsToBytes(short[] input, int offset, int length) {
        byte[] processedValues = new byte[length * 2];
        for (int c = 0; c < length; c++) {
            processedValues[c * 2] = (byte) (input[c + offset] & 0xFF);
            processedValues[c * 2 + 1] = (byte) ((input[c + offset] >> 8) & 0xFF);
        }

        return processedValues;
    }
}
