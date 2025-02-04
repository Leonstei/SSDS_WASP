using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace KinectWASP
{
    public partial class MainWindow : Window
    {
        private KinectSensor _kinectSensor;
        
        //Intermediate storage for the depth data received from the camera 
        private DepthImagePixel[] _depthPixels;
        
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
            MouseDown += OnMouseDown;
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

                _depthData = new byte[_kinectSensor.DepthStream.FramePixelDataLength * sizeof(int)]; 
                
                _depthBitmap = BitmapSource.Create(
                    _kinectSensor.DepthStream.FrameWidth, _kinectSensor.DepthStream.FrameHeight, 96.0, 96.0, 
                    PixelFormats.Gray8, null, _depthData, _kinectSensor.DepthStream.FrameWidth );
                DepthVideo.Source = _depthBitmap;

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

                    // Verarbeitung aller erkannten Skeletts
                    foreach (Skeleton skeleton in skeletons)
                    {
                        // Überprüfen, ob ein Skelett verfolgt wird
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            // Zugriff auf Gelenkpunkte eines getrackten Skeletts
                            Joint headJoint = skeleton.Joints[JointType.Head];
                            Joint handRightJoint = skeleton.Joints[JointType.HandRight];
                            Joint handLeftJoint = skeleton.Joints[JointType.HandLeft];
                            Joint wristRightJoint = skeleton.Joints[JointType.WristRight];
                            

                            if (handRightJoint.TrackingState == JointTrackingState.Tracked)
                            {
                                //Console.WriteLine($"Rechte Hand: X={handRightJoint.Position.X}, Y={handRightJoint.Position.Y}, Z={handRightJoint.Position.Z}");
                            }
                            if (wristRightJoint.TrackingState == JointTrackingState.Tracked)
                            {
                                Console.WriteLine($"Rechte wrist: X={wristRightJoint.Position.X}, Y={wristRightJoint.Position.Y}, Z={wristRightJoint.Position.Z}");
                            }
                            
                            // if (handLeftJoint.TrackingState == JointTrackingState.Tracked)
                            // {
                            //     Console.WriteLine($"Linke Hand: X={handLeftJoint.Position.X}, Y={handLeftJoint.Position.Y}, Z={handLeftJoint.Position.Z}");
                            // }
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
                    
                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = 3600;
                    
                    for (int i = 0; i < _depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = _depthPixels[i].Depth;

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
                    DepthVideo.Source  = grayscaleBitmap;
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
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Position des Mausklicks relativ zum Fenster
                Point clickPosition = e.GetPosition(this);

                // Prüfen, ob die Klickposition im Bereich des Bildes liegt
                if (KinectVideo != null && KinectVideo.Source is WriteableBitmap bitmap)
                {
                    // Position relativ zur Quelle des WriteableBitmap
                    var transform = KinectVideo.TransformToAncestor(this);
                    Point relativePosition = transform.Transform(new Point(0, 0));

                    // Berechnung der Position im Bild (relativ zum "Bitmap-Bereich")
                    int x = (int)(clickPosition.X - relativePosition.X);
                    int y = (int)(clickPosition.Y - relativePosition.Y);

                    // Sicherstellen, dass die Position innerhalb des Bildbereichs liegt
                    if (x >= 0 && x < bitmap.PixelWidth && y >= 0 && y < bitmap.PixelHeight)
                    {
                        // Indizierung des Pixel-Formats: BGR32 -> 4 Bytes pro Pixel
                        int bytesPerPixel = 4;
                        int pixelIndex = (y * bitmap.PixelWidth + x) * bytesPerPixel;

                        // Farbwerte aus dem Buffer extrahieren
                        byte blue = _clolorData[pixelIndex];
                        byte green = _clolorData[pixelIndex + 1];
                        byte red = _clolorData[pixelIndex + 2];

                        // RGB-Werte in der Konsole ausgeben
                        Console.WriteLine($"RGB-Wert bei Klickposition ({x}, {y}): R={red}, G={green}, B={blue}");
                    }
                }
                if (DepthVideo != null && DepthVideo.Source is BitmapSource depthBitmap)
                {
                    // Position relativ zur Quelle des BitmapSource
                    var transform = DepthVideo.TransformToAncestor(this);
                    Point relativePosition = transform.Transform(new Point(0, 0));

                    // Berechnung der Position im Bild (relativ zur "BitmapGröße")
                    int x = (int)(clickPosition.X - relativePosition.X);
                    int y = (int)(clickPosition.Y - relativePosition.Y);

                    // Sicherstellen, dass die Position innerhalb des Bildbereichs liegt
                    if (x >= 0 && x < depthBitmap.PixelWidth && y >= 0 && y < depthBitmap.PixelHeight)
                    {
                        // Berechnen des Indexes im Tiefen-Array
                        int pixelIndex = y * depthBitmap.PixelWidth + x;

                        // Tiefe von _depthPixels extrahieren
                        if (_depthPixels != null && pixelIndex < _depthPixels.Length)
                        {
                            short depthValue = _depthPixels[pixelIndex].Depth;

                            // Tiefe des Pixels in der Konsole ausgeben
                            Console.WriteLine($"Tiefe bei Klickposition ({x}, {y}): {depthValue} Millimeter");
                        }
                    }
                }
            }
        }
    }
}