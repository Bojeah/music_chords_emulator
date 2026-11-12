namespace MusicChordsEmulator
{
    public sealed class HandLandmarkSmoother
    {
        private HandLandmarks? _previous;

        private readonly double _amount;

        public HandLandmarkSmoother(double amount = 0.45)
        {
            _amount = Math.Clamp(amount, 0.05, 1.0);
        }

        public HandLandmarks Update(HandLandmarks current)
        {
            if (_previous == null)
            {
                _previous = current;

                return current;
            }

            _previous = _previous.SmoothTowards(current, _amount);

            return _previous;
        }

        public void Reset()
        {
            _previous = null;
        }
    }
}
