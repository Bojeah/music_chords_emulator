using System.Diagnostics;
using MusicChordsEmulator.Audio;
using MusicChordsEmulator.Music;
using OpenCvSharp;

namespace MusicChordsEmulator
{
    public class Program
    {
        private static readonly (int Start, int End)[] HandConnections =
        {
            (0, 1),
            (1, 2),
            (2, 3),
            (3, 4),
            (0, 5),
            (5, 6),
            (6, 7),
            (7, 8),
            (5, 9),
            (9, 10),
            (10, 11),
            (11, 12),
            (9, 13),
            (13, 14),
            (14, 15),
            (15, 16),
            (13, 17),
            (17, 18),
            (18, 19),
            (19, 20),
            (0, 17),
        };

        public static void Main()
        {
            using var synthEngine = new SynthEngine();

            using var landmarkDetector = new HandLandmarkDetector("Resources/hand_landmark.onnx");

            using var palmDetector = new PalmDetector(
                "Resources/palm_detector.onnx",
                "Resources/palm_anchors.csv"
            );

            using var capture = new VideoCapture(0);

            if (!capture.IsOpened())
            {
                Console.WriteLine("Could not open webcam.");

                return;
            }

            capture.Set(VideoCaptureProperties.FrameWidth, 1280);

            capture.Set(VideoCaptureProperties.FrameHeight, 720);

            using var cameraFrame = new Mat();

            using var displayFrame = new Mat();

            var tracker = new HandTracker();

            var landmarkSmoother = new HandLandmarkSmoother(amount: 0.5);

            var gestureFilter = new StableGestureFilter(requiredFrames: 6);

            var rootFilter = new StableRootFilter(requiredFrames: 4);

            var motionDetector = new MotionDetector(movementThreshold: 12, stillThreshold: 20);

            PitchClass? activeRoot = null;

            ChordQuality? activeQuality = null;

            HandGesture stableGesture = HandGesture.Unknown;

            MotionState motion = new(MotionDirection.None, 0, 0, 0, 0, false);

            var stopwatch = Stopwatch.StartNew();

            long frameCounter = 0;

            while (true)
            {
                if (!capture.Read(cameraFrame) || cameraFrame.Empty())
                {
                    continue;
                }

                Cv2.Flip(cameraFrame, displayFrame, FlipMode.Y);

                frameCounter++;

                DateTime timestamp = DateTime.UtcNow;

                /*
                 * ---------------------------------------------
                 * PALM DETECTION
                 * ---------------------------------------------
                 */

                List<PalmDetection> detections = palmDetector.Detect(
                    displayFrame,
                    scoreThreshold: 0.65f,
                    nmsThreshold: 0.30f
                );

                PalmDetection? palm = tracker.SelectDetection(detections);

                DrawHeader(displayFrame, detections.Count);

                if (palm == null)
                {
                    HandleNoHand(
                        displayFrame,
                        gestureFilter,
                        rootFilter,
                        landmarkSmoother,
                        motionDetector,
                        synthEngine,
                        ref activeRoot,
                        ref activeQuality,
                        ref stableGesture,
                        ref motion
                    );

                    Cv2.ImShow("Music Chords Emulator", displayFrame);

                    int keyWithoutHand = Cv2.WaitKey(1);

                    if (keyWithoutHand == 27)
                        break;

                    continue;
                }

                /*
                 * Draw palm detector box.
                 */

                DrawPalmBox(displayFrame, palm);

                /*
                 * ---------------------------------------------
                 * LANDMARK DETECTION
                 * ---------------------------------------------
                 */

                HandLandmarks? rawLandmarks = landmarkDetector.PredictLandmarks(displayFrame, palm);

                if (rawLandmarks == null)
                {
                    HandleNoHand(
                        displayFrame,
                        gestureFilter,
                        rootFilter,
                        landmarkSmoother,
                        motionDetector,
                        synthEngine,
                        ref activeRoot,
                        ref activeQuality,
                        ref stableGesture,
                        ref motion
                    );

                    Cv2.ImShow("Music Chords Emulator", displayFrame);

                    int keyNoLandmarks = Cv2.WaitKey(1);

                    if (keyNoLandmarks == 27)
                        break;

                    continue;
                }

                /*
                 * ---------------------------------------------
                 * LANDMARK SMOOTHING
                 * ---------------------------------------------
                 */

                HandLandmarks landmarks = landmarkSmoother.Update(rawLandmarks);

                /*
                 * ---------------------------------------------
                 * MOTION DETECTION
                 * ---------------------------------------------
                 */

                motion = motionDetector.Update(landmarks.PalmCenter, timestamp);

                /*
                 * ---------------------------------------------
                 * GESTURE RECOGNITION
                 * ---------------------------------------------
                 */

                HandGesture rawGesture = GestureClassifier.ClassifyGesture(landmarks);

                stableGesture = gestureFilter.Update(rawGesture);

                /*
                 * ---------------------------------------------
                 * ROOT RECOGNITION
                 * ---------------------------------------------
                 */

                PitchClass detectedRoot = GestureClassifier.ClassifyRoot(
                    landmarks,
                    displayFrame.Width
                );

                PitchClass? stableRoot = rootFilter.Update(detectedRoot);

                /*
                 * ---------------------------------------------
                 * MUSIC MAPPING
                 * ---------------------------------------------
                 */

                ChordQuality? quality = GestureToChordQuality(stableGesture);

                bool canPlay =
                    stableRoot.HasValue && quality.HasValue && stableGesture != HandGesture.Unknown;

                if (canPlay)
                {
                    PitchClass root = stableRoot!.Value;

                    ChordQuality chordQuality = quality!.Value;

                    if (activeRoot != root || activeQuality != chordQuality)
                    {
                        var chord = new Chord(root, 4, chordQuality);

                        synthEngine.PlayChord(chord);

                        activeRoot = root;

                        activeQuality = chordQuality;
                    }
                }

                /*
                 * ---------------------------------------------
                 * DRAW LANDMARKS
                 * ---------------------------------------------
                 */

                DrawHand(displayFrame, landmarks);

                /*
                 * ---------------------------------------------
                 * DEBUG UI
                 * ---------------------------------------------
                 */

                DrawDebugInfo(
                    displayFrame,
                    stableGesture,
                    rawGesture,
                    stableRoot,
                    motion,
                    palm.Score,
                    frameCounter
                );

                Cv2.ImShow("Music Chords Emulator", displayFrame);

                int key = Cv2.WaitKey(1);

                if (key == 27)
                    break;
            }

            synthEngine.Stop();
        }

        private static ChordQuality? GestureToChordQuality(HandGesture gesture)
        {
            return gesture switch
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

        private static void HandleNoHand(
            Mat frame,
            StableGestureFilter gestureFilter,
            StableRootFilter rootFilter,
            HandLandmarkSmoother smoother,
            MotionDetector motionDetector,
            SynthEngine synthEngine,
            ref PitchClass? activeRoot,
            ref ChordQuality? activeQuality,
            ref HandGesture stableGesture,
            ref MotionState motion
        )
        {
            gestureFilter.Reset();
            rootFilter.Reset();
            smoother.Reset();
            motionDetector.Reset();

            stableGesture = HandGesture.Unknown;

            motion = new MotionState(MotionDirection.None, 0, 0, 0, 0, false);

            if (activeRoot.HasValue || activeQuality.HasValue)
            {
                synthEngine.Stop();

                activeRoot = null;
                activeQuality = null;
            }

            Cv2.PutText(
                frame,
                "Waiting for hand...",
                new Point(20, 145),
                HersheyFonts.HersheyComplex,
                0.9,
                Scalar.Gray,
                2
            );
        }

        private static void DrawPalmBox(Mat frame, PalmDetection palm)
        {
            var box = palm.ToRect(frame.Width, frame.Height);

            Cv2.Rectangle(frame, box, Scalar.Cyan, 2);

            Cv2.PutText(
                frame,
                $"Palm {palm.Score:0.00}",
                new Point(box.X, Math.Max(20, box.Y - 8)),
                HersheyFonts.HersheySimplex,
                0.5,
                Scalar.Cyan,
                1
            );
        }

        private static void DrawHand(Mat frame, HandLandmarks hand)
        {
            foreach (var (start, end) in HandConnections)
            {
                var p1 = new Point((int)Math.Round(hand[start].X), (int)Math.Round(hand[start].Y));

                var p2 = new Point((int)Math.Round(hand[end].X), (int)Math.Round(hand[end].Y));

                Cv2.Line(frame, p1, p2, Scalar.LightGreen, 2, LineTypes.AntiAlias);
            }

            for (int i = 0; i < 21; i++)
            {
                var point = new Point((int)Math.Round(hand[i].X), (int)Math.Round(hand[i].Y));

                Cv2.Circle(frame, point, 4, Scalar.Red, -1, LineTypes.AntiAlias);
            }
        }

        private static void DrawHeader(Mat frame, int detectionCount)
        {
            Cv2.PutText(
                frame,
                "Move hand horizontally: C -> B",
                new Point(10, 30),
                HersheyFonts.HersheySimplex,
                0.6,
                Scalar.White,
                1
            );

            Cv2.PutText(
                frame,
                "Fist=Minor | 1=Dim | 2=Aug | 3=Dom7 | 4/5=Major",
                new Point(10, 58),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );

            Cv2.PutText(
                frame,
                $"Palm detections: {detectionCount}",
                new Point(10, 86),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );
        }

        private static void DrawDebugInfo(
            Mat frame,
            HandGesture stableGesture,
            HandGesture rawGesture,
            PitchClass? root,
            MotionState motion,
            float confidence,
            long frameCounter
        )
        {
            int y = 120;

            Cv2.PutText(
                frame,
                $"Gesture: {stableGesture}",
                new Point(10, y),
                HersheyFonts.HersheyComplex,
                0.75,
                Scalar.Yellow,
                2
            );

            y += 30;

            Cv2.PutText(
                frame,
                $"Raw: {rawGesture}",
                new Point(10, y),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );

            y += 25;

            Cv2.PutText(
                frame,
                $"Root: {(root?.ToString() ?? "-")}",
                new Point(10, y),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );

            y += 25;

            Cv2.PutText(
                frame,
                $"Motion: {motion.Direction} " + $"Speed={motion.Speed:0}",
                new Point(10, y),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );

            y += 25;

            Cv2.PutText(
                frame,
                $"Confidence: {confidence:0.00}",
                new Point(10, y),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );

            y += 25;

            Cv2.PutText(
                frame,
                $"Frame: {frameCounter}",
                new Point(10, y),
                HersheyFonts.HersheySimplex,
                0.55,
                Scalar.White,
                1
            );
        }
    }
}
