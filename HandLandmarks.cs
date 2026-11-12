using System;
using System.Collections.Generic;

namespace MusicChordsEmulator
{
    public class HandLandmarks
    {
        private readonly IReadOnlyList<Point3D> _points;

        public HandLandmarks(IReadOnlyList<Point3D> points)
        {
            if (points == null || points.Count != 21)
            {
                throw new ArgumentException(
                    "A hand must have exactly 21 landmarks.",
                    nameof(points)
                );
            }

            _points = points;
        }

        public IReadOnlyList<Point3D> Points => _points;

        public Point3D Wrist => _points[0];

        public Point3D this[int index] => _points[index];

        public Point3D PalmCenter
        {
            get
            {
                var indices = new[] { 0, 5, 9, 13, 17 };

                double x = 0;
                double y = 0;
                double z = 0;

                foreach (int index in indices)
                {
                    x += _points[index].X;
                    y += _points[index].Y;
                    z += _points[index].Z;
                }

                return new Point3D(x / indices.Length, y / indices.Length, z / indices.Length);
            }
        }

        public double Distance(int a, int b)
        {
            return _points[a].DistanceTo(_points[b]);
        }

        public HandLandmarks SmoothTowards(HandLandmarks target, double amount)
        {
            var smoothed = new Point3D[21];

            for (int i = 0; i < 21; i++)
            {
                smoothed[i] = Point3D.Lerp(_points[i], target[i], amount);
            }

            return new HandLandmarks(smoothed);
        }
    }
}
