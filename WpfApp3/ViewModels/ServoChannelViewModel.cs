using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.ViewModels;

public partial class ServoChannelViewModel : ObservableObject
{
    public int ServoId { get; }
    public string Name { get; }

    [ObservableProperty]
    private double currentAngle;

    [ObservableProperty]
    private double targetAngle = 90;

    internal bool TargetInitialized { get; set; }

    public ServoChannelViewModel(int servoId, string name)
    {
        ServoId = servoId;
        Name = name;
    }
}
