# CameraPanel UserControl - Documentation

## Overview

A professional Material Design camera panel for WPF applications. Displays live camera feed with real-time FPS, resolution, capture/record controls, and comprehensive camera metrics.

---

## Features

✅ **Camera Display**
- Large image area for live feed
- Placeholder when no image
- Image stretching options (UniformToFill)
- Corner overlays with key info

✅ **Real-time Metrics**
- FPS counter (frames per second)
- Resolution display
- Frame count
- Timestamp

✅ **Camera Controls**
- Capture button (photograph)
- Record button (start/stop video)
- Status indicators
- Quality display

✅ **Camera Settings Display**
- Exposure indicator
- Focus indicator
- White balance indicator
- All as progress bars

✅ **Status Information**
- Connection status (Connected/Disconnected)
- Quality percentage
- Live timestamp and date
- Recording state

✅ **Material Design**
- Dark theme
- Professional styling
- Color-coded elements
- Smooth interactions

---

## File Structure

```
WpfApp3/Controls/
├── CameraPanel.xaml           # UI Layout
└── CameraPanel.xaml.cs        # Code-behind with properties and methods
```

---

## Basic Usage

### Step 1: Add Namespace

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls">
```

### Step 2: Place in Your Layout

```xaml
<Grid>
	<local:CameraPanel x:Name="CameraPanel"/>
</Grid>
```

### Step 3: Set Properties (Optional)

```csharp
CameraPanel.UpdateFps(30);
CameraPanel.UpdateResolution("1920×1080");
CameraPanel.UpdateStatus("Connected");
```

### Step 4: Handle Events (Optional)

```csharp
CameraPanel.CaptureClicked += (s, e) => OnCapture();
CameraPanel.RecordClicked += (s, e) => OnRecordToggle();
```

---

## Layout Breakdown

```
┌─────────────────────────────────────────────────────────────┐
│ HEADER (60px)                                               │
│ 📷 Camera Feed │ ● Connected │ FPS: 30 │ Res: 1920×1080 │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│                 CAMERA IMAGE AREA                            │
│                 (Main Display)                               │
│                                                               │
│  Live Stream ┌─────────────────────────┐ 14:32:45           │
│  Battery ... │                         │ 2024-01-15         │
│              │      (Image Display)    │                     │
│              │                         │ Quality            │
│  FPS: 30     │                         │ ████████░ 85%     │
│  Res: 1920×  │                         │                     │
│  1080        └─────────────────────────┘                     │
│                                                               │
├─────────────────────────────────────────────────────────────┤
│ CONTROLS (50px)                                             │
│ [📸 Capture] [⏹ Record] │ ● Connected │ Frames: 0 │ ...   │
│                                   Exposure: ████░           │
│                                   Focus: ██████░             │
│                                   WhiteB: ████░              │
└─────────────────────────────────────────────────────────────┘
```

---

## Dependency Properties

### CameraImageSource
Binds to the camera image displayed in the control.
```csharp
CameraPanel.CameraImageSource = myBitmapImage;
```

### Fps
Current frames per second value.
```csharp
CameraPanel.Fps = 30;  // Updates header and overlay
```

### Resolution
Current camera resolution (text).
```csharp
CameraPanel.Resolution = "1920×1080";
```

### CameraStatus
Connection status ("Connected" or "Disconnected").
```csharp
CameraPanel.CameraStatus = "Connected";
```

### IsRecording
Recording state (true/false).
```csharp
CameraPanel.IsRecording = true;  // Updates button appearance
```

### FrameCount
Total frames captured.
```csharp
CameraPanel.FrameCount = 1500;
```

### Quality
Image quality percentage (0-100).
```csharp
CameraPanel.Quality = 85;
```

### Exposure
Exposure level (0-100).
```csharp
CameraPanel.Exposure = 65;
```

### Focus
Focus level (0-100).
```csharp
CameraPanel.Focus = 90;
```

### WhiteBalance
White balance level (0-100).
```csharp
CameraPanel.WhiteBalance = 75;
```

---

## Public Methods

### UpdateCameraImage(ImageSource)
Updates the displayed camera image.
```csharp
CameraPanel.UpdateCameraImage(bitmapImage);
```

### UpdateFps(int)
Updates FPS display.
```csharp
CameraPanel.UpdateFps(30);
```

### UpdateResolution(string)
Updates resolution display.
```csharp
CameraPanel.UpdateResolution("1920×1080");
```

### UpdateStatus(string)
Updates camera status ("Connected" or "Disconnected").
```csharp
CameraPanel.UpdateStatus("Connected");
```

### UpdateQuality(int)
Updates quality indicator (0-100).
```csharp
CameraPanel.UpdateQuality(85);
```

### UpdateExposure(int)
Updates exposure value (0-100).
```csharp
CameraPanel.UpdateExposure(65);
```

### UpdateFocus(int)
Updates focus value (0-100).
```csharp
CameraPanel.UpdateFocus(90);
```

### UpdateWhiteBalance(int)
Updates white balance value (0-100).
```csharp
CameraPanel.UpdateWhiteBalance(75);
```

### IncrementFrameCount()
Increments the frame counter.
```csharp
CameraPanel.IncrementFrameCount();
```

### ResetFrameCount()
Resets frame counter to 0.
```csharp
CameraPanel.ResetFrameCount();
```

### SetRecording(bool)
Sets the recording state.
```csharp
CameraPanel.SetRecording(true);  // Start recording
```

### GetRecordingState()
Gets current recording state.
```csharp
bool isRecording = CameraPanel.GetRecordingState();
```

### UpdateTimestamp(DateTime)
Updates the timestamp display.
```csharp
CameraPanel.UpdateTimestamp(DateTime.Now);
```

---

## Events

### CaptureClicked
Raised when the Capture button is clicked.
```csharp
CameraPanel.CaptureClicked += (s, e) =>
{
	Console.WriteLine("Capture button clicked!");
};
```

### RecordClicked
Raised when the Record button is clicked.
```csharp
CameraPanel.RecordClicked += (s, e) =>
{
	Console.WriteLine($"Recording: {CameraPanel.IsRecording}");
};
```

---

## Color Scheme

| Element | Color | Hex Code |
|---------|-------|----------|
| Background | Dark Gray | #2D2D30 |
| Header | Very Dark | #1E1E1E |
| Content | Darker Gray | #252526 |
| Border | Subtle | #3E3E42 |
| Status Online | Teal | #4EC9B0 |
| Button Primary | Blue | #007ACC |
| Button Hover | Darker Blue | #1084D7 |
| Button Pressed | Very Dark Blue | #005A9E |
| Recording | Red | #D13438 |
| Text Primary | White | #FFFFFF |
| Text Secondary | Light Gray | #CCCCCC |
| Text Tertiary | Dim Gray | #858585 |
| Exposure | Gold | #FFD700 |
| Focus | Blue | #007ACC |
| White Balance | Teal | #4EC9B0 |

---

## Complete Integration Example

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:local="clr-namespace:WpfApp3.Controls"
		Title="Battery Inspection System"
		Height="800"
		Width="1200">
	<Grid Background="#1E1E1E">
		<local:CameraPanel x:Name="CameraPanel"
						  Margin="15"
						  CaptureClicked="OnCapture"
						  RecordClicked="OnRecord"/>
	</Grid>
</Window>
```

```csharp
public partial class MainWindow : Window
{
	private DispatcherTimer _updateTimer;
	private int _frameCount = 0;

	public MainWindow()
	{
		InitializeComponent();
		InitializeCamera();
	}

	private void InitializeCamera()
	{
		// Set initial values
		CameraPanel.UpdateFps(30);
		CameraPanel.UpdateResolution("1920×1080");
		CameraPanel.UpdateStatus("Connected");
		CameraPanel.UpdateQuality(85);
		CameraPanel.UpdateTimestamp(DateTime.Now);

		// Start update timer
		_updateTimer = new DispatcherTimer();
		_updateTimer.Interval = TimeSpan.FromMilliseconds(100);
		_updateTimer.Tick += (s, e) =>
		{
			CameraPanel.IncrementFrameCount();
			CameraPanel.UpdateTimestamp(DateTime.Now);
		};
		_updateTimer.Start();
	}

	private void OnCapture(object sender, RoutedEventArgs e)
	{
		MessageBox.Show("Frame captured!");
	}

	private void OnRecord(object sender, RoutedEventArgs e)
	{
		if (CameraPanel.IsRecording)
			MessageBox.Show("Recording started");
		else
			MessageBox.Show("Recording stopped");
	}

	protected override void OnClosed(EventArgs e)
	{
		_updateTimer?.Stop();
		base.OnClosed(e);
	}
}
```

---

## Binding with MVVM

### ViewModel

```csharp
public class CameraViewModel : ViewModelBase
{
	private ImageSource _cameraImage;
	private int _fps = 30;
	private string _resolution = "1920×1080";
	private string _status = "Connected";
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

	public void IncrementFrame()
	{
		FrameCount++;
	}
}
```

### XAML Binding

```xaml
<local:CameraPanel x:Name="CameraPanel"
				   CameraImageSource="{Binding CameraImage}"
				   Fps="{Binding Fps}"
				   Resolution="{Binding Resolution}"
				   CameraStatus="{Binding Status}"
				   FrameCount="{Binding FrameCount}"
				   Quality="{Binding Quality}"/>
```

---

## Dimensions & Spacing

### Component Size
- Minimum Width: 600px
- Minimum Height: 400px
- Recommended: 800px x 600px or larger

### Header Height
- 60px (with content)

### Control Panel Height
- 50px

### Margins
- Global: 15px (adjustable)
- Section: 10px
- Component: 5px

### Overlay Position
- Top-Left: Info (Live Stream, Battery Inspection)
- Top-Right: Timestamp
- Bottom-Left: FPS/Resolution
- Bottom-Right: Quality indicator

---

## Styling Customization

### Change Button Colors

Edit CameraPanel.xaml:

```xaml
<!-- Primary Button (Capture) -->
<Button Background="#007ACC">  <!-- Change this -->

<!-- Record Button -->
<Button Background="#6C757D">  <!-- Change this -->
```

### Change Theme Colors

Edit hex codes in XAML:

```xaml
<!-- Background -->
Background="#2D2D30"  <!-- Change this -->

<!-- Text -->
Foreground="#CCCCCC"  <!-- Change this -->
```

### Adjust Overlay Opacity

```xaml
<!-- Overlay background -->
Background="#000000" Opacity="0.6"  <!-- Change opacity value -->
```

---

## Performance Considerations

- **Lightweight**: ~30KB XAML
- **Image Rendering**: Uses native WPF Image control
- **Update Frequency**: Handle timers at 30-60fps
- **Memory**: Depends on image resolution
  - 1920×1080 BGRA: ~8MB per frame
  - Consider memory usage for retained history

---

## Best Practices

✅ **DO: Update timestamp regularly**
```csharp
CameraPanel.UpdateTimestamp(DateTime.Now);
```

✅ **DO: Increment frame count on each frame**
```csharp
CameraPanel.IncrementFrameCount();
```

✅ **DO: Handle capture and record events**
```csharp
CameraPanel.CaptureClicked += OnCapture;
CameraPanel.RecordClicked += OnRecord;
```

✅ **DO: Use DispatcherTimer for updates**
```csharp
var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
timer.Tick += (s, e) => CameraPanel.UpdateTimestamp(DateTime.Now);
timer.Start();
```

❌ **DON'T: Flood with image updates**
- Update image on frame received, not every tick
- Batch updates when possible

❌ **DON'T: Block UI thread**
- Use async/await for heavy operations
- Load images on background thread

❌ **DON'T: Forget to dispose resources**
```csharp
// Clean up on window close
protected override void OnClosed(EventArgs e)
{
	_updateTimer?.Stop();
	_cameraService?.Dispose();
	base.OnClosed(e);
}
```

---

## Troubleshooting

**Q: Image not displaying**
A: Ensure CameraImageSource is set to a valid BitmapImage

**Q: FPS not updating**
A: Call UpdateFps() or bind to Fps property

**Q: Buttons not responding**
A: Ensure events are properly connected in OnInitialized

**Q: Timestamp not updating**
A: Call UpdateTimestamp() regularly in a timer

**Q: Memory usage high**
A: Consider lower resolution or frame rate
- Use image compression
- Implement frame buffering limit

---

## Future Enhancement Ideas

- [ ] Zoom controls (in/out)
- [ ] Brightness/Contrast sliders
- [ ] Snapshot history panel
- [ ] Recording duration display
- [ ] Motion detection visualization
- [ ] Face/Object detection overlay
- [ ] Histogram display
- [ ] Picture-in-picture mode

---

This CameraPanel is production-ready and fully integrated into your Battery Inspection System!
