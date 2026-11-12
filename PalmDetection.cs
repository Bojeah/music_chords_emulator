using OpenCvSharp;

namespace MusicChordsEmulator
{
    public class PalmDetection
    {
        public float Score { get; init; }

        public (float X1, float Y1, float X2, float Y2) Box { get; init; }

        public float Width => Math.Max(0, Box.X2 - Box.X1);

        public float Height => Math.Max(0, Box.Y2 - Box.Y1);

        public float Area => Width * Height;

        public Point2f Center => new((Box.X1 + Box.X2) / 2f, (Box.Y1 + Box.Y2) / 2f);

        public Rect ToRect(int frameWidth, int frameHeight)
        {
            int x1 = Math.Clamp((int)MathF.Round(Box.X1), 0, frameWidth - 1);

            int y1 = Math.Clamp((int)MathF.Round(Box.Y1), 0, frameHeight - 1);

            int x2 = Math.Clamp((int)MathF.Round(Box.X2), x1 + 1, frameWidth);

            int y2 = Math.Clamp((int)MathF.Round(Box.Y2), y1 + 1, frameHeight);

            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }
    }
}
