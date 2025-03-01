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


using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Data;
using CtrInvoice.Models;
using CtrInvoice.Services;


namespace CtrInvoice.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        
        private string _bowPath;
        private string _drawingsPath;

        private bool _qualityCheck;
        private bool _cableSummary;
        private bool _cableDetails;
        private bool _test1;
        private bool _isRotateVerticalDrawings;
        private bool _isRevertVerticalDrawings;
        private bool _isNoRotationDrawings = true;
        
        private readonly PdfTextService _pdfTextService;
        private string documentType;
        private List<int> verticalPages;
        private Stopwatch stopwatch;
        private string _statusMessage;
        private bool qualityChecked = false;
        private bool definedException = false;
        string dbPath = Path.Combine(Path.GetTempPath(), "data.db");
        
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }
        public string BowPath
        {
            get => _bowPath;
            set { _bowPath = value; OnPropertyChanged(nameof(BowPath)); }
        }
        public string DrawingsPath
        {
            get => _drawingsPath;
            set { _drawingsPath = value; OnPropertyChanged(nameof(DrawingsPath)); }
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
        public bool QualityCheck
        {
            get => _qualityCheck;
            set
            {
                _qualityCheck = value;
                OnPropertyChanged(nameof(QualityCheck));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool CableSummary
        {
            get => _cableSummary;
            set
            {
                _cableSummary = value;
                OnPropertyChanged(nameof(CableSummary));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool IsRotateVerticalDrawings
        {
            get => _isRotateVerticalDrawings;
            set
            {
                _isRotateVerticalDrawings = value;
                if (value)
                {
                    IsRevertVerticalDrawings = false;
                    IsNoRotationDrawings = false;
                }
                OnPropertyChanged(nameof(IsRotateVerticalDrawings));
            }
        }
        public bool IsRevertVerticalDrawings
        {
            get => _isRevertVerticalDrawings;
            set
            {
                _isRevertVerticalDrawings = value;
                if (value)
                {
                    IsRotateVerticalDrawings = false;
                    IsNoRotationDrawings = false;
                }
                OnPropertyChanged(nameof(IsRevertVerticalDrawings));
            }
        }
        public bool IsNoRotationDrawings
        {
            get => _isNoRotationDrawings;
            set
            {
                _isNoRotationDrawings = value;
                if (value)
                {
                    IsRotateVerticalDrawings = false;
                    IsRevertVerticalDrawings = false;
                }
                OnPropertyChanged(nameof(IsNoRotationDrawings));
            }
        }

        public ICommand BrowseBowCommand { get; }
        public ICommand BrowseDrawingsCommand { get; }
        public ICommand ProcessCommand { get; }

        public MainWindowViewModel()
        {
            BrowseBowCommand = new RelayCommand(BrowseBow);
            BrowseDrawingsCommand = new RelayCommand(BrowseDrawings);
            ProcessCommand = new RelayCommand(async () => await Process(), 
                () => IsEnabled && (QualityCheck || CableSummary));
        }

        private void BrowseBow()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a bill of wire PDF binder";
                openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    BowPath = openFileDialog.FileName;
                }
            }
        }
        
        private void BrowseDrawings()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a Drawings PDF binder";
                openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    DrawingsPath = openFileDialog.FileName;
                }
            }
        }
        
        private void FileIsAccessible(string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        
        private async Task Process()
        {
            IsEnabled = false;
            StatusMessage = "Processing started...";
            
            if (string.IsNullOrEmpty(BowPath))
            {
                StatusMessage = "Please select Bill of Wire PDF.";
                IsEnabled = true;
                return;
            }
            if (string.IsNullOrEmpty(DrawingsPath))
            {
                StatusMessage = "Please select Drawings PDF.";
                IsEnabled = true;
                return;
            }
            
            //-----------------------Check quality----------------------------------------------------------------------
            if (QualityCheck)
            {
                //------------------------------------Delete previously generated database file-------------------------
                if (File.Exists(dbPath))
                {
                    try
                    {
                        File.Delete(dbPath);
                        Console.WriteLine("Temp database file deleted.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete temp file: {ex.Message}");
                    }
                }
                //------------------------------------------------------------------------------------------------------
                
                
                
                //---------------------------------------Extract text from cable schedule-------------------------------
                StatusMessage = "Processing cable schedule...";
                documentType = "BOW";
                var result = await Task.Run(() =>
                {
                    PdfTextService pdfTextService = new PdfTextService();
                    return pdfTextService.ExtractTextAndCoordinates(BowPath, documentType);
                });
                List<PdfTextModel> extractedBowData = result.ExtractedText;
                
                // Save text to database
                ExportService exportService = new ExportService();
                await exportService.SaveToDatabase(extractedBowData, dbPath, documentType);
                // exportService.SaveToCsv(extractedBowData, Path.Combine(Path.GetDirectoryName(BowPath), 
                //     Path.GetFileNameWithoutExtension(BowPath) + ".csv"));

                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    CableScheduleService cableScheduleService = new CableScheduleService();
                    cableScheduleService.ProcessDatabase(dbPath);
                });
                //------------------------------------------------------------------------------------------------------
                
                
                
               //---------------------------------------Extract text from title block-----------------------------------
               StatusMessage = "Processing drawings...";
               documentType = "TITLE";
                result = await Task.Run(() =>
                {
                    PdfTextService pdfTextService = new PdfTextService();
                    return pdfTextService.ExtractTextAndCoordinates(DrawingsPath, documentType);
                });
                List<PdfTextModel> extractedTitleData = result.ExtractedText;
                
                
                documentType = "DWG";
                // Save text to database
                exportService = new ExportService();
                await exportService.SaveToDatabase(extractedTitleData, dbPath, documentType);
                // exportService.SaveToCsv(extractedTitleData, Path.Combine(Path.GetDirectoryName(DrawingsPath), 
                //     Path.GetFileNameWithoutExtension(DrawingsPath) + ".csv"));
                
                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    DwgTitleService dwgTitleService = new DwgTitleService();
                    dwgTitleService.ProcessDatabase(dbPath);
                });
                //------------------------------------------------------------------------------------------------------
                
                
                
                //-------------------------------highlight region in a pdf----------------------------------------------
                // RegionHighlightService regionHighlightService = new RegionHighlightService();
                // regionHighlightService.HighlightRegion(DrawingsPath, "TITLE");
                //------------------------------------------------------------------------------------------------------
                
                
                
                 //---------------------------------------Extract text from drawing area--------------------------------
                 result = await Task.Run(() =>
                 {
                     PdfTextService pdfTextService = new PdfTextService();
                     return pdfTextService.ExtractTextAndCoordinates(DrawingsPath, documentType);
                 });
                 List<PdfTextModel> extractedDwgData = result.ExtractedText;
                 verticalPages = result.VerticalPages;
                
                 // Save text to database
                 exportService = new ExportService();
                 await exportService.SaveToDatabase(extractedDwgData, dbPath, documentType);
                 // exportService.SaveToCsv(extractedBowData, Path.Combine(Path.GetDirectoryName(BowPath), 
                 //     Path.GetFileNameWithoutExtension(BowPath) + ".csv"));
                
                 // Add tags to relevant texts
                 await Task.Run(() =>
                 {
                     DrawingService drawingService = new DrawingService();
                     drawingService.ProcessDatabase(dbPath);
                 });
                 //-----------------------------------------------------------------------------------------------------
                 
                 
                 
                 //----------------------------Compare cable schedule to drawings---------------------------------------
                 StatusMessage = "Comparing cable schedule to drawings...";
                 ComparisonLogic comparisonLogic = new ComparisonLogic();
                 comparisonLogic.CompareDatabase(dbPath);
                 //-----------------------------------------------------------------------------------------------------
                 
                 
                 
                 //--------------------------Annotate DWG and BOW-------------------------------------------------------
                 string outputBowPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BowPath), 
                     $"highlighted_BOW.pdf");
                 string outputDwgPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(DrawingsPath), 
                     $"highlighted_DWG.pdf");

                 try
                 {
                     // Check "highlighted_BOW.pdf"
                     if (File.Exists(outputBowPath))
                     {
                         // If it exists, ensure it’s not open
                         FileIsAccessible(outputBowPath);
                         Console.WriteLine("BOW PDF exists and is accessible.");
                     }
                     // Check "highlighted_DWG.pdf"
                     if (File.Exists(outputDwgPath))
                     {
                         // If it exists, ensure it’s not open
                         FileIsAccessible(outputDwgPath);
                         Console.WriteLine("DWG PDF exists and is accessible.");
                     }
                     
                     AnnotationService annotationService = new AnnotationService();
                     annotationService.AnnotatePdf(BowPath, dbPath,"BOW");
                     annotationService.AnnotatePdf(DrawingsPath, dbPath, "DWG");
                 }
                 catch (IOException)
                 {
                     StatusMessage = "Highlighted PDFs are open! Close PDFs before continuing.";
                     File.Delete(dbPath);
                     IsEnabled = true;
                     definedException = false;
                     return;
                 }
                 //------------------------------------------------------------------------------------------------------
                 
                 
                 
                 //----------------------------Create hyperlinks--------------------------------------------------------
                 StatusMessage = "Creating hyperlinks...";
                 HyperlinkService hyperlinkService = new HyperlinkService();
                 hyperlinkService.HyperlinkMain(dbPath, BowPath);
                 //-----------------------------------------------------------------------------------------------------
                 
                 
            
                 //-----------------------Add keymarks to the cable schedule----------------------------------------
                 CableDetailsService cableDetailsService = new CableDetailsService();
                 cableDetailsService.ProcessDatabase(dbPath, BowPath);
                 //-----------------------------------------------------------------------------------------------------
                 
                 
                 
                 //-----------------------Rotation of vertical drawings--------------------------------------------------
                 if (IsRotateVerticalDrawings)
                 {
                     StatusMessage = "Rotating vertical drawings...";
                     PdfRotationService pdfRotationService = new PdfRotationService();
                     pdfRotationService.RotatePdfPages(DrawingsPath, verticalPages);
                     definedException = false;
                 }
                 if (IsRevertVerticalDrawings)
                 {
                     StatusMessage = "Reverting vertical drawings rotation...";
                     PdfRotationService pdfRotationService = new PdfRotationService();
                     pdfRotationService.RevertRotations(DrawingsPath);
                     definedException = false;
                 }
                 if (IsNoRotationDrawings)
                 {
                     StatusMessage = "Skipping vertical drawings rotation...";
                     Console.WriteLine($"Vertical pages rotation skipped");
                     definedException = false;
                 }
                 //-----------------------------------------------------------------------------------------------------
                 
                 
                qualityChecked = true;
                definedException = false;
            }
            //----------------------------------------------------------------------------------------------------------
            
            
            
            //-----------------------Generate Cable summary-------------------------------------------------------------
            if (CableSummary)
            {
                if (qualityChecked && File.Exists(dbPath))
                {
              
                    CableSummaryService  cableSummaryService = new  CableSummaryService();
                    cableSummaryService.GenerateCableSummaryCsv(dbPath, BowPath);
                    definedException = false;
                }
                else
                {
                    StatusMessage = "Run quality check !!";
                    definedException = true;
                }
            }
            //----------------------------------------------------------------------------------------------------------
            
            
            
            if (!definedException)
            {
                StatusMessage = "Processing success!";
            }
            IsEnabled = true;
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
    public class RelayCommand : ICommand
    {
        
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;
        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        
        public void RaiseCanExecuteChanged() 
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}
