using System;
using MusicChordsEmulator.Audio;
using MusicChordsEmulator.Music;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicChordsEmulator
{
    public class AudioEngine : IDisposable
    {
        private readonly WaveOutEvent _outputDevice;
        private readonly MixingSampleProvider _mixer;
        private EnvelopedSampleProvider _activeChordProvider;
        private PitchClass? _currentRoot;
        private ChordQuality? _currentQuality;

        public AudioEngine()
        {
            _outputDevice = new WaveOutEvent();
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
            _mixer.ReadFully = true;
            _outputDevice.Init(_mixer);
            _outputDevice.Play();
        }

        public void PlayChord(PitchClass root, ChordQuality quality)
        {
            if (_currentRoot == root && _currentQuality == quality)
                return;

            if (_activeChordProvider != null && !_activeChordProvider.IsFinished)
            {
                _activeChordProvider.Release();
            }

            _currentRoot = root;
            _currentQuality = quality;

            var chord = new Chord(root, 4, quality);
            var synth = new ChordSynthProvider(chord.Notes, 44100);
            var envelope = new EnvelopeGenerator(0.05, 0.1, 0.7, 0.5, 44100);
            var envelopedSynth = new EnvelopedSampleProvider(synth, envelope);

            var autoRemoveProvider = new AutoRemoveSampleProvider(envelopedSynth);

            _activeChordProvider = envelopedSynth;
            _mixer.AddMixerInput(autoRemoveProvider);
        }

        public void StopChord()
        {
            if (_activeChordProvider != null && !_activeChordProvider.IsFinished)
            {
                _activeChordProvider.Release();
            }
            _currentRoot = null;
            _currentQuality = null;
        }

        public void Dispose()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
        }
    }

    public class AutoRemoveSampleProvider : ISampleProvider
    {
        private readonly EnvelopedSampleProvider _source;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public AutoRemoveSampleProvider(EnvelopedSampleProvider source)
        {
            _source = source;
        }

        public int Read(Span<float> buffer)
        {
            if (_source.IsFinished)
                return 0;

            return _source.Read(buffer);
        }
    }
}
