namespace MusicChordsEmulator.Audio
{
    public class Chord
    {
        private static readonly Dictionary<ChordQuality, int[]> Intervals = new()
        {
            { ChordQuality.Major, new[] { 0, 4, 7 } },
            { ChordQuality.Minor, new[] { 0, 3, 7 } },
            { ChordQuality.Diminished, new[] { 0, 3, 6 } },
            { ChordQuality.Augmented, new[] { 0, 4, 8 } },
            { ChordQuality.Dominant7, new[] { 0, 4, 7, 10 } },
        };
        public PitchClass Root { get; }
        public ChordQuality Quality { get; }
        public IReadOnlyList<Pitch> Notes { get; }

        public Chord(PitchClass root, int rootOctave, ChordQuality quality)
        {
            Root = root;
            Quality = quality;
            Notes = BuildNotes(root, rootOctave, quality);
        }

        private static List<Pitch> BuildNotes(PitchClass root, int rootOctave, ChordQuality quality)
        {
            var notes = new List<Pitch>();
            foreach (int interval in Intervals[quality])
            {
                int totalSemitones = (int)root + interval;
                int noteValue = totalSemitones % 12;
                int octaveOffset = totalSemitones / 12;
                notes.Add(new Pitch((PitchClass)noteValue, rootOctave + octaveOffset));
            }
            return notes;
        }
    }
}
