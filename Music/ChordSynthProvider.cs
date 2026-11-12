using NAudio.Wave;
using MusicChordsEmulator.Audio;

namespace MusicChordsEmulator.Music
{
    public class ChordSynthProvider : ISampleProvider
    {
        private readonly List<Oscillator> _voices;
        public WaveFormat WaveFormat { get; }

        public ChordSynthProvider(IEnumerable<Pitch> pitches, int sampleRate = 44100)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _voices = pitches.Select(p => new Oscillator(p.ToFrequencyHz(), sampleRate)).ToList();
        }

        public int Read(Span<float> buffer)
        {
            buffer.Clear();
            Span<float> voiceBuffer = stackalloc float[buffer.Length];

            foreach (var voice in _voices)
            {
                voice.Read(voiceBuffer);
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] += voiceBuffer[i];
            }

            if (_voices.Count > 0)
            {
                float scale = 1f / _voices.Count;
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] *= scale;
            }

            return buffer.Length;
        }
    }
}
