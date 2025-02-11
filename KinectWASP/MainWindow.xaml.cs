using System.Windows;
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
        
        //storage for color data
        private byte[] _clolorData;
        
        //storage for depth data
        private byte[] _depthData;
        

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
                //_kinectSensor.ColorFrameReady += KinectSensor_ColorFrameReady;
                _kinectSensor.DepthFrameReady += KinectSensor_DepthFrameReady;
                _kinectSensor.SkeletonFrameReady += KinectSensor_SkeletonFrameReady;
                
                _depthPixels = new DepthImagePixel[_kinectSensor.DepthStream.FramePixelDataLength];
                _depthPixels2 = new short[_kinectSensor.DepthStream.FramePixelDataLength];

                _depthData = new byte[_kinectSensor.DepthStream.FramePixelDataLength * sizeof(int)]; 
                
                _depthBitmap = BitmapSource.Create(
                    _kinectSensor.DepthStream.FrameWidth, _kinectSensor.DepthStream.FrameHeight, 96.0, 96.0, 
                    PixelFormats.Gray8, null, _depthData, _kinectSensor.DepthStream.FrameWidth );
                DepthVideo.Source = _depthBitmap;

                _clolorData = new byte[_kinectSensor.ColorStream.FramePixelDataLength];
                _colorBitmap = new WriteableBitmap(640, 480, 96.0, 96.0, System.Windows.Media.PixelFormats.Bgr32, null);
                

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

                    // Verarbeitung aller erkannten Skeletts
                    foreach (Skeleton skeleton in skeletons)
                    {
                        // Überprüfen, ob ein Skelett verfolgt wird
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            // Zugriff auf Gelenkpunkte eines getrackten Skeletts
                            //Joint headJoint = skeleton.Joints[JointType.Head];
                            Joint handRightJoint = skeleton.Joints[JointType.HandRight];
                            //Joint handLeftJoint = skeleton.Joints[JointType.HandLeft];
                            Joint wristRightJoint = skeleton.Joints[JointType.WristRight];
                            Joint elbowRightJoint = skeleton.Joints[JointType.ElbowRight];

                            if (wristRightJoint.TrackingState == JointTrackingState.Tracked &&
                                elbowRightJoint.TrackingState == JointTrackingState.Tracked)
                            {
                                
                                //Console.WriteLine($"wristRight:{wristRightJoint.Position.X}elbow:{wristRightJoint.Position.Y}");
                                SkeletonPoint wristPosition = wristRightJoint.Position;
                                DepthImagePoint wristDepthPoint = _kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                    wristPosition, 
                                    DepthImageFormat.Resolution640x480Fps30
                                );
                                SkeletonPoint elbowPosition = elbowRightJoint.Position;
                                DepthImagePoint elbowDepthPoint = _kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                    elbowPosition, 
                                    DepthImageFormat.Resolution640x480Fps30
                                );
                                // for (int i = 0; i < _depthPixels.Length; i++)
                                // {
                                //     // Berechne die (x, y)-Koordinaten des Pixels aus dem Array-Index
                                //     int x = i % 640; // Spaltenindex
                                //     int y = i / 640;
                                //     if (Math.Abs(_depthPixels2[i] - wristDepthPoint.Depth) < 110)
                                //     {
                                //         _depthData[i] = 0;
                                //     }
                                // }

                                //Console.WriteLine($"wristRight:{wristDepthPoint.X}elbow:{wristDepthPoint.Y}");
                                DepthTransformation depthTransformation = new();
                                byte[] newDepthPixels = depthTransformation.StartDepthTransformation(_depthPixels2, wristDepthPoint, elbowDepthPoint);
                                BitmapSource grayscaleBitmap = BitmapSource.Create(
                                    640,
                                    480,
                                    96,
                                    96,
                                    PixelFormats.Gray8,
                                    null,
                                    newDepthPixels,
                                    640
                                );
                                
                                // BitmapSource grayscaleBitmap2 = BitmapSource.Create(
                                //     640,
                                //     480,
                                //     96,
                                //     96,
                                //     PixelFormats.Gray8,
                                //     null,
                                //     _depthData,
                                //     640
                                // );
                                //
                                // DepthVideo.Source  = grayscaleBitmap2;
                                KinectVideo.Source = grayscaleBitmap;
                                
                            }
                            if (handRightJoint.TrackingState == JointTrackingState.Tracked)
                            {

                                 SkeletonPoint wristPosition = handRightJoint.Position;
                                 DepthImagePoint wristDepthPoint = _kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                     wristPosition, 
                                     DepthImageFormat.Resolution640x480Fps30
                                 );
                                // int wristX = wristDepthPoint.X; 
                                // int wristY = wristDepthPoint.Y; 
                                // int wristDepth = wristDepthPoint.Depth;
                                //
                                // int boxWidth = 110;  // Breite des Bereichs
                                // int boxHeight = 110; // Höhe des Bereichs
                                //
                                // // Grenzen des Rechtecks berechnen (stellen Sie sicher, dass sie im Bildbereich bleiben)
                                // int startX = Math.Max(0, wristX - boxWidth / 2);
                                // int startY = Math.Max(0, wristY - boxHeight / 2);
                                // int endX = Math.Min(639, wristX + boxWidth / 2); // 640 ist typischerweise die Breite des Tiefenbilds
                                // int endY = Math.Min(479, wristY + boxHeight / 2); // 480 ist typischerweise die Höhe des Tiefenbilds
                                //
                                // DepthImagePixel[] extractedDepth = new DepthImagePixel[_depthPixels.Length];
                                // byte[] depthData = new byte[12100];
                                // int i = 0;
                                // for (int y = startY; y < endY; y++)
                                // {
                                //     for (int x = startX; x < endX; x++)
                                //     {
                                //         short depth = _depthPixels[x + y * 640].Depth;
                                //         // Tiefe-Wert übernehmen
                                //         extractedDepth[i] =
                                //             _depthPixels[x + y * 640]; // 640 = Breite des ursprünglichen Tiefenbilds
                                //         
                                //         if (depth >= 800 && depth <= 3600)
                                //         {
                                //             depthData[i] = (byte)(255 - ((depth - 800) * 255 / (3600 - 800)));
                                //         }
                                //         else
                                //         {
                                //             depthData[i] = 0;
                                //         }
                                //         i++;
                                //     }
                                // }
                                // BitmapSource grayscaleBitmap = BitmapSource.Create(
                                //     110,
                                //     110,
                                //     96,
                                //     96,
                                //     PixelFormats.Gray8,
                                //     null,
                                //     depthData,
                                //     110
                                // );
                                // KinectVideo.Source = grayscaleBitmap;
                                //
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
                    Parallel.For(0, _depthPixels.Length, i =>
                    {
                        _depthPixels2[i] = _depthPixels[i].Depth; 
                    });
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
                            // Setzen Sie außerhalb des Bereichs liegende Pixel auf Schwarz (ARGB = 0)
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_kinectSensor != null && _kinectSensor.IsRunning)
            {
                _kinectSensor.Stop();
            }
        }
    }
}