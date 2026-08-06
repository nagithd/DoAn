using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp3.Dialogs;
using WpfApp3.Services;
using WpfApp3.ViewModels;

namespace WpfApp3;

public partial class MainWindow : Window
{
    // Logical workspace required by the three-column camera page and the
    // side-by-side DOFBOT controls. Displays larger than this remain at 100%.
    private const double ReferenceUiWidth = 1400;
    private const double ReferenceUiHeight = 850;
    private const double MinimumUiScale = 0.50;

    private readonly CameraViewModel _cameraViewModel;
    private double _currentUiScale = 1.0;
    private bool _logScrollPending;

    public MainWindow()
    {
        InitializeComponent();

        _cameraViewModel = new CameraViewModel();
        DataContext = _cameraViewModel;
        _cameraViewModel.InspectionResultRequested +=
            CameraViewModel_InspectionResultRequested;
        _cameraViewModel.RefreshCommand.Execute(null);

        Loaded += MainWindow_Loaded;
        LayoutViewport.SizeChanged += LayoutViewport_SizeChanged;
        Closed += MainWindow_Closed;
        SystemLogService.Entries.CollectionChanged +=
            SystemLog_CollectionChanged;

        SystemLogService.Add(
            "SYSTEM",
            "UI ready. Arduino conveyor serial control and the IMITECH " +
            "Capture Zone can send automatic captures to the local AI service.");
    }

    private void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        // Wait until the maximized window has its final client dimensions.
        Dispatcher.BeginInvoke(
            UpdateResponsiveScale,
            DispatcherPriority.Loaded);
        ScrollSystemLogToLatest();
    }

    private void LayoutViewport_SizeChanged(
        object sender,
        SizeChangedEventArgs e) =>
        UpdateResponsiveScale();

    private void UpdateResponsiveScale()
    {
        var availableWidth = LayoutViewport.ActualWidth;
        var availableHeight = LayoutViewport.ActualHeight;

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        var scale = Math.Min(
            1.0,
            Math.Min(
                availableWidth / ReferenceUiWidth,
                availableHeight / ReferenceUiHeight));
        scale = Math.Max(MinimumUiScale, scale);

        // Give the controls a larger logical workspace, then scale that
        // workspace to exactly fit the available client area.
        ScalableLayoutRoot.Width = availableWidth / scale;
        ScalableLayoutRoot.Height = availableHeight / scale;

        if (Math.Abs(scale - _currentUiScale) < 0.001)
            return;

        _currentUiScale = scale;
        ScalableLayoutRoot.LayoutTransform =
            new ScaleTransform(scale, scale);
    }

    private void SystemLog_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        // ScrollIntoView forces layout. Running it synchronously from inside
        // CollectionChanged can re-enter the ItemsControl generator before it
        // has applied the current add/remove event, leaving its count out of
        // sync with ObservableCollection. Defer and coalesce scrolling until
        // the collection notification has fully completed.
        if (_logScrollPending || !IsLoaded)
            return;

        _logScrollPending = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _logScrollPending = false;
                ScrollSystemLogToLatest();
            },
            DispatcherPriority.ContextIdle);
    }

    private void ScrollSystemLogToLatest()
    {
        if (SystemLogService.Entries.Count > 0)
        {
            SystemLogList.ScrollIntoView(
                SystemLogService.Entries[^1]);
        }

        if (SystemLogService.SessionEntries.Count > 0)
        {
            WorkflowLogList.ScrollIntoView(
                SystemLogService.SessionEntries[^1]);
        }
    }

    private void MainWindow_Closed(
        object? sender,
        EventArgs e)
    {
        SystemLogService.Entries.CollectionChanged -=
            SystemLog_CollectionChanged;
        LayoutViewport.SizeChanged -= LayoutViewport_SizeChanged;
        _cameraViewModel.InspectionResultRequested -=
            CameraViewModel_InspectionResultRequested;
        _cameraViewModel.Dispose();
        ConveyorControl.Dispose();
        RobotManualControl.Dispose();
        VisionTriggerControl.Dispose();
    }

    private void CameraViewModel_InspectionResultRequested(
        object? sender,
        Models.InspectionResult result)
    {
        var dialog = new AIDetectionResultDialog(result)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }
}
