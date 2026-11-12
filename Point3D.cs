namespace MusicChordsEmulator
{
    public readonly record struct Point3D(double X, double Y, double Z)
    {
        public double DistanceTo(Point3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static Point3D Lerp(
            Point3D a,
            Point3D b,
            double amount)
        {
            amount = Math.Clamp(amount, 0.0, 1.0);

            return new Point3D(
                a.X + (b.X - a.X) * amount,
                a.Y + (b.Y - a.Y) * amount,
                a.Z + (b.Z - a.Z) * amount
            );
        }
    }
}