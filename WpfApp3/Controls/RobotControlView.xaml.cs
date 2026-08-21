using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels;

namespace WpfApp3.Controls;

public partial class RobotControlView : UserControl, IDisposable
{
    private readonly RobotControlViewModel _viewModel;
    public RobotControlViewModel ViewModel => _viewModel;
    private Slider? _activeServoSlider;
    private readonly Dictionary<int, double> _textEditStartAngles = new();

    public RobotControlView()
    {
        InitializeComponent();
        _viewModel = new RobotControlViewModel();
        DataContext = _viewModel;
    }

    private void ServoSlider_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _activeServoSlider = sender as Slider;
        if (_activeServoSlider?.DataContext is ServoChannelViewModel servo)
            _viewModel.BeginServoInteraction(servo.ServoId);
    }

    private void ServoSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider slider ||
            !ReferenceEquals(slider, _activeServoSlider) ||
            slider.DataContext is not ServoChannelViewModel servo)
        {
            return;
        }

        _viewModel.QueueLiveServoMove(
            servo,
            e.NewValue);
    }

    private void ServoSlider_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Slider slider &&
            slider.DataContext is ServoChannelViewModel servo)
        {
            // Send the final value even if intermediate values were skipped.
            _viewModel.QueueLiveServoMove(
                servo,
                slider.Value);
            _viewModel.EndServoInteraction(servo.ServoId);
        }

        _activeServoSlider = null;
    }

    private void ServoSlider_KeyUp(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key is not (
            Key.Left or
            Key.Right or
            Key.Up or
            Key.Down or
            Key.PageUp or
            Key.PageDown or
            Key.Home or
            Key.End))
        {
            return;
        }

        if (sender is Slider slider &&
            slider.DataContext is ServoChannelViewModel servo)
        {
            _viewModel.QueueLiveServoMove(
                servo,
                slider.Value);
        }
    }

    private void ServoAngleTextBox_GotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox ||
            textBox.DataContext is not ServoChannelViewModel servo)
        {
            return;
        }

        _textEditStartAngles[servo.ServoId] = servo.TargetAngle;
        _viewModel.BeginServoInteraction(servo.ServoId);
        textBox.SelectAll();
    }

    private void ServoAngleTextBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
            return;

        CommitServoAngleTextBox(textBox, forceSubmit: true);
        e.Handled = true;
    }

    private void ServoAngleTextBox_LostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox ||
            textBox.DataContext is not ServoChannelViewModel servo)
        {
            return;
        }

        CommitServoAngleTextBox(textBox, forceSubmit: false);
        _viewModel.EndServoInteraction(servo.ServoId);
        _textEditStartAngles.Remove(servo.ServoId);
    }

    private void CommitServoAngleTextBox(
        TextBox textBox,
        bool forceSubmit)
    {
        if (textBox.DataContext is not ServoChannelViewModel servo)
            return;

        string normalized = textBox.Text.Trim().Replace(',', '.');
        bool isValid = double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double angle) &&
            double.IsFinite(angle) &&
            angle >= 0 &&
            angle <= 180;

        if (!isValid)
        {
            if (_textEditStartAngles.TryGetValue(
                    servo.ServoId,
                    out double originalAngle))
            {
                servo.TargetAngle = originalAngle;
            }

            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            textBox.ToolTip = "Servo angle must be between 0 and 180 degrees.";
            return;
        }

        double target = Math.Round(angle, 1);
        textBox.ToolTip = "Enter an angle from 0 to 180, then press Enter";

        bool changed =
            !_textEditStartAngles.TryGetValue(
                servo.ServoId,
                out double previousTarget) ||
            Math.Abs(previousTarget - target) >= 0.05;

        servo.TargetAngle = target;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();

        if (!changed && !forceSubmit)
            return;

        _textEditStartAngles[servo.ServoId] = target;
        _viewModel.SetServoCommand.Execute(servo);
    }

    public void Dispose() => _viewModel.Dispose();
}
