using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp3.Views;
using WpfApp3.ViewModels;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Initialize CameraViewModel and set DataContext for bindings used in MainWindow
            var camVm = new CameraViewModel();
            this.DataContext = camVm;
            // Pre-populate camera list
            camVm.RefreshCommand.Execute(null);
        }
        // Note: Sidebar and Dashboard were removed; legacy navigation handler intentionally left out.
    }
}