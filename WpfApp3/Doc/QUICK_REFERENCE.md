# Quick Reference: MVVM Architecture Patterns

## Pattern 1: Creating a New Feature

### Step 1: Create the View (Views/FeatureName.xaml)
```xaml
<UserControl x:Class="WpfApp3.Views.FeatureNameView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 mc:Ignorable="d" d:DesignHeight="300" d:DesignWidth="300">
	<Grid>
		<!-- Bindings to ViewModel properties -->
	</Grid>
</UserControl>
```

### Step 2: Create the ViewModel (ViewModels/FeatureNameViewModel.cs)
```csharp
public class FeatureNameViewModel : ViewModelBase
{
	private string _propertyName;

	public string PropertyName
	{
		get => _propertyName;
		set => SetProperty(ref _propertyName, value);
	}

	private ICommand _buttonCommand;
	public ICommand ButtonCommand => _buttonCommand ??= new RelayCommand(OnButtonClick);

	private void OnButtonClick()
	{
		// Handle action
	}
}
```

### Step 3: Create the Service Interface (Services/IFeatureService.cs)
```csharp
public interface IFeatureService
{
	Task<ResultType> PerformOperationAsync();
}

public class FeatureService : IFeatureService
{
	public async Task<ResultType> PerformOperationAsync()
	{
		// Implementation
	}
}
```

### Step 4: Create Models (Models/FeatureName.cs)
```csharp
public class FeatureName : ModelBase
{
	public string PropertyName { get; set; }
	public int Value { get; set; }
}
```

---

## Pattern 2: Data Binding in XAML

### Property Binding
```xaml
<TextBlock Text="{Binding PropertyName}" />
```

### Command Binding
```xaml
<Button Command="{Binding ButtonCommand}" Content="Click Me" />
```

### Converter Binding
```xaml
<TextBlock Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisibilityConverter}}" />
```

### Two-Way Binding (for TextBox, ComboBox, etc.)
```xaml
<TextBox Text="{Binding PropertyName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

---

## Pattern 3: Dependency Injection Setup

### In ServiceProvider.cs
```csharp
public static class ServiceProvider
{
	public static IServiceProvider BuildServiceProvider()
	{
		var services = new ServiceCollection();

		// Register Services
		services.AddSingleton<ICameraService, CameraService>();
		services.AddSingleton<IConveyorService, ConveyorService>();

		// Register ViewModels
		services.AddTransient<CameraViewModel>();
		services.AddTransient<ConveyorViewModel>();

		return services.BuildServiceProvider();
	}
}
```

### In App.xaml.cs or MainViewModel
```csharp
var serviceProvider = ServiceProvider.BuildServiceProvider();
var cameraService = serviceProvider.GetRequiredService<ICameraService>();
```

---

## Pattern 4: Implementing INotifyPropertyChanged in ViewModels

### Using SetProperty Helper (Recommended)
```csharp
private string _status;

public string Status
{
	get => _status;
	set => SetProperty(ref _status, value);  // Automatically calls OnPropertyChanged
}
```

### Manual Implementation (Alternative)
```csharp
private string _status;

public string Status
{
	get => _status;
	set
	{
		if (_status != value)
		{
			_status = value;
			OnPropertyChanged(nameof(Status));
		}
	}
}
```

---

## Pattern 5: Creating a Custom Control

### Control XAML (Controls/CustomControl.xaml)
```xaml
<UserControl x:Class="WpfApp3.Controls.CustomControl"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<Grid>
		<Ellipse Fill="Red" Width="50" Height="50" />
	</Grid>
</UserControl>
```

### Control Code-Behind (Controls/CustomControl.xaml.cs)
```csharp
public partial class CustomControl : UserControl
{
	public static readonly DependencyProperty StatusProperty =
		DependencyProperty.Register("Status", typeof(string), typeof(CustomControl));

	public string Status
	{
		get => (string)GetValue(StatusProperty);
		set => SetValue(StatusProperty, value);
	}

	public CustomControl()
	{
		InitializeComponent();
	}
}
```

### Using the Control in a View
```xaml
<local:CustomControl Status="{Binding CurrentStatus}" />
```

---

## Pattern 6: Creating a Value Converter

### In Converters/MyConverter.cs
```csharp
public class MyConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is bool b)
			return b ? Visibility.Visible : Visibility.Collapsed;
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
```

### Register in Resources/Converters.xaml
```xaml
<ResourceDictionary xmlns="...">
	<local:MyConverter x:Key="MyConverter" />
</ResourceDictionary>
```

### Use in View
```xaml
<TextBlock Visibility="{Binding IsActive, Converter={StaticResource MyConverter}}" />
```

---

## Pattern 7: Async Operations in ViewModels

### With Loading State
```csharp
private bool _isLoading;
public bool IsLoading
{
	get => _isLoading;
	set => SetProperty(ref _isLoading, value);
}

private async void OnLoadDataCommand()
{
	IsLoading = true;
	try
	{
		var data = await _service.GetDataAsync();
		// Update properties
	}
	finally
	{
		IsLoading = false;
	}
}
```

### In XAML (Disable button during loading)
```xaml
<Button Command="{Binding LoadDataCommand}" 
		IsEnabled="{Binding IsLoading, Converter={StaticResource InverseBoolConverter}}" />
```

---

## Pattern 8: Observable Collections

### In ViewModel
```csharp
public ObservableCollection<BatteryInspection> Inspections { get; } = 
	new ObservableCollection<BatteryInspection>();

private async void OnLoadInspections()
{
	var items = await _service.GetInspectionsAsync();
	Inspections.Clear();
	foreach (var item in items)
		Inspections.Add(item);
}
```

### In XAML
```xaml
<ItemsControl ItemsSource="{Binding Inspections}">
	<ItemsControl.ItemTemplate>
		<DataTemplate>
			<TextBlock Text="{Binding Name}" />
		</DataTemplate>
	</ItemsControl.ItemTemplate>
</ItemsControl>
```

---

## Pattern 9: Communication Between ViewModels

### Using Events
```csharp
public class EventMediator
{
	public event EventHandler<DataEventArgs> DataChanged;

	public void RaiseDataChanged(object data)
	{
		DataChanged?.Invoke(this, new DataEventArgs { Data = data });
	}
}

// In ViewModel
public CameraViewModel(EventMediator mediator)
{
	mediator.DataChanged += OnDataChanged;
}
```

### Using Service (Recommended)
```csharp
// Service acts as data provider
public interface IDataService
{
	Task<Data> GetCurrentDataAsync();
}

// Both ViewModels use same service
private readonly IDataService _dataService;
```

---

## Common Commands Implementation

### Simple RelayCommand (add to Helpers/)
```csharp
public class RelayCommand : ICommand
{
	private readonly Action _execute;
	private readonly Func<bool> _canExecute;

	public event EventHandler CanExecuteChanged;

	public RelayCommand(Action execute, Func<bool> canExecute = null)
	{
		_execute = execute;
		_canExecute = canExecute;
	}

	public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
	public void Execute(object parameter) => _execute();
}
```

---

## Resource Access Patterns

### Access Color Resource
```xaml
<SolidColorBrush Color="{DynamicResource PrimaryColor}" />
```

### Access Style Resource
```xaml
<Button Style="{StaticResource ButtonStyle}" />
```

### Access Converter Resource
```xaml
<TextBlock Text="{Binding Value, Converter={StaticResource PercentageConverter}}" />
```

---

## Validation Pattern

### In ViewModel with IDataErrorInfo
```csharp
public string this[string columnName]
{
	get
	{
		switch (columnName)
		{
			case nameof(PropertyName):
				return string.IsNullOrEmpty(PropertyName) ? "Required" : "";
			default:
				return "";
		}
	}
}
```

### In XAML
```xaml
<TextBox Text="{Binding PropertyName, ValidatesOnDataErrors=True}" />
```

---

This quick reference provides the core patterns used in your MVVM architecture!
