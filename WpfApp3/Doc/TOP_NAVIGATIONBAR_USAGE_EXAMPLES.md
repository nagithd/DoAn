# Top Navigation Bar - Usage Examples

## Quick Start

### 1. Basic Setup

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls">
	<Grid>
		<Grid.RowDefinitions>
			<RowDefinition Height="60"/>
			<RowDefinition Height="*"/>
		</Grid.RowDefinitions>

		<local:TopNavigationBar Grid.Row="0"/>

		<Grid Grid.Row="1" Background="#1E1E1E">
			<!-- Main content -->
		</Grid>
	</Grid>
</Window>
```

### 2. Update Time Display

```csharp
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		StartClockTimer();
	}

	private void StartClockTimer()
	{
		var timer = new DispatcherTimer();
		timer.Interval = TimeSpan.FromSeconds(1);
		timer.Tick += (s, e) =>
		{
			var topNav = (TopNavigationBar)FindName("TopNavigationBar");
			topNav?.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
		};
		timer.Start();
	}
}
```

---

## Complete Examples

### Example 1: Simple Time Update

```csharp
private TopNavigationBar _topNav;

public MainWindow()
{
	InitializeComponent();
	_topNav = (TopNavigationBar)FindName("TopNavigationBar");

	// Update time every second
	var timer = new DispatcherTimer();
	timer.Interval = TimeSpan.FromSeconds(1);
	timer.Tick += Timer_Tick;
	timer.Start();
}

private void Timer_Tick(object sender, EventArgs e)
{
	_topNav.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
}
```

### Example 2: Status Monitoring

```csharp
private void UpdateSystemStatus()
{
	if (IsSystemHealthy())
	{
		_topNav.UpdateSystemStatus("Ready", "#4EC9B0");  // Green/Teal
	}
	else if (IsSystemWarning())
	{
		_topNav.UpdateSystemStatus("Warning", "#FFD700");  // Gold/Yellow
	}
	else
	{
		_topNav.UpdateSystemStatus("Error", "#FF6B6B");  // Red
	}
}

private bool IsSystemHealthy() => true;
private bool IsSystemWarning() => false;
```

### Example 3: Emergency Stop Handler

```csharp
public MainWindow()
{
	InitializeComponent();
	var topNav = (TopNavigationBar)FindName("TopNavigationBar");
	topNav.EmergencyStopPressed += OnEmergencyStop;
}

private void OnEmergencyStop(object sender, RoutedEventArgs e)
{
	// Immediate shutdown
	StopAllOperations();

	// Update status
	var topNav = (TopNavigationBar)FindName("TopNavigationBar");
	topNav.UpdateSystemStatus("STOPPED", "#FF6B6B");

	// Show alert
	MessageBox.Show(
		"EMERGENCY STOP ACTIVATED\n\nAll operations have been halted.\n" +
		"Contact system administrator to resume.",
		"SYSTEM ALERT",
		MessageBoxButton.OK,
		MessageBoxImage.Stop);
}

private void StopAllOperations()
{
	// Stop all processing
	// Close all connections
	// Disable controls
}
```

### Example 4: Uptime Tracking

```csharp
private DateTime _systemStartTime;
private DispatcherTimer _uptimeTimer;

public MainWindow()
{
	InitializeComponent();
	_systemStartTime = DateTime.Now;
	StartUptimeCounter();
}

private void StartUptimeCounter()
{
	_uptimeTimer = new DispatcherTimer();
	_uptimeTimer.Interval = TimeSpan.FromSeconds(1);
	_uptimeTimer.Tick += (s, e) =>
	{
		var uptime = DateTime.Now - _systemStartTime;
		var topNav = (TopNavigationBar)FindName("TopNavigationBar");
		topNav.UpdateUptime(string.Format("{0:D2}:{1:D2}:{2:D2}",
			uptime.Hours, uptime.Minutes, uptime.Seconds));
	};
	_uptimeTimer.Start();
}
```

### Example 5: User Authentication

```csharp
public class LoginService
{
	public void OnUserLoggedIn(string userName, string userRole)
	{
		var topNav = (TopNavigationBar)Application.Current.MainWindow.FindName("TopNavigationBar");
		topNav.UpdateUserName(userName);

		// Note: Role would need additional XAML binding
		MessageBox.Show($"Logged in as: {userName} ({userRole})");
	}
}
```

### Example 6: Connection Status Monitoring

```csharp
private async void MonitorConnectionStatus()
{
	while (true)
	{
		bool isConnected = await CheckServerConnection();
		var topNav = (TopNavigationBar)FindName("TopNavigationBar");

		if (isConnected)
		{
			topNav.UpdateSystemStatus("Online", "#4EC9B0");
		}
		else
		{
			topNav.UpdateSystemStatus("Offline", "#FF6B6B");
		}

		await Task.Delay(5000);  // Check every 5 seconds
	}
}

private async Task<bool> CheckServerConnection()
{
	// Implementation
	return true;
}
```

### Example 7: Multiple Status Updates

```csharp
public class SystemMonitor
{
	private TopNavigationBar _topNav;
	private DispatcherTimer _monitoringTimer;

	public SystemMonitor(TopNavigationBar topNav)
	{
		_topNav = topNav;
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
		// Update time
		_topNav.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));

		// Update status
		var statusColor = GetSystemStatusColor();
		var statusText = GetSystemStatusText();
		_topNav.UpdateSystemStatus(statusText, statusColor);

		// Update uptime
		var uptime = CalculateUptime();
		_topNav.UpdateUptime(uptime);
	}

	private string GetSystemStatusColor()
	{
		return IsSystemHealthy() ? "#4EC9B0" : "#FF6B6B";
	}

	private string GetSystemStatusText()
	{
		return IsSystemHealthy() ? "Ready" : "Error";
	}

	private string CalculateUptime()
	{
		var elapsed = DateTime.Now - _systemStartTime;
		return string.Format("{0:D2}:{1:D2}:{2:D2}",
			elapsed.Hours, elapsed.Minutes, elapsed.Seconds);
	}

	private bool IsSystemHealthy() => true;
}
```

### Example 8: MVVM ViewModel Integration

```csharp
public class MainWindowViewModel : ViewModelBase
{
	private readonly TopNavigationBarViewModel _navBarVm;
	private DispatcherTimer _updateTimer;

	public TopNavigationBarViewModel NavBarViewModel
	{
		get => _navBarVm;
	}

	public MainWindowViewModel()
	{
		_navBarVm = new TopNavigationBarViewModel();
		InitializeTimers();
	}

	private void InitializeTimers()
	{
		_updateTimer = new DispatcherTimer();
		_updateTimer.Interval = TimeSpan.FromSeconds(1);
		_updateTimer.Tick += (s, e) =>
		{
			_navBarVm.UpdateCurrentTime();

			// Update other properties
			_navBarVm.SetSystemStatus("Ready", "#4EC9B0");
		};
		_updateTimer.Start();
	}
}
```

### Example 9: ViewModel Binding in XAML

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls">
	<Grid>
		<Grid.RowDefinitions>
			<RowDefinition Height="60"/>
			<RowDefinition Height="*"/>
		</Grid.RowDefinitions>

		<!-- Binding to ViewModel -->
		<local:TopNavigationBar x:Name="TopNavBar" Grid.Row="0"/>

		<Grid Grid.Row="1">
			<!-- Main content -->
		</Grid>
	</Grid>
</Window>
```

```csharp
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		var viewModel = new MainWindowViewModel();
		DataContext = viewModel;

		// Set ViewModel for binding
		TopNavBar.DataContext = viewModel.NavBarViewModel;
	}
}
```

### Example 10: Emergency Stop with Confirmation

```csharp
private void OnEmergencyStop(object sender, RoutedEventArgs e)
{
	var result = MessageBox.Show(
		"EMERGENCY STOP CONFIRMATION\n\n" +
		"This will immediately halt all operations.\n" +
		"Continue?",
		"WARNING",
		MessageBoxButton.YesNo,
		MessageBoxImage.Warning);

	if (result == MessageBoxResult.Yes)
	{
		ExecuteEmergencyStop();
	}
}

private void ExecuteEmergencyStop()
{
	var topNav = (TopNavigationBar)FindName("TopNavigationBar");

	// Update UI
	topNav.SetEmergencyStopEnabled(false);
	topNav.UpdateSystemStatus("EMERGENCY STOP", "#FF6B6B");

	// Stop all operations
	try
	{
		StopCamera();
		StopConveyor();
		StopRobotArm();
		DisconnectAI();
	}
	catch (Exception ex)
	{
		MessageBox.Show($"Error during emergency stop: {ex.Message}", "Error");
	}
	finally
	{
		MessageBox.Show("Emergency stop complete. System safe.", "Status");
	}
}

private void StopCamera() { }
private void StopConveyor() { }
private void StopRobotArm() { }
private void DisconnectAI() { }
```

---

## Pattern Examples

### Time Update Pattern

```csharp
private void SetupClockUpdate()
{
	var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
	timer.Tick += (s, e) => _topNav.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
	timer.Start();
}
```

### Status Change Pattern

```csharp
private void ChangeSystemStatus(string status, string color)
{
	_topNav.UpdateSystemStatus(status, color);
	LogStatusChange(status, color);
}

private void LogStatusChange(string status, string color)
{
	// Log to system
}
```

### Uptime Display Pattern

```csharp
private void UpdateUptimeDisplay()
{
	var uptime = DateTime.Now - _startTime;
	var timeString = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
	_topNav.UpdateUptime(timeString);
}
```

---

## Event Handling Pattern

```csharp
public class NavigationBarEventHandler
{
	private TopNavigationBar _topNav;

	public NavigationBarEventHandler(TopNavigationBar topNav)
	{
		_topNav = topNav;
		_topNav.EmergencyStopPressed += OnEmergencyStopPressed;
	}

	private void OnEmergencyStopPressed(object sender, RoutedEventArgs e)
	{
		HandleEmergencyStop();
	}

	private void HandleEmergencyStop()
	{
		// Implementation
	}
}
```

---

## Best Practice Examples

✅ **DO: Update on timer interval**
```csharp
var timer = new DispatcherTimer();
timer.Tick += (s, e) => _topNav.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
```

❌ **DON'T: Update on every operation**
```csharp
// Don't call this in tight loops
_topNav.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
```

✅ **DO: Handle emergency stop**
```csharp
_topNav.EmergencyStopPressed += OnEmergencyStop;
```

❌ **DON'T: Ignore emergency stop**
```csharp
// Don't ignore the event
```

✅ **DO: Use meaningful colors**
```csharp
_topNav.UpdateSystemStatus("Error", "#FF6B6B");
```

❌ **DON'T: Random colors**
```csharp
_topNav.UpdateSystemStatus("Ready", "#12AB34");  // Random hex
```

---

These examples cover all common usage patterns for the Top Navigation Bar component!
