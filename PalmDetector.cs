using System.Globalization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace MusicChordsEmulator
{
    public sealed class PalmDetector : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly List<(float X, float Y)> _anchors;

        private readonly int _inputWidth;
        private readonly int _inputHeight;

        private readonly string _inputName;

        public PalmDetector(string modelPath, string anchorsCsvPath)
        {
            _session = new InferenceSession(modelPath);

            var inputMeta = _session.InputMetadata.First().Value;

            var dims = inputMeta.Dimensions;

            if (dims.Count() != 4)
            {
                throw new InvalidOperationException(
                    $"Expected a 4D palm input tensor, got {string.Join(", ", dims)}."
                );
            }

            _inputHeight = dims[1];
            _inputWidth = dims[2];

            _inputName = _session.InputMetadata.Keys.First();

            _anchors = File.ReadAllLines(anchorsCsvPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line =>
                {
                    string[] parts = line.Split(',', StringSplitOptions.TrimEntries);

                    if (parts.Length < 2)
                    {
                        throw new FormatException($"Invalid palm anchor: {line}");
                    }

                    return (
                        X: float.Parse(parts[0], CultureInfo.InvariantCulture),
                        Y: float.Parse(parts[1], CultureInfo.InvariantCulture)
                    );
                })
                .ToList();

            if (_anchors.Count == 0)
            {
                throw new InvalidOperationException("No palm anchors were loaded.");
            }

            Console.WriteLine($"Palm model input: {_inputWidth}x{_inputHeight}");

            Console.WriteLine($"Palm anchors: {_anchors.Count}");
        }

        private (Mat Letterboxed, float Scale, int PadX, int PadY) Letterbox(Mat frame)
        {
            int size = Math.Max(frame.Width, frame.Height);

            int padX = (size - frame.Width) / 2;
            int padY = (size - frame.Height) / 2;

            using var square = new Mat(size, size, frame.Type(), Scalar.Black);

            using var destination = new Mat(
                square,
                new Rect(padX, padY, frame.Width, frame.Height)
            );

            frame.CopyTo(destination);

            var resized = new Mat();

            Cv2.Resize(
                square,
                resized,
                new Size(_inputWidth, _inputHeight),
                0,
                0,
                InterpolationFlags.Linear
            );

            float scale = (float)size / _inputWidth;

            return (resized, scale, padX, padY);
        }

        private Tensor<float> BuildInputTensor(Mat letterboxed)
        {
            var tensor = new DenseTensor<float>(new[] { 1, _inputHeight, _inputWidth, 3 });

            for (int y = 0; y < _inputHeight; y++)
            {
                for (int x = 0; x < _inputWidth; x++)
                {
                    Vec3b pixel = letterboxed.At<Vec3b>(y, x);

                    // OpenCV is BGR.
                    // Model expects RGB.
                    float r = pixel.Item2 / 255f;

                    float g = pixel.Item1 / 255f;

                    float b = pixel.Item0 / 255f;

                    tensor[0, y, x, 0] = (r - 0.5f) * 2f;

                    tensor[0, y, x, 1] = (g - 0.5f) * 2f;

                    tensor[0, y, x, 2] = (b - 0.5f) * 2f;
                }
            }

            return tensor;
        }

        public List<PalmDetection> Detect(
            Mat frame,
            float scoreThreshold = 0.65f,
            float nmsThreshold = 0.3f
        )
        {
            if (frame.Empty())
                return new List<PalmDetection>();

            var letterboxedData = Letterbox(frame);

            using Mat letterboxed = letterboxedData.Letterboxed;

            Tensor<float> inputTensor = BuildInputTensor(letterboxed);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor),
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(
                inputs
            );

            var boxesAndLandmarks = results.First(r => r.Name == "Identity").AsTensor<float>();

            var scores = results.First(r => r.Name == "Identity_1").AsTensor<float>();

            int detectionCount = Math.Min(_anchors.Count, scores.Dimensions[1]);

            float squareSize = letterboxedData.Scale * _inputWidth;

            var rawBoxes = new List<Rect>();

            var rawScores = new List<float>();

            for (int i = 0; i < detectionCount; i++)
            {
                float rawScore = scores[0, i, 0];

                float confidence = Sigmoid(rawScore);

                if (confidence < scoreThreshold)
                    continue;

                /*
                 * IMPORTANT:
                 *
                 * This decoder assumes your ONNX export uses:
                 *
                 * cx = rawCx / inputWidth + anchorX
                 * cy = rawCy / inputHeight + anchorY
                 * w  = rawW  / inputWidth
                 * h  = rawH  / inputHeight
                 *
                 * If your original model was exported from a
                 * MediaPipe/BlazePalm implementation with additional
                 * x/y/w/h scaling, those constants must match the
                 * exporter.
                 */

                float cx = boxesAndLandmarks[0, i, 0] / _inputWidth;

                float cy = boxesAndLandmarks[0, i, 1] / _inputHeight;

                float w = boxesAndLandmarks[0, i, 2] / _inputWidth;

                float h = boxesAndLandmarks[0, i, 3] / _inputHeight;

                var anchor = _anchors[i];

                float decodedCx = cx + anchor.X;

                float decodedCy = cy + anchor.Y;

                float x1 = (decodedCx - w / 2f) * squareSize - letterboxedData.PadX;

                float y1 = (decodedCy - h / 2f) * squareSize - letterboxedData.PadY;

                float x2 = (decodedCx + w / 2f) * squareSize - letterboxedData.PadX;

                float y2 = (decodedCy + h / 2f) * squareSize - letterboxedData.PadY;

                int ix1 = Math.Clamp((int)MathF.Floor(x1), 0, frame.Width - 1);

                int iy1 = Math.Clamp((int)MathF.Floor(y1), 0, frame.Height - 1);

                int ix2 = Math.Clamp((int)MathF.Ceiling(x2), ix1 + 1, frame.Width);

                int iy2 = Math.Clamp((int)MathF.Ceiling(y2), iy1 + 1, frame.Height);

                int width = ix2 - ix1;

                int height = iy2 - iy1;

                if (width < 10 || height < 10)
                    continue;

                rawBoxes.Add(new Rect(ix1, iy1, width, height));

                rawScores.Add(confidence);
            }

            if (rawBoxes.Count == 0)
                return new List<PalmDetection>();

            CvDnn.NMSBoxes(
                rawBoxes,
                rawScores,
                scoreThreshold,
                nmsThreshold,
                out int[] keepIndices
            );

            var detections = new List<PalmDetection>();

            foreach (int index in keepIndices)
            {
                Rect box = rawBoxes[index];

                detections.Add(
                    new PalmDetection
                    {
                        Score = rawScores[index],

                        Box = (box.X, box.Y, box.X + box.Width, box.Y + box.Height),
                    }
                );
            }

            return detections.OrderByDescending(x => x.Score).ToList();
        }

        private static float Sigmoid(float value)
        {
            if (value >= 0)
            {
                float z = MathF.Exp(-value);

                return 1f / (1f + z);
            }

            float e = MathF.Exp(value);

            return e / (1f + e);
        }

        public int AnchorCount => _anchors.Count;

        public IReadOnlyDictionary<string, NodeMetadata> OutputInfo => _session.OutputMetadata;

        public int InputHeight => _inputHeight;

        public int InputWidth => _inputWidth;

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
