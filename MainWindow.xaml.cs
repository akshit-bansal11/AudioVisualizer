using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.Wave;
using NAudio.Dsp;

namespace AudioVisualizer
{
    public partial class MainWindow : Window
    {
        private WasapiLoopbackCapture _capture;
        private DispatcherTimer _timer;
        private BufferedWaveProvider _bufferedWaveProvider;

        private float[] _audioBuffer = new float[0];
        private const int BarCount = 200;
        private readonly List<Rectangle> _bars = new();
        private readonly List<double> _previousHeights = new(new double[BarCount]);

        private const int FftSize = 2048;
        private readonly Complex[] _fftBuffer = new Complex[FftSize];
        private int _fftBufferOffset = 0;

        // New: Smoothing and sensitivity
        private readonly double[] _smoothedBands = new double[BarCount];
        private const double SmoothFactor = 0.3;
        private const double Sensitivity = 10.0;

        public MainWindow()
        {
            InitializeComponent();
            this.AllowsTransparency = true;
            this.WindowStyle = WindowStyle.None;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.IsHitTestVisible = false; // ⬅️ Makes it click-through
            this.Left = 0;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height + 200;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = 200;


            SetupBars();
            StartAudioCapture();
            StartUiUpdater();
        }

        private void SetupBars()
        {
            VisualizerCanvas.Children.Clear();
            _bars.Clear();

            double canvasWidth = VisualizerCanvas.ActualWidth;
            double spacing = 6;
            double barWidth = Math.Max(6, (canvasWidth - spacing * (BarCount - 1)) / BarCount);

            for (int i = 0; i < BarCount; i++)
            {
                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = 10,
                    RadiusX = 4,
                    RadiusY = 4,
                    Fill = new SolidColorBrush(Colors.Transparent),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        ShadowDepth = 2,
                        BlurRadius = 8,
                        Opacity = 0.3
                    }
                };
                _bars.Add(rect);
                VisualizerCanvas.Children.Add(rect);
            }

            VisualizerCanvas.Background = Brushes.Transparent;

        }

        private void StartAudioCapture()
        {
            _capture = new WasapiLoopbackCapture();
            _bufferedWaveProvider = new BufferedWaveProvider(_capture.WaveFormat)
            {
                BufferLength = 1024 * 1024,
                DiscardOnBufferOverflow = true
            };

            _capture.DataAvailable += (s, e) =>
            {
                _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };

            _capture.RecordingStopped += (s, e) =>
            {
                _capture.Dispose();
            };

            _capture.StartRecording();
        }

        private void StartUiUpdater()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _timer.Tick += (s, e) => UpdateVisualizer();
            _timer.Start();
        }

        private void UpdateVisualizer()
        {
            if (VisualizerCanvas.ActualWidth == 0 || VisualizerCanvas.ActualHeight == 0)
                return;

            ReadAudioBuffer();

            if (_audioBuffer.Length == 0)
                return;

            while (_audioBuffer.Length > 0 && _fftBufferOffset < FftSize)
            {
                _fftBuffer[_fftBufferOffset] = new Complex
                {
                    X = _audioBuffer[0],
                    Y = 0
                };
                _fftBufferOffset++;
                _audioBuffer = _audioBuffer.Skip(1).ToArray();
            }

            if (_fftBufferOffset == FftSize)
            {
                ApplyWindow();
                FastFourierTransform.FFT(true, (int)Math.Log(FftSize, 2.0), _fftBuffer);

                double[] magnitudes = new double[FftSize / 2];
                for (int i = 0; i < magnitudes.Length; i++)
                {
                    magnitudes[i] = Math.Sqrt(_fftBuffer[i].X * _fftBuffer[i].X + _fftBuffer[i].Y * _fftBuffer[i].Y);
                }

                double[] bandLevels = MapMagnitudesToBands_Log(magnitudes, BarCount);

                double[] smoothed = SmoothBands(bandLevels);
                DrawBars(smoothed);

                _fftBufferOffset = 0;
            }
        }

        private void ApplyWindow()
        {
            for (int i = 0; i < FftSize; i++)
            {
                _fftBuffer[i].X *= (float)FastFourierTransform.HammingWindow(i, FftSize);
            }
        }

        private void ReadAudioBuffer()
        {
            int bytes = _bufferedWaveProvider.BufferedBytes;
            if (bytes <= 0) return;

            byte[] byteBuffer = new byte[bytes];
            _bufferedWaveProvider.Read(byteBuffer, 0, bytes);

            int samples = bytes / 4;
            _audioBuffer = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                _audioBuffer[i] = BitConverter.ToSingle(byteBuffer, i * 4);
            }
        }

        private double[] MapMagnitudesToBands_Log(double[] magnitudes, int bandCount, int sampleRate = 44100, int freqMin = 20, int freqMax = 16000)
        {
            double[] bands = new double[bandCount];
            double binFreq = (double)sampleRate / (magnitudes.Length * 2);

            int half = bandCount / 2;

            for (int i = 0; i < half; i++)
            {
                // Reverse: map high frequencies at center, low at edges
                double t = 1.0 - (double)i / (half - 1); // flip direction
                double logMin = Math.Log10(freqMin);
                double logMax = Math.Log10(freqMax);
                double logStart = logMin + (logMax - logMin) * t;
                double logEnd = logMin + (logMax - logMin) * (t + 1.0 / half);

                double bandStartFreq = Math.Pow(10, logStart);
                double bandEndFreq = Math.Pow(10, logEnd);

                int startBin = (int)(bandStartFreq / binFreq);
                int endBin = Math.Min((int)(bandEndFreq / binFreq), magnitudes.Length - 1);

                double sum = 0;
                for (int j = startBin; j <= endBin; j++)
                    sum += magnitudes[j];

                double avg = sum / Math.Max(1, endBin - startBin + 1);

                // Place high freqs in center, low freqs on edges
                bands[i] = avg;
                bands[bandCount - 1 - i] = avg;
            }

            return bands;
        }


        private double[] SmoothBands(double[] rawBands)
        {
            for (int i = 0; i < BarCount; i++)
            {
                _smoothedBands[i] = _smoothedBands[i] * (1 - SmoothFactor) + rawBands[i] * SmoothFactor;
            }
            return _smoothedBands.ToArray();
        }

        private void DrawBars(double[] bandLevels)
        {
            double canvasWidth = VisualizerCanvas.ActualWidth;
            double canvasHeight = VisualizerCanvas.ActualHeight;

            double spacing = 6;
            double barWidth = Math.Max(6, (canvasWidth - spacing * (BarCount - 1)) / BarCount);

            for (int i = 0; i < BarCount; i++)
            {
                double level = bandLevels[i];
                level = Math.Min(1.0, level * Sensitivity); // Use Sensitivity constant

                double targetHeight = level * (canvasHeight - 20);
                targetHeight = Math.Max(10, targetHeight);

                Rectangle rect = _bars[i];
                rect.Width = barWidth;

                // Animate height only (not position)
                DoubleAnimation heightAnimation = new DoubleAnimation
                {
                    From = _previousHeights[i],
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                rect.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);
                _previousHeights[i] = targetHeight;

                // Set fixed base (bottom-aligned)
                double x = i * (barWidth + spacing);

                Canvas.SetLeft(rect, x);
                Canvas.SetBottom(rect, 0);

                // White fill
                rect.Fill = new SolidColorBrush(Colors.White);
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            _capture?.StopRecording();
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}
