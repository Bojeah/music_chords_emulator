namespace MusicChordsEmulator
{
    public enum MotionDirection
    {
        None,
        Left,
        Right,
        Up,
        Down,
        Still,
    }

    public readonly record struct MotionState(
        MotionDirection Direction,
        double VelocityX,
        double VelocityY,
        double Speed,
        double Distance,
        bool IsMoving
    );

    public sealed class MotionDetector
    {
        private Point3D? _previousPosition;
        private DateTime _previousTimestamp;

        private readonly double _movementThreshold;
        private readonly double _stillThreshold;

        public MotionDetector(double movementThreshold = 18.0, double stillThreshold = 8.0)
        {
            _movementThreshold = movementThreshold;

            _stillThreshold = stillThreshold;
        }

        public MotionState Update(Point3D position, DateTime timestamp)
        {
            if (!_previousPosition.HasValue)
            {
                _previousPosition = position;

                _previousTimestamp = timestamp;

                return new MotionState(MotionDirection.None, 0, 0, 0, 0, false);
            }

            double dt = (timestamp - _previousTimestamp).TotalSeconds;

            dt = Math.Clamp(dt, 0.001, 0.25);

            Point3D previous = _previousPosition.Value;

            double dx = position.X - previous.X;

            double dy = position.Y - previous.Y;

            double distance = Math.Sqrt(dx * dx + dy * dy);

            double velocityX = dx / dt;

            double velocityY = dy / dt;

            double speed = Math.Sqrt(velocityX * velocityX + velocityY * velocityY);

            MotionDirection direction;

            if (speed < _stillThreshold)
            {
                direction = MotionDirection.Still;
            }
            else if (Math.Abs(velocityX) > Math.Abs(velocityY))
            {
                direction = velocityX > 0 ? MotionDirection.Right : MotionDirection.Left;
            }
            else
            {
                direction = velocityY > 0 ? MotionDirection.Down : MotionDirection.Up;
            }

            bool moving = distance >= _movementThreshold;

            _previousPosition = position;

            _previousTimestamp = timestamp;

            return new MotionState(direction, velocityX, velocityY, speed, distance, moving);
        }

        public void Reset()
        {
            _previousPosition = null;

            _previousTimestamp = default;
        }
    }
}
