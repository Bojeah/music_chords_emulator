using NAudio.Wave;

namespace MusicChordsEmulator.Music
{
    public class Oscillator : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }
        public double Frequency { get; set; }

        private double _phase;
        private readonly int _sampleRate;

        public Oscillator(double frequency, int sampleRate = 44100)
        {
            Frequency = frequency;
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        }

        public int Read(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)Math.Sin(_phase);
                _phase += 2 * Math.PI * Frequency / _sampleRate;
                if (_phase > 2 * Math.PI)
                    _phase -= 2 * Math.PI;
            }
            return buffer.Length;
        }
    }
}
