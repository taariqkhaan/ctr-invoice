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
        
        private string _ntpPath;
        private string _ctrPath;
        private string _invoicePath;

        private bool _generateCtr;
        private bool _testCheckBox;
        private bool _cableDetails;
        private bool _isRotateVerticalDrawings;
        private bool _isRevertVerticalDrawings;
        private bool _isNoRotationDrawings = true;
        
        private readonly PdfTextService _pdfTextService;
        private string documentType;
        private List<int> verticalPages;
        private Stopwatch stopwatch;
        private string _statusMessage;
        private bool qualityChecked = false;
        private bool customException = false;
        string dbPath = Path.Combine(Path.GetTempPath(), "data.db");
        
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }
        public string ntpPath
        {
            get => _ntpPath;
            set { _ntpPath = value; OnPropertyChanged(nameof(ntpPath)); }
        }
        public string ctrPath
        {
            get => _ctrPath;
            set { _ctrPath = value; OnPropertyChanged(nameof(ctrPath)); }
        }
        public string invoicePath
        {
            get => _invoicePath;
            set { _invoicePath = value; OnPropertyChanged(nameof(invoicePath)); }
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
        public bool GenerateCtr
        {
            get => _generateCtr;
            set
            {
                _generateCtr = value;
                OnPropertyChanged(nameof(GenerateCtr));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool TestCheckBox
        {
            get => _testCheckBox;
            set
            {
                _testCheckBox = value;
                OnPropertyChanged(nameof(TestCheckBox));
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

        public ICommand BrowseNtpCommand { get; }
        public ICommand BrowseCtrCommand { get; }
        public ICommand BrowseInvoiceCommand { get; }
        public ICommand ProcessCommand { get; }

        public MainWindowViewModel()
        {
            BrowseNtpCommand = new RelayCommand(BrowseNtp);
            BrowseCtrCommand = new RelayCommand(BrowseCtr);
            BrowseInvoiceCommand = new RelayCommand(BrowseInvoice);
            ProcessCommand = new RelayCommand(async () => await Process(), 
                () => IsEnabled && (GenerateCtr || TestCheckBox));
        }

        private void BrowseNtp()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select Duke NTP spreadsheet";
                openFileDialog.Filter = "Excel Files (*.xlsx, *.xls)|*.xlsx;*.xls";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ntpPath = openFileDialog.FileName;
                }
            }
        }
        private void BrowseCtr()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select Duke CTR spreadsheet";
                openFileDialog.Filter = "Excel Files (*.xlsx, *.xls)|*.xlsx;*.xls";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ctrPath = openFileDialog.FileName;
                }
            }
        }
        private void BrowseInvoice()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select BMCD draft invoice PDF";
                openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    invoicePath = openFileDialog.FileName;
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
            
            if (string.IsNullOrEmpty(ntpPath))
            {
                // StatusMessage = "Please Duke NTP spreadsheet.";
                // IsEnabled = true;
                // return;
            }
            if (string.IsNullOrEmpty(ctrPath))
            {
                // StatusMessage = "Please Duke CTR spreadsheet.";
                // IsEnabled = true;
                // return;
            }
            if (string.IsNullOrEmpty(invoicePath))
            {
                // StatusMessage = "Please select BMCD draft invoice PDF.";
                // IsEnabled = true;
                // return;
            }
            
            //-------------------------------------------------Generate CTR---------------------------------------------
            if (GenerateCtr)
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
                
                
                //-----------------------------------Extract text from BMCD Draft Invoice-------------------------------
                StatusMessage = "Processing BMCD invoice...";
                documentType = "INVOICE";
                var result = await Task.Run(() =>
                {
                    PdfTextService pdfTextService = new PdfTextService();
                    return pdfTextService.ExtractTextAndCoordinates(invoicePath, documentType);
                });
                List<PdfTextModel> extractedInvoiceData = result.ExtractedText;
                
                // Save text to database
                ExportService exportService = new ExportService();
                await exportService.SaveToDatabase(extractedInvoiceData, dbPath, documentType);
                //exportService.SaveToCsv(extractedInvoiceData, invoicePath);

                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    BmcdInvoiceService bmcdInvoiceService = new BmcdInvoiceService();
                    bmcdInvoiceService.ProcessDatabase(dbPath);
                });
                //------------------------------------------------------------------------------------------------------
                
                
                
                //-------------------------------Generate CTR-----------------------------------------------------------
                DukeCtrService dukeCtrService = new DukeCtrService();
                dukeCtrService.GenerateCtr(ctrPath, dbPath);
                //------------------------------------------------------------------------------------------------------
                
                
                
                //-------------------------------highlight region in a pdf----------------------------------------------
                // RegionHighlightService regionHighlightService = new RegionHighlightService();
                // regionHighlightService.HighlightRegion(invoicePath, "INVOICE");
                //------------------------------------------------------------------------------------------------------
                
                
               // //---------------------------------------Extract text from title block-----------------------------------
               // StatusMessage = "Processing drawings...";
               // documentType = "TITLE";
               //  result = await Task.Run(() =>
               //  {
               //      PdfTextService pdfTextService = new PdfTextService();
               //      return pdfTextService.ExtractTextAndCoordinates(ctrPath, documentType);
               //  });
               //  List<PdfTextModel> extractedTitleData = result.ExtractedText;
               //  
               //  
               //  documentType = "DWG";
               //  // Save text to database
               //  exportService = new ExportService();
               //  await exportService.SaveToDatabase(extractedTitleData, dbPath, documentType);
               //  // exportService.SaveToCsv(extractedTitleData, Path.Combine(Path.GetDirectoryName(DrawingsPath), 
               //  //     Path.GetFileNameWithoutExtension(DrawingsPath) + ".csv"));
               //  
               //  // Add tags to relevant texts
               //  await Task.Run(() =>
               //  {
               //      DwgTitleService dwgTitleService = new DwgTitleService();
               //      dwgTitleService.ProcessDatabase(dbPath);
               //  });
               //  //------------------------------------------------------------------------------------------------------
               //  
               //  
               //  
               //  
               //  
               //  
               //   //---------------------------------------Extract text from drawing area--------------------------------
               //   result = await Task.Run(() =>
               //   {
               //       PdfTextService pdfTextService = new PdfTextService();
               //       return pdfTextService.ExtractTextAndCoordinates(ctrPath, documentType);
               //   });
               //   List<PdfTextModel> extractedDwgData = result.ExtractedText;
               //   verticalPages = result.VerticalPages;
               //  
               //   // Save text to database
               //   exportService = new ExportService();
               //   await exportService.SaveToDatabase(extractedDwgData, dbPath, documentType);
               //   // exportService.SaveToCsv(extractedBowData, Path.Combine(Path.GetDirectoryName(BowPath), 
               //   //     Path.GetFileNameWithoutExtension(BowPath) + ".csv"));
               //  
               //   // Add tags to relevant texts
               //   await Task.Run(() =>
               //   {
               //       DrawingService drawingService = new DrawingService();
               //       drawingService.ProcessDatabase(dbPath);
               //   });
               //   //-----------------------------------------------------------------------------------------------------
               //   
               //   
               //   
               //   //----------------------------Compare cable schedule to drawings---------------------------------------
               //   StatusMessage = "Comparing cable schedule to drawings...";
               //   ComparisonLogic comparisonLogic = new ComparisonLogic();
               //   comparisonLogic.CompareDatabase(dbPath);
               //   //-----------------------------------------------------------------------------------------------------
               //   
               //   
               //   
               //   //--------------------------Annotate DWG and BOW-------------------------------------------------------
               //   string outputBowPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ntpPath), 
               //       $"highlighted_BOW.pdf");
               //   string outputDwgPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ctrPath), 
               //       $"highlighted_DWG.pdf");
               //
               //   try
               //   {
               //       // Check "highlighted_BOW.pdf"
               //       if (File.Exists(outputBowPath))
               //       {
               //           // If it exists, ensure it’s not open
               //           FileIsAccessible(outputBowPath);
               //           Console.WriteLine("BOW PDF exists and is accessible.");
               //       }
               //       // Check "highlighted_DWG.pdf"
               //       if (File.Exists(outputDwgPath))
               //       {
               //           // If it exists, ensure it’s not open
               //           FileIsAccessible(outputDwgPath);
               //           Console.WriteLine("DWG PDF exists and is accessible.");
               //       }
               //       
               //       AnnotationService annotationService = new AnnotationService();
               //       annotationService.AnnotatePdf(ntpPath, dbPath,"BOW");
               //       annotationService.AnnotatePdf(ctrPath, dbPath, "DWG");
               //   }
               //   catch (IOException)
               //   {
               //       StatusMessage = "Highlighted PDFs are open! Close PDFs before continuing.";
               //       File.Delete(dbPath);
               //       IsEnabled = true;
               //       customException = false;
               //       return;
               //   }
               //   //------------------------------------------------------------------------------------------------------
               //   
               //   
               //   
               //   //----------------------------Create hyperlinks--------------------------------------------------------
               //   StatusMessage = "Creating hyperlinks...";
               //   HyperlinkService hyperlinkService = new HyperlinkService();
               //   hyperlinkService.HyperlinkMain(dbPath, ntpPath);
               //   //-----------------------------------------------------------------------------------------------------
               //   
               //   
               //
               //   //-----------------------Add keymarks to the cable schedule----------------------------------------
               //   CableDetailsService cableDetailsService = new CableDetailsService();
               //   cableDetailsService.ProcessDatabase(dbPath, ntpPath);
               //   //-----------------------------------------------------------------------------------------------------
               //   
               //   
               //   
               //   //-----------------------Rotation of vertical drawings--------------------------------------------------
               //   if (IsRotateVerticalDrawings)
               //   {
               //       StatusMessage = "Rotating vertical drawings...";
               //       PdfRotationService pdfRotationService = new PdfRotationService();
               //       pdfRotationService.RotatePdfPages(ctrPath, verticalPages);
               //       customException = false;
               //   }
               //   if (IsRevertVerticalDrawings)
               //   {
               //       StatusMessage = "Reverting vertical drawings rotation...";
               //       PdfRotationService pdfRotationService = new PdfRotationService();
               //       pdfRotationService.RevertRotations(ctrPath);
               //       customException = false;
               //   }
               //   if (IsNoRotationDrawings)
               //   {
               //       StatusMessage = "Skipping vertical drawings rotation...";
               //       Console.WriteLine($"Vertical pages rotation skipped");
               //       customException = false;
               //   }
               //   //-----------------------------------------------------------------------------------------------------
               //   
                 
                qualityChecked = true;
                customException = false;
            }
            //----------------------------------------------------------------------------------------------------------
            
            
            
            //-----------------------Testing----------------------------------------------------------------------------
            if (TestCheckBox)
            {
                
            }
            //----------------------------------------------------------------------------------------------------------
            
            
            
            if (!customException)
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
