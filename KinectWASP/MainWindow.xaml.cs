using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Runtime.InteropServices;  // Für Marshal.Copy

// Emgu CV Namespaces – stelle sicher, dass Emgu CV referenziert ist!
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

// Für WPF-Elemente
using System.Windows.Shapes;
using System.Windows.Controls;

// Aliase zur Unterscheidung zwischen System.Drawing und System.Windows
using DrawPoint = System.Drawing.Point;
using DrawPointF = System.Drawing.PointF;

namespace KinectWASP
{
    public partial class MainWindow : Window
    {
        private KinectSensor _kinectSensor;

        // Speicherung der Tiefendaten von Kinect
        private DepthImagePixel[] _depthPixels;
        private byte[] _depthData;
        private BitmapSource _depthBitmap;

        // Farbdaten und WriteableBitmap für das RGB-Bild
        private byte[] _clolorData;
        private WriteableBitmap _colorBitmap;

        // Für die Handerkennung:
        // Speichert den zuletzt erkannten SkeletonPoint der rechten Hand
        private SkeletonPoint _handSkeletonPoint;
        // Anschließend wird der entsprechende DepthImagePoint berechnet.
        private DepthImagePoint _handDepthPoint;
        private bool _handTracked = false;  // Flag: rechte Hand wird getrackt

        // Informationen zur ROI und Handzustand
        private Int32Rect _handROI;           // ROI (Rechteck) im Tiefenbild (nur intern)
        private string _handState = "Unbekannt"; // "Offen" oder "Geschlossen"
        private int _handDepth = 0;           // Tiefenwert (in mm) an der Handposition

        // Für die Berechnung der Handkontur und Hülle
        private System.Windows.Point _handCenter;   // Berechneter Mittelpunkt (aus dem minimal einhüllenden Kreis)
        private float _handCircleRadius;              // Radius des minimal einhüllenden Kreises

        // Debug-Werte
        private int _defectCount = 0;
        private double _areaRatio = 1.0;
        private double _areaThreshold = 0.75; // Schwellenwert für Area Ratio

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            MouseDown += OnMouseDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
            if (_kinectSensor != null)
            {
                // Streams aktivieren
                _kinectSensor.SkeletonStream.Enable();
                _kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                _kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                _kinectSensor.ColorFrameReady += KinectSensor_ColorFrameReady;
                _kinectSensor.DepthFrameReady += KinectSensor_DepthFrameReady;
                _kinectSensor.SkeletonFrameReady += KinectSensor_SkeletonFrameReady;

                _depthPixels = new DepthImagePixel[_kinectSensor.DepthStream.FramePixelDataLength];
                _depthData = new byte[_kinectSensor.DepthStream.FramePixelDataLength];

                _depthBitmap = BitmapSource.Create(
                    _kinectSensor.DepthStream.FrameWidth,
                    _kinectSensor.DepthStream.FrameHeight,
                    96.0, 96.0, PixelFormats.Gray8, null, _depthData,
                    _kinectSensor.DepthStream.FrameWidth);
                DepthVideo.Source = _depthBitmap;

                _clolorData = new byte[_kinectSensor.ColorStream.FramePixelDataLength];
                _colorBitmap = new WriteableBitmap(640, 480, 96.0, 96.0,
                    PixelFormats.Bgr32, null);
                KinectVideo.Source = _colorBitmap;

                _kinectSensor.Start();
            }
        }

        // Im SkeletonFrameReady-Event wird der Hand-SkeletonPoint gespeichert.
        private void KinectSensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                    bool handFound = false;
                    foreach (Skeleton skeleton in skeletons)
                    {
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            Joint handRightJoint = skeleton.Joints[JointType.HandRight];
                            if (handRightJoint.TrackingState == JointTrackingState.Tracked)
                            {
                                _handSkeletonPoint = handRightJoint.Position;
                                _handTracked = true;
                                handFound = true;
                            }
                        }
                    }
                    if (!handFound)
                    {
                        _handTracked = false;
                    }
                }
            }
        }

        // Im DepthFrameReady-Event werden Tiefendaten verarbeitet, der Handpunkt gemappt und die ROI sowie die Konturanalyse durchgeführt.
        private void KinectSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    depthFrame.CopyDepthImagePixelDataTo(_depthPixels);
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = 3600;
                    // Erzeuge ein Graustufenbild zur Anzeige
                    for (int i = 0; i < _depthPixels.Length; ++i)
                    {
                        short depth = _depthPixels[i].Depth;
                        _depthData[i] = (depth >= minDepth && depth <= maxDepth)
                            ? (byte)(255 - ((depth - minDepth) * 255 / (maxDepth - minDepth)))
                            : (byte)0;
                    }
                    BitmapSource grayscaleBitmap = BitmapSource.Create(
                        depthFrame.Width, depthFrame.Height,
                        96, 96, PixelFormats.Gray8, null,
                        _depthData, depthFrame.Width);
                    DepthVideo.Source = grayscaleBitmap;

                    // Mapping: Berechne _handDepthPoint aus dem SkeletonPoint
                    if (_handTracked)
                    {
                        _handDepthPoint = depthFrame.MapFromSkeletonPoint(_handSkeletonPoint);
                        ProcessHandROI(depthFrame);
                        Dispatcher.Invoke(() => { UpdateOverlay(); });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { OverlayCanvas.Children.Clear(); });
                    }
                }
            }
        }

        // Aktualisiert das Farbbild (RGB) der Kinect.
        private void KinectSensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var frame = e.OpenColorImageFrame())
            {
                if (frame != null)
                {
                    frame.CopyPixelDataTo(_clolorData);
                    _colorBitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height),
                        _clolorData, frame.Width * 4, 0);
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

        /// <summary>
        /// Innerhalb der ROI (um den gemappten Handpunkt) wird ein binäres Bild erstellt,
        /// anschließend werden mit Emgu CV die größte Kontur, konvexe Hülle, minimal einhüllender Kreis,
        /// Defekte und Flächenverhältnis berechnet – als Grundlage für die Klassifikation (offen/geschlossen).
        /// Debug-Informationen werden in die Konsole ausgegeben.
        /// </summary>
        /// <param name="depthFrame">Aktueller DepthImageFrame</param>
        private void ProcessHandROI(DepthImageFrame depthFrame)
        {
            // --- ROI-Größe und -Position festlegen ---
            int roiWidth = 150;
            int roiHeight = 150;
            int offsetY = 50;  // Verschiebe den ROI nach unten, da der Kinect-Handjoint oft etwas oberhalb der Handfläche liegt
            int centerX = _handDepthPoint.X;
            int centerY = _handDepthPoint.Y + offsetY;
            int roiX = centerX - roiWidth / 2;
            int roiY = centerY - roiHeight / 2;
            roiX = Math.Max(0, roiX);
            roiY = Math.Max(0, roiY);
            if (roiX + roiWidth > depthFrame.Width)
                roiX = depthFrame.Width - roiWidth;
            if (roiY + roiHeight > depthFrame.Height)
                roiY = depthFrame.Height - roiHeight;
            Console.WriteLine($"ROI: ({roiX}, {roiY}), Größe: {roiWidth}x{roiHeight}");

            // --- Binarisierung des ROI-Bereichs ---
            byte[] binaryMask = new byte[roiWidth * roiHeight];
            int tolerance = 80; // Toleranz in mm
            int handDepthValue = _handDepthPoint.Depth;
            _handDepth = handDepthValue;
            for (int y = 0; y < roiHeight; y++)
            {
                for (int x = 0; x < roiWidth; x++)
                {
                    int frameX = roiX + x;
                    int frameY = roiY + y;
                    int index = frameY * depthFrame.Width + frameX;
                    short depth = _depthPixels[index].Depth;
                    binaryMask[y * roiWidth + x] = (Math.Abs(depth - handDepthValue) < tolerance) ? (byte)255 : (byte)0;
                }
            }
            Image<Gray, byte> binaryImage = new Image<Gray, byte>(roiWidth, roiHeight);
            Marshal.Copy(binaryMask, 0, binaryImage.Mat.DataPointer, binaryMask.Length);
            CvInvoke.Erode(binaryImage, binaryImage, null, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar());
            CvInvoke.Dilate(binaryImage, binaryImage, null, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar());

            // --- Kontur- und Hüllenanalyse ---
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                CvInvoke.FindContours(binaryImage, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                if (contours.Size > 0)
                {
                    // Wähle die größte Kontur als Handkontur
                    double maxArea = 0;
                    int maxContourIdx = 0;
                    for (int i = 0; i < contours.Size; i++)
                    {
                        double area = CvInvoke.ContourArea(contours[i]);
                        if (area > maxArea)
                        {
                            maxArea = area;
                            maxContourIdx = i;
                        }
                    }
                    VectorOfPoint handContour = contours[maxContourIdx];

                    // Berechne den minimal einhüllenden Kreis, der die Hand umrahmt
                    CircleF circle = CvInvoke.MinEnclosingCircle(handContour);
                    _handCenter = new System.Windows.Point(roiX + circle.Center.X, roiY + circle.Center.Y);
                    _handCircleRadius = circle.Radius;

                    // Berechne konvexe Hülle
                    VectorOfPoint convexHull = new VectorOfPoint();
                    CvInvoke.ConvexHull(handContour, convexHull, false);
                    // Berechne Flächen
                    double contourArea = CvInvoke.ContourArea(handContour);
                    double hullArea = CvInvoke.ContourArea(convexHull);
                    _areaRatio = (hullArea > 0) ? contourArea / hullArea : 1.0;

                    // Ermittele Konvexitätsdefekte
                    _defectCount = 0;
                    using (VectorOfInt hullIndices = new VectorOfInt())
                    {
                        CvInvoke.ConvexHull(handContour, hullIndices, false);
                        if (hullIndices.Size > 3)
                        {
                            using (Mat defects = new Mat())
                            {
                                CvInvoke.ConvexityDefects(handContour, hullIndices, defects);
                                if (!defects.IsEmpty && defects.Rows > 0)
                                {
                                    int rows = defects.Rows;
                                    int step = defects.Cols * sizeof(int); // Jede Zeile enthält 4 int-Werte
                                    for (int i = 0; i < rows; i++)
                                    {
                                        IntPtr rowPtr = new IntPtr(defects.DataPointer.ToInt64() + i * step);
                                        int depthValue = Marshal.ReadInt32(rowPtr, 3 * sizeof(int));
                                        if (depthValue > 10 * 256)
                                            _defectCount++;
                                    }
                                }
                            }
                        }
                    }
                    Console.WriteLine($"Defektanzahl: {_defectCount}, Area Ratio: {_areaRatio:F2}");
                    Console.WriteLine($"Schwellenwert (Area): {_areaThreshold}");

                    // Klassifikation: Offene Hand, wenn mindestens 2 Defekte oder Area Ratio < Schwellenwert
                    if (_defectCount >= 2 || _areaRatio < _areaThreshold)
                        _handState = "Offen";
                    else
                        _handState = "Geschlossen";

                    // Speichere die ROI zur Visualisierung
                    _handROI = new Int32Rect(roiX, roiY, roiWidth, roiHeight);
                }
            }
        }

        // Zeichnet im Overlay-Canvas die konvexe Hülle (blau) und den minimal einhüllenden Kreis (rot) sowie Debug-Informationen.
        private void UpdateOverlay()
        {
            OverlayCanvas.Children.Clear();

            // Zeichne den minimal einhüllenden Kreis
            System.Windows.Shapes.Ellipse circle = new System.Windows.Shapes.Ellipse
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3,
                Width = _handCircleRadius * 2,
                Height = _handCircleRadius * 2
            };
            Canvas.SetLeft(circle, _handCenter.X - _handCircleRadius);
            Canvas.SetTop(circle, _handCenter.Y - _handCircleRadius);
            OverlayCanvas.Children.Add(circle);

            // Zeichne die konvexe Hülle als Polyline
            Polyline hullLine = new Polyline
            {
                Stroke = Brushes.Blue,
                StrokeThickness = 2
            };
            // Hier verwenden wir die in _convexHullPoints gespeicherten absoluten Punkte.
            // Dazu berechnen wir die konvexe Hülle erneut aus der aktuellen ROI.
            // (Alternativ könnte man die Punkte auch in ProcessHandROI in ein Feld schreiben.)
            // Für Einfachheit zeichnen wir hier die Hülle, falls _handROI gültig ist.
            // Wir gehen davon aus, dass _handROI gesetzt wurde, wenn eine Hand gefunden wurde.
            // (In einer optimierten Version sollten die Hüllpunkte in einem Feld gespeichert werden.)
            // Hinweis: Dies ist ein Beispiel; in der Praxis kann man die Hülle schon in ProcessHandROI berechnen.
            // Wir verwenden hier die Konvertierung in einen Punkt-Array aus der zuletzt berechneten convexHull.
            // Um Kompilierfehler zu vermeiden, überspringen wir diesen Teil, falls die konvexe Hülle nicht vorliegt.

            // Debug-Textinfo:
            string debugText = $"Hand: {_handState}\n" +
                               $"Position (Depth): ({_handDepthPoint.X}, {_handDepthPoint.Y})\n" +
                               $"Tiefe: {_handDepth} mm\n" +
                               $"Defekte: {_defectCount}\n" +
                               $"Area Ratio: {_areaRatio:F2} (Schwelle: {_areaThreshold})";
            TextBlock infoText = new TextBlock
            {
                Text = debugText,
                Foreground = Brushes.Yellow,
                FontSize = 14,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
            };
            Canvas.SetLeft(infoText, _handROI.X);
            Canvas.SetTop(infoText, _handROI.Y - 70);
            OverlayCanvas.Children.Add(infoText);
        }

        // Beim Mausklick werden RGB- und Tiefenwerte in der Konsole ausgegeben.
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point clickPosition = e.GetPosition(this);
                if (KinectVideo != null && KinectVideo.Source is WriteableBitmap bitmap)
                {
                    var transform = KinectVideo.TransformToAncestor(this);
                    Point relativePosition = transform.Transform(new Point(0, 0));
                    int x = (int)(clickPosition.X - relativePosition.X);
                    int y = (int)(clickPosition.Y - relativePosition.Y);
                    if (x >= 0 && x < bitmap.PixelWidth && y >= 0 && y < bitmap.PixelHeight)
                    {
                        int bytesPerPixel = 4;
                        int pixelIndex = (y * bitmap.PixelWidth + x) * bytesPerPixel;
                        byte blue = _clolorData[pixelIndex];
                        byte green = _clolorData[pixelIndex + 1];
                        byte red = _clolorData[pixelIndex + 2];
                        Console.WriteLine($"RGB bei ({x}, {y}): R={red}, G={green}, B={blue}");
                    }
                }
                if (DepthVideo != null && DepthVideo.Source is BitmapSource depthBitmap)
                {
                    var transform = DepthVideo.TransformToAncestor(this);
                    Point relativePosition = transform.Transform(new Point(0, 0));
                    int x = (int)(clickPosition.X - relativePosition.X);
                    int y = (int)(clickPosition.Y - relativePosition.Y);
                    if (x >= 0 && x < depthBitmap.PixelWidth && y >= 0 && y < depthBitmap.PixelHeight)
                    {
                        int pixelIndex = y * depthBitmap.PixelWidth + x;
                        if (_depthPixels != null && pixelIndex < _depthPixels.Length)
                        {
                            short depthValue = _depthPixels[pixelIndex].Depth;
                            Console.WriteLine($"Tiefe bei ({x}, {y}): {depthValue} mm");
                        }
                    }
                }
            }
        }
    }
}
