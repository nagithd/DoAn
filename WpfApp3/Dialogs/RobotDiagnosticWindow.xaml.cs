using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels;

namespace WpfApp3.Dialogs;

public partial class RobotDiagnosticWindow : Window
{
    private readonly RobotDiagnosticViewModel _viewModel;
    private bool _initialized;
    private Slider? _activeSlider;

    public RobotDiagnosticWindow(string robotBaseUrl)
    {
        InitializeComponent();
        _viewModel = new RobotDiagnosticViewModel(robotBaseUrl);
        DataContext = _viewModel;
        Loaded += RobotDiagnosticWindow_Loaded;
        Closed += RobotDiagnosticWindow_Closed;
    }

    private async void RobotDiagnosticWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_initialized)
            return;

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ServoSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider slider ||
            !ReferenceEquals(_activeSlider, slider) ||
            slider.DataContext is not ServoChannelViewModel servo)
        {
            return;
        }

        _viewModel.QueueLiveServoMove(servo, e.NewValue);
    }

    private void ServoSlider_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Slider slider)
            _activeSlider = slider;
    }

    private async void ServoSlider_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Slider slider ||
            !ReferenceEquals(_activeSlider, slider))
        {
            return;
        }

        _activeSlider = null;
        await _viewModel.EndLiveServoInteractionAsync();
    }

    private async void ServoSlider_LostMouseCapture(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Slider slider ||
            !ReferenceEquals(_activeSlider, slider))
        {
            return;
        }

        _activeSlider = null;
        await _viewModel.EndLiveServoInteractionAsync();
    }

    private void RobotDiagnosticWindow_Closed(object? sender, EventArgs e)
    {
        Loaded -= RobotDiagnosticWindow_Loaded;
        Closed -= RobotDiagnosticWindow_Closed;
        _viewModel.Dispose();
    }
}
