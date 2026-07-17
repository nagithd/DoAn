# Sidebar Component - Quick Start (30 seconds)

## ⚡ Super Quick Setup

### 1. Add to MainWindow.xaml
```xaml
<Window ...
		xmlns:local="clr-namespace:WpfApp3.Controls">
	<Grid>
		<Grid.ColumnDefinitions>
			<ColumnDefinition Width="280"/>
			<ColumnDefinition Width="*"/>
		</Grid.ColumnDefinitions>

		<local:Sidebar Grid.Column="0" ItemSelected="OnNavigation"/>
		<Border Grid.Column="1" Background="#1E1E1E"/>
	</Grid>
</Window>
```

### 2. Add to MainWindow.xaml.cs
```csharp
private void OnNavigation(object sender, RoutedEventArgs e)
{
	string item = e.OriginalSource?.ToString() ?? "Unknown";
	MessageBox.Show($"Selected: {item}");
}
```

### 3. Run!
Press F5 - Done! ✅

---

## 📋 What You Get

✅ 8 pre-configured navigation items
✅ Material Design styling
✅ Smooth hover animations
✅ Blue selection highlighting
✅ Dark theme (industrial HMI ready)
✅ Fully reusable component
✅ No navigation implementation (you add that)

---

## 🎨 The Items

```
📊 Dashboard (selected by default)
📷 Camera
🦾 Robot
📈 Statistics
📜 History
⚙ Settings
───────────────
📖 Documentation
❓ About
```

---

## 🔧 Common Tasks

### Select Programmatically
```csharp
var sidebar = (Sidebar)FindName("Sidebar");
sidebar.SelectItemByName("Camera");
```

### Change Colors
Edit `Sidebar.xaml.cs` line 37-39:
```csharp
button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#YOUR_COLOR"));
```

### Handle Navigation
```csharp
private void OnNavigation(object sender, RoutedEventArgs e)
{
	switch (e.OriginalSource?.ToString())
	{
		case "Dashboard": LoadView<DashboardView>(); break;
		case "Camera": LoadView<CameraView>(); break;
		case "Robot": LoadView<RobotView>(); break;
		case "Statistics": LoadView<StatisticsView>(); break;
		case "History": LoadView<HistoryView>(); break;
		case "Settings": LoadView<SettingsView>(); break;
	}
}

private void LoadView<T>() where T : new()
{
	ContentArea.Content = new T();
}
```

---

## 📂 Files

- `Controls/Sidebar.xaml` - The component
- `Controls/Sidebar.xaml.cs` - Selection logic
- `Models/SidebarItem.cs` - Optional model

---

## 📚 Full Docs

- `SIDEBAR_DOCUMENTATION.md` - Complete reference
- `SIDEBAR_USAGE_EXAMPLES.md` - Code samples
- `SIDEBAR_VISUAL_REFERENCE.md` - Design specs
- `SIDEBAR_SUMMARY.md` - Overview

---

## ✨ Features

**Material Design** - Modern dark theme
**Selection Highlighting** - Blue accent, white text, bold font
**Hover Animations** - Smooth 200ms transitions
**Routed Events** - Proper WPF event handling
**Fully Reusable** - Use in any window

---

**That's it! You're ready to go! 🚀**

See full documentation for advanced features.
