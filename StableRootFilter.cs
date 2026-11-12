using MusicChordsEmulator.Audio;

namespace MusicChordsEmulator
{
    public sealed class StableRootFilter
    {
        private PitchClass? _candidate;

        private PitchClass? _stable;

        private int _candidateFrames;

        private readonly int _requiredFrames;

        public StableRootFilter(
            int requiredFrames = 4)
        {
            _requiredFrames =
                Math.Max(
                    1,
                    requiredFrames
                );
        }

        public PitchClass? Update(
            PitchClass? detected)
        {
            if (!detected.HasValue)
            {
                Reset();
                return null;
            }

            if (_candidate == detected)
            {
                _candidateFrames++;
            }
            else
            {
                _candidate =
                    detected;

                _candidateFrames = 1;
            }

            if (_candidateFrames >=
                _requiredFrames)
            {
                _stable =
                    _candidate;
            }

            return _stable;
        }

        public void Reset()
        {
            _candidate = null;
            _stable = null;
            _candidateFrames = 0;
        }
    }
}