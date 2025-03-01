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


using CtrInvoice.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Util;

namespace CtrInvoice.Services
{
    public class PdfTextService
    {
        private double topRightX = 0;
        private double topRightY = 0;
        private double bottomLeftX = 0;
        private double bottomLeftY = 0;
        private double dateY1 = 0;
        private string wordTag = "NA";
        private int itemNumber = 0;
        private int colorFlag = 0;
        private bool dateFoundOnPage = false;
        List<int> VerticalPageList = new List<int>();
        
        
        private readonly PdfRegionService _regionService;
        public PdfTextService()
        {
            _regionService = new PdfRegionService();
        }
        public PdfExtractionResult ExtractTextAndCoordinates(string pdfPath, string searchRegionType)
        {
            List<PdfTextModel> extractedText = new List<PdfTextModel>();
            VerticalPageList.Clear();

            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                int pageNumber = 1;
                
                foreach (Page page in document.GetPages())
                {
                    double pageWidth = page.Width;
                    double pageHeight = page.Height;
                    int pageRotation = page.Rotation.Value;
                    
                    extractedText.AddRange(ExtractTextFromPage(page, pageRotation, pageWidth, pageHeight, pageNumber, searchRegionType));
                    pageNumber++;
                }
            }
            Console.WriteLine($"{searchRegionType} texts extracted.");
            return new PdfExtractionResult
            {
                ExtractedText = extractedText,
                VerticalPages = VerticalPageList
            };
        }
        private List<PdfTextModel> ExtractTextFromPage(Page page, int pageRotation, double pageWidth, 
            double pageHeight, int pageNumber, string searchRegionType)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>();
            List<Word> words = null;

            if (searchRegionType == "TITLE")
            {
                words = page.GetWords(DefaultWordExtractor.Instance).ToList();
            }
            else
            {
                words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
            }
            
            double lowestBottomLeftX = double.MaxValue;
            double lowestBottomLeftY = double.MaxValue;
            double highestTopRightX = double.MinValue;
            double highestTopRightY = double.MinValue;
           
            int zeroRotation = 0;
            int ninetyRotation = 0;
            int oneEightyRotation = 0;
            int twoSeventyRotation = 0;
            
            foreach (Word word in words)
            {
                if (!string.IsNullOrWhiteSpace(word.Text))
                {
                    var firstChar = word.Letters.First();
                    var lastChar = word.Letters.Last();
                    

                    bottomLeftX = firstChar.GlyphRectangle.BottomLeft.X;
                    bottomLeftY = firstChar.GlyphRectangle.BottomLeft.Y;
                    topRightX = lastChar.GlyphRectangle.TopRight.X;
                    topRightY = lastChar.GlyphRectangle.TopRight.Y;

                    lowestBottomLeftX = Math.Min(lowestBottomLeftX, bottomLeftX);
                    lowestBottomLeftY = Math.Min(lowestBottomLeftY, bottomLeftY);
                    highestTopRightX = Math.Max(highestTopRightX, topRightX);
                    highestTopRightY = Math.Max(highestTopRightY, topRightY);
                }
            }

            // Check if the word falls within the search region
            PdfRectangle searchRegion = _regionService.GetRegionByName(searchRegionType, pageWidth, pageHeight, 
                pageRotation, lowestBottomLeftX,lowestBottomLeftY, highestTopRightX, highestTopRightY);
            
            foreach (Word word in words)
            {
                if (!string.IsNullOrWhiteSpace(word.Text))
                {
                    var firstChar = word.Letters.First();
                    var lastChar = word.Letters.Last();
                    
                    bottomLeftX = firstChar.GlyphRectangle.BottomLeft.X;
                    bottomLeftY = firstChar.GlyphRectangle.BottomLeft.Y;
                    topRightX = lastChar.GlyphRectangle.TopRight.X;
                    topRightY = lastChar.GlyphRectangle.TopRight.Y;
                    
                    // Check word rotation
                    int wordRotation = DetermineWordRotation(word, bottomLeftX, bottomLeftY, topRightX, topRightY);
                    
                    // Check if the word falls within the search region
                    if (!IsWithinRegion(bottomLeftX, bottomLeftY, topRightX, topRightY, searchRegion))
                        continue;
                    
                    if (wordRotation == 0)
                    {
                        zeroRotation++;
                    }
                    if (wordRotation == 90)
                    {
                        ninetyRotation++;
                    }
                    if (wordRotation == 180)
                    {
                        oneEightyRotation++;
                    }
                    if (wordRotation == 270)
                    {
                        twoSeventyRotation++;
                    }
                    
                    textModels.Add(new PdfTextModel(
                        word.Text,
                        bottomLeftX,
                        bottomLeftY,
                        topRightX,
                        topRightY,
                        pageNumber,
                        pageRotation,
                        wordRotation,
                        wordTag,
                        itemNumber,
                        colorFlag
                    ));
                    
                }
            }

            if (zeroRotation - twoSeventyRotation < 20)
            {
                VerticalPageList.Add(pageNumber);
            }
            return textModels;
        }
        
        private int DetermineWordRotation(Word word, double bottomLeftX, double bottomLeftY, double topRightX, double topRightY)
        {
            double xDiff = bottomLeftX - topRightX;
            double yDiff = bottomLeftY - topRightY;
            double xDiffAbs = Math.Abs(xDiff);
            double yDiffAbs = Math.Abs(yDiff);

            if (word.Text.Length > 1)
            {
                if (xDiffAbs > yDiffAbs)
                {
                    if (xDiff < 0)
                    {
                        return 0;
                    }
                    if (xDiff > 0)
                    {
                        return 180;
                    }

                }
                else
                {
                    if (yDiff < 0)
                    {
                        return 270;
                    }
                    if (yDiff > 0)
                    {
                        return 90;
                    }
                }

            }
            else
            {
                if (xDiffAbs > yDiffAbs)
                {
                    if (yDiff < 0)
                    {
                        if (word.Letters.First().Value == "w"|| word.Letters.Last().Value == "w")
                        {
                            return 0;
                        }
                        return 270;
                    }
                    if (yDiff > 0)
                    {
                        if (word.Letters.First().Value == "w"|| word.Letters.Last().Value == "w")
                        {
                            return 180;
                        }
                        return 90;
                    }

                }
                else
                {
                    if (yDiff < 0)
                    {
                        if (word.Letters.First().Value == "w"|| word.Letters.Last().Value == "w")
                        {
                            return 270;
                        }
                        return 0;
                    }
                    if (yDiff > 0)
                    {
                        if (word.Letters.First().Value == "w"|| word.Letters.Last().Value == "w")
                        {
                            return 90;
                        }
                        return 180;
                    }
                }
            }
            return 0;
        }
        
        private bool IsWithinRegion(double wordX1, double wordY1, double wordX2, double wordY2, 
            PdfRectangle region)
        {
            return wordX1 >= region.Left && wordX2 <= region.Right &&
                   wordY1 >= region.Bottom && wordY2 <= region.Top;
        }
        
    }
    
    public class PdfExtractionResult
    {
        public List<PdfTextModel> ExtractedText { get; set; }
        public List<int> VerticalPages { get; set; }
    }
} 