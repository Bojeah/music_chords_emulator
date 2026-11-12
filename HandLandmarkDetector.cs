using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace MusicChordsEmulator
{
    public sealed class HandLandmarkDetector : IDisposable
    {
        private readonly InferenceSession _session;

        private readonly int _inputWidth;
        private readonly int _inputHeight;

        private readonly string _inputName;

        public HandLandmarkDetector(
            string modelPath)
        {
            _session =
                new InferenceSession(modelPath);

            var inputMeta =
                _session.InputMetadata.First().Value;

            var dims =
                inputMeta.Dimensions;

            if (dims.Count() != 4)
            {
                throw new InvalidOperationException(
                    $"Expected 4D landmark input, got {string.Join(", ", dims)}."
                );
            }

            _inputHeight =
                dims[1] > 0
                    ? dims[1]
                    : 256;

            _inputWidth =
                dims[2] > 0
                    ? dims[2]
                    : 256;

            _inputName =
                _session.InputMetadata.Keys.First();

            Console.WriteLine(
                $"Landmark model input: {_inputWidth}x{_inputHeight}"
            );
        }

        public HandLandmarks? PredictLandmarks(
            Mat frame,
            PalmDetection palm)
        {
            if (frame.Empty())
                return null;

            Rect squareRoi =
                BuildSquareHandRoi(
                    frame,
                    palm
                );

            if (squareRoi.Width < 20 ||
                squareRoi.Height < 20)
            {
                return null;
            }

            using var rgbFrame =
                new Mat();

            Cv2.CvtColor(
                frame,
                rgbFrame,
                ColorConversionCodes.BGR2RGB
            );

            using var cropped =
                new Mat(
                    rgbFrame,
                    squareRoi
                );

            using var resized =
                new Mat();

            Cv2.Resize(
                cropped,
                resized,
                new Size(
                    _inputWidth,
                    _inputHeight
                ),
                0,
                0,
                InterpolationFlags.Linear
            );

            Tensor<float> tensor =
                BuildInputTensor(resized);

            var inputs =
                new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(
                        _inputName,
                        tensor
                    )
                };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                _session.Run(inputs);

            var output =
                results.First(
                    r => r.Name == "Identity"
                ).AsTensor<float>();

            var points =
                new List<Point3D>(21);

            for (int i = 0; i < 21; i++)
            {
                float modelX =
                    output[0, i * 3];

                float modelY =
                    output[0, i * 3 + 1];

                float modelZ =
                    output[0, i * 3 + 2];

                double normalizedX =
                    modelX / _inputWidth;

                double normalizedY =
                    modelY / _inputHeight;

                double frameX =
                    squareRoi.X +
                    normalizedX *
                    squareRoi.Width;

                double frameY =
                    squareRoi.Y +
                    normalizedY *
                    squareRoi.Height;

                points.Add(
                    new Point3D(
                        frameX,
                        frameY,
                        modelZ
                    )
                );
            }

            return new HandLandmarks(points);
        }

        private Rect BuildSquareHandRoi(
            Mat frame,
            PalmDetection palm)
        {
            float x1 = palm.Box.X1;
            float y1 = palm.Box.Y1;
            float x2 = palm.Box.X2;
            float y2 = palm.Box.Y2;

            float width =
                Math.Max(1f, x2 - x1);

            float height =
                Math.Max(1f, y2 - y1);

            float centerX =
                (x1 + x2) / 2f;

            float centerY =
                (y1 + y2) / 2f;

            /*
             * The palm detector sees the palm, not necessarily
             * the complete extended hand.
             *
             * Give the landmark model enough room for the fingers.
             */
            float squareSize =
                Math.Max(width, height) * 2.2f;

            int size =
                Math.Max(
                    32,
                    (int)MathF.Round(squareSize)
                );

            int left =
                (int)MathF.Round(
                    centerX - size / 2f
                );

            int top =
                (int)MathF.Round(
                    centerY - size / 2f
                );

            left =
                Math.Clamp(
                    left,
                    0,
                    Math.Max(
                        0,
                        frame.Width - size
                    )
                );

            top =
                Math.Clamp(
                    top,
                    0,
                    Math.Max(
                        0,
                        frame.Height - size
                    )
                );

            int right =
                Math.Min(
                    frame.Width,
                    left + size
                );

            int bottom =
                Math.Min(
                    frame.Height,
                    top + size
                );

            int finalWidth =
                right - left;

            int finalHeight =
                bottom - top;

            /*
             * Near the edge of the camera frame the requested square
             * may be clipped. Recenter it where possible.
             */
            if (finalWidth != finalHeight)
            {
                int finalSize =
                    Math.Min(
                        finalWidth,
                        finalHeight
                    );

                right =
                    left + finalSize;

                bottom =
                    top + finalSize;
            }

            return new Rect(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top)
            );
        }

        private Tensor<float> BuildInputTensor(
            Mat rgbImage)
        {
            var tensor =
                new DenseTensor<float>(
                    new[]
                    {
                        1,
                        _inputHeight,
                        _inputWidth,
                        3
                    }
                );

            for (int y = 0;
                 y < _inputHeight;
                 y++)
            {
                for (int x = 0;
                     x < _inputWidth;
                     x++)
                {
                    Vec3b pixel =
                        rgbImage.At<Vec3b>(y, x);

                    /*
                     * rgbImage is already RGB.
                     *
                     * Current landmark model preprocessing:
                     * 0..255 -> 0..1
                     *
                     * Verify this against the exporter if
                     * landmarks are incorrect.
                     */
                    tensor[0, y, x, 0] =
                        pixel.Item0 / 255f;

                    tensor[0, y, x, 1] =
                        pixel.Item1 / 255f;

                    tensor[0, y, x, 2] =
                        pixel.Item2 / 255f;
                }
            }

            return tensor;
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}