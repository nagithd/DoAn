using System.Windows.Controls;
using WpfApp3.ViewModels;

namespace WpfApp3.Controls;

public partial class VisionTriggerView : UserControl, IDisposable
{
    private readonly VisionTriggerViewModel _viewModel;

    public VisionTriggerViewModel ViewModel => _viewModel;

    public VisionTriggerView()
    {
        InitializeComponent();
        _viewModel = new VisionTriggerViewModel();
        DataContext = _viewModel;
    }

    public void Dispose() => _viewModel.Dispose();
}
