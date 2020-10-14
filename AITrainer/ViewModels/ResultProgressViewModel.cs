using Prism.Mvvm;

namespace AITrainer.ViewModels
{
    public class ResultProgressViewModel : BindableBase
    {
        #region Properties
        private double _Percentage;
        public double Percentage
        {
            get { return _Percentage; }
            set { SetProperty(ref _Percentage, value); }
        }

        private string _Slotname;
        public string Slotname
        {
            get { return _Slotname; }
            set { SetProperty(ref _Slotname, value); }
        }
        #endregion

        public ResultProgressViewModel()
        {

        }
    }
}
