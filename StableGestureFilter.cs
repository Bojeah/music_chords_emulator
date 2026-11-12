namespace MusicChordsEmulator
{
    public sealed class StableGestureFilter
    {
        private HandGesture _candidate = HandGesture.Unknown;

        private HandGesture _stable = HandGesture.Unknown;

        private int _candidateFrames;

        private readonly int _requiredFrames;

        public StableGestureFilter(int requiredFrames = 6)
        {
            _requiredFrames = Math.Max(1, requiredFrames);
        }

        public HandGesture Update(HandGesture detected)
        {
            if (detected == HandGesture.Unknown)
            {
                _candidate = HandGesture.Unknown;

                _candidateFrames = 0;

                return _stable;
            }

            if (detected == _candidate)
            {
                _candidateFrames++;
            }
            else
            {
                _candidate = detected;

                _candidateFrames = 1;
            }

            if (_candidateFrames >= _requiredFrames)
            {
                _stable = _candidate;
            }

            return _stable;
        }

        public void Reset()
        {
            _candidate = HandGesture.Unknown;

            _stable = HandGesture.Unknown;

            _candidateFrames = 0;
        }

        public HandGesture StableGesture => _stable;
    }
}
