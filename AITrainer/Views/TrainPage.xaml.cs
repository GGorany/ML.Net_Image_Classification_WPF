using AITrainer.Helpers;

using System;
using System.Windows.Controls;

namespace AITrainer.Views
{
    /// <summary>
    /// TrainPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TrainPage : UserControl
    {
        public TrainPage()
        {
            InitializeComponent();

            Console.SetOut(new ControlWriter(this.LogTextBox));
        }
    }
}
