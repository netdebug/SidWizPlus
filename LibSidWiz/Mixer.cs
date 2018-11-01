﻿using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NReplayGain;

namespace LibSidWiz
{
    /// <summary>
    /// Deals with mixing audio to a "master file"
    /// </summary>
    public class Mixer
    {
        public static void MixToFile(IList<Channel> channels, string filename, bool applyReplayGain)
        {
            Console.WriteLine("Mixing per-channel data...");

            // We make new readers...
            IList<WaveFileReader> readers = new List<WaveFileReader>();
            try
            {
                foreach (var channel in channels)
                {
                    readers.Add(new WaveFileReader(channel.Filename));
                }

                if (applyReplayGain)
                {
                    Console.WriteLine("Computing ReplayGain...");
                    // We read it in a second at a time, to calculate Replay Gains
                    var mixer = new MixingSampleProvider(readers.Select(x => x.ToSampleProvider().ToStereo()));
                    // We use a 1s buffer...
                    var buffer = new float[mixer.WaveFormat.SampleRate * 2];
                    var replayGain = new TrackGain(channels.First().SampleRate);
                    for (;;)
                    {
                        int numRead = mixer.Read(buffer, 0, buffer.Length);
                        if (numRead == 0)
                        {
                            break;
                        }

                        // And analyze
                        replayGain.AnalyzeSamples(buffer, numRead);
                    }

                    // The +3 is to make it at "YouTube loudness", which is a lot louder than ReplayGain defaults to.
                    // TODO make this configurable?
                    var gain = replayGain.GetGain() + 3;

                    Console.WriteLine($"Applying ReplayGain ({gain:N} dB) and saving to {filename}");

                    // Reset the readers
                    foreach (var reader in readers)
                    {
                        reader.Position = 0;
                    }

                    // We make a new mixer just in case resetting the previous one is problematic...
                    mixer = new MixingSampleProvider(readers.Select(x => x.ToSampleProvider().ToStereo()));
                    var amplifier = new VolumeSampleProvider(mixer) {Volume = (float) Math.Pow(10, gain / 20)};
                    WaveFileWriter.CreateWaveFile(filename, amplifier.ToWaveProvider());
                }
                else
                {
                    var mixer = new MixingSampleProvider(readers.Select(x => x.ToSampleProvider().ToStereo()));
                    WaveFileWriter.CreateWaveFile(filename, mixer.ToWaveProvider());
                }
            }
            finally
            {
                foreach (var reader in readers)
                {
                    reader.Dispose();
                }
            }
        }
    }
}