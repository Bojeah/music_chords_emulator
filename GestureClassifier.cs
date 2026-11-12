using MusicChordsEmulator.Audio;

namespace MusicChordsEmulator
{
    public static class GestureClassifier
    {
        private const double ExtendedAngleThreshold = 155.0;

        private const double ThumbExtendedAngleThreshold = 135.0;

        public static bool[] GetFingerStates(HandLandmarks hand)
        {
            /*
             * Order:
             * thumb, index, middle, ring, pinky
             */
            return new[]
            {
                IsThumbExtended(hand),
                IsFingerExtended(hand, 5, 6, 7, 8),
                IsFingerExtended(hand, 9, 10, 11, 12),
                IsFingerExtended(hand, 13, 14, 15, 16),
                IsFingerExtended(hand, 17, 18, 19, 20),
            };
        }

        public static int CountExtendedFingers(HandLandmarks hand)
        {
            return GetFingerStates(hand).Count(isExtended => isExtended);
        }

        public static HandGesture ClassifyGesture(HandLandmarks hand)
        {
            bool[] fingers = GetFingerStates(hand);

            int count = fingers.Count(x => x);

            /*
             * Don't blindly turn every frame into a chord.
             *
             * A very small / badly shaped hand should remain unknown.
             */
            if (!IsHandGeometryValid(hand))
                return HandGesture.Unknown;

            return count switch
            {
                0 => HandGesture.Fist,
                1 => HandGesture.OneFinger,
                2 => HandGesture.TwoFingers,
                3 => HandGesture.ThreeFingers,
                4 => HandGesture.FourFingers,
                5 => HandGesture.OpenPalm,
                _ => HandGesture.Unknown,
            };
        }

        public static ChordQuality? ClassifyQuality(HandLandmarks hand)
        {
            return ClassifyGesture(hand) switch
            {
                HandGesture.Fist => ChordQuality.Minor,

                HandGesture.OneFinger => ChordQuality.Diminished,

                HandGesture.TwoFingers => ChordQuality.Augmented,

                HandGesture.ThreeFingers => ChordQuality.Dominant7,

                HandGesture.FourFingers => ChordQuality.Major,

                HandGesture.OpenPalm => ChordQuality.Major,

                _ => null,
            };
        }

        public static PitchClass ClassifyRoot(HandLandmarks hand, double frameWidth)
        {
            if (frameWidth <= 0)
                return PitchClass.C;

            /*
             * Use palm center rather than wrist.
             *
             * Palm center is much less sensitive to wrist
             * articulation.
             */
            double normalizedX = hand.PalmCenter.X / frameWidth;

            normalizedX = Math.Clamp(normalizedX, 0.0, 0.999999);

            int index = (int)(normalizedX * 12);

            return (PitchClass)Math.Clamp(index, 0, 11);
        }

        private static bool IsFingerExtended(HandLandmarks hand, int mcp, int pip, int dip, int tip)
        {
            double pipAngle = Angle(hand[mcp], hand[pip], hand[dip]);

            double dipAngle = Angle(hand[pip], hand[dip], hand[tip]);

            return pipAngle >= ExtendedAngleThreshold && dipAngle >= ExtendedAngleThreshold;
        }

        private static bool IsThumbExtended(HandLandmarks hand)
        {
            /*
             * Thumb:
             * 1 = MCP
             * 2 = IP
             * 3 = TIP
             * 0 = wrist
             */
            double thumbAngle = Angle(hand[1], hand[2], hand[3]);

            /*
             * The thumb is also expected to be separated
             * from the index MCP area.
             */
            double thumbTipDistance = hand[4].DistanceTo(hand[5]);

            double thumbMcpDistance = hand[2].DistanceTo(hand[5]);

            return thumbAngle >= ThumbExtendedAngleThreshold
                && thumbTipDistance > thumbMcpDistance * 0.9;
        }

        private static double Angle(Point3D a, Point3D b, Point3D c)
        {
            double abX = a.X - b.X;
            double abY = a.Y - b.Y;

            double cbX = c.X - b.X;
            double cbY = c.Y - b.Y;

            double magnitudeAB = Math.Sqrt(abX * abX + abY * abY);

            double magnitudeCB = Math.Sqrt(cbX * cbX + cbY * cbY);

            if (magnitudeAB < 1e-6 || magnitudeCB < 1e-6)
            {
                return 0;
            }

            double dot = abX * cbX + abY * cbY;

            double cosine = dot / (magnitudeAB * magnitudeCB);

            cosine = Math.Clamp(cosine, -1.0, 1.0);

            return Math.Acos(cosine) * 180.0 / Math.PI;
        }

        private static bool IsHandGeometryValid(HandLandmarks hand)
        {
            double palmWidth = hand.Distance(5, 17);

            double palmHeight = hand.Distance(0, 9);

            if (palmWidth < 5 || palmHeight < 5)
            {
                return false;
            }

            return true;
        }
    }
}
