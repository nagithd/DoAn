using System.Windows;
using System.Windows.Input;
using WpfApp3.Models;

namespace WpfApp3.Dialogs;

public partial class AIDetectionResultDialog : Window
{
    public AIDetectionResultDialog(
        InspectionResult? result = null,
        string? aiServiceStatus = null,
        ICommand? checkAiServiceCommand = null)
    {
        InitializeComponent();
        ResultView.Result = result;
        ResultView.AiServiceStatus =
            aiServiceStatus ?? "AI service status unavailable";
        ResultView.CheckAiServiceCommand = checkAiServiceCommand;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        Close();
}
