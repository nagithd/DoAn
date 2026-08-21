using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp3.Dialogs;
using WpfApp3.Models;
using WpfApp3.Services;
using WpfApp3.ViewModels;

namespace WpfApp3;

public partial class MainWindow : Window
{
    private const double ReferenceUiWidth = 1400;
    private const double ReferenceUiHeight = 850;
    private const double MinimumUiScale = 0.50;

    private readonly CameraViewModel _cameraViewModel;
    private double _currentUiScale = 1.0;
    private bool _logScrollPending;
    private bool _robotCheckpointInProgress;
    private bool _systemStartInProgress;
    private bool _systemStopInProgress;
    private bool _systemStopRequested;
    private bool _systemStarted;
    private CancellationTokenSource? _systemStartCancellation;

    public MainWindow()
    {
        InitializeComponent();

        _cameraViewModel = new CameraViewModel();
        DataContext = _cameraViewModel;
        _cameraViewModel.InspectionResultRequested +=
            CameraViewModel_InspectionResultRequested;
        ConveyorControl.ViewModel.CameraStopReached +=
            Conveyor_CameraStopReached;
        ConveyorControl.ViewModel.RobotStopReached +=
            Conveyor_RobotStopReached;
        _cameraViewModel.RefreshCommand.Execute(null);

        Loaded += MainWindow_Loaded;
        LayoutViewport.SizeChanged += LayoutViewport_SizeChanged;
        MainTabs.SelectionChanged += MainTabs_SelectionChanged;
        Closed += MainWindow_Closed;
        SystemLogService.SessionEntries.CollectionChanged +=
            SystemLog_CollectionChanged;

        SystemLogService.Add(
            "SYSTEM",
            "UI ready. Arduino sensor events synchronize IMITECH capture; " +
            "at the robot checkpoint WPF sends one direct fixed-pose pick " +
            "signal to Jetson. Arduino independently controls checkpoint " +
            "timing and conveyor restart; WPF does not send robot_done@.");
    }

    private async void Conveyor_CameraStopReached(
        object? sender,
        ConveyorEventArgs e)
    {
        SystemLogService.Add(
            "SYSTEM",
            "Arduino confirmed conveyor stop at the IMITECH camera; " +
            "starting synchronized capture.");

        InspectionResult? result =
            await _cameraViewModel.CaptureAndInspectFromArduinoAsync();
        if (result == null)
            return;

        VisionTriggerControl.ViewModel.StoreDirectInspectionResult(result);
    }

    private async void Conveyor_RobotStopReached(
        object? sender,
        ConveyorEventArgs e)
    {
        if (_robotCheckpointInProgress)
        {
            SystemLogService.Add(
                "SAFETY",
                "Duplicate robot checkpoint event ignored.");
            return;
        }

        _robotCheckpointInProgress = true;
        SystemLogService.Add(
            "SYSTEM",
            "Arduino confirmed conveyor stop at the DOFBOT checkpoint; " +
            "submitting one fixed-pose AI routing job. Arduino retains " +
            "authority over the conveyor cycle.");

        try
        {
            await VisionTriggerControl.ViewModel
                .TriggerAtRobotCheckpointAsync();
        }
        finally
        {
            _robotCheckpointInProgress = false;
        }
    }

    private void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            UpdateResponsiveScale,
            DispatcherPriority.Loaded);
        ScrollSystemLogToLatest();

        if (ReportPreviewExporter.IsRequested)
            _ = ReportPreviewExporter.ExportAsync(this, _cameraViewModel);
    }

    internal void LoadDofbotReportPlaceholder(string imagePath)
    {
        VisionTriggerControl.ViewModel.LoadReportPlaceholder(imagePath);
        MainTabs.SelectedIndex = 1;
    }

    internal void SelectReportTab(int index) =>
        MainTabs.SelectedIndex = index;

    private void LayoutViewport_SizeChanged(
        object sender,
        SizeChangedEventArgs e) =>
        UpdateResponsiveScale();

    private void UpdateResponsiveScale()
    {
        double availableWidth = LayoutViewport.ActualWidth;
        double availableHeight = LayoutViewport.ActualHeight;

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        double scale = Math.Min(
            1.0,
            Math.Min(
                availableWidth / ReferenceUiWidth,
                availableHeight / ReferenceUiHeight));
        scale = Math.Max(MinimumUiScale, scale);

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
        if (_logScrollPending ||
            !IsLoaded ||
            MainTabs.SelectedIndex != 2)
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
        if (MainTabs.SelectedIndex == 2 &&
            SystemLogService.SessionEntries.Count > 0)
            WorkflowLogList.ScrollIntoView(SystemLogService.SessionEntries[^1]);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _systemStartCancellation?.Cancel();
        _systemStartCancellation?.Dispose();
        _systemStartCancellation = null;
        SystemLogService.SessionEntries.CollectionChanged -=
            SystemLog_CollectionChanged;
        LayoutViewport.SizeChanged -= LayoutViewport_SizeChanged;
        MainTabs.SelectionChanged -= MainTabs_SelectionChanged;
        _cameraViewModel.InspectionResultRequested -=
            CameraViewModel_InspectionResultRequested;
        ConveyorControl.ViewModel.CameraStopReached -=
            Conveyor_CameraStopReached;
        ConveyorControl.ViewModel.RobotStopReached -=
            Conveyor_RobotStopReached;
        _cameraViewModel.Dispose();
        ConveyorControl.Dispose();
        VisionTriggerControl.Dispose();
    }

    private void MainTabs_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabs)
            return;

        _cameraViewModel.SetLivePreviewActive(MainTabs.SelectedIndex == 0);
        if (MainTabs.SelectedIndex == 2)
            ScrollSystemLogToLatest();
    }

    private void CameraViewModel_InspectionResultRequested(
        object? sender,
        InspectionResult result)
    {
        var dialog = new AIDetectionResultDialog(
            result,
            _cameraViewModel.AiServiceStatus,
            _cameraViewModel.CheckAiServiceCommand)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async void StartSystemButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_systemStartInProgress || _systemStopInProgress)
            return;

        if (_systemStarted)
        {
            SystemStartupStatus.Text = "System is already running";
            return;
        }

        _systemStartInProgress = true;
        _systemStopRequested = false;
        var startCancellation = new CancellationTokenSource();
        _systemStartCancellation?.Dispose();
        _systemStartCancellation = startCancellation;
        StartSystemButton.IsEnabled = false;
        StopSystemButton.IsEnabled = true;
        SystemStartupStatus.Text = "Initializing camera and AI...";
        SystemLogService.Add(
            "SYSTEM",
            "START SYSTEM requested. Conveyor remains stopped until all " +
            "required components pass their readiness checks.");

        try
        {
            SystemStartupStatus.Text = "Connecting Arduino safely...";
            bool arduinoReady = await ConveyorControl.ViewModel
                .ConnectForSystemStartAsync();
            if (_systemStopRequested)
            {
                if (arduinoReady)
                    ConveyorControl.ViewModel.StopForSystemStart();
                return;
            }

            if (!arduinoReady ||
                !ConveyorControl.ViewModel.StopForSystemStart())
            {
                SystemStartupStatus.Text =
                    "Start blocked: Arduino COM connection failed";
                SystemLogService.Add(
                    "SAFETY",
                    "System start stopped because Arduino could not be " +
                    "connected and placed at PWM 250 STOP.");
                return;
            }

            SystemStartupStatus.Text = "Initializing camera and AI...";
            bool cameraAndAiReady = await _cameraViewModel
                .PrepareCameraAndAiForSystemStartAsync();
            if (_systemStopRequested)
            {
                _cameraViewModel.StopCameraForSystem();
                return;
            }

            if (!cameraAndAiReady)
            {
                SystemStartupStatus.Text =
                    "Start blocked: camera or AI service is unavailable";
                SystemLogService.Add(
                    "SAFETY",
                    "System start stopped before conveyor motion because " +
                    "the IMITECH camera or AI service is not ready.");
                return;
            }

            _cameraViewModel.SetLivePreviewActive(
                MainTabs.SelectedIndex == 0);

            SystemStartupStatus.Text = "Preparing DOFBOT HOME...";
            bool robotReady = await VisionTriggerControl.ViewModel
                .PrepareRobotForSystemStartAsync(startCancellation.Token);
            if (_systemStopRequested)
                return;

            if (!robotReady)
            {
                SystemStartupStatus.Text =
                    "Start blocked: DOFBOT could not reach HOME";
                return;
            }

            if (!ConveyorControl.ViewModel.StartForSystem())
            {
                SystemStartupStatus.Text =
                    "Start blocked: conveyor command was not sent";
                return;
            }

            _systemStarted = true;
            SystemStartupStatus.Text =
                $"Running — PWM {ConveyorControl.ViewModel.SelectedPwm}";
            StartSystemButton.Content = "SYSTEM RUNNING";
            StopSystemButton.IsEnabled = true;
            SystemLogService.Add(
                "SYSTEM",
                "All readiness checks passed. Arduino conveyor cycle was " +
                $"started at PWM {ConveyorControl.ViewModel.SelectedPwm}.");
        }
        catch (Exception ex)
        {
            SystemStartupStatus.Text = "System start failed";
            SystemLogService.Add(
                "SAFETY",
                $"System start failed before completion: {ex.Message}");
        }
        finally
        {
            _systemStartInProgress = false;
            if (ReferenceEquals(_systemStartCancellation, startCancellation))
            {
                _systemStartCancellation.Dispose();
                _systemStartCancellation = null;
            }

            StartSystemButton.IsEnabled =
                !_systemStarted && !_systemStopInProgress;
        }
    }

    private async void StopSystemButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_systemStopInProgress)
            return;

        _systemStopInProgress = true;
        _systemStopRequested = true;
        _systemStartCancellation?.Cancel();
        StartSystemButton.IsEnabled = false;
        StopSystemButton.IsEnabled = false;
        SystemStartupStatus.Text = "Stopping conveyor safely...";
        SystemLogService.Add(
            "SYSTEM",
            "STOP SYSTEM requested. Conveyor stop has highest priority.");

        bool conveyorWasConnected = ConveyorControl.ViewModel.IsConnected;
        bool conveyorStopSent = conveyorWasConnected &&
            ConveyorControl.ViewModel.StopForSystemStart();

        try
        {
            // Give the serial STOP command a short opportunity to leave the
            // Windows transmit buffer before camera acquisition is closed.
            await Task.Delay(250);

            SystemStartupStatus.Text = "Stopping camera streams...";
            _cameraViewModel.StopCameraForSystem();
            await VisionTriggerControl.ViewModel.StopForSystemAsync();

            _systemStarted = false;
            StartSystemButton.Content = "START SYSTEM";
            SystemStartupStatus.Text = conveyorStopSent
                ? "Stop command sent — verify Arduino acknowledgement"
                : "Stop incomplete — verify conveyor manually";

            SystemLogService.Add(
                conveyorStopSent ? "SYSTEM" : "SAFETY",
                conveyorStopSent
                    ? "PWM 250 STOP was written to the Arduino serial " +
                      "port. Camera streams are closed; AI service, Robot " +
                      "API and Jetson remain running. Confirm the Arduino " +
                      "acknowledgement and physical conveyor state."
                    : "STOP SYSTEM could not confirm the Arduino STOP " +
                      (conveyorWasConnected
                          ? "command. Camera streams were closed; operator " +
                            "inspection is required."
                          : "command because the serial connection was not " +
                            "open. Camera streams were closed; operator " +
                            "inspection is required."));
        }
        catch (Exception ex)
        {
            _systemStarted = false;
            StartSystemButton.Content = "START SYSTEM";
            SystemStartupStatus.Text =
                "Stop completed with warnings — inspect system";
            SystemLogService.Add(
                "SAFETY",
                "STOP SYSTEM encountered an error after the conveyor stop " +
                $"request: {ex.Message}");
        }
        finally
        {
            _systemStopInProgress = false;
            StartSystemButton.IsEnabled = true;
            StopSystemButton.IsEnabled = true;
        }
    }
}
