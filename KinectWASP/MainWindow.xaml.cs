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
                //_kinectSensor.SkeletonFrameReady += KinectSensor_SkeletonFrameReady;
                
                _depthPixels = new DepthImagePixel[_kinectSensor.DepthStream.FramePixelDataLength];

                _depthData = new byte[_kinectSensor.DepthStream.FramePixelDataLength * sizeof(int)]; 
                
                _depthBitmap = BitmapSource.Create(
                    _kinectSensor.DepthStream.FrameWidth, _kinectSensor.DepthStream.FrameHeight, 96.0, 96.0, 
                    PixelFormats.Gray8, null, _depthData, _kinectSensor.DepthStream.FrameWidth );
                DepthVideo.Source = _depthBitmap;

                _clolorData = new byte[_kinectSensor.ColorStream.FramePixelDataLength];
                _colorBitmap = new WriteableBitmap(640, 480, 96.0, 96.0, System.Windows.Media.PixelFormats.Bgr32, null);
                //KinectVideo.Source = _colorBitmap;

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
                    DepthImagePixel[] denoisedOnce = DenoisDepthPixels(_depthPixels, 640, 480);
                    //DepthImagePixel[] denoisedDepthPixels = DenoisDepthPixels(denoisedOnce, 640, 480);
                    byte[] depthData2 = new byte[_depthData.Length];
                    
                    for (int i = 0; i < _depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = _depthPixels[i].Depth;
                        short denoisedDepth = denoisedOnce[i].Depth;

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
                        if (denoisedDepth >= minDepth && denoisedDepth <= maxDepth)
                        {
                            byte brightness2 = (byte)(255 - ((denoisedDepth - minDepth) * 255 / (maxDepth - minDepth)));
                            depthData2[i] = brightness2; // Helle Objekte = Nah / Dunkle Objekte = Fern
                        }
                        else
                        {
                            // Setzen Sie außerhalb des Bereichs liegende Pixel auf Schwarz (ARGB = 0)
                            depthData2[i] = 0;
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
                    BitmapSource grayscaleBitmap2 = BitmapSource.Create(
                        depthFrame.Width,
                        depthFrame.Height,
                        96,
                        96,
                        PixelFormats.Gray8,
                        null,
                        depthData2,
                        depthFrame.Width
                    );
                    KinectVideo.Source = grayscaleBitmap2;
                }
                
            }
        }
        
        // Funktion zum Glätten des Tiefenbildes mit anpassbarem Sigma und Kernelgröße
        private DepthImagePixel[] DenoisDepthPixels(DepthImagePixel[] depthPixels, int width, int height)
        { 
            double[,] gaussian16x16_sigma15 = new double[,]
{
    {0.0039, 0.0040, 0.0040, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0040, 0.0040, 0.0040, 0.0039},
    {0.0040, 0.0041, 0.0041, 0.0042, 0.0042, 0.0042, 0.0042, 0.0042, 0.0042, 0.0042, 0.0042, 0.0042, 0.0041, 0.0041, 0.0041, 0.0040},
    {0.0040, 0.0041, 0.0042, 0.0042, 0.0042, 0.0043, 0.0043, 0.0043, 0.0043, 0.0043, 0.0043, 0.0042, 0.0042, 0.0042, 0.0041, 0.0040},
    {0.0041, 0.0042, 0.0042, 0.0043, 0.0043, 0.0044, 0.0044, 0.0044, 0.0044, 0.0044, 0.0043, 0.0043, 0.0043, 0.0042, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0042, 0.0043, 0.0044, 0.0044, 0.0045, 0.0045, 0.0045, 0.0044, 0.0044, 0.0044, 0.0043, 0.0042, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0044, 0.0045, 0.0045, 0.0046, 0.0045, 0.0045, 0.0044, 0.0044, 0.0043, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0045, 0.0045, 0.0046, 0.0046, 0.0046, 0.0045, 0.0045, 0.0044, 0.0044, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0045, 0.0046, 0.0046, 0.0047, 0.0046, 0.0046, 0.0045, 0.0045, 0.0044, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0045, 0.0046, 0.0046, 0.0046, 0.0046, 0.0046, 0.0045, 0.0045, 0.0044, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0045, 0.0046, 0.0046, 0.0046, 0.0046, 0.0046, 0.0045, 0.0045, 0.0044, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0045, 0.0046, 0.0046, 0.0046, 0.0046, 0.0046, 0.0045, 0.0045, 0.0044, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0043, 0.0044, 0.0044, 0.0045, 0.0045, 0.0046, 0.0045, 0.0045, 0.0044, 0.0044, 0.0043, 0.0043, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0042, 0.0043, 0.0044, 0.0044, 0.0045, 0.0045, 0.0045, 0.0044, 0.0044, 0.0044, 0.0043, 0.0042, 0.0042, 0.0041},
    {0.0041, 0.0042, 0.0042, 0.0043, 0.0043, 0.0044, 0.0044, 0.0044, 0.0044, 0.0044, 0.0043, 0.0043, 0.0043, 0.0042, 0.0042, 0.0041},
    {0.0040, 0.0041, 0.0042, 0.0042, 0.0042, 0.0043, 0.0043, 0.0043, 0.0043, 0.0043, 0.0043, 0.0042, 0.0042, 0.0042, 0.0041, 0.0040},
    {0.0039, 0.0040, 0.0040, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0041, 0.0040, 0.0040, 0.0040, 0.0039}
};


            

        int kernelSize = 3; // 3x3 Kernel
        int kernelSum = 16; // Summe der Kernelwerte zur Normalisierung
        int offset = kernelSize / 2;

        // Neues Array für geglättete Tiefenwerte
        DepthImagePixel[] smoothedPixels = new DepthImagePixel[depthPixels.Length];

        // Durch das Bild iterieren
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double weightedSum = 0;
                double validWeightSum = 0; // Um zu verhindern, dass ungültige Tiefenwerte das Ergebnis verfälschen

                // Über den Kernel iterieren
                for (int ky = -offset; ky <= offset; ky++)
                {
                    for (int kx = -offset; kx <= offset; kx++)
                    {
                        int pixelX = x + kx;
                        int pixelY = y + ky;

                        // Überprüfen, ob der Pixel innerhalb des Bildbereichs liegt
                        if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                        {
                            double kernelValue = gaussian16x16_sigma15[ky + offset, kx + offset];
                            int pixelIndex = pixelY * width + pixelX;
                            int depthValue = depthPixels[pixelIndex].Depth;

                            // Überprüfen, ob der Tiefenwert gültig ist (z.B. nicht 0 bei Kinect)
                            if (depthValue > 0)
                            {
                                weightedSum += depthValue * kernelValue;
                                validWeightSum += kernelValue;
                            }
                        }
                    }
                }

                // Falls keine gültigen Werte gefunden wurden, den Originalwert übernehmen
                int currentIndex = y * width + x;
                if (validWeightSum > 0)
                {
                    smoothedPixels[currentIndex].Depth = (short)(weightedSum / validWeightSum);
                }
                else
                {
                    smoothedPixels[currentIndex].Depth = depthPixels[currentIndex].Depth;
                }
            }
        }

        return smoothedPixels;
        }

        // Funktion zur Erzeugung des Gaussian-Kernels
        private double[,] GenerateGaussianKernel(int kernelSize, double sigma)
        {
            double[,] kernel = new double[kernelSize, kernelSize];
            double sum = 0.0;

            int offset = kernelSize / 2;
            double sigmaSquared = 2 * sigma * sigma;
            double piSigma = 2 * Math.PI * sigma * sigma;

            // Kernel-Werte berechnen
            for (int y = -offset; y <= offset; y++)
            {
                for (int x = -offset; x <= offset; x++)
                {
                    double exponent = -(x * x + y * y) / sigmaSquared;
                    double value = (1 / piSigma) * Math.Exp(exponent);
                    kernel[y + offset, x + offset] = value;
                    sum += value;
                }
            }

            // Normalisierung des Kernels (Summe aller Werte = 1)
            for (int y = 0; y < kernelSize; y++)
            {
                for (int x = 0; x < kernelSize; x++)
                {
                    kernel[y, x] /= sum;
                }
            }

            return kernel;
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