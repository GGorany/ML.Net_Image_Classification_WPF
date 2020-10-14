using AITrainer.Views;

using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace AITrainer.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;

        #region Properties
        private string _title = "Deep Learning Image Classification for AI Vision - Last Update : 2020.10.14";
        public string Title {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }
        #endregion

        #region Commands
        public DelegateCommand<string> NavigateCommand { get; private set; }
        #endregion

        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            NavigateCommand = new DelegateCommand<string>(Navigate);

            _regionManager.RegisterViewWithRegion("MainPageRegion", typeof(TrainPage));
            _regionManager.RegisterViewWithRegion("MainPageRegion", typeof(PredictPage));
        }

        #region private Method
        private void Navigate(string navigatePath)
        {
            if (navigatePath != null)
            {
                _regionManager.RequestNavigate("MainPageRegion", navigatePath);
            }
        }
        #endregion

    }
}
