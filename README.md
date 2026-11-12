# Music Chords Emulator

Music Chords Emulator is a Windows desktop application that turns hand gestures
into synthesized chords. It uses a webcam to detect a hand, tracks 21 hand
landmarks with ONNX models, maps the palm's horizontal position to a root note,
and maps the number of extended fingers to a chord quality.

The application displays the camera feed with detection overlays and plays the
current chord through the default audio output.

## Features

- Real-time webcam hand tracking.
- Palm detection followed by 21-point hand landmark detection.
- Landmark smoothing and temporal filtering to reduce unstable detections.
- Horizontal hand-position mapping across the chromatic scale from C to B.
- Procedurally generated oscillator-based chord audio.
- On-screen detection, gesture, root, motion, and frame diagnostics.

## Requirements

- Windows.
- .NET 10 SDK.
- A working webcam accessible as camera device `0`.
- A working audio output device.
- A machine capable of running the bundled ONNX models in real time.

The project targets `net10.0-windows` and uses:

- [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime)
  `1.30.0` for model inference.
- [NAudio](https://www.nuget.org/packages/NAudio) `3.1.0` for audio output.
- [OpenCvSharp4](https://www.nuget.org/packages/OpenCvSharp4) and its Windows
  runtime `4.13.0.20260627` for camera and image processing.

## Getting started

### 1. Clone the repository

```powershell
git clone <repository-url>
cd MusicChordsEmulator
```

Run the commands below from the directory containing
`MusicChordsEmulator.csproj`.

### 2. Restore dependencies

```powershell
dotnet restore
```

The ONNX models and anchor data are stored in `Resources/` and are configured
by the project to copy to the build output directory automatically.

### 3. Build

```powershell
dotnet build
```

For an explicit configuration:

```powershell
dotnet build --configuration Release
```

### 4. Run

```powershell
dotnet run
```

The application opens a window titled **Music Chords Emulator**. Position one
hand in view of the webcam and move it horizontally to select a root note.
Press **Esc** while the camera window is focused to stop the application.

## Using the emulator

### Root note

The palm center's horizontal position is divided into 12 zones across the
camera frame:

| Position from left to right | Root |
| --- | --- |
| 1 | C |
| 2 | C# |
| 3 | D |
| 4 | D# |
| 5 | E |
| 6 | F |
| 7 | F# |
| 8 | G |
| 9 | G# |
| 10 | A |
| 11 | A# |
| 12 | B |

The root must remain stable for several frames before it is used. The displayed
video is mirrored, so the on-screen left-to-right movement corresponds to the
application's displayed C-to-B guide.

### Chord quality

The number of extended fingers selects the chord quality:

| Gesture | Extended fingers | Chord quality |
| --- | ---: | --- |
| Fist | 0 | Minor |
| One finger | 1 | Diminished |
| Two fingers | 2 | Augmented |
| Three fingers | 3 | Dominant seventh |
| Four fingers | 4 | Major |
| Open palm | 5 | Major |

The gesture must remain stable for several frames before a new chord is played.
When no hand is detected, the current voice is released and the filters reset.

## How it works

Each camera frame passes through this pipeline:

1. Capture a frame from webcam device `0`.
2. Mirror the frame for the display.
3. Run the palm detector using `Resources/palm_detector.onnx` and
   `Resources/palm_anchors.csv`.
4. Select and track the most likely hand across frames.
5. Crop the detected palm area and run `Resources/hand_landmark.onnx`.
6. Smooth the 21 landmark coordinates.
7. Classify the hand gesture and estimate palm motion.
8. Stabilize the gesture and root note over multiple frames.
9. Build a chord rooted at octave 4.
10. Synthesize the chord and send it to the default audio device.

The chord intervals are defined in `Audio/Chord.cs`:

| Quality | Intervals |
| --- | --- |
| Major | 0, 4, 7 |
| Minor | 0, 3, 7 |
| Diminished | 0, 3, 6 |
| Augmented | 0, 4, 8 |
| Dominant7 | 0, 4, 7, 10 |

## Project structure

```text
.
├── Audio/
│   ├── Chord.cs                 Chord interval construction
│   ├── ChordQuality.cs          Supported chord qualities
│   ├── Pitch.cs                 Pitch and frequency conversion
│   └── PitchClass.cs            Chromatic root-note enum
├── Music/
│   ├── ChordSynthProvider.cs    Multi-voice chord sample provider
│   ├── CleanupSampleProvider.cs Mixer voice cleanup
│   ├── EnvelopeGenerator.cs     ADSR envelope
│   ├── EnvelopedSampleProvider.cs
│   ├── Oscillator.cs            Basic oscillator
│   └── SynthEngine.cs           Audio mixer and voice lifecycle
├── Resources/
│   ├── hand_landmark.onnx       Hand landmark model
│   ├── palm_anchors.csv         Palm detector anchors
│   ├── palm_detector.onnx       Palm detection model
│   └── test_hand.jpg            Test image asset
├── GestureClassifier.cs         Finger and chord classification
├── HandLandmarkDetector.cs      ONNX landmark inference
├── HandLandmarkSmoother.cs      Landmark smoothing
├── HandTracker.cs               Detection selection and tracking
├── MotionDetector.cs            Palm motion estimation
├── PalmDetector.cs              ONNX palm inference and decoding
├── Program.cs                   Application loop and camera UI
├── StableGestureFilter.cs       Gesture temporal filtering
├── StableRootFilter.cs          Root temporal filtering
└── MusicChordsEmulator.csproj   Target framework and dependencies
```

Build output is written under `bin/`; intermediate NuGet and compiler files are
written under `obj/`. These generated directories should not be edited or
committed.

## Troubleshooting

### “Could not open webcam.”

- Confirm that the camera is connected and available to other applications.
- Close applications that may already be using the camera.
- Check Windows camera privacy permissions for the terminal or IDE.
- If the intended camera is not device `0`, update the `VideoCapture` index in
  `Program.cs`.

### Model or resource file errors

Run the application from the project directory with `dotnet run`, or verify
that the build output contains:

```text
bin/<Configuration>/net10.0-windows/Resources/
├── hand_landmark.onnx
├── palm_anchors.csv
└── palm_detector.onnx
```

These files are copied by the `None` items in the project file. A clean rebuild
can repair an incomplete output directory:

```powershell
dotnet clean
dotnet build
```

### Detection is unstable

Use even lighting, keep the full hand inside the camera frame, and avoid a
busy background. The application intentionally waits for stable gesture and
root detections before changing the chord.

### No sound

Verify that Windows has a default audio output device and that the application
is not muted. The synthesizer outputs mono floating-point audio at 44.1 kHz.

## Development notes

The camera resolution is requested as 1280x720, but the actual resolution is
controlled by the webcam driver. The detector uses the dimensions reported by
each ONNX model for inference input.

The `AudioEngine` class is an alternative audio wrapper retained in the
project; the main application currently uses `Music/SynthEngine.cs`.

No automated test project is currently included. When changing gesture
thresholds, model decoding, or chord mappings, validate the behavior with a
live webcam run and confirm that the resource files are present in the output
directory.
