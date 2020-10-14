using AITrainer.Models;
using AITrainer.Helpers;
using AITrainer.Utils;

using Prism.Commands;
using Prism.Mvvm;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;

using Microsoft.ML;
using static Microsoft.ML.Transforms.ValueToKeyMappingEstimator;

namespace AITrainer.ViewModels
{
    public class TrainPageViewModel : BindableBase
    {
        MLContext mlContext = null;

        string OutputModelFilePath = null;

        #region Properties
        private string _OutputModelFileName;
        public string OutputModelFileName
        {
            get { return _OutputModelFileName; }
            set { 
                SetProperty(ref _OutputModelFileName, value);
                OutputModelFilePath = null;
                OutputModelFilePath = Path.Combine(Directory.GetCurrentDirectory(), OutputModelFileName);
            }
        }

        private string _ImagesetFolderPath;
        public string ImagesetFolderPath
        {
            get { return _ImagesetFolderPath; }
            set { 
                SetProperty(ref _ImagesetFolderPath, value);
                if (_ImagesetFolderPath != null)
                    EnStartTrain = true;
            }
        }

        private bool _EnStartTrain;
        public bool EnStartTrain
        {
            get { return _EnStartTrain; }
            set { SetProperty(ref _EnStartTrain, value); }
        }
        #endregion

        #region Commands
        private DelegateCommand _ImagesetFolderSelect;
        private DelegateCommand _StartTrain;

        public DelegateCommand ImagesetFolderSelect => _ImagesetFolderSelect ?? (_ImagesetFolderSelect = new DelegateCommand(OnImagesetFolderSelect));
        public DelegateCommand StartTrain => _StartTrain ?? (_StartTrain = new DelegateCommand(OnStartTrain));
        #endregion

        #region Constructor
        public TrainPageViewModel()
        {
            OutputModelFileName = "ExampleMLModel.zip";

            InitML();
        }
        #endregion

        #region Private Methods
        private void InitML()
        {
            mlContext = null;

            ImagesetFolderPath = null;
            EnStartTrain = false;

            mlContext = new MLContext(seed: 1);

            // Specify MLContext Filter to only show feedback log/traces about ImageClassification
            // This is not needed for feedback output if using the explicit MetricsCallback parameter
            mlContext.Log += FilterMLContextLog;
        }

        private void OnImagesetFolderSelect()
        {
            try
            {
                ImagesetFolderPath = null;

                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.ShowDialog();

                ImagesetFolderPath = dialog.SelectedPath;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void OnStartTrain()
        {
            EnStartTrain = false;

            Task task = new Task(TrainMain);
            task.Start();
        }

        private void TrainMain()
        {
            try
            {
                // 2. Load the initial full image-set into an IDataView and shuffle so it'll be better balanced
                IEnumerable<ImageData> images = LoadImagesFromDirectory(folder: ImagesetFolderPath, useFolderNameAsLabel: true);
                IDataView fullImagesDataset = mlContext.Data.LoadFromEnumerable(images);
                IDataView shuffledFullImageFilePathsDataset = mlContext.Data.ShuffleRows(fullImagesDataset);

                // 3. Load Images with in-memory type within the IDataView and Transform Labels to Keys (Categorical)
                IDataView shuffledFullImagesDataset = mlContext.Transforms.Conversion.
                        MapValueToKey(outputColumnName: "LabelAsKey", inputColumnName: "Label", keyOrdinality: KeyOrdinality.ByValue)
                    .Append(mlContext.Transforms.LoadRawImageBytes(
                                                    outputColumnName: "Image",
                                                    imageFolder: ImagesetFolderPath,
                                                    inputColumnName: "ImagePath"))
                    .Fit(shuffledFullImageFilePathsDataset)
                    .Transform(shuffledFullImageFilePathsDataset);

                // 4. Split the data 80:20 into train and test sets, train and evaluate.
                var trainTestData = mlContext.Data.TrainTestSplit(shuffledFullImagesDataset, testFraction: 0.2);
                IDataView trainDataView = trainTestData.TrainSet;
                IDataView testDataView = trainTestData.TestSet;

                // 5. Define the model's training pipeline using DNN default values
                //
                var pipeline = mlContext.MulticlassClassification.Trainers
                        .ImageClassification(featureColumnName: "Image",
                                             labelColumnName: "LabelAsKey",
                                             validationSet: testDataView)
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue(outputColumnName: "PredictedLabel",
                                                                          inputColumnName: "PredictedLabel"));

                // 5.1 (OPTIONAL) Define the model's training pipeline by using explicit hyper-parameters
                //
                //var options = new ImageClassificationTrainer.Options()
                //{
                //    FeatureColumnName = "Image",
                //    LabelColumnName = "LabelAsKey",
                //    // Just by changing/selecting InceptionV3/MobilenetV2/ResnetV250  
                //    // you can try a different DNN architecture (TensorFlow pre-trained model). 
                //    Arch = ImageClassificationTrainer.Architecture.MobilenetV2,
                //    Epoch = 50,       //100
                //    BatchSize = 10,
                //    LearningRate = 0.01f,
                //    MetricsCallback = (metrics) => Console.WriteLine(metrics),
                //    ValidationSet = testDataView
                //};

                //var pipeline = mlContext.MulticlassClassification.Trainers.ImageClassification(options)
                //        .Append(mlContext.Transforms.Conversion.MapKeyToValue(
                //            outputColumnName: "PredictedLabel",
                //            inputColumnName: "PredictedLabel"));

                // 6. Train/create the ML model
                Console.WriteLine("*** Training the image classification model with DNN Transfer Learning on top of the selected pre-trained model/architecture ***");

                // Measuring training time
                var watch = Stopwatch.StartNew();

                //Train
                ITransformer trainedModel = pipeline.Fit(trainDataView);

                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;

                Console.WriteLine($"Training with transfer learning took: {elapsedMs / 1000} seconds");

                // 7. Get the quality metrics (accuracy, etc.)
                EvaluateModel(mlContext, testDataView, trainedModel);

                // 8. Save the model to assets/outputs (You get ML.NET .zip model file and TensorFlow .pb model file)
                mlContext.Model.Save(trainedModel, trainDataView.Schema, OutputModelFilePath);
                Console.WriteLine($"Model saved to: {OutputModelFilePath}");

                // 9. Try a single prediction simulating an end-user app
                //TrySinglePrediction(imagesFolderPathForPredictions, mlContext, trainedModel);

                Console.WriteLine("**************");
                Console.WriteLine("**  Finish  **");
                Console.WriteLine("**************");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                EnStartTrain = true;
                InitML();
            }
        }

        private void EvaluateModel(MLContext mlContext, IDataView testDataset, ITransformer trainedModel)
        {
            Console.WriteLine("Making predictions in bulk for evaluating model's quality...");

            // Measuring time
            var watch = Stopwatch.StartNew();

            var predictionsDataView = trainedModel.Transform(testDataset);

            var metrics = mlContext.MulticlassClassification.Evaluate(predictionsDataView, labelColumnName: "LabelAsKey", predictedLabelColumnName: "PredictedLabel");
            ConsoleHelper.PrintMultiClassClassificationMetrics("TensorFlow DNN Transfer Learning", metrics);

            watch.Stop();
            var elapsed2Ms = watch.ElapsedMilliseconds;

            Console.WriteLine($"Predicting and Evaluation took: {elapsed2Ms / 1000} seconds");
        }

        private void FilterMLContextLog(object sender, LoggingEventArgs e)
        {
            if (e.Message.StartsWith("[Source=ImageClassificationTrainer;"))
            {
                Console.WriteLine(e.Message);
            }
        }
        #endregion

        #region Public Methods
        public IEnumerable<ImageData> LoadImagesFromDirectory(
            string folder,
            bool useFolderNameAsLabel = true)
            => FileUtils.LoadImagesFromDirectory(folder, useFolderNameAsLabel)
                .Select(x => new ImageData(x.imagePath, x.label));
        #endregion

    }
}
