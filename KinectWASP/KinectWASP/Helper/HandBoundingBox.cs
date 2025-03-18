using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Microsoft.Kinect;

namespace KinectTest.Helper;

public class HandBoundingBox
{
    const int UMax = 80;
    const int VMax = 160;
    const int Threshold = 110;
    
    int bLeft, bRight, bTop, bBottom;
    private short[] _depthdata;
    private Vector2[] _conturPoints = new Vector2[360];

    public HandBoundingBox(short[] depthdata)
    {
        _depthdata = depthdata;
    }

    
    public (int bLeft, int bRight, int bTop) CalculateBoundingBox( DepthImagePoint wristPosition, DepthImagePoint elbowPosition )
    {
        bLeft = -UMax;
        bRight = UMax;
        bTop = VMax;
        int wristDepth = wristPosition.Depth;

        // Calculate bLeft
        for (int u = wristPosition.X; u >= wristPosition.X -UMax; u--)
        {
            bool found = false;
            for (int v = wristPosition.Y; v <= wristPosition.Y + VMax && v < 480; v++)
            {
                if (Math.Abs(GetDepth( u, v) - wristDepth)  < Threshold)
                {
                    bLeft = u;
                    found = true;
                    break;
                }
            }
            if (!found || u == 0) break;
        }

        // Calculate bRight
        for (int u = wristPosition.X; u <= wristPosition.X + UMax && u < 640; u++)
        {
            bool found = false;
            for (int v = wristPosition.Y; v <= wristPosition.Y + VMax && v < 480; v++)
            {
                if (Math.Abs(GetDepth( u, v) - wristDepth)  < Threshold)
                {
                    bRight = u;
                    found = true;
                    break;
                }
            }
            if (!found) break;
        }

        // Calculate bTop
        for (int v = wristPosition.Y; v <= wristPosition.Y + VMax && v < 480; v++)
        {
            bool found = false;
            for (int u = bLeft; u <= bRight; u++)
            {
                if (Math.Abs(GetDepth( u, v) - wristDepth)  < Threshold)
                {
                    bTop = v;
                    found = true;
                    break;
                }
            }
            if (!found) break;
        }

        // Console.WriteLine($"bLeft = {bLeft} bRight = {bRight} bTop = {bTop} distTop = {wristPosition.Y-bTop} wristY = {wristPosition.Y}");
        return (bLeft, bRight, bTop);
    }

    private int GetDepth( int u, int v)
    {
        if (u < 0 || u >= 640 || v < 0 || v >= 480)
        {
            // Console.WriteLine($" index wrong u= {u}, v= {v}");
            return int.MaxValue;
        }
        return _depthdata[v * 640 + u];
    }
    
    public WriteableBitmap DrawBoundingBox(WriteableBitmap bitmap, int wristY, double[] conturPixels)
    {
        if (bLeft < 0) bLeft = 0;
        if ( bRight > 640) bRight = 640;
        if (bTop > 480) bTop = 480;
        if (wristY < 0) wristY = 0;
            
        // Console.WriteLine($"bLeft = {bLeft} bRight = {bRight} bTop = {bTop} bBottom = {wristY} wristY = {wristY} ");
        bitmap.Lock();
        for (int u = bLeft; u <= bRight; u++)
        {
            bitmap.WritePixels(new System.Windows.Int32Rect(u, bTop, 1, 1), new byte[] { 255, 0, 0, 255 }, 4, 0);
            bitmap.WritePixels(new System.Windows.Int32Rect(u, wristY, 1, 1), new byte[] { 255, 0, 0, 255 }, 4, 0);
        }
        for (int v = bTop; v >= wristY; v--)
        {
            bitmap.WritePixels(new System.Windows.Int32Rect(bLeft, v, 1, 1), new byte[] { 255, 0, 0, 255 }, 4, 0);
            bitmap.WritePixels(new System.Windows.Int32Rect(bRight, v, 1, 1), new byte[] { 255, 0, 0, 255 }, 4, 0);
        }
        
        bitmap.Unlock();
        return bitmap;
    }
    
    public BitmapSource DrawBoundingBoxBlack( int wristY)
    {
        if (bLeft < 0) bLeft = 0;
        if ( bRight > 640) bRight = 640;
        if (bTop > 480) bTop = 480;
        if (wristY < 0) wristY = 0;
        if (bLeft >= bRight || wristY >= bTop)
            return null;
            
        // short width = (short)(bRight - bLeft+1);
        // short height = (short)(bTop - wristY+1);
        // byte[] depthData = new byte[width * height];
        // for (int i = 0; i < depthData.Length; i++)
        // {
        //     if (i < width) depthData[i] = 0;
        //     else if(i >= depthData.Length-width) depthData[i] = 0;
        //     else if (i % width == 0) depthData[i] = 0;
        //     else if ((i + 1) % width == 0) depthData[i] = 0;
        //     else depthData[i] = 255;
        // }
        // foreach (var point in _conturPoints)
        // {
        //     int u = (int)point.X  -bLeft  ;
        //     int v = (int)point.Y -wristY; ;
        //     int index = v * width + u;
        //     if (u >= 0 && u <= width && v >= 0 && v <= height )
        //     {
        //         depthData[index] = 0;
        //     }
        //     // else
        //     // {
        //     //     Console.WriteLine($"u = {u}; v = {v}; index = {index}");
        //     // }
        // }
        
        int width = 640;
        int height = 480;
        byte[] depthData = new byte[width * height];

        for (int i = 0; i < depthData.Length; i++)
        {
                if (i < width) depthData[i] = 0;
                else if(i >= depthData.Length-width) depthData[i] = 0;
                else if (i % width == 0) depthData[i] = 0;
                else if ((i + 1) % width == 0) depthData[i] = 0;
                else depthData[i] = 255;
        }
        foreach (var point in _conturPoints)
        {
            int u = (int)point.X ;
            int v = (int)point.Y ;
            int index = v * width + u;
            if (u >= 0 && u <= width && v >= 0 && v <= height)
            {
                depthData[index] = 0;
            }
            else
            {
                Console.WriteLine($"u = {u}; v = {v}; index = {index}");
            }
        }
        
        BitmapSource grayscaleBitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Gray8,
            null,
            depthData,
            width
        );
        return grayscaleBitmap;
    }


    public double[] MovingAverageFilter(double[] input, int rf)
    {
        int length = input.Length;
        double[] smoothed = new double[length];
        int windowSize = 2 * rf + 1;

        // Prüfen, ob das Signal für den Filter lang genug ist
        if (length < windowSize)
        {
            throw new ArgumentException("Das Signal ist kürzer als die Filterfenstergröße.");
        }

        // 1. Initiale Berechnung des Mittelwerts für das erste Fenster
        double sum = 0;
        for (int j = -rf; j <= rf; j++)
        {
            sum += input[rf + j];
        }
        for (int i = 0; i <= rf; i++) smoothed[i] = sum / windowSize;

        // 2. Iterative Berechnung (Gleitender Durchschnitt)
        for (int i = rf + 1; i < length - rf; i++)
        {
            sum += input[i + rf] - input[i - rf - 1];
            smoothed[i] = sum / windowSize;
        }
        for (int j = length - rf; j < length; j++) smoothed[j] = smoothed[j-rf]; 

        return smoothed;
    }

    public List<(int Index, double Value)> FindLocalMaximaWithIndices(double[] input, int rn, float mu)
    {
        var maxima = new List<(int, double)>();
        for (int i = rn + 1; i < input.Length - rn; i++)
        {
            double current = input[i];
            

            // 1. Monotoniekriterium
            bool isMonotone = true;
            for (int j = -rn; j < 0; j++)
            {
                if (input[i + j] > input[i + j + 1] ||
                    input[i - j] > input[i - j - 1])
                {
                    isMonotone = false;
                    break;
                }
            }
            // for (int j = -rn; j < rn; j++)
            // {
            //     if ((current < input[i + j]))
            //     {
            //         isMonotone = false;
            //         break;
            //     }
            // }
            if (!isMonotone) continue;

            // 2. Mindest-Steigung
            double leftSlope = (current - input[i - rn]) / rn;
            double rightSlope = (current - input[i + rn]) / rn;
            if (leftSlope + rightSlope > mu)
            {
                maxima.Add((i, current));
            }
        }
        return maxima;
    }


    public double[] FindContourPixels(int wristX, int wristY,short[] depthData,int wristDepth )
    {
        int strahlAnzahl = 360;
        double winkelGröße = 0.5;
        double[] countourPixels = new Double[strahlAnzahl];
        double winkelRadiant;
        for (int strahl = 0; strahl < strahlAnzahl; strahl++)
        {
            winkelRadiant =  strahl* winkelGröße * Math.PI / 180;
            double tRight = (bRight-wristX) / Math.Cos(winkelRadiant);
            double yRight = wristY + tRight * Math.Sin(winkelRadiant);
            if (yRight <= bTop && yRight >= wristY)
            {
                countourPixels[strahl] = FindFirstHandPilxel(new Vector2D(bRight, yRight),wristX,wristY,wristDepth,strahl);
                continue;
            }
            double tLeft = (bLeft-wristX) / Math.Cos(winkelRadiant);
            double yLeft = wristY + tLeft * Math.Sin(winkelRadiant);
            if (yLeft <= bTop && yLeft >= wristY)
            {
                countourPixels[strahl] =  FindFirstHandPilxel(new Vector2D(bLeft, yLeft),wristX,wristY,wristDepth,strahl);
                continue;
            }
            double tTop = (bTop-wristY) / Math.Sin(winkelRadiant);
            double xTop = wristX + tTop * Math.Cos(winkelRadiant);
            if (xTop <= bRight && xTop >= bLeft)
                countourPixels[strahl] = FindFirstHandPilxel(new Vector2D(xTop, bTop),wristX,wristY,wristDepth,strahl);
            
        }

        return countourPixels;
    }
    

    private double FindFirstHandPilxel( Vector2D schnittpunkt,int wristX,int wristY, int wristDepth,int index)
    {
        double distX = schnittpunkt.X-wristX;
        double distY = schnittpunkt.Y-wristY;
        double length = Math.Sqrt(distX * distX + distY * distY);
        double stepX = distX / length;
        double stepY = distY/ length;
        
        double u = schnittpunkt.X;
        double v = schnittpunkt.Y;
        
        
        while (u >= bLeft && u <= bRight && v <= bTop && v >= wristY)
        {
            int depth = GetDepth( (int)Math.Round(u), (int)Math.Round(v));
            if(Math.Abs(depth - wristDepth)  < Threshold)
            {
                length = Math.Sqrt((u-wristX) * (u -wristX) + (v -wristY) * (v-wristY));
                if ((int)Math.Round(u) <= 0 && (int)Math.Round(v) <= 0)
                {
                    u += v;
                }
                _conturPoints[index] = new Vector2((int)Math.Round(u), (int)Math.Round(v));
                return length;
            }
            u -= stepX;
            v -= stepY;
        }
        // Console.WriteLine("kein handPixel gefunden");
        return 0.0;
    }

    public BitmapSource  DrawHandpixelInBoundingBox( int wristY, int wristDepth)
    {
        if (bLeft < 0) bLeft = 0;
        if ( bRight > 640) bRight = 640;
        if (bTop > 480) bTop = 480;
        if (wristY < 0) wristY = 0;
        if (bLeft >= bRight || wristY >= bTop)
            return null;
        
        short width = (short)(bRight - bLeft+1);
        short height = (short)(bTop - wristY+1);
        byte[] depthData = new byte[width * height];
        
        for (int i = 0; i < depthData.Length; i++)
        {
            int x = (i % width) + bLeft;
            int y = (i / width) + wristY;
            if (Math.Abs(GetDepth(x, y) - wristDepth) < Threshold) depthData[i] = 0;
            else if (i < width) depthData[i] = 0;
            else if(i >= depthData.Length-width) depthData[i] = 0;
            else if (i % width == 0) depthData[i] = 0;
            else if ((i + 1) % width == 0) depthData[i] = 0;
            else depthData[i] = 255;
        }   
        
        BitmapSource grayscaleBitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Gray8,
            null,
            depthData,
            width
        );
        return grayscaleBitmap; 
    }
}
