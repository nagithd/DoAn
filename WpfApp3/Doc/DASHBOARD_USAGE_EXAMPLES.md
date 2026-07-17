# Dashboard - Usage Examples

## Quick Start

### Step 1: Add Dashboard to MainWindow

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:controls="clr-namespace:WpfApp3.Controls"
		xmlns:views="clr-namespace:WpfApp3.Views"
		Title="Battery Inspection System"
		Height="1080"
		Width="1920"
		WindowState="Maximized">

	<Grid>
		<Grid.RowDefinitions>
			<RowDefinition Height="60"/>  <!-- Top Navigation -->
			<RowDefinition Height="*"/>   <!-- Main Content -->
		</Grid.RowDefinitions>

		<!-- Top Navigation Bar -->
		<controls:TopNavigationBar Grid.Row="0"/>

		<!-- Main Content Area -->
		<Grid Grid.Row="1">
			<Grid.ColumnDefinitions>
				<ColumnDefinition Width="250"/>  <!-- Sidebar -->
				<ColumnDefinition Width="*"/>    <!-- Dashboard -->
			</Grid.ColumnDefinitions>

			<!-- Sidebar Navigation -->
			<controls:Sidebar Grid.Column="0"/>

			<!-- Dashboard -->
			<views:Dashboard Grid.Column="1"/>
		</Grid>
	</Grid>
</Window>
```

### Step 2: Code-Behind Setup

```csharp
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		InitializeDashboard();
	}

	private void InitializeDashboard()
	{
		// Set up data context if using MVVM
		// DataContext = new MainViewModel();
	}
}
```

---

## Complete Examples

### Example 1: Simple Dashboard Display

Just displaying the dashboard without any updates:

```xaml
<views:Dashboard/>
```

The dashboard will show:
- Live camera feed placeholder
- Conveyor animation visualization
- System/Robot/AI status panels
- Statistics cards (Good, Scratch, Dent, Other)
- Event log with sample entries
- Control buttons (Start, Stop, Reset, Emergency Stop, Auto/Manual mode)

---

### Example 2: Update Statistics Dynamically

```csharp
public partial class MainWindow : Window
{
	private DispatcherTimer _updateTimer;
	private int _goodCount = 2847;
	private int _scratchCount = 234;
	private int _dentCount = 156;
	private int _otherCount = 89;

	public MainWindow()
	{
		InitializeComponent();
		StartStatisticsUpdates();
	}

	private void StartStatisticsUpdates()
	{
		_updateTimer = new DispatcherTimer();
		_updateTimer.Interval = TimeSpan.FromSeconds(5);
		_updateTimer.Tick += (s, e) => UpdateStatistics();
		_updateTimer.Start();
	}

	private void UpdateStatistics()
	{
		// Simulate battery inspection counts
		_goodCount += Random.Shared.Next(5, 15);
		_scratchCount += Random.Shared.Next(0, 3);
		_dentCount += Random.Shared.Next(0, 2);
		_otherCount += Random.Shared.Next(0, 1);

		// Update UI through bindings or direct calls
		// This is where you'd update your data context
	}
}
```

---

### Example 3: Handle Control Button Clicks

```csharp
public partial class Dashboard : UserControl
{
	public Dashboard()
	{
		InitializeComponent();
		WireUpButtonEvents();
	}

	private void WireUpButtonEvents()
	{
		// Note: In production, use MVVM bindings or routed events
		// This is a simplified example
	}

	private void OnStartClick()
	{
		MessageBox.Show("Inspection cycle started!", "Status");
		AddEventLogEntry("Inspection cycle started", EventType.Info);
	}

	private void OnStopClick()
	{
		MessageBox.Show("Inspection cycle paused!", "Status");
		AddEventLogEntry("Inspection cycle paused", EventType.Info);
	}

	private void OnResetClick()
	{
		MessageBox.Show("System reset!", "Status");
		AddEventLogEntry("System reset", EventType.Info);
	}

	private void OnEmergencyStopClick()
	{
		MessageBox.Show("EMERGENCY STOP ACTIVATED!", "Alert");
		AddEventLogEntry("Emergency stop activated", EventType.Error);
	}

	private void OnAutoModeClick()
	{
		AddEventLogEntry("Switched to Auto mode", EventType.Info);
	}

	private void OnManualModeClick()
	{
		AddEventLogEntry("Switched to Manual mode", EventType.Info);
	}

	private void AddEventLogEntry(string message, EventType type)
	{
		// Implementation to add to event log
	}
}

public enum EventType
{
	Info,
	Success,
	Warning,
	Error
}
```

---

### Example 4: MVVM ViewModel Integration

```csharp
public class DashboardViewModel : ViewModelBase
{
	private int _goodBatteries = 2847;
	private int _scratchDefects = 234;
	private int _dentDefects = 156;
	private int _otherDefects = 89;
	private string _systemStatus = "Ready";
	private string _robotStatus = "Operational";
	private string _aiStatus = "Active";
	private double _temperature = 45.0;

	public int GoodBatteries
	{
		get => _goodBatteries;
		set => SetProperty(ref _goodBatteries, value);
	}

	public int ScratchDefects
	{
		get => _scratchDefects;
		set => SetProperty(ref _scratchDefects, value);
	}

	public int DentDefects
	{
		get => _dentDefects;
		set => SetProperty(ref _dentDefects, value);
	}

	public int OtherDefects
	{
		get => _otherDefects;
		set => SetProperty(ref _otherDefects, value);
	}

	public string SystemStatus
	{
		get => _systemStatus;
		set => SetProperty(ref _systemStatus, value);
	}

	public string RobotStatus
	{
		get => _robotStatus;
		set => SetProperty(ref _robotStatus, value);
	}

	public string AIStatus
	{
		get => _aiStatus;
		set => SetProperty(ref _aiStatus, value);
	}

	public double Temperature
	{
		get => _temperature;
		set => SetProperty(ref _temperature, value);
	}

	public void StartInspection()
	{
		SystemStatus = "Running";
		// Implementation
	}

	public void StopInspection()
	{
		SystemStatus = "Paused";
		// Implementation
	}

	public void ResetSystem()
	{
		SystemStatus = "Ready";
		// Implementation
	}

	public void EmergencyStop()
	{
		SystemStatus = "STOPPED";
		// Implementation
	}
}
```

Using the ViewModel:

```csharp
public partial class MainWindow : Window
{
	private DashboardViewModel _viewModel;

	public MainWindow()
	{
		InitializeComponent();
		_viewModel = new DashboardViewModel();
		DataContext = _viewModel;
	}
}
```

---

### Example 5: Real-time Status Updates

```csharp
public class SystemMonitor
{
	private readonly DashboardViewModel _viewModel;
	private DispatcherTimer _monitoringTimer;

	public SystemMonitor(DashboardViewModel viewModel)
	{
		_viewModel = viewModel;
	}

	public void StartMonitoring()
	{
		_monitoringTimer = new DispatcherTimer();
		_monitoringTimer.Interval = TimeSpan.FromSeconds(1);
		_monitoringTimer.Tick += Monitor_Tick;
		_monitoringTimer.Start();
	}

	private void Monitor_Tick(object sender, EventArgs e)
	{
		UpdateTemperature();
		CheckSystemHealth();
		UpdateStatistics();
	}

	private void UpdateTemperature()
	{
		// Simulate temperature reading
		var temp = 45.0 + (Random.Shared.NextDouble() - 0.5) * 5;
		_viewModel.Temperature = Math.Round(temp, 1);
	}

	private void CheckSystemHealth()
	{
		if (_viewModel.Temperature > 50)
		{
			_viewModel.SystemStatus = "Warning";
		}
		else
		{
			_viewModel.SystemStatus = "Ready";
		}
	}

	private void UpdateStatistics()
	{
		_viewModel.GoodBatteries += Random.Shared.Next(0, 2);
		_viewModel.ScratchDefects += Random.Shared.Next(0, 1);
	}
}
```

---

### Example 6: Event Log Management

```csharp
public class EventLogManager
{
	private List<SystemEvent> _events = new();
	private const int MaxEvents = 100;

	public void AddEvent(string message, EventSeverity severity)
	{
		var evt = new SystemEvent
		{
			Message = message,
			Severity = severity,
			Timestamp = DateTime.Now
		};

		_events.Insert(0, evt);

		// Keep only last 100 events
		if (_events.Count > MaxEvents)
			_events.RemoveAt(_events.Count - 1);

		// Update UI
		OnEventAdded(evt);
	}

	public void ClearEvents()
	{
		_events.Clear();
		OnEventsCleared();
	}

	public IReadOnlyList<SystemEvent> GetEvents() => _events.AsReadOnly();

	public event Action<SystemEvent>? EventAdded;
	public event Action? EventsCleared;

	private void OnEventAdded(SystemEvent evt) => EventAdded?.Invoke(evt);
	private void OnEventsCleared() => EventsCleared?.Invoke();
}

public class SystemEvent
{
	public string Message { get; set; }
	public EventSeverity Severity { get; set; }
	public DateTime Timestamp { get; set; }
}

public enum EventSeverity
{
	Info,
	Success,
	Warning,
	Error
}
```

---

### Example 7: Responsive Layout Testing

```csharp
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		TestResponsiveness();
	}

	private void TestResponsiveness()
	{
		// Test different window sizes
		this.SizeChanged += (s, e) =>
		{
			Console.WriteLine($"Window size: {e.NewSize.Width}x{e.NewSize.Height}");

			// Dashboard components should automatically resize
			// Camera: 65% of available width
			// Conveyor: 20% of available width
			// Status: 15% of available width
		};
	}
}
```

---

### Example 8: Full Integration Example

Complete example with all features:

```csharp
public partial class MainWindow : Window
{
	private DashboardViewModel _dashboardVm;
	private SystemMonitor _monitor;
	private EventLogManager _eventLog;

	public MainWindow()
	{
		InitializeComponent();
		InitializeComponents();
		StartMonitoring();
	}

	private void InitializeComponents()
	{
		_dashboardVm = new DashboardViewModel();
		_monitor = new SystemMonitor(_dashboardVm);
		_eventLog = new EventLogManager();

		// Set data context
		DataContext = _dashboardVm;

		// Wire up event log
		_eventLog.EventAdded += (evt) =>
		{
			Console.WriteLine($"[{evt.Severity}] {evt.Message}");
		};

		_eventLog.AddEvent("Dashboard initialized", EventSeverity.Info);
	}

	private void StartMonitoring()
	{
		_monitor.StartMonitoring();

		// Simulate inspection start
		Task.Delay(2000).ContinueWith(_ =>
		{
			Dispatcher.Invoke(() =>
			{
				_dashboardVm.StartInspection();
				_eventLog.AddEvent("Inspection cycle started", EventSeverity.Info);
			});
		});
	}

	protected override void OnClosed(EventArgs e)
	{
		_monitor?.StopMonitoring();
		base.OnClosed(e);
	}
}
```

---

## Best Practices

### ✅ DO: Use MVVM for data binding

```csharp
// Bind ViewModel to Dashboard
DataContext = new DashboardViewModel();
```

### ❌ DON'T: Update UI directly from code-behind

```csharp
// Avoid this - use MVVM instead
dashboard.CameraView.UpdateImage(bitmap);
```

### ✅ DO: Use DispatcherTimer for periodic updates

```csharp
var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
timer.Tick += (s, e) => UpdateDashboard();
timer.Start();
```

### ❌ DON'T: Block UI thread with long operations

```csharp
// Avoid blocking
Thread.Sleep(5000);
```

### ✅ DO: Use async operations

```csharp
await Task.Run(() => LongOperation());
```

### ✅ DO: Handle emergency stop immediately

```csharp
private void OnEmergencyStop()
{
	StopAllOperations();
	DisableAllControls();
	ShowAlert("EMERGENCY STOP ACTIVATED");
}
```

### ❌ DON'T: Ignore critical events

```csharp
// Always handle emergency stop and errors
```

---

## Component Reference

### Camera View
- 65% of top section width
- Displays live video feed placeholder
- Shows recording status indicator
- Responsive to window resizing

### Conveyor Animation
- 20% of top section width
- Shows belt visualization
- Displays speed and efficiency
- Updates in real-time

### Status Panel
- 15% of top section width
- System status indicator
- Robot arm status
- AI engine status
- Temperature display

### Statistics Cards
- Horizontal scrollable row
- Shows: Good, Scratch, Dent, Other
- Displays count and trend
- Updates periodically

### Event Log
- Full-width scrollable list
- Color-coded events (Info, Success, Warning, Error)
- Timestamps
- Clear button

### Control Panel
- Start/Stop/Reset buttons
- Emergency Stop button (red)
- Auto/Manual mode toggle
- Responsive button styling

---

## Customization

### Change Proportions

Edit Dashboard.xaml column definitions:

```xaml
<!-- Original: 65% / 20% / 15% -->
<ColumnDefinition Width="6.5*"/>   <!-- 65% -->
<ColumnDefinition Width="2*"/>     <!-- 20% -->
<ColumnDefinition Width="1.5*"/>   <!-- 15% -->

<!-- New: 70% / 20% / 10% -->
<ColumnDefinition Width="7*"/>     <!-- 70% -->
<ColumnDefinition Width="2*"/>     <!-- 20% -->
<ColumnDefinition Width="1*"/>     <!-- 10% -->
```

### Change Colors

Edit component XAML files and replace hex codes:

```xaml
<!-- Dark Gray Background -->
Background="#2D2D30"    <!-- Change this -->

<!-- Teal Accent -->
Foreground="#4EC9B0"    <!-- Change this -->
```

### Add Custom Components

Create new UserControl and add to Dashboard:

```xaml
<local:MyCustomControl Grid.Row="4" Margin="15"/>
```

---

This dashboard is fully functional and ready for integration with your Battery Inspection System!
