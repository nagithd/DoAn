using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.ViewModels;

public partial class ServoChannelViewModel : ObservableObject
{
    public int ServoId { get; }
    public string Name { get; }
    public double MinimumAngle => 0;
    public double MaximumAngle => 180;

    [ObservableProperty]
    private double? currentAngle;

    [ObservableProperty]
    private double targetAngle = 90;

    public ServoChannelViewModel(int servoId, string name)
    {
        ServoId = servoId;
        Name = name;
    }
}
