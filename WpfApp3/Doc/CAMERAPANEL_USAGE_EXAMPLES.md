# CameraPanel - Usage Examples

## Quick Start

### Example 1: Basic Setup

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls">
	<Grid Background="#1E1E1E">
		<local:CameraPanel x:Name="CameraPanel" Margin="15"/>
	</Grid>
</Window>
```

```csharp
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		// Camera panel is ready to use with default values
	}
}
```

---

## Complete Examples

### Example 2: Update All Metrics

```csharp
public void UpdateCameraMetrics()
{
	CameraPanel.UpdateFps(30);
	CameraPanel.UpdateResolution("1920×1080");
	CameraPanel.UpdateStatus("Connected");
	CameraPanel.UpdateQuality(85);
	CameraPanel.UpdateExposure(65);
	CameraPanel.UpdateFocus(90);
	CameraPanel.UpdateWhiteBalance(75);
	CameraPanel.UpdateTimestamp(DateTime.Now);
}
```

### Example 3: Simulate Live Camera Feed

```csharp
private DispatcherTimer _cameraTimer;
private int _frameCounter = 0;

public void StartCameraSimulation()
{
	// Initialize camera panel
	CameraPanel.UpdateStatus("Connected");
	CameraPanel.UpdateResolution("1920×1080");

	// Update metrics every 33ms (30fps)
	_cameraTimer = new DispatcherTimer();
	_cameraTimer.Interval = TimeSpan.FromMilliseconds(33);
	_cameraTimer.Tick += (s, e) =>
	{
		_frameCounter++;
		CameraPanel.IncrementFrameCount();
		CameraPanel.UpdateTimestamp(DateTime.Now);

		// Simulate FPS variation
		int fps = 25 + (_frameCounter % 5);
		CameraPanel.UpdateFps(fps);

		// Simulate quality changes
		int quality = 80 + (_frameCounter % 10);
		CameraPanel.UpdateQuality(quality);
	};
	_cameraTimer.Start();
}

protected override void OnClosed(EventArgs e)
{
	_cameraTimer?.Stop();
	base.OnClosed(e);
}
```

### Example 4: Handle Capture Button

```csharp
private int _captureCount = 0;

public MainWindow()
{
	InitializeComponent();
	CameraPanel.CaptureClicked += OnCaptureBtnClick;
}

private void OnCaptureBtnClick(object sender, RoutedEventArgs e)
{
	_captureCount++;
	MessageBox.Show($"Captured image #{_captureCount}");

	// You would save the current image here
	SaveCapturedImage();
}

private void SaveCapturedImage()
{
	// Implementation to save image from CameraImageSource
	// Example:
	// var bitmap = CameraPanel.CameraImageSource as BitmapImage;
	// Save bitmap to file
}
```

### Example 5: Handle Record Button

```csharp
public MainWindow()
{
	InitializeComponent();
	CameraPanel.RecordClicked += OnRecordBtnClick;
}

private void OnRecordBtnClick(object sender, RoutedEventArgs e)
{
	if (CameraPanel.IsRecording)
	{
		MessageBox.Show("Recording started");
		StartVideoRecording();
	}
	else
	{
		MessageBox.Show("Recording stopped");
		StopVideoRecording();
	}
}

private void StartVideoRecording()
{
	// Implementation to start video recording
	Console.WriteLine("Recording video...");
}

private void StopVideoRecording()
{
	// Implementation to stop video recording
	Console.WriteLine("Recording stopped");
}
```

### Example 6: Real-time FPS Counter

```csharp
private DispatcherTimer _fpsTimer;
private int _framesSinceLastUpdate = 0;
private Stopwatch _fpsStopwatch;

public void StartFpsCounter()
{
	_fpsStopwatch = Stopwatch.StartNew();

	_fpsTimer = new DispatcherTimer();
	_fpsTimer.Interval = TimeSpan.FromSeconds(1);
	_fpsTimer.Tick += (s, e) =>
	{
		// Calculate FPS
		int fps = _framesSinceLastUpdate;
		CameraPanel.UpdateFps(fps);

		// Reset counter
		_framesSinceLastUpdate = 0;
	};
	_fpsTimer.Start();
}

public void ReportFrame()
{
	_framesSinceLastUpdate++;
	CameraPanel.IncrementFrameCount();
}
```

### Example 7: MVVM Integration

```csharp
public class CameraViewModel : ViewModelBase
{
	private ImageSource _cameraImage;
	private int _fps = 30;
	private string _resolution = "1920×1080";
	private string _status = "Disconnected";
	private int _frameCount = 0;
	private int _quality = 85;

	public ImageSource CameraImage
	{
		get => _cameraImage;
		set => SetProperty(ref _cameraImage, value);
	}

	public int Fps
	{
		get => _fps;
		set => SetProperty(ref _fps, value);
	}

	public string Resolution
	{
		get => _resolution;
		set => SetProperty(ref _resolution, value);
	}

	public string Status
	{
		get => _status;
		set => SetProperty(ref _status, value);
	}

	public int FrameCount
	{
		get => _frameCount;
		set => SetProperty(ref _frameCount, value);
	}

	public int Quality
	{
		get => _quality;
		set => SetProperty(ref _quality, value);
	}

	public async void ConnectCamera()
	{
		Status = "Connecting...";

		// Simulate connection delay
		await Task.Delay(1000);

		Status = "Connected";
		StartCameraFeed();
	}

	public void StartCameraFeed()
	{
		var timer = new DispatcherTimer();
		timer.Interval = TimeSpan.FromMilliseconds(33);
		timer.Tick += (s, e) =>
		{
			FrameCount++;
		};
		timer.Start();
	}
}

// In XAML
/*
<local:CameraPanel CameraImageSource="{Binding CameraImage}"
				   Fps="{Binding Fps}"
				   Resolution="{Binding Resolution}"
				   CameraStatus="{Binding Status}"
				   FrameCount="{Binding FrameCount}"
				   Quality="{Binding Quality}"/>
*/

// In code-behind
public partial class MainWindow : Window
{
	private CameraViewModel _viewModel;

	public MainWindow()
	{
		InitializeComponent();
		_viewModel = new CameraViewModel();
		DataContext = _viewModel;

		_viewModel.ConnectCamera();
	}
}
```

### Example 8: Adjust Camera Settings

```csharp
public class CameraSettingsControl
{
	private CameraPanel _cameraPanel;

	public CameraSettingsControl(CameraPanel cameraPanel)
	{
		_cameraPanel = cameraPanel;
	}

	public void IncreaseExposure()
	{
		var newExposure = Math.Min(100, _cameraPanel.Exposure + 5);
		_cameraPanel.UpdateExposure(newExposure);
	}

	public void DecreaseExposure()
	{
		var newExposure = Math.Max(0, _cameraPanel.Exposure - 5);
		_cameraPanel.UpdateExposure(newExposure);
	}

	public void IncreaseFocus()
	{
		var newFocus = Math.Min(100, _cameraPanel.Focus + 5);
		_cameraPanel.UpdateFocus(newFocus);
	}

	public void DecreaseFocus()
	{
		var newFocus = Math.Max(0, _cameraPanel.Focus - 5);
		_cameraPanel.UpdateFocus(newFocus);
	}

	public void AutoFocus()
	{
		_cameraPanel.UpdateFocus(90);  // Good default
	}

	public void AutoWhiteBalance()
	{
		_cameraPanel.UpdateWhiteBalance(75);  // Good default
	}
}
```

### Example 9: Camera Status Monitoring

```csharp
public class CameraMonitor
{
	private CameraPanel _cameraPanel;
	private DispatcherTimer _statusTimer;

	public CameraMonitor(CameraPanel cameraPanel)
	{
		_cameraPanel = cameraPanel;
	}

	public void StartMonitoring()
	{
		_statusTimer = new DispatcherTimer();
		_statusTimer.Interval = TimeSpan.FromSeconds(5);
		_statusTimer.Tick += (s, e) => CheckCameraStatus();
		_statusTimer.Start();
	}

	private void CheckCameraStatus()
	{
		// Simulate checking camera connection
		bool isConnected = Random.Shared.Next(0, 100) > 10;  // 90% connected

		if (isConnected)
		{
			_cameraPanel.UpdateStatus("Connected");
		}
		else
		{
			_cameraPanel.UpdateStatus("Disconnected");
		}
	}

	public void StopMonitoring()
	{
		_statusTimer?.Stop();
	}
}
```

### Example 10: Full Integration with Dashboard

```csharp
public partial class MainWindow : Window
{
	private CameraPanel _cameraPanel;
	private DispatcherTimer _updateTimer;
	private CameraMonitor _monitor;
	private int _frameCount = 0;

	public MainWindow()
	{
		InitializeComponent();
		InitializeCamera();
	}

	private void InitializeCamera()
	{
		_cameraPanel = new CameraPanel();

		// Set initial values
		_cameraPanel.UpdateFps(30);
		_cameraPanel.UpdateResolution("1920×1080");
		_cameraPanel.UpdateStatus("Connected");
		_cameraPanel.UpdateQuality(85);
		_cameraPanel.UpdateTimestamp(DateTime.Now);

		// Wire up events
		_cameraPanel.CaptureClicked += OnCapture;
		_cameraPanel.RecordClicked += OnRecord;

		// Start monitoring
		_monitor = new CameraMonitor(_cameraPanel);
		_monitor.StartMonitoring();

		// Start update timer
		_updateTimer = new DispatcherTimer();
		_updateTimer.Interval = TimeSpan.FromMilliseconds(33);  // 30fps
		_updateTimer.Tick += (s, e) =>
		{
			_frameCount++;
			_cameraPanel.IncrementFrameCount();
			_cameraPanel.UpdateTimestamp(DateTime.Now);

			// Simulate varying FPS
			int fps = 25 + (_frameCount % 6);
			_cameraPanel.UpdateFps(fps);

			// Simulate quality variations
			int quality = 80 + (_frameCount % 15);
			_cameraPanel.UpdateQuality(quality);
		};
		_updateTimer.Start();
	}

	private void OnCapture(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("Capture button clicked!");
		// Save image here
	}

	private void OnRecord(object sender, RoutedEventArgs e)
	{
		if (_cameraPanel.IsRecording)
			Console.WriteLine("Recording started");
		else
			Console.WriteLine("Recording stopped");
	}

	protected override void OnClosed(EventArgs e)
	{
		_updateTimer?.Stop();
		_monitor?.StopMonitoring();
		base.OnClosed(e);
	}
}
```

---

## Pattern Examples

### Property Binding Pattern

```xaml
<local:CameraPanel x:Name="CameraPanel"
				   CameraImageSource="{Binding Image}"
				   Fps="{Binding Fps, UpdateSourceTrigger=PropertyChanged}"
				   Resolution="{Binding Resolution}"
				   CameraStatus="{Binding Status}"
				   FrameCount="{Binding FrameCount}"
				   Quality="{Binding Quality}"/>
```

### Event Handling Pattern

```csharp
private void SetupEventHandlers()
{
	CameraPanel.CaptureClicked += (s, e) =>
	{
		HandleCapture();
	};

	CameraPanel.RecordClicked += (s, e) =>
	{
		HandleRecordToggle();
	};
}
```

### Update Timer Pattern

```csharp
private void SetupUpdateTimer(int intervalMs = 33)
{
	var timer = new DispatcherTimer 
	{ 
		Interval = TimeSpan.FromMilliseconds(intervalMs) 
	};

	timer.Tick += (s, e) =>
	{
		CameraPanel.UpdateTimestamp(DateTime.Now);
		CameraPanel.IncrementFrameCount();
	};

	timer.Start();
}
```

### Status Change Pattern

```csharp
public void UpdateCameraStatus(ConnectionStatus status)
{
	switch (status)
	{
		case ConnectionStatus.Connected:
			CameraPanel.UpdateStatus("Connected");
			CameraPanel.UpdateQuality(85);
			break;
		case ConnectionStatus.Connecting:
			CameraPanel.UpdateStatus("Connecting...");
			CameraPanel.UpdateQuality(50);
			break;
		case ConnectionStatus.Disconnected:
			CameraPanel.UpdateStatus("Disconnected");
			CameraPanel.UpdateQuality(0);
			break;
	}
}
```

---

## Best Practices

✅ **DO: Clean up resources on exit**
```csharp
protected override void OnClosed(EventArgs e)
{
	_updateTimer?.Stop();
	_cameraService?.Dispose();
	base.OnClosed(e);
}
```

✅ **DO: Use MVVM for complex logic**
```csharp
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		DataContext = new CameraViewModel();
	}
}
```

✅ **DO: Handle events appropriately**
```csharp
CameraPanel.CaptureClicked += (s, e) =>
{
	try { SaveImage(); }
	catch (Exception ex) { ShowError(ex.Message); }
};
```

❌ **DON'T: Update UI from background thread**
```csharp
// Wrong - will crash
new Thread(() => CameraPanel.UpdateFps(30)).Start();

// Correct
Dispatcher.Invoke(() => CameraPanel.UpdateFps(30));
```

❌ **DON'T: Leak resources**
```csharp
// Don't forget to dispose
_timer?.Stop();
_camera?.Dispose();
```

---

This comprehensive set of examples covers all common scenarios for using the CameraPanel UserControl!
