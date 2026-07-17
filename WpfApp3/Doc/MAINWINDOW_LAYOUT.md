# MainWindow.xaml - Modern Industrial HMI Layout Documentation

## Overview

The MainWindow.xaml implements a professional industrial HMI (Human-Machine Interface) design for the Battery Inspection System with a modern dark theme, optimal for factory floor visibility and reduced eye strain during long operational hours.

---

## Layout Structure

### Primary Grid: 3-Row Layout
```
┌─────────────────────────────────────────────────────┐
│ TOP NAVIGATION BAR (Height: 60px)                   │  
├──────────────┬───────────────────────┬──────────────┤
│              │                       │              │
│ LEFT         │  CENTER DASHBOARD    │ RIGHT        │
│ SIDEBAR      │  (Main Content)      │ STATUS       │
│ (220px)      │  (* Auto-Fill)       │ PANEL        │
│              │                       │ (280px)      │
│              │                       │              │
├──────────────┴───────────────────────┴──────────────┤
│ BOTTOM CONTROL PANEL (Height: 80px)                 │
└─────────────────────────────────────────────────────┘
```

---

## Section Descriptions

### 1. **TOP NAVIGATION BAR** (Height: 60px)
**Background:** Dark gray (#252526) with subtle bottom border

**Components:**
- **Left Side:** Application logo/icon + "Battery Inspection System" title
- **Center:** System status indicator (Online/Offline) + uptime display
- **Right Side:** Current user info + system time icon

**Purpose:** Provides quick system status overview and application branding

---

### 2. **LEFT SIDEBAR** (Width: 220px)
**Background:** Slightly darker (#2D2D30) with right border divider

**Features:**
- **Scrollable Navigation** for responsive design
- **Module Navigation Section:**
  - 📷 Camera
  - 🔄 Conveyor
  - 🤖 AI Detection (currently active - highlighted)
  - 🦾 Robot Arm
  - 📊 Statistics

- **System Section:**
  - 📋 Event Log
  - ⚙ Settings
  - 📖 Help

**Interaction:**
- Active module highlighted with blue background (#0E639C) and accent border
- Hover states show slightly lighter background
- Currently showing AI Detection as selected

**Purpose:** Primary navigation hub for all system modules

---

### 3. **CENTER DASHBOARD** (Auto-Fill Width)
**Background:** Dark theme (#1E1E1E) for minimal distraction

**Two-Row Layout:**

#### Top Section: Module Header
- Current module name: "AI Detection Module - Live Feed"
- Recording indicator: Red blinking dot + "RECORDING" text
- Subtle visual hierarchy

#### Main Area: Camera Feed Preview (2:1 Height)
- Black background simulating camera display
- Placeholder area for live camera feed
- Camera specifications displayed:
  - Resolution: 1920x1080
  - FPS: 30
  - Exposure: Auto
- Image placeholder with icon

#### Bottom Area: Detection Results Grid (3 columns)
1. **Defects Detected Panel**
   - Large red metric display (0)
   - Last detection timestamp

2. **Avg Confidence Panel**
   - Teal metric display (98.5%)
   - Visual progress bar

3. **Avg Process Time Panel**
   - Golden metric display (45ms)
   - Real-time status indicator

**Purpose:** Primary workspace for monitoring the selected module (AI Detection shown as example)

---

### 4. **RIGHT STATUS PANEL** (Width: 280px)
**Background:** Dark sidebar (#2D2D30) with left border divider

**Content Cards (Stacked with spacing):**

#### System Status Card
- Component health indicators with colored dots:
  - 🟢 Camera: Ready
  - 🟢 AI Engine: Active
  - 🟢 Conveyor: Running
  - 🟡 Robot Arm: Idle

#### Performance Metrics Card
- **CPU Usage:** 45% with visual progress bar (blue)
- **Memory Usage:** 62% with visual progress bar (gold)
- **GPU Usage:** 78% with visual progress bar (red)

#### Today's Stats Card
- **Inspected:** 1,247 units (teal)
- **Passed:** 1,198 units (teal)
- **Rejected:** 49 units (red)
- **Pass Rate:** 96.1% (gold)

#### Alerts Card
- ⚠️ **Temperature High** (Red border) - Camera 2: 68°C
- ℹ️ **Calibration OK** (Green border) - Last check: 2 hours ago

**Purpose:** Real-time system health, performance metrics, and alert management

---

### 5. **BOTTOM CONTROL PANEL** (Height: 80px)
**Background:** Dark gray (#252526) with top border divider

**Three-Zone Layout:**

#### Left Zone: Primary Controls
- **▶ START** (Teal button) - Begin inspection sequence
- **⏸ PAUSE** (Gold button) - Pause current operation
- **⏹ STOP** (Red button) - Emergency stop
- Vertical separator
- **🔧 RESET** (Gray button) - Reset system state
- **📋 CALIBRATE** (Gray button) - Calibration routine

#### Center Zone: Status Display
- **Mode:** AUTO (Teal status)
- **Batches:** 15/50 (Gold counter)
- **Throughput:** 120 units/min (Blue metric)

#### Right Zone: Secondary Controls
- **💾 EXPORT** (Gray button) - Export inspection data
- **⚙ CONFIG** (Gray button) - Access configuration
- **❌ EXIT** (Red button) - Close application

**Purpose:** Operational control and quick status monitoring

---

## Color Scheme

### Primary Colors
- **Dark Background:** #1E1E1E (Main area)
- **Dark Sidebar:** #2D2D30 (Sidebars)
- **Dark Bar:** #252526 (Top/Bottom bars)
- **Text:** #CCCCCC (Standard), #858585 (Labels), #FFFFFF (Emphasis)

### Accent Colors
- **Primary Blue:** #007ACC / #0E639C (Active state)
- **Success Green:** #4EC9B0 (Online/Ready)
- **Warning Gold:** #FFD700 (Attention needed)
- **Error Red:** #FF6B6B (Critical/Offline)

### UI Element Colors
- **Borders:** #3E3E42 (Subtle dividers)
- **Hover:** #3E3E42 (Button hover)
- **Hover Active:** #3C3F41 (Lighter alternative)

---

## Typography

- **Font Family:** Segoe UI (Default WPF)
- **Monospace:** Courier New (Time displays, technical values)
- **Weights:**
  - Regular: Standard content
  - Bold: Section headers, active states, metrics
  - SemiBold: Module names, important labels

- **Sizes:**
  - 8-10px: Status text, small labels
  - 11px: Standard body text, button text
  - 12px: Navigation items
  - 14px: Module title
  - 16px: App title
  - 18-24px: Metric displays
  - 60px: Large icon in dashboard

---

## Layout Responsibilities

| Section | Responsibility |
|---------|-----------------|
| **Top Nav** | System status, branding, quick access |
| **Left Sidebar** | Module navigation and selection |
| **Center Dashboard** | Primary workspace for selected module |
| **Right Panel** | System health, metrics, alerts |
| **Bottom Panel** | Operational controls, status summary |

---

## Responsive Behavior

- **Window Maximized:** Default startup state
- **Sidebar:** Fixed width (220px) for stable navigation
- **Status Panel:** Fixed width (280px) maintains readability
- **Center Area:** Flexible - expands/contracts with window
- **Top/Bottom:** Fixed height - maintains button/icon visibility

---

## Grid Structure Technical Details

### Root Grid Row Definitions
```xml
<RowDefinition Height="60"/>      <!-- Top Navigation -->
<RowDefinition Height="*"/>       <!-- Main Content (auto-fill) -->
<RowDefinition Height="80"/>      <!-- Bottom Control -->
```

### Main Content Column Definitions
```xml
<ColumnDefinition Width="220"/>   <!-- Left Sidebar -->
<ColumnDefinition Width="*"/>     <!-- Center Dashboard -->
<ColumnDefinition Width="280"/>   <!-- Right Status -->
```

### Dashboard Sub-Grid (2 rows)
```xml
<RowDefinition Height="Auto"/>    <!-- Module Header -->
<RowDefinition Height="*"/>       <!-- Main Content Area -->
```

### Bottom Panel 3-Column Layout
```xml
<ColumnDefinition Width="Auto"/>  <!-- Left Controls -->
<ColumnDefinition Width="*"/>     <!-- Center Status -->
<ColumnDefinition Width="Auto"/>  <!-- Right Controls -->
```

---

## Styling & Themes

### Button Styles Applied
- **NavButtonStyle:** Left sidebar navigation buttons
  - Hover effect with background change
  - Active state with blue accent and border
  - Smooth transitions

- **ControlPanelButtonStyle:** Bottom panel control buttons
  - Background color transitions on interaction
  - Consistent sizing and padding

- **ActionButtonStyle:** Start/Pause/Stop buttons
  - Color-coded (Green/Yellow/Red)
  - Opacity-based hover/press feedback

---

## Visual Hierarchy

1. **Primary Focus:** Center dashboard area with camera feed
2. **Secondary Focus:** Metric cards showing real-time data
3. **Navigation:** Left sidebar with clear module selection
4. **Context:** Right panel with system health
5. **Controls:** Bottom panel for operations

---

## Accessibility Features

✅ **High Contrast:** Dark theme with light text for readability
✅ **Color Coding:** Status colors (Green/Yellow/Red) for quick recognition
✅ **Icon Usage:** Visual indicators along with text labels
✅ **Spacing:** Generous padding and margins for touch targets
✅ **Readable Text:** 11-12px minimum for standard content

---

## Implementation Notes

- No business logic or data binding present - purely structural layout
- Resources/Styles.xaml provides button and text styles
- Resources/Colors.xaml provides color definitions
- Ready for ViewModel integration via DataContext binding
- Placeholder content easily replaceable with real controls

---

## Next Steps for Implementation

1. **Connect Views to ViewModels** via DataContext
2. **Add Data Bindings** to metric displays, lists, etc.
3. **Implement Command Bindings** for button click handlers
4. **Create Custom Controls** (Camera feed, charts, etc.)
5. **Add Animation** for state transitions and alerts
6. **Integrate Converters** for data formatting

---

This layout provides the foundation for a professional industrial inspection system interface.
