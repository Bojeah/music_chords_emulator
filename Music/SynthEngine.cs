using System;
using System.Collections.Generic;
using MusicChordsEmulator.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicChordsEmulator.Music
{
    public class SynthEngine : IDisposable
    {
        private readonly MixingSampleProvider _mixer;

        private readonly WaveOut _output;

        private readonly List<EnvelopedSampleProvider> _activeVoices = new();

        private readonly object _lock = new();

        private bool _disposed;

        public SynthEngine(int sampleRate = 44100)
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

            _mixer = new MixingSampleProvider(format) { ReadFully = true };

            _output = new WaveOut();

            _output.Init(new CleanupSampleProvider(_mixer, CleanupFinishedVoices));

            _output.Play();
        }

        public void PlayChord(Chord chord)
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                foreach (var voice in _activeVoices)
                {
                    if (!voice.IsFinished)
                        voice.Release();
                }

                var synth = new ChordSynthProvider(chord.Notes);

                var envelope = new EnvelopeGenerator(
                    attack: 0.05,
                    decay: 0.1,
                    sustain: 0.7,
                    release: 0.8
                );

                var newVoice = new EnvelopedSampleProvider(synth, envelope);

                _activeVoices.Add(newVoice);

                _mixer.AddMixerInput(newVoice);
            }
        }

        public void Stop()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                foreach (var voice in _activeVoices)
                {
                    if (!voice.IsFinished)
                        voice.Release();
                }
            }
        }

        private void CleanupFinishedVoices()
        {
            lock (_lock)
            {
                for (int i = _activeVoices.Count - 1; i >= 0; i--)
                {
                    var voice = _activeVoices[i];

                    if (!voice.IsFinished)
                        continue;

                    try
                    {
                        _mixer.RemoveMixerInput(voice);
                    }
                    catch
                    {
                        // Voice may already have been removed.
                    }

                    _activeVoices.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_lock)
            {
                foreach (var voice in _activeVoices)
                {
                    if (!voice.IsFinished)
                        voice.Release();
                }

                _activeVoices.Clear();
            }

            _output.Stop();
            _output.Dispose();
        }
    }
}
