using AITrainer.Helpers;
using AITrainer.Models;
using AITrainer.Views;

using Microsoft.ML;
using Microsoft.ML.Data;

using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace AITrainer.ViewModels
{
    public class PredictPageViewModel : BindableBase
    {
        IRegionManager _regionManager;

        List<ResultProgressViewModel> resultProgressList = null;

        MLContext mlContext = null;
        ITransformer loadedModel = null;
        PredictionEngine<InMemoryImageData, ImagePrediction> predictionEngine = null;

        #region Properties
        private string _ModelFileName;
        public string ModelFileName
        {
            get { return _ModelFileName; }
            set { SetProperty(ref _ModelFileName, value); }
        }

        private string _FolderName;
        public string FolderName
        {
            get { return _FolderName; }
            set { SetProperty(ref _FolderName, value); }
        }

        private BitmapImage _BeforeImage;
        public BitmapImage BeforeImage
        {
            get { return _BeforeImage; }
            set { SetProperty(ref _BeforeImage, value); }
        }

        private string _ResultText;
        public string ResultText
        {
            get { return _ResultText; }
            set { SetProperty(ref _ResultText, value); }
        }

        private long _PridictTime;
        public long PridictTime
        {
            get { return _PridictTime; }
            set { SetProperty(ref _PridictTime, value); }
        }

        private ObservableCollection<TargetImage> _TargetImages;
        public ObservableCollection<TargetImage> TargetImages
        {
            get { return _TargetImages; }
            set { SetProperty(ref _TargetImages, value); }
        }

        private TargetImage _SelectedTargetImage;
        public TargetImage SelectedTargetImage
        {
            get { return _SelectedTargetImage; }
            set
            {
                SetProperty(ref _SelectedTargetImage, value);
                OnFileSelected();
            }
        }
        #endregion

        #region Commands
        private DelegateCommand _FolderSelect;
        private DelegateCommand _ModelFileSelect;

        public DelegateCommand FolderSelect => _FolderSelect ?? (_FolderSelect = new DelegateCommand(OnFolderSelect));
        public DelegateCommand ModelFileSelect => _ModelFileSelect ?? (_ModelFileSelect = new DelegateCommand(OnModelFileSelect));
        #endregion

        #region Constructor
        public PredictPageViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            resultProgressList = new List<ResultProgressViewModel>();
            TargetImages = new ObservableCollection<TargetImage>();
            mlContext = new MLContext(seed: 1);
        }
        #endregion

        #region private Method
        private void OnModelFileSelect()
        {
            try
            {
                ModelFileName = "";

                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "ML.NET 모델 파일 (*.zip)|*.zip";
                dialog.ShowDialog();

                ModelFileName = dialog.FileName;

                Debug.WriteLine($"Loading model from: {ModelFileName}");

                // Load the model
                loadedModel = mlContext.Model.Load(ModelFileName, out DataViewSchema modelInputSchema);

                // Create prediction engine to try a single prediction (input = ImageData, output = ImagePrediction)
                predictionEngine = mlContext.Model.CreatePredictionEngine<InMemoryImageData, ImagePrediction>(loadedModel);

                //private static List<string> GetSlotNames(DataViewSchema schema, string name)
                List<string> SlotNames = GetSlotNames(predictionEngine.OutputSchema, "Score");

                IRegion region = _regionManager.Regions["ResultRegion"];
                region.RemoveAll();

                resultProgressList.Clear();

                foreach (string slotname in SlotNames)
                {
                    ResultProgress rP = new ResultProgress();
                    (rP.DataContext as ResultProgressViewModel).Slotname = slotname;

                    region.Add(rP);
                    resultProgressList.Add(rP.DataContext as ResultProgressViewModel);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void OnFolderSelect()
        {
            try
            {
                SelectedTargetImage = null;
                TargetImages.Clear();

                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.ShowDialog();

                FolderName = dialog.SelectedPath;

                DirectoryInfo di = new DirectoryInfo(FolderName);

                foreach (FileInfo File in di.GetFiles())
                {
                    if ((File.Extension.ToLower().CompareTo(".png") == 0) || (File.Extension.ToLower().CompareTo(".jpg") == 0))
                    {
                        TargetImages.Add(new TargetImage(File.Name, File.FullName));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void OnFileSelected()
        {
            if (SelectedTargetImage != null)
            {
                if (loadedModel != null)
                {
                    Bitmap bitmap = new Bitmap(SelectedTargetImage.FullFileName);
                    BeforeImage = ConvertImage.BitmapToImageSource(bitmap);

                    byte[] imagebyte;
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(BeforeImage));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        imagebyte = ms.ToArray();
                    }

                    InMemoryImageData inMemoryImageData = new InMemoryImageData(
                        image: imagebyte,
                        //image: File.ReadAllBytes(SelectedTargetImage.FullFileName),
                        label: SelectedTargetImage.FileName,
                        imageFileName: SelectedTargetImage.FullFileName
                    );

                    // Measure #1 prediction execution time.
                    Stopwatch watch = Stopwatch.StartNew();

                    var prediction = predictionEngine.Predict(inMemoryImageData);

                    // Stop measuring time.
                    watch.Stop();
                    PridictTime = watch.ElapsedMilliseconds;

                    ResultText = prediction.PredictedLabel;

                    int i = 0;
                    foreach (float score in prediction.Score)
                    {
                        resultProgressList[i++].Percentage = score * 100;
                    }
                }
            }
            else
            {
                BeforeImage = null;
            }
        }

        private static List<string> GetSlotNames(DataViewSchema schema, string name)
        {
            var column = schema.GetColumnOrNull(name);

            var slotNames = new VBuffer<ReadOnlyMemory<char>>();
            column.Value.GetSlotNames(ref slotNames);
            var names = new string[slotNames.Length];
            var num = 0;
            foreach (var denseValue in slotNames.DenseValues())
            {
                names[num++] = denseValue.ToString();
            }

            return names.ToList();
        }

        #endregion
    }
}
