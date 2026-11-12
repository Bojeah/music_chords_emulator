using NAudio.Wave;

namespace MusicChordsEmulator.Music
{
    public class CleanupSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Action _onRead;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public CleanupSampleProvider(ISampleProvider source, Action onRead)
        {
            _source = source;
            _onRead = onRead;
        }

        public int Read(Span<float> buffer)
        {
            int count = _source.Read(buffer);
            _onRead();
            return count;
        }
    }
}
