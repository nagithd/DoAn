using System.Windows.Controls;
using WpfApp3.ViewModels;

namespace WpfApp3.Controls;

public partial class ConveyorControlView : UserControl, IDisposable
{
    private readonly ConveyorControlViewModel _viewModel;

    public ConveyorControlViewModel ViewModel => _viewModel;

    public ConveyorControlView()
    {
        InitializeComponent();
        _viewModel = new ConveyorControlViewModel();
        DataContext = _viewModel;
    }

    public void Dispose() => _viewModel.Dispose();
}
