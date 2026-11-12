using OpenCvSharp;

namespace MusicChordsEmulator
{
    public sealed class HandTracker
    {
        private Point2f? _lastCenter;

        private int _framesSinceDetection;

        private readonly int _maxFramesWithoutDetection;

        public HandTracker(int maxFramesWithoutDetection = 8)
        {
            _maxFramesWithoutDetection = maxFramesWithoutDetection;
        }

        public PalmDetection? SelectDetection(IReadOnlyList<PalmDetection> detections)
        {
            if (detections.Count == 0)
            {
                _framesSinceDetection++;

                if (_framesSinceDetection > _maxFramesWithoutDetection)
                {
                    Reset();
                }

                return null;
            }

            PalmDetection selected;

            if (!_lastCenter.HasValue)
            {
                selected = detections
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Area)
                    .First();
            }
            else
            {
                Point2f previous = _lastCenter.Value;

                selected = detections
                    .OrderBy(x => Distance(x.Center, previous))
                    .ThenByDescending(x => x.Score)
                    .First();
            }

            _lastCenter = selected.Center;

            _framesSinceDetection = 0;

            return selected;
        }

        public void Reset()
        {
            _lastCenter = null;

            _framesSinceDetection = 0;
        }

        private static double Distance(Point2f a, Point2f b)
        {
            double dx = a.X - b.X;

            double dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
