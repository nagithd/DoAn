# Sidebar UserControl - Material Design Navigation

## Overview

A reusable, Material Design-styled sidebar component for WPF applications. Features smooth animations, visual selection highlighting, and built-in Material Design aesthetics.

---

## Features

✅ **Material Design Styling**
- Dark theme optimized for industrial applications
- Smooth hover transitions with opacity animations
- Color-coded accent borders (blue for selection)
- Professional typography and spacing

✅ **Selection Highlighting**
- Visual feedback with color change and border highlighting
- Bold font weight for selected items
- Automatic deselection of previous item
- Dashboard is selected by default

✅ **Pre-configured Items**
- Dashboard (📊)
- Camera (📷)
- Robot (🦾)
- Statistics (📈)
- History (📜)
- Settings (⚙)
- Documentation (📖)
- About (❓)

✅ **Reusable Component**
- Can be embedded in any WPF Window
- Works with or without MVVM patterns
- Event-based selection notification
- Programmatic item selection support

---

## File Structure

```
WpfApp3/
├── Controls/
│   ├── Sidebar.xaml              # XAML Layout
│   └── Sidebar.xaml.cs           # Code-behind with selection logic
└── Models/
	└── SidebarItem.cs            # Data model for MVVM binding (optional)
```

---

## Usage

### Basic Usage in XAML

```xaml
<Window x:Class="WpfApp3.MainWindow"
		xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:local="clr-namespace:WpfApp3.Controls">

	<Grid>
		<Grid.ColumnDefinitions>
			<ColumnDefinition Width="280"/>  <!-- Sidebar width -->
			<ColumnDefinition Width="*"/>    <!-- Main content -->
		</Grid.ColumnDefinitions>

		<!-- Sidebar Component -->
		<local:Sidebar Grid.Column="0" ItemSelected="OnSidebarItemSelected"/>

		<!-- Main Content Area -->
		<ContentPresenter Grid.Column="1"/>
	</Grid>
</Window>
```

### Code-Behind Event Handler

```csharp
private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = e.OriginalSource?.ToString() ?? "Unknown";

	switch (selectedItem)
	{
		case "Dashboard":
			// Show dashboard view
			break;
		case "Camera":
			// Show camera view
			break;
		case "Robot":
			// Show robot view
			break;
		case "Statistics":
			// Show statistics view
			break;
		case "History":
			// Show history view
			break;
		case "Settings":
			// Show settings view
			break;
	}
}
```

### Programmatic Selection

```csharp
// Get the sidebar from your window
var sidebar = (Sidebar)FindName("SidebarControl");

// Select item by name
sidebar.SelectItemByName("Camera");
```

---

## Material Design Implementation

### Color Scheme

| Element | Color | Hex Code | Purpose |
|---------|-------|----------|---------|
| Background | Dark Gray | #2D2D30 | Main sidebar background |
| Header | Darker Gray | #252526 | Header background |
| Selected Item | Blue | #1F71B8 | Active item highlight |
| Selected Border | Blue | #0E639C | Active item accent |
| Hover Background | Medium Gray | #3E3E42 | Hover state |
| Text - Normal | Light Gray | #CCCCCC | Standard text |
| Text - Selected | White | #FFFFFF | Selected text |
| Text - Disabled | Dim Gray | #858585 | Disabled/secondary text |
| Accent | Blue | #007ACC | Interactive elements |

### Animation Effects

#### Hover Animation
```
1. Background transitions to #3E3E42 (150ms)
2. Text color transitions to #FFFFFF (200ms)
3. Left border shows blue accent (#007ACC)
```

#### Selection Animation
```
1. Background becomes #1F71B8 (solid, no animation)
2. Border becomes #0E639C (solid)
3. Font weight becomes Bold
4. Smooth transition from previous selection
```

### Typography

- **Header Font:** Segoe UI, 14px, Bold
- **Item Font:** Segoe UI, 13px, Normal/Bold (when selected)
- **Section Label:** Segoe UI, 11px, Bold, Opacity 0.7

### Spacing

- **Item Height:** 50px
- **Item Padding:** 20px (left/right), 15px (top/bottom)
- **Item Margin:** 2px (top/bottom for separation)
- **Header Padding:** 20px (left/right), 15px (top/bottom)
- **Section Margin:** 20px (top), 5px (bottom)

---

## Component Architecture

### XAML Structure

```
UserControl (Sidebar)
├── Grid (Main container)
│   ├── Row 0: Border (Header)
│   │   └── StackPanel (Header content)
│   │       ├── TextBlock ("☰")
│   │       └── TextBlock ("Navigation")
│   └── Row 1: ScrollViewer
│       └── StackPanel (Items container)
│           ├── Button (Dashboard - selected by default)
│           ├── Button (Camera)
│           ├── Button (Robot)
│           ├── Button (Statistics)
│           ├── Button (History)
│           ├── Button (Settings)
│           ├── Separator
│           ├── TextBlock (SYSTEM section header)
│           ├── Button (Documentation)
│           ├── Button (About)
│           └── StackPanel (Spacer)
```

### Code-Behind Logic

**SelectItem(Button button):**
- Deselects previous button (resets colors, border, font weight)
- Applies selected styling to new button
- Raises ItemSelected event with button tag

**OnXxxxxClick(RoutedEventArgs e):**
- Calls SelectItem() for corresponding button
- Updates UI state

**SelectItemByName(string itemName):**
- Allows programmatic selection
- Uses switch expression to find button by name
- Calls SelectItem() if button found

---

## Customization

### Adding New Items

Edit **Sidebar.xaml**, add new Button in StackPanel:

```xaml
<Button x:Name="YourItemButton"
		Content="🔧 Your Item"
		Style="{StaticResource SidebarItemButtonStyle}"
		Click="OnYourItemClick"
		Tag="YourItem"/>
```

Edit **Sidebar.xaml.cs**, add click handler:

```csharp
private void OnYourItemClick(object sender, RoutedEventArgs e)
{
	SelectItem(YourItemButton);
}
```

Update **SelectItemByName()** switch expression:

```csharp
public void SelectItemByName(string itemName)
{
	var button = itemName switch
	{
		// ... existing cases
		"YourItem" => YourItemButton,
		_ => null
	};
	// ... rest of method
}
```

### Changing Colors

Edit **Sidebar.xaml.cs**, in SelectItem() method:

```csharp
private void SelectItem(Button button)
{
	// Change these color hex codes to your preference
	button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#YourColor"));
	button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#YourAccent"));
	// ... rest of method
}
```

### Changing Item Width

Modify the desired width when embedding in parent:

```xaml
<local:Sidebar Grid.Column="0" Width="320"/>  <!-- Change from default 280 -->
```

---

## MVVM Integration (Optional)

For MVVM patterns, use **SidebarItem** model class:

```csharp
public class SidebarViewModel : ViewModelBase
{
	public ObservableCollection<SidebarItem> Items { get; }

	public SidebarViewModel()
	{
		Items = new ObservableCollection<SidebarItem>
		{
			new SidebarItem("Dashboard", "📊 Dashboard", "📊") { IsSelected = true },
			new SidebarItem("Camera", "📷 Camera", "📷"),
			new SidebarItem("Robot", "🦾 Robot", "🦾"),
			new SidebarItem("Statistics", "📈 Statistics", "📈"),
			new SidebarItem("History", "📜 History", "📜"),
			new SidebarItem("Settings", "⚙ Settings", "⚙"),
		};
	}

	private ICommand _itemSelectedCommand;
	public ICommand ItemSelectedCommand => _itemSelectedCommand ??= 
		new RelayCommand<string>(OnItemSelected);

	private void OnItemSelected(string itemName)
	{
		foreach (var item in Items)
		{
			item.IsSelected = item.Name == itemName;
		}
		// Handle navigation
	}
}
```

Bind in XAML:

```xaml
<ItemsControl ItemsSource="{Binding Items}">
	<ItemsControl.ItemTemplate>
		<DataTemplate>
			<Button Content="{Binding DisplayName}"
					Command="{Binding DataContext.ItemSelectedCommand, RelativeSource={RelativeSource AncestorType=Window}}"
					CommandParameter="{Binding Name}"/>
		</DataTemplate>
	</ItemsControl.ItemTemplate>
</ItemsControl>
```

---

## Browser-like Selection Behavior

The sidebar mimics modern browser tab/navigation behavior:

1. **Initial State:** Dashboard is pre-selected
2. **Click Action:** Clicking an item immediately highlights it
3. **Visual Feedback:** Instant color/style change
4. **Event Raised:** ItemSelected event fires for parent handling
5. **Deselection:** Previous item automatically deselected

---

## Event Handling

### ItemSelected Event

**Signature:**
```csharp
public event RoutedEventHandler ItemSelected;
```

**Event Args:**
```csharp
RoutedEventArgs e
{
	OriginalSource = selectedButtonTag  // String identifier
}
```

**Usage:**
```xaml
<local:Sidebar ItemSelected="OnSidebarItemSelected"/>
```

```csharp
private void OnSidebarItemSelected(object sender, RoutedEventArgs e)
{
	string selectedItem = (string?)e.OriginalSource;
	// Handle selection
}
```

---

## Known Limitations & Future Enhancements

**Current Limitations:**
- Hard-coded items (no dynamic list binding in current version)
- No keyboard navigation support yet
- No collapse/expand functionality

**Potential Enhancements:**
- Collapse/expand sidebar animation
- Keyboard arrow key navigation
- Nested/hierarchical items support
- Dynamic item binding from ViewModel
- Custom icons/templates per item
- Width animation on collapse

---

## Integration with MainWindow

The sidebar is designed to replace the left column in MainWindow.xaml. Example integration:

```xaml
<Window ...>
	<Grid>
		<Grid.ColumnDefinitions>
			<ColumnDefinition Width="280"/>  <!-- Sidebar -->
			<ColumnDefinition Width="*"/>    <!-- Main content -->
		</Grid.ColumnDefinitions>

		<!-- Sidebar -->
		<local:Sidebar Grid.Column="0" ItemSelected="OnNavigate"/>

		<!-- Main Content -->
		<ContentPresenter Grid.Column="1" x:Name="ContentArea"/>
	</Grid>
</Window>
```

---

## Testing the Component

1. **Build the project:** Compiles all XAML and code-behind
2. **Run in designer:** Visual Studio shows preview in XAML designer
3. **Run application:** See live interaction with hover and selection effects
4. **Verify events:** Subscribe to ItemSelected and check RoutedEventArgs

---

## Performance Considerations

- **Scrolling:** ScrollViewer handles overflow automatically
- **Animations:** GPU-accelerated via WPF Storyboard
- **Memory:** Minimal footprint - all controls pre-created
- **Responsiveness:** Click handlers execute immediately

---

## Accessibility

- Text-based labels alongside icons
- High contrast colors for readability
- Clear visual feedback on selection
- Large click targets (50px height)

---

## Material Design References

This component follows Material Design 3 principles:
- Emphasis on depth through shadows (via borders)
- Motion via smooth transitions
- Clear visual hierarchy
- Consistent spacing and typography
- Dark theme optimization

---

This sidebar component is production-ready and can be immediately integrated into your Battery Inspection System HMI!
