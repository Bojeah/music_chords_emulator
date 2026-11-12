namespace MusicChordsEmulator.Audio
{
    public readonly struct Pitch
    {
        public PitchClass Note { get; }
        public int Octave { get; }

        public Pitch(PitchClass note, int octave)
        {
            Note = note;
            Octave = octave;
        }

        public double ToFrequencyHz()
        {
            int midiNumber = 12 * (Octave + 1) + (int)Note;
            return 440.0 * Math.Pow(2.0, (midiNumber - 69) / 12.0);
        }

        public override string ToString() => $"{Note}{Octave}";
    }
}
