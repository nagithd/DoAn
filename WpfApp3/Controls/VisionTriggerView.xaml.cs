using System.Windows.Controls;
using System.Windows;
using WpfApp3.Dialogs;
using WpfApp3.ViewModels;

namespace WpfApp3.Controls;

public partial class VisionTriggerView : UserControl, IDisposable
{
    private readonly VisionTriggerViewModel _viewModel;
    private RobotDiagnosticWindow? _diagnosticWindow;

    public VisionTriggerViewModel ViewModel => _viewModel;

    public VisionTriggerView()
    {
        InitializeComponent();
        _viewModel = new VisionTriggerViewModel();
        DataContext = _viewModel;
    }

    private void OpenRobotDiagnostics_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_diagnosticWindow is { IsVisible: true })
        {
            _diagnosticWindow.Activate();
            return;
        }

        var window = new RobotDiagnosticWindow(
            _viewModel.RobotBaseUrl)
        {
            Owner = Window.GetWindow(this)
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_diagnosticWindow, window))
                _diagnosticWindow = null;
        };
        _diagnosticWindow = window;
        window.Show();
    }

    public void Dispose()
    {
        _diagnosticWindow?.Close();
        _diagnosticWindow = null;
        _viewModel.Dispose();
    }
}
