﻿//----------------------------------------------------------------------------
//  Copyright (C) 2004-2018 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Emgu.TF.Lite;
using Emgu.Models;
using System.IO;
using System.ComponentModel;
using System.Net;

namespace Emgu.TF.Lite.Models
{
    public class CocoSsdMobilenet : Emgu.TF.Util.UnmanagedObject
    {
        private FileDownloadManager _downloadManager;


        private Interpreter _interpreter = null;
        private String[] _labels = null;
        private FlatBufferModel _model = null;
        private Tensor _inputTensor;
        private Tensor[] _outputTensors;

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
        public double DownloadProgress
        {
            get
            {
                if (_downloadManager == null)
                    return 0;
                if (_downloadManager.CurrentWebClient == null)
                    return 1;
                return _downloadManager.CurrentWebClient.downloadProgress;
            }
        }

        public String DownloadFileName
        {
            get
            {
                if (_downloadManager == null)
                    return null;
                if (_downloadManager.CurrentWebClient == null)
                    return null;
                return _downloadManager.CurrentWebClient.url;
            }
        }
#endif

        public CocoSsdMobilenet()
        {
            _downloadManager = new FileDownloadManager();

            _downloadManager.OnDownloadProgressChanged += onDownloadProgressChanged;
            _downloadManager.OnDownloadCompleted += onDownloadCompleted;
                
        }

        private void onDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ImportGraph();
            if (OnDownloadCompleted != null)
            {
                OnDownloadCompleted(sender, e);
            }
        }

        private void onDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (OnDownloadProgressChanged != null)
                OnDownloadProgressChanged(sender, e);
        }

        public event System.Net.DownloadProgressChangedEventHandler OnDownloadProgressChanged;
        public event System.ComponentModel.AsyncCompletedEventHandler OnDownloadCompleted;


        public
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            IEnumerator
#else
            void
#endif
            Init(String[] modelFiles = null, String downloadUrl = null)
        {

            _downloadManager.Clear();
            String url = downloadUrl == null ? "https://github.com/emgucv/models/raw/master/coco_ssd_mobilenet_v1_1.0_quant_2018_06_29/" : downloadUrl;
            String[] fileNames = modelFiles == null ? new string[] { "detect.tflite", "labelmap.txt" } : modelFiles;
            for (int i = 0; i < fileNames.Length; i++)
                _downloadManager.AddFile(url + fileNames[i]);

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            yield return _downloadManager.Download();
#else
            _downloadManager.Download();
#endif
        }

        public bool Imported
        {
            get
            {
                return _interpreter != null;
            }
        }

        private void ImportGraph()
        {
            if (_interpreter != null)
                _interpreter.Dispose();

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            UnityEngine.Debug.Log("Reading model definition");
#endif

            String modelFileName = _downloadManager.Files[0].LocalFile;
            String labelFileName = _downloadManager.Files[1].LocalFile;

            System.Diagnostics.Debug.Assert(File.Exists(modelFileName), String.Format("File {0} doesn't exist", modelFileName));
            System.Diagnostics.Debug.Assert(File.Exists(labelFileName), String.Format("File {0} doesn't exist", labelFileName));

            if (!File.Exists(modelFileName) || !File.Exists(labelFileName))
                return;

            if (_labels == null)
                _labels = File.ReadAllLines(labelFileName);

            if (_model == null)
            {
                _model = new FlatBufferModel(modelFileName);
                if (!_model.CheckModelIdentifier())
                    throw new Exception("Model indentifier check failed");
            }

            if (_interpreter == null)
            {
                _interpreter = new Interpreter(_model);
                Status allocateTensorStatus = _interpreter.AllocateTensors();
                if (allocateTensorStatus == Status.Error)
                    throw new Exception("Failed to allocate tensor");
            }

            if (_inputTensor == null)
            {
                _inputTensor = _interpreter.Inputs[0];
            }

            if (_outputTensors == null)
            {
                _outputTensors = _interpreter.Outputs;
            }


        }

        
        public Interpreter Interpreter
        {
            get
            {
                return _interpreter;
            }
        }

        public String[] Labels
        {
            get { return _labels; }
        }

        public RecognitionResult[] Recognize(String imageFile)
        {
            //NativeImageIO.ReadImageFileToTensor<byte>(imageFile, _inputTensor.DataPointer, _inputTensor.Dims[1], _inputTensor.Dims[2], _inputTensor.QuantizationParams.ZeroPoint, _inputTensor.QuantizationParams.Scale);
            NativeImageIO.ReadImageFileToTensor<byte>(imageFile, _inputTensor.DataPointer, _inputTensor.Dims[1], _inputTensor.Dims[2], 0.0f, 1.0f);

            _interpreter.Invoke();

            float[,,] outputLocations = _interpreter.Outputs[0].JaggedData as float[,,];
            float[] classes = _interpreter.Outputs[1].Data as float[];
            float[] scores = _interpreter.Outputs[2].Data as float[];
            int numDetections = (int) Math.Round( (_interpreter.Outputs[3].Data as float[])[0]);

            // SSD Mobilenet V1 Model assumes class 0 is background class
            // in label file and class labels start from 1 to number_of_classes+1,
            // while outputClasses correspond to class index from 0 to number_of_classes
            List<RecognitionResult> results = new List<RecognitionResult>();
            for (int i = 0; i < numDetections; i++)
            {
                RecognitionResult r = new RecognitionResult();
                r.Class = (int)Math.Round(classes[i]);
                r.Label = _labels[r.Class];

            }

            return results.ToArray() ;
        }

        /*
        public RecognitionResult MostLikely(String imageFile)
        {
            float[] probability = Recognize(imageFile);

            RecognitionResult result = new RecognitionResult();
            result.Label = String.Empty;

            if (probability != null)
            {
                float maxVal = 0;
                int maxIdx = 0;
                for (int i = 0; i < probability.Length; i++)
                {
                    if (probability[i] > maxVal)
                    {
                        maxVal = probability[i];
                        maxIdx = i;
                    }
                }
                result.Label = _labels[maxIdx];
                result.Probability = maxVal;
            }
            return result;
        }*/

        public class RecognitionResult
        {
            /// <summary>
            /// Rectangles will be in the format of (x, y, width, height) in the original image corrdinate
            /// </summary>
            public float[][] Rectangles;
            /// <summary>
            /// The object label
            /// </summary>
            public String Label;
            /// <summary>
            /// The score of the matching
            /// </summary>
            public double Score;
            /// <summary>
            /// The class index
            /// </summary>
            public int Class;
        }

        protected override void DisposeObject()
        {
            
            if (_interpreter != null)
            {
                _interpreter.Dispose();
                _interpreter = null;
            }

            if (_model != null)
            {
                _model.Dispose();
                _model = null;
            }
        }
    }
}
