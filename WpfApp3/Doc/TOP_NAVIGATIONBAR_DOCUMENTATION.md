# Top Navigation Bar UserControl - Documentation

## Overview

A professional Material Design Top Navigation Bar component for WPF applications. Displays application branding, system status, current time, user information, and emergency controls in a single, reusable component.

---

## Features

✅ **Application Branding**
- Animated logo (⚙ icon)
- Application title ("Battery Inspection System")
- Version number

✅ **System Status Display**
- Connection status with indicator
- System status (Ready, Warning, Error)
- Current system time (HH:MM:SS)
- System uptime counter

✅ **User Information**
- Current logged-in user
- User role/title
- User icon

✅ **Emergency Controls**
- Prominent emergency stop button
- Red alert color
- Accessible in all situations

✅ **Material Design**
- Dark theme matching industrial HMI
- Smooth color transitions
- Professional typography
- Proper spacing and alignment

✅ **Fully Reusable**
- Works in any WPF window
- Optional ViewModel binding
- Event-based communication
- Public methods for programmatic updates

---

## File Structure

```
WpfApp3/
├── Controls/
│   ├── TopNavigationBar.xaml           # UI Layout
│   └── TopNavigationBar.xaml.cs        # Code-behind with public methods
└── ViewModels/
	└── TopNavigationBarViewModel.cs    # Data binding (optional)
```

---

## Basic Usage

### Step 1: Add Namespace to Window

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls">
```

### Step 2: Place in Your Layout

```xaml
<Grid>
	<Grid.RowDefinitions>
		<RowDefinition Height="60"/>   <!-- Top Navigation -->
		<RowDefinition Height="*"/>    <!-- Main content -->
	</Grid.RowDefinitions>

	<!-- Top Navigation Bar -->
	<local:TopNavigationBar Grid.Row="0" EmergencyStopPressed="OnEmergencyStop"/>

	<!-- Main Content -->
	<Grid Grid.Row="1">
		<!-- Your content here -->
	</Grid>
</Grid>
```

### Step 3: Handle Events (Optional)

```csharp
private void OnEmergencyStop(object sender, RoutedEventArgs e)
{
	MessageBox.Show("Emergency Stop Activated!");
	// Implement emergency stop logic
}
```

---

## Component Layout

```
┌──────────────────────────────────────────────────────────────────────────┐
│ ⚙ Battery Inspection System │ • Online │ Time: 12:34:56 │ Status: Ready │
│   v1.0.0                   │ Uptime: 24:15:47                           │
│                                                    │ 👤 Administrator │
│                                                    │  System Admin    │
│                                                    │ [🛑 EMERGENCY STOP] │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Sections Breakdown

### Left Section: Branding
```
⚙ Battery Inspection System
  v1.0.0
```
- Logo: ⚙ (customizable)
- Title: Application name
- Version: Current version string

### Center Section: System Status
```
• Online | Time: 12:34:56 | Status: Ready | Uptime: 24:15:47
```
- Connection indicator (green dot when online)
- Current system time (24-hour format)
- System status (color-coded)
- System uptime counter

### Right Section: User & Emergency
```
👤 Administrator        [🛑 EMERGENCY STOP]
   System Admin
```
- User icon
- User name
- User role
- Emergency Stop button (red, high-visibility)

---

## Color Scheme

| Element | Color | Hex Code | Usage |
|---------|-------|----------|-------|
| Background | Dark Gray | #252526 | Main bar background |
| Border | Subtle | #3E3E42 | Bottom border |
| Status Online | Teal | #4EC9B0 | Connection OK |
| Status Error | Red | #D13438 | Error state |
| Emergency Button | Red | #D13438 | Stop button |
| Emergency Hover | Dark Red | #A92222 | Hover state |
| Emergency Press | Very Dark Red | #7A1818 | Pressed state |
| Text Normal | Light Gray | #CCCCCC | Standard text |
| Text Label | Dim Gray | #858585 | Labels |
| Text Title | White | #FFFFFF | Titles |
| Accent | Blue | #007ACC | Logo/accent |

---

## Public Methods

### UpdateTime(string time)
Updates the current time display.
```csharp
topNavBar.UpdateTime("14:32:45");
```

### UpdateSystemStatus(string status, string color)
Updates system status with color.
```csharp
topNavBar.UpdateSystemStatus("Warning", "#FFD700");
```

### UpdateUptime(string uptime)
Updates uptime display.
```csharp
topNavBar.UpdateUptime("25:10:30");
```

### UpdateUserName(string userName)
Updates displayed user name.
```csharp
topNavBar.UpdateUserName("John Doe");
```

### SetEmergencyStopEnabled(bool enabled)
Enable/disable emergency stop button.
```csharp
topNavBar.SetEmergencyStopEnabled(false);
```

---

## Events

### EmergencyStopPressed
Fired when user clicks emergency stop button.

```xaml
<local:TopNavigationBar EmergencyStopPressed="OnEmergencyStop"/>
```

```csharp
private void OnEmergencyStop(object sender, RoutedEventArgs e)
{
	// Stop all operations
}
```

---

## ViewModel Integration

### Using TopNavigationBarViewModel

```xaml
<local:TopNavigationBar Grid.Row="0"/>
```

```csharp
public partial class MainWindow : Window
{
	private TopNavigationBarViewModel _viewModel;

	public MainWindow()
	{
		InitializeComponent();
		_viewModel = new TopNavigationBarViewModel();
		DataContext = _viewModel;
	}
}
```

### ViewModel Properties

- `ApplicationTitle` - Application name
- `ApplicationVersion` - Version string
- `CurrentTime` - Current time (HH:MM:SS)
- `CurrentUser` - User name
- `UserRole` - User role/title
- `SystemStatus` - Status text
- `SystemStatusColor` - Status color (hex)
- `Uptime` - Uptime string
- `IsOnline` - Connection status boolean

### ViewModel Methods

```csharp
_viewModel.UpdateCurrentTime();
_viewModel.SetSystemStatus("Warning", "#FFD700");
_viewModel.SetConnectionStatus(false);
_viewModel.SetUserInfo("Jane Smith", "Operator");
_viewModel.UpdateUptime(new TimeSpan(24, 15, 47));
```

---

## Styling Customization

### Change Emergency Stop Color

Edit **TopNavigationBar.xaml** in EmergencyStopButtonStyle:

```xaml
<Setter Property="Background" Value="#YOUR_COLOR"/>
```

### Change Logo

Replace text in XAML:

```xaml
<TextBlock Text="🏭"/>  <!-- Or any emoji/character -->
```

### Change Status Colors

Modify hex codes in UpdateSystemStatus calls:

```csharp
topNavBar.UpdateSystemStatus("Error", "#FF6B6B");
```

---

## Dimensions & Spacing

### Component Size
- Height: 60px (standard HMI height)
- Padding: 15px horizontal margins
- Spacing: 15px between sections

### Text Sizes
- Logo: 28px
- Title: 14px Bold
- Version: 9px
- Status Text: 11px
- Labels: 10px

### Dividers
- Width: 1px
- Color: #3E3E42
- Height: 25-35px
- Spacing: 15px on each side

---

## Status Indicators

### Connection Status
```
🟢 Online (Green)   - System connected
🔴 Offline (Red)    - System disconnected
🟡 Warning (Yellow) - Connection unstable
```

### System Status
```
✓ Ready     (#4EC9B0 - Teal)
⚠ Warning   (#FFD700 - Gold)
✗ Error     (#FF6B6B - Red)
⏸ Paused    (#007ACC - Blue)
```

---

## Integration Examples

### With TimeSpan Binding

```csharp
private void StartUptimeTimer()
{
	var timer = new System.Timers.Timer(1000);
	var startTime = DateTime.Now;

	timer.Elapsed += (s, e) =>
	{
		var uptime = DateTime.Now - startTime;
		topNavBar.UpdateUptime(string.Format("{0:D2}:{1:D2}:{2:D2}",
			uptime.Hours, uptime.Minutes, uptime.Seconds));
	};

	timer.Start();
}
```

### With Clock Display

```csharp
private void StartClockTimer()
{
	var timer = new DispatcherTimer();
	timer.Interval = TimeSpan.FromSeconds(1);
	timer.Tick += (s, e) => topNavBar.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
	timer.Start();
}
```

### Emergency Stop Handler

```csharp
private void OnEmergencyStop(object sender, RoutedEventArgs e)
{
	// Disable system
	_viewModel.SetSystemStatus("EMERGENCY STOP", "#FF6B6B");

	// Alert user
	MessageBox.Show("EMERGENCY STOP ACTIVATED\nAll operations halted!", 
					"SYSTEM ALERT", 
					MessageBoxButton.OK, 
					MessageBoxImage.Stop);

	// Stop all operations
	StopAllOperations();
}

private void StopAllOperations()
{
	// Implementation
}
```

---

## Complete MainWindow Example

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:local="clr-namespace:WpfApp3.Controls"
		Title="Battery Inspection System" Height="900" Width="1400">
	<Grid>
		<Grid.RowDefinitions>
			<RowDefinition Height="60"/>
			<RowDefinition Height="*"/>
		</Grid.RowDefinitions>

		<local:TopNavigationBar Grid.Row="0" EmergencyStopPressed="OnEmergencyStop"/>

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
		StartClockTimer();
	}

	private void StartClockTimer()
	{
		var timer = new DispatcherTimer();
		timer.Interval = TimeSpan.FromSeconds(1);
		timer.Tick += (s, e) =>
		{
			var topNav = (TopNavigationBar)FindName("TopNavigationBar");
			if (topNav != null)
				topNav.UpdateTime(DateTime.Now.ToString("HH:mm:ss"));
		};
		timer.Start();
	}

	private void OnEmergencyStop(object sender, RoutedEventArgs e)
	{
		MessageBox.Show("Emergency Stop Activated!", "Alert");
	}
}
```

---

## Best Practices

1. **Always handle EmergencyStopPressed** - Never ignore emergency stop events
2. **Update time regularly** - Use DispatcherTimer for clock display
3. **Color-code statuses** - Use consistent colors across application
4. **Make it accessible** - Emergency stop should be always visible and clickable
5. **Test all interactions** - Especially emergency controls
6. **Provide visual feedback** - Animate status changes

---

## Known Limitations & Future Enhancements

**Current Limitations:**
- No built-in timer (must be managed by parent)
- Time format fixed to HH:MM:SS
- Logo is text only (no image support in base)

**Potential Enhancements:**
- Alarm notification bell icon
- Network diagnostics dropdown
- Quick settings menu
- User profile menu
- Animated status indicators
- Sound alert on emergency

---

## Troubleshooting

**Q: Emergency button not responding**
A: Ensure EmergencyStopPressed event is properly connected

**Q: Time not updating**
A: Implement DispatcherTimer in code-behind

**Q: Colors not showing correctly**
A: Verify hex codes are valid (#RRGGBB format)

**Q: Component too tall**
A: Set RowDefinition Height="60" in parent Grid

---

## Performance Considerations

- Lightweight component (~30KB)
- No complex bindings by default
- Efficient text updates via public methods
- GPU-accelerated rendering

---

This Top Navigation Bar component is production-ready and integrates seamlessly with your HMI application!
