using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using OfficeOpenXml;               // EPPlus
using OfficeOpenXml.Drawing.Chart; // Für Diagramm-Typen
using OfficeOpenXml.Drawing;       // Für eFillStyle
using OfficeOpenXml.Style;

namespace KinectWASP
{
    public static class DataSaver
    {
        public static void SaveHandDataToExcelWithChart(
            bool isHandOpen,
            (int bLeft, int bRight, int bTop) boundingBox,
            double[] contourPixels,
            double[] averageContourPixels,
            List<(int Index, double Value)> maxima,  // Enthält Index und Maxima-Wert
            BitmapSource contourImage,
            BitmapSource handPixelImage
        )
        {
            // EPPlus ab Version 5 erfordert das Setzen des LicenseContext
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // 1) Ordner mit Zeitstempel anlegen
            string folderName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folderPath = Path.Combine("Recordings", folderName); 
            Console.WriteLine($" current = {Directory.GetCurrentDirectory()}\\{folderPath}");
            Directory.CreateDirectory(folderPath);

            // 2) Neues Excel-Package und Worksheet erstellen
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("HandData" + folderName);

                // 3) Metadaten (Timestamp, Handstatus, BoundingBox) oben in die Tabelle
                ws.Cells["A1"].Value = "Timestamp";
                ws.Cells["B1"].Value = "HandOpen";
                ws.Cells["C1"].Value = "bLeft";
                ws.Cells["D1"].Value = "bRight";
                ws.Cells["E1"].Value = "bTop";

                string timeString = DateTime.Now.ToString("o");
                ws.Cells["A2"].Value = timeString;
                ws.Cells["B2"].Value = isHandOpen ? "Open" : "Closed";
                ws.Cells["C2"].Value = boundingBox.bLeft;
                ws.Cells["D2"].Value = boundingBox.bRight;
                ws.Cells["E2"].Value = boundingBox.bTop;

                // 4) Konturdaten in Spalten schreiben (Index, Contour, AverageContour)
                ws.Cells["A4"].Value = "Index";
                ws.Cells["B4"].Value = "Contour";
                ws.Cells["C4"].Value = "AverageContour";

                int startRow = 5;
                for (int i = 0; i < contourPixels.Length; i++)
                {
                    int row = startRow + i;
                    ws.Cells[row, 1].Value = i;                     // Index
                    ws.Cells[row, 2].Value = contourPixels[i];       // Contour
                    ws.Cells[row, 3].Value = averageContourPixels[i]; // AverageContour
                }

                // 5) Lokale Maxima (nur als Übersicht) in Spalte E
                ws.Cells["E4"].Value = "LocalMaxima";
                if (maxima.Count > 0)
                {
                    for (int i = 0; i < maxima.Count; i++)
                    {
                        ws.Cells[startRow + i, 5].Value = $"({maxima[i].Index}, {maxima[i].Value})";
                    }
                }
                else
                {
                    ws.Cells[startRow, 5].Value = "None";
                }

                // 6) Diagramm erstellen: XYScatterLines für numerische X-Werte
                var chart = ws.Drawings.AddChart("ContourChart", eChartType.XYScatterLines);
                chart.Title.Text = "Contour vs. AverageContour";
                chart.SetPosition(1, 0, 7, 0);  // Position im Worksheet
                chart.SetSize(800, 400);       // Größe in Pixeln

                // Datenbereiche definieren:
                string xRange = ws.Cells[startRow, 1, startRow + contourPixels.Length - 1, 1].Address;
                string contourRange = ws.Cells[startRow, 2, startRow + contourPixels.Length - 1, 2].Address;
                string avgRange = ws.Cells[startRow, 3, startRow + averageContourPixels.Length - 1, 3].Address;

                // Serie 1: Contour
                var series1 = chart.Series.Add(contourRange, xRange);
                series1.Header = "Contour";

                // Serie 2: AverageContour
                var series2 = chart.Series.Add(avgRange, xRange);
                series2.Header = "AverageContour";

                // 7) Vertikale Linien für Maxima hinzufügen
                //    In Spalten F und G je 2 Zeilen: (Index, 0) und (Index, Value)
                int maximaDataRow = startRow;     // Startzeile für Maxima-Daten
                int maximaDataCol = 6;            // Spalte F = 6, G = 7
                ws.Cells["F4"].Value = "Maxima Lines (X=Index, Y=Value)";

                foreach (var (maxIndex, maxValue) in maxima)
                {
                    // Zeile 1: (maxIndex, 0)
                    ws.Cells[maximaDataRow, maximaDataCol].Value = maxIndex;
                    ws.Cells[maximaDataRow, maximaDataCol + 1].Value = 0;

                    // Zeile 2: (maxIndex, maxValue)
                    ws.Cells[maximaDataRow + 1, maximaDataCol].Value = maxIndex;
                    ws.Cells[maximaDataRow + 1, maximaDataCol + 1].Value = maxValue;

                    // Neue Datenreihe für diese zwei Punkte erstellen
                    string xRangeMax = ws.Cells[maximaDataRow, maximaDataCol,
                                                maximaDataRow + 1, maximaDataCol].Address;
                    string yRangeMax = ws.Cells[maximaDataRow, maximaDataCol + 1,
                                                maximaDataRow + 1, maximaDataCol + 1].Address;

                    var maxSeries = chart.Series.Add(yRangeMax, xRangeMax);
                    maxSeries.Header = $"Max at {maxIndex}";

                    // Statt Border verwenden wir nun Line, um den Linienstil festzulegen:
                    maxSeries.Border.Fill.Style = OfficeOpenXml.Drawing.eFillStyle.SolidFill;
                    maxSeries.Border.Fill.Color = System.Drawing.Color.Red;

                    // Zwei Zeilen weiter für das nächste Maximum
                    maximaDataRow += 2;
                }

                // 8) Excel-Datei speichern
                string excelPath = Path.Combine(folderPath, "handData.xlsx");
                package.SaveAs(new FileInfo(excelPath));
            }

            // 9) Konturbild (falls vorhanden) als PNG speichern
            if (contourImage != null)
            {
                string imagePath = Path.Combine(folderPath, "handContour.png");
                SaveBitmapSourceAsPng(contourImage, imagePath);
            }
            // 10) Konturbild (falls vorhanden) als PNG speichern
            if (handPixelImage != null)
            {
                string imagePath = Path.Combine(folderPath, "handPixelImage.png");
                SaveBitmapSourceAsPng(handPixelImage, imagePath);
            }
        }

        private static void SaveBitmapSourceAsPng(BitmapSource image, string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(stream);
            }
        }
    }
}
