namespace MusicChordsEmulator.Music
{
    public class EnvelopeGenerator
    {
        private readonly double _attackSeconds,
            _decaySeconds,
            _sustainLevel,
            _releaseSeconds;
        private readonly int _sampleRate;
        private EnvelopeStage _stage = EnvelopeStage.Attack;
        private long _samplesInStage;
        private double _currentLevel;

        public bool IsFinished => _stage == EnvelopeStage.Finished;

        public EnvelopeGenerator(
            double attack,
            double decay,
            double sustain,
            double release,
            int sampleRate = 44100
        )
        {
            _attackSeconds = attack;
            _decaySeconds = decay;
            _sustainLevel = sustain;
            _releaseSeconds = release;
            _sampleRate = sampleRate;
        }

        public void Release()
        {
            _stage = EnvelopeStage.Release;
            _samplesInStage = 0;
        }

        public float NextAmplitude()
        {
            switch (_stage)
            {
                case EnvelopeStage.Attack:
                    int attackSamples = (int)(_attackSeconds * _sampleRate);
                    _currentLevel =
                        attackSamples == 0 ? 1.0 : (double)_samplesInStage / attackSamples;
                    if (_samplesInStage++ >= attackSamples)
                    {
                        _stage = EnvelopeStage.Decay;
                        _samplesInStage = 0;
                    }
                    break;

                case EnvelopeStage.Decay:
                    int decaySamples = (int)(_decaySeconds * _sampleRate);
                    double decayProgress =
                        decaySamples == 0 ? 1.0 : (double)_samplesInStage / decaySamples;
                    _currentLevel = 1.0 - decayProgress * (1.0 - _sustainLevel);
                    if (_samplesInStage++ >= decaySamples)
                    {
                        _stage = EnvelopeStage.Sustain;
                    }
                    break;

                case EnvelopeStage.Sustain:
                    _currentLevel = _sustainLevel;
                    break;

                case EnvelopeStage.Release:
                    int releaseSamples = (int)(_releaseSeconds * _sampleRate);
                    double releaseProgress =
                        releaseSamples == 0 ? 1.0 : (double)_samplesInStage / releaseSamples;
                    _currentLevel = _sustainLevel * (1.0 - releaseProgress);
                    if (_samplesInStage++ >= releaseSamples)
                    {
                        _stage = EnvelopeStage.Finished;
                        _currentLevel = 0;
                    }
                    break;

                case EnvelopeStage.Finished:
                    _currentLevel = 0;
                    break;
            }
            return (float)_currentLevel;
        }
    }
}
