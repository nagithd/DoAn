# Sidebar Component - Usage Examples

## Quick Start

### Step 1: Add Namespace to Your Window

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls">
	...
</Window>
```

### Step 2: Place Sidebar in Your Layout

```xaml
<Grid>
	<Grid.ColumnDefinitions>
		<ColumnDefinition Width="280"/>  <!-- Sidebar width -->
		<ColumnDefinition Width="*"/>    <!-- Main content -->
	</Grid.ColumnDefinitions>

	<!-- Sidebar Component -->
	<local:Sidebar Grid.Column="0" ItemSelected="OnSidebarItemSelected"/>

	<!-- Main Content Area -->
	<ContentPresenter Grid.Column="1" x:Name="ContentArea"/>
</Grid>
```

### Step 3: Handle Selection in Code-Behind

```csharp
private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = e.OriginalSource?.ToString() ?? "Unknown";
	MessageBox.Show($"Selected: {selectedItem}");
}
```

---

## Complete MainWindow.xaml Integration

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:local="clr-namespace:WpfApp3.Controls"
		Title="Battery Inspection System" Height="900" Width="1400">
	<Grid Background="#1E1E1E">
		<Grid.ColumnDefinitions>
			<ColumnDefinition Width="280"/>
			<ColumnDefinition Width="*"/>
		</Grid.ColumnDefinitions>

		<!-- Sidebar -->
		<local:Sidebar Grid.Column="0" ItemSelected="OnNavigationItemSelected"/>

		<!-- Main Content Area -->
		<Grid Grid.Column="1">
			<!-- Your main content here -->
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
	}

	private void OnNavigationItemSelected(object sender, RoutedEventArgs e)
	{
		string selectedModule = e.OriginalSource?.ToString() ?? "Unknown";

		switch (selectedModule)
		{
			case "Dashboard":
				// Load Dashboard view
				break;
			case "Camera":
				// Load Camera view
				break;
			case "Robot":
				// Load Robot view
				break;
			case "Statistics":
				// Load Statistics view
				break;
			case "History":
				// Load History view
				break;
			case "Settings":
				// Load Settings view
				break;
		}
	}
}
```

---

## View Switching Pattern

### Using ContentControl for Dynamic Views

```xaml
<Grid Grid.Column="1">
	<Grid.RowDefinitions>
		<RowDefinition Height="Auto"/>
		<RowDefinition Height="*"/>
	</Grid.RowDefinitions>

	<!-- Header -->
	<Border Grid.Row="0" Background="#252526" Padding="20">
		<TextBlock x:Name="PageTitle" FontSize="18" FontWeight="Bold" Foreground="#FFFFFF"/>
	</Border>

	<!-- Content -->
	<ContentControl Grid.Row="1" x:Name="ContentArea" Background="#1E1E1E"/>
</Grid>
```

```csharp
private void OnNavigationItemSelected(object sender, RoutedEventArgs e)
{
	string selectedModule = e.OriginalSource?.ToString() ?? "Unknown";

	PageTitle.Text = selectedModule;

	switch (selectedModule)
	{
		case "Dashboard":
			ContentArea.Content = new Views.DashboardView();
			break;
		case "Camera":
			ContentArea.Content = new Views.CameraView();
			break;
		case "Robot":
			ContentArea.Content = new Views.RobotView();
			break;
		case "Statistics":
			ContentArea.Content = new Views.StatisticsView();
			break;
		case "History":
			ContentArea.Content = new Views.HistoryView();
			break;
		case "Settings":
			ContentArea.Content = new Views.SettingsView();
			break;
	}
}
```

---

## Programmatic Item Selection

```csharp
public partial class MainWindow : Window
{
	private Sidebar sidebar;

	public MainWindow()
	{
		InitializeComponent();
		sidebar = (Sidebar)FindName("Sidebar");
	}

	private void SomeMethod()
	{
		// Select an item programmatically
		sidebar.SelectItemByName("Camera");
	}
}
```

---

## MVVM Pattern Integration

### Create a Navigation Service

```csharp
public interface INavigationService
{
	void NavigateTo(string viewName);
	string CurrentView { get; }
}

public class NavigationService : INavigationService
{
	private string _currentView = "Dashboard";

	public string CurrentView => _currentView;

	public void NavigateTo(string viewName)
	{
		_currentView = viewName;
	}
}
```

### Create a MainViewModel

```csharp
using System.Windows.Input;

public class MainViewModel : ViewModelBase
{
	private readonly INavigationService _navigationService;
	private string _currentView = "Dashboard";

	public string CurrentView
	{
		get => _currentView;
		set => SetProperty(ref _currentView, value);
	}

	private ICommand _navigateCommand;
	public ICommand NavigateCommand => _navigateCommand ??= 
		new RelayCommand<string>(Navigate);

	public MainViewModel(INavigationService navigationService)
	{
		_navigationService = navigationService;
	}

	private void Navigate(string viewName)
	{
		_navigationService.NavigateTo(viewName);
		CurrentView = viewName;
	}
}
```

### Bind in MainWindow.xaml

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns:local="clr-namespace:WpfApp3.Controls"
		Background="#1E1E1E">
	<Grid>
		<Grid.ColumnDefinitions>
			<ColumnDefinition Width="280"/>
			<ColumnDefinition Width="*"/>
		</Grid.ColumnDefinitions>

		<!-- Sidebar with routed event binding -->
		<local:Sidebar Grid.Column="0" ItemSelected="OnSidebarItemSelected"/>

		<!-- Main Content -->
		<TextBlock Grid.Column="1" 
				   Text="{Binding CurrentView}" 
				   Foreground="White" 
				   FontSize="20"/>
	</Grid>
</Window>
```

---

## Event Handling Examples

### Toast Notification on Selection

```csharp
private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = e.OriginalSource?.ToString() ?? "Unknown";
	ShowNotification($"Navigating to {selectedItem}...");
}

private void ShowNotification(string message)
{
	// Your notification implementation
	MessageBox.Show(message, "Navigation");
}
```

### Track Selection History

```csharp
private Stack<string> _navigationHistory = new Stack<string>();

private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = e.OriginalSource?.ToString() ?? "Unknown";
	_navigationHistory.Push(selectedItem);
	LoadView(selectedItem);
}

private string GoBack()
{
	if (_navigationHistory.Count > 1)
	{
		_navigationHistory.Pop();
		return _navigationHistory.Peek();
	}
	return null;
}
```

### Log Navigation

```csharp
private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = e.OriginalSource?.ToString() ?? "Unknown";

	// Log navigation
	LoggerHelper.Info($"User navigated to {selectedItem} at {DateTime.Now}");

	LoadView(selectedItem);
}
```

---

## Styling Customization

### Change Selected Color

Edit **Sidebar.xaml.cs** SelectItem method:

```csharp
// Change from blue (#1F71B8) to green (#4EC9B0)
button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C10"));
```

### Change Hover Color

Edit **Sidebar.xaml**, in SidebarItemButtonStyle:

```xaml
<Trigger Property="IsMouseOver" Value="True">
	<Setter TargetName="HoverBorder" Property="Background" Value="#FF6B6B"/>
	<Setter Property="Foreground" Value="#FFFFFF"/>
</Trigger>
```

---

## Advanced Features

### Disable Specific Items

```csharp
public void DisableItem(string itemName)
{
	Button button = itemName switch
	{
		"Camera" => CameraButton,
		"Robot" => RobotButton,
		_ => null
	};

	if (button != null)
	{
		button.IsEnabled = false;
		button.Opacity = 0.5;
	}
}
```

### Add Item Count Badge

Modify Sidebar.xaml to add TextBlock with count:

```xaml
<Button Content="📊 Dashboard (5 alerts)"/>
```

### Dynamic Button Content

```csharp
public void UpdateItemContent(string itemName, string newContent)
{
	Button button = itemName switch
	{
		"Camera" => CameraButton,
		"Statistics" => StatisticsButton,
		_ => null
	};

	if (button != null)
	{
		button.Content = newContent;
	}
}
```

---

## Common Patterns

### Navigate and Update Header

```csharp
private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = e.OriginalSource?.ToString() ?? "Unknown";

	// Update header
	PageTitle.Text = $"{GetIcon(selectedItem)} {selectedItem}";
	PageTitle.Foreground = new SolidColorBrush(GetIconColor(selectedItem));

	// Load view
	LoadView(selectedItem);
}

private string GetIcon(string item) => item switch
{
	"Dashboard" => "📊",
	"Camera" => "📷",
	"Robot" => "🦾",
	"Statistics" => "📈",
	"History" => "📜",
	"Settings" => "⚙",
	_ => "?"
};

private Color GetIconColor(string item) => item switch
{
	"Dashboard" => Colors.Cyan,
	"Camera" => Colors.LimeGreen,
	"Robot" => Colors.Orange,
	"Statistics" => Colors.Yellow,
	"History" => Colors.LightBlue,
	"Settings" => Colors.Purple,
	_ => Colors.White
};
```

### Loading Indicator

```csharp
private async void LoadView(string viewName)
{
	LoadingIndicator.Visibility = Visibility.Visible;

	try
	{
		await Task.Delay(500); // Simulate load

		switch (viewName)
		{
			case "Dashboard":
				ContentArea.Content = new DashboardView();
				break;
			// ... other cases
		}
	}
	finally
	{
		LoadingIndicator.Visibility = Visibility.Collapsed;
	}
}
```

---

## Best Practices

1. **Always handle the ItemSelected event** - Don't rely on button clicks alone
2. **Use consistent naming** - Keep item names matching your view names
3. **Update UI state** - Sync header, breadcrumbs, etc. with sidebar selection
4. **Log navigation** - Helpful for debugging and user tracking
5. **Handle errors gracefully** - Wrap view loading in try-catch
6. **Test all paths** - Ensure each sidebar item loads correctly

---

## Troubleshooting

**Q: Sidebar doesn't appear**
A: Ensure namespace is correct: `xmlns:local="clr-namespace:WpfApp3.Controls"`

**Q: Selection doesn't highlight**
A: Verify ItemSelected event handler is attached and called

**Q: Colors not changing**
A: Check ColorConverter syntax and hex codes are valid

**Q: Items not responding to clicks**
A: Ensure Click handlers are properly named and connected in XAML

---

This sidebar component is ready for immediate integration into your application!
