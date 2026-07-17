using System.Windows.Controls;
using WpfApp3.ViewModels;

namespace WpfApp3.Controls
{
    /// <summary>
    /// Interaction logic for CameraView.xaml
    /// Display control for the live camera feed.
    /// Camera logic is managed by CameraViewModel.
    /// </summary>
    public partial class CameraView : UserControl
    {
        public CameraView()
        {
            InitializeComponent();
            // DataContext is inherited from parent MainWindow (CameraViewModel)
            // This enables binding to CameraFrame and other properties
        }
    }
}
