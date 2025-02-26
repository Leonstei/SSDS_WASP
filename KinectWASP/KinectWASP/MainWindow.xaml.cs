using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KinectTest.Helper;
using Microsoft.Kinect;

namespace KinectWASP
{
    public partial class MainWindow : Window
    {
        private KinectSensor _kinectSensor;
        
        //Intermediate storage for the depth data received from the camera 
        private DepthImagePixel[] _depthPixels;
        private short[] _depthPixels2;
        
        //Bitmap that will hold depth information
        private BitmapSource _depthBitmap;
        
        private WriteableBitmap _colorBitmap;
        Stopwatch _timer = new Stopwatch();
        
        //storage for color data
        private byte[] _clolorData;
        
        //storage for depth data
        private byte[] _depthData;
        private LinkedList<short> _handStates = new LinkedList<short>();
        bool handOpen = false;
        short handStateUnchanged = 0;

        //DataSaver storage
        private bool _lastHandOpen;
        private (int bLeft, int bRight, int bTop) _lastBoundingBox;
        private double[] _lastContourPixels;
        private double[] _lastAverageContourPixels;
        private List<(int,double)> _lastMaxima;
        private BitmapSource _lastContourImage;



        public MainWindow()

        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            MouseDown += Button_Click;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
            if (_kinectSensor != null)
            {
                _kinectSensor.SkeletonStream.Enable();
                _kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                _kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                _kinectSensor.ColorFrameReady += KinectSensor_ColorFrameReady;
                _kinectSensor.DepthFrameReady += KinectSensor_DepthFrameReady;
                _kinectSensor.SkeletonFrameReady += KinectSensor_SkeletonFrameReady;
                
                _depthPixels = new DepthImagePixel[_kinectSensor.DepthStream.FramePixelDataLength];
                _depthPixels2 = new short[_kinectSensor.DepthStream.FramePixelDataLength];

                _depthData = new byte[_kinectSensor.DepthStream.FramePixelDataLength * sizeof(int)]; 
                
                // _depthBitmap = BitmapSource.Create(
                //     _kinectSensor.DepthStream.FrameWidth, _kinectSensor.DepthStream.FrameHeight, 96.0, 96.0, 
                //     PixelFormats.Gray8, null, _depthData, _kinectSensor.DepthStream.FrameWidth );
                // DepthVideo.Source = _depthBitmap;

                _clolorData = new byte[_kinectSensor.ColorStream.FramePixelDataLength];
                _colorBitmap = new WriteableBitmap(640, 480, 96.0, 96.0, System.Windows.Media.PixelFormats.Bgr32, null);
                
                KinectVideo.Source = _colorBitmap;
                _kinectSensor.Start();
            }
        }
        private void KinectSensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    // Speicherplatz für Skeleton-Daten bereitstellen
                    Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    // Skeleton-Daten extrahieren
                    skeletonFrame.CopySkeletonDataTo(skeletons);

                    // Verarbeitung aller erkannten Skelette
                    foreach (Skeleton skeleton in skeletons)
                    {
                        // Überprüfen, ob ein Skelett verfolgt wird
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            Joint handRightJoint = skeleton.Joints[JointType.HandRight];
                            Joint wristRightJoint = skeleton.Joints[JointType.WristRight];
                            Joint elbowRightJoint = skeleton.Joints[JointType.ElbowRight];

                            // Prüfe, ob Handgelenk und Ellbogen getrackt werden
                            if (wristRightJoint.TrackingState == JointTrackingState.Tracked &&
                                elbowRightJoint.TrackingState == JointTrackingState.Tracked)
                            {
                                // 1) Hole die Depth-Koordinaten von Handgelenk und Ellbogen
                                DepthImagePoint wristDepthPoint = _kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                    wristRightJoint.Position,
                                    DepthImageFormat.Resolution640x480Fps30
                                );
                                DepthImagePoint elbowDepthPoint = _kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                    elbowRightJoint.Position,
                                    DepthImageFormat.Resolution640x480Fps30
                                );

                                // 2) Starte die Tiefentransformation
                                DepthTransformation depthTransformation = new();
                                short[] newDepthPixels = depthTransformation.StartDepthTransformation(
                                    _depthPixels2,
                                    wristDepthPoint,
                                    elbowDepthPoint
                                );

                                // 3) Hand-BoundingBox und Kontur ermitteln
                                HandBoundingBox handBoundingBox = new(newDepthPixels);
                                (int bLeft, int bRight, int bTop) boundingBox = handBoundingBox.CalculateBoundingBox(
                                    wristDepthPoint,
                                    elbowDepthPoint
                                );

                                double[] contourPixels = handBoundingBox.FindContourPixels(
                                    wristDepthPoint.X,
                                    wristDepthPoint.Y,
                                    _depthPixels2,      // Original-Tiefenwerte
                                    wristDepthPoint.Depth
                                );
                                double[] averageContourPixels = handBoundingBox.MovingAverageFilter(contourPixels, 6);
                                List<(int Index, double Value)> maximaPoints =
                                    handBoundingBox.FindLocalMaximaWithIndices(averageContourPixels, 8, 0.6f);

                                // 4) Hand offen oder zu? (deine Logik)
                                //    Du hast zusätzlich eine State-Machine mit _handStates. Hier nur ein Beispiel:
                                if (maximaPoints.Count > 1)
                                    _handStates.AddLast(1);
                                else
                                    _handStates.AddLast(0);

                                if (_handStates.Count > 3)
                                    _handStates.RemoveFirst();

                                if (_handStates.Count == 3 &&
                                    _handStates.First.Value == _handStates.First.Next.Value &&
                                    _handStates.First.Value == _handStates.Last.Value)
                                {
                                    if (_handStates.First.Value == 1)
                                    {
                                        handOpen = true;
                                        Debug.WriteLine("hand offen");
                                    }
                                    else
                                    {
                                        handOpen = false;
                                        Debug.WriteLine("hand zu");
                                    }
                                }

                                // 5) Kontur-Bitmap zeichnen
                                BitmapSource contourBitmap = handBoundingBox.DrawBoundingBoxBlack(wristDepthPoint.Y);
                                KinectVideo.Source = contourBitmap; // Zeigen der Kontur im UI

                                // 6) Einfärben der transformierten Tiefenwerte -> DepthVideo
                                for (int i = 0; i < newDepthPixels.Length; i++)
                                {
                                    short depth = newDepthPixels[i];
                                    if (depth >= 800 && depth <= 3600)
                                    {
                                        byte brightness = (byte)(255 - ((depth - 800) * 255 / (3600 - 800)));
                                        _depthData[i] = brightness;
                                    }
                                    else
                                    {
                                        _depthData[i] = 0;
                                    }
                                }
                                BitmapSource grayscaleBitmap = BitmapSource.Create(
                                    640,
                                    480,
                                    96,
                                    96,
                                    PixelFormats.Gray8,
                                    null,
                                    _depthData,
                                    640
                                );
                                DepthVideo.Source = grayscaleBitmap;

                                // 7) Felder für späteres CSV/Bild-Speichern aktualisieren
                                _lastHandOpen = handOpen;             // bool
                                _lastBoundingBox = boundingBox;        // (int bLeft, int bRight, int bTop)
                                _lastContourPixels = contourPixels;    // double[]
                                _lastAverageContourPixels = averageContourPixels; // double[]
                                _lastMaxima = maximaPoints;                  // List<double>
                                _lastContourImage = contourBitmap;     // BitmapSource
                            }
                        }
                    }
                }
            }
        }


        private void KinectSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(_depthPixels);
                    for(int i = 0; i < _depthPixels2.Length; ++i)
                    {
                        _depthPixels2[i] = _depthPixels[i].Depth;
                    }
                    // Get the min and max reliable depth for the current frame
                    int minDepth = 800;
                    int maxDepth = 4000;
                    
                    for (int i = 0; i < _depthPixels2.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = _depthPixels2[i];
                        
                        if (depth >= minDepth && depth <= maxDepth)
                        {
                            byte brightness = (byte)(255 - ((depth - minDepth) * 255 / (maxDepth - minDepth)));
                            _depthData[i] = brightness; // Helle Objekte = Nah / Dunkle Objekte = Fern
                            
                        }
                        else
                        {
                            _depthData[i] = 0;
                        }
                        
                    }
                    BitmapSource grayscaleBitmap = BitmapSource.Create(
                        depthFrame.Width,
                        depthFrame.Height,
                        96,
                        96,
                        PixelFormats.Gray8,
                        null,
                        _depthData,
                        depthFrame.Width
                    );


                    KinectVideo.Source = grayscaleBitmap;
                }

            }
        }

        private void KinectSensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var frame = e.OpenColorImageFrame())
            {
                if (frame != null)
                {
                    frame.CopyPixelDataTo(_clolorData);
                    _colorBitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), _clolorData, frame.Width * 4, 0);
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S)
            {
                // 1) Prüfen, ob wir überhaupt schon Daten haben
                if (_lastContourPixels == null || _lastAverageContourPixels == null || _lastMaxima == null)
                {
                    MessageBox.Show("Noch keine Hand-Daten erfasst!");
                    return;
                }

                // 2) Zuerst Hand-Daten speichern (CSV + Kontur-PNG), 
                //    wie im vorherigen Beispiel. Wir nehmen an, du hast
                //    eine DataSaver-Klasse mit SaveHandData(...).
                DataSaver.SaveHandDataToExcelWithChart(
                    isHandOpen: _lastHandOpen,
                    boundingBox: _lastBoundingBox,
                    contourPixels: _lastContourPixels,
                    averageContourPixels: _lastAverageContourPixels,
                    maxima: _lastMaxima,
                    contourImage: _lastContourImage // aus HandBoundingBox.DrawBoundingBoxBlack
                );

                // 3) Screenshot des ganzen Fensters erstellen und speichern.
                //    Wir verwenden RenderTargetBitmap.
                string folderName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string folderPath = System.IO.Path.Combine("Recordings", folderName);
                System.IO.Directory.CreateDirectory(folderPath);

                var rtb = new RenderTargetBitmap(
                    (int)this.ActualWidth,
                    (int)this.ActualHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32
                );
                rtb.Render(this); // "this" = ganzes Window

                // Als PNG codieren und schreiben
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                string screenshotPath = System.IO.Path.Combine(folderPath, "Screenshot.png");
                using (var fs = new FileStream(screenshotPath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                MessageBox.Show($"Hand-Daten und Screenshot gespeichert in:\n{folderPath}");
            }
        }


        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_kinectSensor != null && _kinectSensor.IsRunning)
            {
                _kinectSensor.Stop();
            }
        }
    }
}