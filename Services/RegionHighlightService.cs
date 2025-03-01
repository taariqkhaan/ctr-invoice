/*
 * [CtrInvoice]
 * Copyright (C) [2025] [Tariq Khan / Burns & McDonnell]
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */



using System.IO;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using UglyToad.PdfPig.Core;

namespace CtrInvoice.Services
{
    public class RegionHighlightService
    {
        private readonly PdfRegionService _regionService;
        
        public RegionHighlightService()
        {
            _regionService = new PdfRegionService();
        }

        public void HighlightRegion(string pdfPath, string regionName)
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("The specified PDF file does not exist.", pdfPath);
            }

            string outputPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(pdfPath)!, "region_highlighted.pdf");

            using (PdfReader reader = new PdfReader(pdfPath))
            using (PdfWriter writer = new PdfWriter(outputPath))
            using (PdfDocument pdfDoc = new PdfDocument(reader, writer))
            {
                int totalPages = pdfDoc.GetNumberOfPages();

                for (int pageIndex = 1; pageIndex <= totalPages; pageIndex++)
                {
                    PdfPage page = pdfDoc.GetPage(pageIndex);

                    Rectangle pageSize = page.GetCropBox(); // Or use page.GetMediaBox()
                    float pageWidth = pageSize.GetWidth();
                    float pageHeight = pageSize.GetHeight();
                    int pageRotation = page.GetRotation();

                    PdfRectangle uglyRect = _regionService.GetRegionByName(regionName, pageWidth, pageHeight,
                        pageRotation, 0, 0, pageWidth, pageHeight);

                    // Convert PdfPig PdfRectangle to iText Rectangle
                    Rectangle iTextRect = new Rectangle(
                        (float)uglyRect.Left, (float)uglyRect.Bottom,
                        (float)uglyRect.Width, (float)uglyRect.Height);

                    float[] colorComponents = new[] { 1f, 0.9f, 0f };
                    float opacity = 0.6f;

                    // Create a square annotation (highlight)
                    PdfSquareAnnotation annotation = new PdfSquareAnnotation(iTextRect);
                    annotation.SetColor(new DeviceRgb(colorComponents[0], colorComponents[1], colorComponents[2]));
                    annotation.SetInteriorColor(new PdfArray(colorComponents));
                    annotation.SetBorder(new PdfArray(new float[] { 0.1f, 0.1f, 0.1f }));
                    annotation.Put(PdfName.CA, new PdfNumber(opacity)); // Opacity (60%)

                    // Add annotation to the page
                    page.AddAnnotation(annotation);
                }

                pdfDoc.Close();
            }

            Console.WriteLine($"Highlighted PDF saved at: {outputPath}");
        }
    }
}
