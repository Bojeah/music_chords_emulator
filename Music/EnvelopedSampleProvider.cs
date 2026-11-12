using NAudio.Wave;

namespace MusicChordsEmulator.Music
{
    public class EnvelopedSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly EnvelopeGenerator _envelope;

        public WaveFormat WaveFormat => _source.WaveFormat;
        public bool IsFinished => _envelope.IsFinished;

        public EnvelopedSampleProvider(ISampleProvider source, EnvelopeGenerator envelope)
        {
            _source = source;
            _envelope = envelope;
        }

        public int Read(Span<float> buffer)
        {
            int count = _source.Read(buffer);
            for (int i = 0; i < count; i++)
                buffer[i] *= _envelope.NextAmplitude();
            return count;
        }

        public void Release() => _envelope.Release();
    }
}
