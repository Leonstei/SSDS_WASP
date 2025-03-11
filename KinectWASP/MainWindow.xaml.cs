using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace KinectWASP
{
    public partial class MainWindow : Window
    {
        private KinectSensor _kinectSensor;

        // Tiefendaten
        private DepthImagePixel[] _depthPixels;
        private byte[] _depthData;

        // Ob die Ausgabe gerade pausiert ist
        private bool _isPaused = false;

        // Für die Rechteck-Auswahl
        private Point? _selectionStart = null;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Kinect suchen und starten
            _kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
            if (_kinectSensor != null)
            {
                // Nur Tiefenstream aktivieren (640x480)
                _kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                _kinectSensor.DepthFrameReady += KinectSensor_DepthFrameReady;

                // Arrays für Pixel-Daten anlegen
                _depthPixels = new DepthImagePixel[_kinectSensor.DepthStream.FramePixelDataLength];
                _depthData = new byte[_kinectSensor.DepthStream.FramePixelDataLength];

                _kinectSensor.Start();
            }
        }

        private void KinectSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            // Falls pausiert, keine Aktualisierung
            if (_isPaused) return;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    depthFrame.CopyDepthImagePixelDataTo(_depthPixels);

                    int minDepth = depthFrame.MinDepth; // normal: 800
                    int maxDepth = 3600;                // z.B. 3.6 m

                    for (int i = 0; i < _depthPixels.Length; i++)
                    {
                        short depth = _depthPixels[i].Depth;
                        if (depth >= minDepth && depth <= maxDepth)
                        {
                            // Grau-Skalierung: nahe Objekte heller, entfernte dunkler
                            byte intensity = (byte)(255 - ((depth - minDepth) * 255 / (maxDepth - minDepth)));
                            _depthData[i] = intensity;
                        }
                        else
                        {
                            _depthData[i] = 0;
                        }
                    }

                    // Graustufen-Bild erzeugen
                    var bitmap = BitmapSource.Create(
                        depthFrame.Width, depthFrame.Height,
                        96, 96,
                        PixelFormats.Gray8,
                        null,
                        _depthData,
                        depthFrame.Width);

                    DepthVideo.Source = bitmap;
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_kinectSensor != null && _kinectSensor.IsRunning)
            {
                _kinectSensor.Stop();
            }
        }

        // ---------------------------------------
        //  Leertaste: Pause / Fortsetzen
        // ---------------------------------------
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _isPaused = !_isPaused;

                if (_isPaused)
                {
                    CalculationText.Text = "Pausiert – bitte Rechteck ziehen, um Tiefe zu berechnen.";
                    CalculationText.Visibility = Visibility.Visible;
                }
                else
                {
                    // Fortsetzen: Rechteck & Text ausblenden
                    SelectionRectangle.Visibility = Visibility.Collapsed;
                    CalculationText.Visibility = Visibility.Collapsed;
                    _selectionStart = null;
                }
            }
        }

        // ---------------------------------------
        //  Rechteck-Auswahl per Maus
        // ---------------------------------------
        private void DepthCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isPaused)
            {
                _selectionStart = e.GetPosition(DepthCanvas);

                // Rechteck initialisieren
                Canvas.SetLeft(SelectionRectangle, _selectionStart.Value.X);
                Canvas.SetTop(SelectionRectangle, _selectionStart.Value.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
                SelectionRectangle.Visibility = Visibility.Visible;
            }
        }

        private void DepthCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPaused && _selectionStart.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPos = e.GetPosition(DepthCanvas);

                double x = Math.Min(currentPos.X, _selectionStart.Value.X);
                double y = Math.Min(currentPos.Y, _selectionStart.Value.Y);
                double width = Math.Abs(currentPos.X - _selectionStart.Value.X);
                double height = Math.Abs(currentPos.Y - _selectionStart.Value.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
            }
        }

        private void DepthCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPaused && _selectionStart.HasValue)
            {
                Point endPos = e.GetPosition(DepthCanvas);

                double x = Math.Min(endPos.X, _selectionStart.Value.X);
                double y = Math.Min(endPos.Y, _selectionStart.Value.Y);
                double width = Math.Abs(endPos.X - _selectionStart.Value.X);
                double height = Math.Abs(endPos.Y - _selectionStart.Value.Y);

                // In "Pixel-Koordinaten" umwandeln (1:1, da Image = 640x480)
                int startX = (int)x;
                int startY = (int)y;
                int rectWidth = (int)width;
                int rectHeight = (int)height;

                // Bildgröße: 640x480
                const int imageWidth = 640;
                const int imageHeight = 480;

                // Begrenzen auf Bildbereich
                if (startX < 0) startX = 0;
                if (startY < 0) startY = 0;
                if (startX + rectWidth > imageWidth) rectWidth = imageWidth - startX;
                if (startY + rectHeight > imageHeight) rectHeight = imageHeight - startY;

                // Durchschnittstiefe berechnen
                long sum = 0;
                int count = 0;

                for (int row = startY; row < startY + rectHeight; row++)
                {
                    for (int col = startX; col < startX + rectWidth; col++)
                    {
                        int index = row * imageWidth + col;
                        if (_depthPixels != null && index < _depthPixels.Length)
                        {
                            short depthVal = _depthPixels[index].Depth;
                            if (depthVal > 0)  // 0 = außerhalb Min/Max oder kein gültiger Wert
                            {
                                sum += depthVal;
                                count++;
                            }
                        }
                    }
                }

                double avgDepth = (count > 0) ? sum / (double)count : 0.0;

                // Ergebnis anzeigen
                CalculationText.Text =
                    $"Rechteck: [{startX},{startY}] - {rectWidth}x{rectHeight}\n" +
                    $"Summe: {sum}, Pixel: {count}\n" +
                    $"Ø Tiefe: {avgDepth:0.##} mm";
                CalculationText.Visibility = Visibility.Visible;

                _selectionStart = null; // Auswahl zurücksetzen
            }
        }
    }
}
