using System;
using System.Collections.Generic;
using System.Text;

namespace AITrainer.Models
{
    public sealed class TargetImage
    {
        public string FileName { get; set; }
        public string FullFileName { get; set; }

        public TargetImage(string fileName, string fullFileName)
        {
            FileName = fileName;
            FullFileName = fullFileName;
        }
    }
}
