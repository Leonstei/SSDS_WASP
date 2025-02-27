using System.Collections;
using Microsoft.Kinect;

namespace KinectTest.Helper;
// Falls du mit Kinect arbeitest

public class DepthTransformation
{
    
    public short[] StartDepthTransformation(short[] depthPixels, DepthImagePoint wrist, DepthImagePoint elbow)
    {
        
        // Beispielwerte (ersetze diese durch deine tatsächlichen Daten)
        int imageWidth = 640;  // Breite des Tiefenbildes
        int imageHeight = 480; // Höhe des Tiefenbildes
        

        // Hand- und Ellbogenpositionen definieren (für die Rotationsmatrix)
        Vector2D wirstPosition = new Vector2D(wrist.X, wrist.Y);   // Handgelenk in der Mitte des Bildes
        Vector2D elbowPosition = new Vector2D(elbow.X, elbow.Y);  // Ellbogen weiter unten im Bild

        // Berechne die Rotationsmatrix
        Matrix2x2 rotationMatrix = ComputeRotationMatrix(wirstPosition, elbowPosition);

        // Transformiere die Tiefenpixel
        short[] transformedDepthPixels = TransformDepthPixels(depthPixels, imageWidth, imageHeight, rotationMatrix, wirstPosition);

        // Ausgabe des ersten transformierten Pixels zur Überprüfung
        return transformedDepthPixels;
    }

    // Transformation der Tiefenpixel mit der Rotationsmatrix
    public static short[] TransformDepthPixels(short[] depthPixels, int width, int height, Matrix2x2 rotationMatrix, Vector2D wirstPosition)
    {
        short[] transformedPixels = new short[depthPixels.Length];
        // byte[] newDepth = new byte[depthPixels.Length];

        Parallel.For(0, depthPixels.Length, i =>
        {
            // Berechne die (x, y)-Koordinaten des Pixels aus dem Array-Index
            int x = i % width; // Spaltenindex
            int y = i / width; // Zeilenindex

            // double distanceSquared = Math.Abs(x - translation.X) + Math.Abs(y - translation.Y);
            // if (distanceSquared < 100)
            // {
            Vector2D originalPosition = new Vector2D(x - wirstPosition.X, y - wirstPosition.Y);
            

            // Wende die Rotationsmatrix und die Translation an
            Vector2D newPosition = rotationMatrix.Multiply(originalPosition) + wirstPosition;
            //Vector2D newPosition =  rotatedPosition +  translation;


            // Überprüfe, ob die neuen Koordinaten im Bildbereich liegen
            if (newPosition.X >= 0 && newPosition.X < width && newPosition.Y >= 0 && newPosition.Y < height)
            {
                //newIndexes++;
                int newIndex = (int)newPosition.Y * width + (int)newPosition.X;
                // Übertrage den Tiefenwert auf die neue Position
                transformedPixels[newIndex] = depthPixels[i];
                //newDepth[newIndex] = (byte)(255 - ((depthPixels[i] - 800) * 255 / (3600 - 800)));
            }
            // else
            // {
            //     newDepth[i] = 0;
            // }
            //}

        });
        //Console.WriteLine($"Anzahl der transformierten Pixel: {newIndexes}");
        //Console.WriteLine($"Rotationsmatrix: {rotationMatrix.M11}, {rotationMatrix.M12}, {rotationMatrix.M21}, {rotationMatrix.M22}");
        
        return transformedPixels;
    }

    // Berechnung der Rotationsmatrix basierend auf Hand- und Ellbogenposition
    public static Matrix2x2 ComputeRotationMatrix(Vector2D hand, Vector2D elbow)
    {
        Vector2D direction = (hand - elbow).Normalize();
        double b1 = direction.X;
        double b2 = direction.Y;

        return new Matrix2x2(b2, -b1, b1, b2);
    }
    public static Matrix2x2 CreateRotationMatrix(Vector2D hand, Vector2D elbow)
    {
        Vector2D direction = hand - elbow;

        double currentAngle = Math.Atan2(direction.Y, direction.X);

        // Zielwinkel: 90 Grad (nach oben), d.h. π/2
        double targetAngle = Math.PI / 2;

        // Berechne den Rotationswinkel, um die Hand nach oben zu richten
        double angle = targetAngle - currentAngle;
        return new Matrix2x2(
            Math.Cos(angle), -Math.Sin(angle),
            Math.Sin(angle), Math.Cos(angle)
        );
    }

}





// 2D-Vektor-Struktur
public struct Vector2D
{
    public double X { get; set; }
    public double Y { get; set; }

    public Vector2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vector2D operator +(Vector2D v1, Vector2D v2)
    {
        return new Vector2D(v1.X + v2.X, v1.Y + v2.Y);
    }

    public static Vector2D operator -(Vector2D v1, Vector2D v2)
    {
        return new Vector2D(v1.X - v2.X, v1.Y - v2.Y);
    }

    public double Norm()
    {
        return Math.Sqrt(X * X + Y * Y);
    }

    public Vector2D Normalize()
    {
        double norm = this.Norm();
        return new Vector2D(X / norm, Y / norm);
    }

    public override string ToString() => $"({X}, {Y})";
}

// 2x2 Matrix-Struktur für Rotation
public struct Matrix2x2
{
    public double M11, M12, M21, M22;

    public Matrix2x2(double m11, double m12, double m21, double m22)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
    }

    // Multiplikation der Matrix mit einem Vektor
    public Vector2D Multiply(Vector2D v)
    {
        double x = M11 * v.X + M12 * v.Y;
        double y = M21 * v.X + M22 * v.Y;
        return new Vector2D(x, y);
    }
}