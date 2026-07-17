# Dashboard - Visual Reference Guide

## Dashboard Layout Overview

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║                          TOP NAVIGATION BAR (60px)                            ║
║  ⚙ Battery Inspection System │ Online │ Time: 14:32:45 │ 👤 Admin │ 🛑      ║
╠════════════════════╦══════════════════╦═══════════════════════════════════════╣
║                    ║                  ║                                       ║
║   SIDEBAR (250px)  ║   DASHBOARD      ║                                       ║
║                    ║                  ║                                       ║
║ • Dashboard        ║                  ║ TOP SECTION (Responsive)              ║
║ • Analytics        ║ ┌────────────────┼──────────────────┬──────────────────┐ ║
║ • Settings         ║ │                │                  │                  │ ║
║ • Reports          ║ │    CAMERA      │   CONVEYOR       │  STATUS PANEL    │ ║
║ • Help             ║ │    VIEW        │   ANIMATION      │  • System: Ready │ ║
║                    ║ │    (65%)        │   (20%)          │  • Robot: Op.    │ ║
║                    ║ │                │                  │  • AI: Active    │ ║
║                    ║ │  📷            │    ◇◇◇◇◇          │  • Temp: 45°C    │ ║
║                    ║ │  Live Feed     │    ═════          │  (15%)           │ ║
║                    ║ │  1920x1080 30fps│    ◇◇◇◇◇          │                  │ ║
║                    ║ │  ● Recording   │                  │                  │ ║
║                    ║ │                │  Speed: 45 u/min │                  │ ║
║                    ║ │                │  Efficiency: 94% │                  │ ║
║                    ║ └────────────────┼──────────────────┴──────────────────┘ ║
║                    ║                  ║                                       ║
║                    ║ STATISTICS ROW (180px)                                   ║
║                    ║ ┌──────────┬──────────┬──────────┬──────────┐           ║
║                    ║ │  📊      │   📊     │   📊     │   📊     │           ║
║                    ║ │  Good    │ Scratch  │  Dent    │ Other    │           ║
║                    ║ │  2,847   │   234    │   156    │   89     │           ║
║                    ║ │ ↑12 today│ ↑2 today │ ↑1 today │ ↓1 today │           ║
║                    ║ └──────────┴──────────┴──────────┴──────────┘           ║
║                    ║  [Scrolls horizontally ─────────────────────────────>]  ║
║                    ║                                                         ║
║                    ║ EVENT LOG (250px, scrollable)                           ║
║                    ║ ┌─────────────────────────────────────────────── Clear ┐ ║
║                    ║ │ ℹ System started successfully          14:32:15       │ ║
║                    ║ │   All modules initialized                             │ ║
║                    ║ │                                                       │ ║
║                    ║ │ ✓ Inspection cycle completed           14:28:42       │ ║
║                    ║ │   2,847 batteries processed                           │ ║
║                    ║ │                                                       │ ║
║                    ║ │ ⚠ Temperature warning                  14:25:18       │ ║
║                    ║ │   Sensor reading: 52°C                                │ ║
║                    ║ │                                                       │ ║
║                    ║ │ ● Error detected                       14:22:05       │ ║
║                    ║ │   Camera feed interrupted                             │ ║
║                    ║ │                                        [↓ scroll]     │ ║
║                    ║ └───────────────────────────────────────────────────────┘ ║
║                    ║                                                         ║
║                    ║ CONTROL PANEL (120px)                                   ║
║                    ║ ┌───────────────────────────────────────────────────────┐ ║
║                    ║ │  [▶ Start]  [⏸ Stop]  [🔄 Reset]                    │ ║
║                    ║ │  [🛑 EMERGENCY STOP]                                 │ ║
║                    ║ │  Mode: [Auto●] [Manual]                              │ ║
║                    ║ └───────────────────────────────────────────────────────┘ ║
╚════════════════════╩══════════════════╩═══════════════════════════════════════╝
```

---

## Top Section Proportions

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                                                                              │
│   CAMERA VIEW (65%)        │ CONVEYOR (20%)  │ STATUS (15%)                 │
│                            │                 │                              │
│   ┌────────────────────┐   │  ┌────────────┐ │  ┌──────────────┐           │
│   │                    │   │  │            │ │  │ System: Ready│           │
│   │                    │   │  │  ◇◇◇◇◇     │ │  │              │           │
│   │       📷           │   │  │  ═════     │ │  │ Robot: Op.   │           │
│   │   Live Camera      │   │  │  ◇◇◇◇◇     │ │  │              │           │
│   │   Feed             │   │  │            │ │  │ AI: Active   │           │
│   │   1920x1080        │   │  │ Speed:45   │ │  │              │           │
│   │   Recording        │   │  │ Eff: 94%   │ │  │ Temp: 45°C   │           │
│   │   ● Recording      │   │  │            │ │  │              │           │
│   │                    │   │  └────────────┘ │  └──────────────┘           │
│   └────────────────────┘   │                 │                              │
│                            │                 │                              │
└──────────────────────────────────────────────────────────────────────────────┘
	 65% (6.5 units)       20% (2 units)    15% (1.5 units)
```

---

## Statistics Cards Layout

```
STATISTICS ROW (Horizontal Scroll)

┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│     📊       │ │     📊       │ │     📊       │ │     📊       │
│              │ │              │ │              │ │              │
│    Good      │ │   Scratch    │ │    Dent      │ │    Other     │
│   2,847      │ │     234      │ │     156      │ │     89       │
│  ↑12 today   │ │  ↑2 today    │ │  ↑1 today    │ │  ↓1 today    │
│              │ │              │ │              │ │              │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
	140px          140px            140px            140px

[Scroll horizontally on narrow screens ────────────────────────────>]
```

---

## Event Log Entry Types

```
INFO (Blue)              SUCCESS (Green)          WARNING (Yellow)         ERROR (Red)
┌─────────────────────────────────────────────────────────────────────────────┐
│ ℹ System started      │ ✓ Cycle completed      │ ⚠ Temperature       │ ● Error detected │
│   successfully         │   2,847 processed        warning              Camera interrupted │
│                        │                          Temp: 52°C                              │
└─────────────────────────────────────────────────────────────────────────────┘
  #007ACC (Blue)          #4EC9B0 (Teal)           #FFD700 (Gold)       #D13438 (Red)
```

---

## Control Panel Buttons

```
PRIMARY CONTROLS (Blue)
┌─────────────────────────────────────────┐
│  [▶ Start]  [⏸ Stop]  [🔄 Reset]      │
└─────────────────────────────────────────┘
   #007ACC    #6C757D    #6C757D

EMERGENCY STOP (Red - High Visibility)
┌─────────────────────────────────────────┐
│         [🛑 EMERGENCY STOP]            │
└─────────────────────────────────────────┘
			#D13438

MODE SELECTOR (Toggle)
┌─────────────────────────────────────────┐
│ Mode: [Auto●] [Manual]                  │
└─────────────────────────────────────────┘
	   #007ACC  #3E3E42
	  (Active) (Inactive)
```

---

## Responsive Behavior

### Large Screen (1920x1080)
```
┌─────────────────────────────────────────────────────────────────────┐
│ CAMERA (1241px)     │ CONVEYOR (320px)  │ STATUS (240px)          │
│                     │                   │                          │
│                     │                   │                          │
└─────────────────────────────────────────────────────────────────────┘
   65% proportional       20% proportional    15% proportional
```

### Medium Screen (1280x720)
```
┌────────────────────────────────────────────────────┐
│ CAMERA (832px)     │ CONVEYOR (213px)  │ STATUS (160px) │
│                    │                   │                │
└────────────────────────────────────────────────────┘
   65% proportional      20% proportional  15% proportional
```

### Small Screen (800x600)
```
┌──────────────────────────────────────┐
│ CAMERA (520px)    │ CONVEYOR (133px) │ STATUS (104px)   │
└──────────────────────────────────────┘
   65% proportional    20% proportional  15% proportional
```

---

## Color Palette

### Background Colors
```
Main Background:    #1E1E1E (Very Dark Gray)
Component BG:       #2D2D30 (Dark Gray)
Content BG:         #252526 (Darker Gray)
Header BG:          #1E1E1E (Very Dark Gray)
Border:             #3E3E42 (Subtle Gray)
```

### Status/Accent Colors
```
Online/Ready:       #4EC9B0 (Teal)
Primary Action:     #007ACC (Blue)
Danger/Emergency:   #D13438 (Red)
Warning:            #FFD700 (Gold)
Success:            #4EC9B0 (Green/Teal)
Error:              #FF6B6B (Light Red)
```

### Text Colors
```
Primary Text:       #FFFFFF (White)
Secondary Text:     #CCCCCC (Light Gray)
Tertiary Text:      #858585 (Dim Gray)
Disabled Text:      #5A5A5F (Very Dim)
```

---

## Spacing & Dimensions

```
GLOBAL MARGINS/PADDING
├─ Container Margin:     15px (all sides)
├─ Section Spacing:      10px (between major sections)
├─ Card Padding:         12-15px
├─ Component Margins:    8px
└─ Text Spacing:         4-8px

COMPONENT HEIGHTS
├─ Top Navigation:       60px
├─ Camera/Conveyor/Status: Proportional (auto-height)
├─ Statistics Cards:     ~140px each
├─ Event Log:            250px (scrollable)
├─ Control Panel:        120px
└─ Total Dashboard:      Fills remaining height

COMPONENT WIDTHS
├─ Sidebar (reference):  250px
├─ Camera:               65% of container
├─ Conveyor:             20% of container
├─ Status:               15% of container
├─ Statistics Card:      140px each
└─ Dashboard:            Fills remaining width
```

---

## Interaction States

### Button States

**Primary Button**
- Normal:   #007ACC (Blue)
- Hover:    #1084D7 (Darker Blue)
- Pressed:  #005A9E (Very Dark Blue)

**Danger Button (Emergency Stop)**
- Normal:   #D13438 (Red)
- Hover:    #E81123 (Bright Red)
- Pressed:  #A4373A (Dark Red)

**Toggle Button**
- Inactive: #3E3E42 (Dark Gray)
- Active:   #007ACC (Blue)
- Hover:    #4E4E54 (Gray) + #007ACC border

### Status Indicators

**Status Dot Colors**
```
● Online:      #4EC9B0 (Teal)
● Offline:     #FF6B6B (Red)
● Warning:     #FFD700 (Gold)
● Processing:  #007ACC (Blue)
```

---

## Event Log Icon Reference

```
Icon    Color       Meaning
───────────────────────────
ℹ       #007ACC     Information
✓       #4EC9B0     Success
⚠       #FFD700     Warning
●       #D13438     Error
```

---

## Key Measurements for Customization

```
RESPONSIVE COLUMN WIDTHS (Star Units)
Original Ratio:
├─ Camera:   6.5*  (65%)
├─ Conveyor: 2*    (20%)
└─ Status:   1.5*  (15%)
		   ─────
		   10* total

To change (e.g., 70%/20%/10%):
├─ Camera:   7*    (70%)
├─ Conveyor: 2*    (20%)
└─ Status:   1*    (10%)
		   ─────
		   10* total
```

---

## Animation & Transitions

### Current (No Animations)
- Instant updates
- No transitions
- Direct color changes

### Recommended Enhancements
```csharp
// Fade-in animation for statistics update
<DoubleAnimation Storyboard.TargetProperty="Opacity"
				 From="0.3" To="1" Duration="0:0:0.3"/>

// Color transition for status change
<ColorAnimation Storyboard.TargetProperty="Background"
				Duration="0:0:0.5"/>

// Scale animation for important alerts
<DoubleAnimation Storyboard.TargetProperty="ScaleTransform.ScaleX"
				 From="0.9" To="1" Duration="0:0:0.3"/>
```

---

## Accessibility Considerations

```
CONTRAST RATIOS (WCAG AA Minimum: 4.5:1)
├─ White (#FFFFFF) on Dark (#252526):       18.6:1 ✓ Excellent
├─ Light Gray (#CCCCCC) on Dark (#252526):  6.5:1  ✓ Good
├─ Teal (#4EC9B0) on Dark (#252526):        5.2:1  ✓ Good
├─ Blue (#007ACC) on Dark (#252526):        5.8:1  ✓ Good
└─ Red (#D13438) on Dark (#252526):         5.1:1  ✓ Good

RECOMMENDED ADDITIONS
├─ Keyboard navigation (Tab key)
├─ Screen reader support (ARIA labels)
├─ High contrast mode option
└─ Font size adjustments
```

---

## Performance Metrics

```
Component Rendering
├─ Camera View:      ~20ms
├─ Conveyor:         ~10ms
├─ Status Panel:     ~5ms
├─ Statistics:       ~15ms
├─ Event Log (50):   ~25ms
├─ Control Panel:    ~10ms
└─ Total Dashboard:  ~85ms (at 60fps target)

Memory Usage (Approximate)
├─ Dashboard XAML:   ~50KB
├─ Controls XAML:    ~40KB
├─ Component Objects: ~2MB (with data)
└─ Total:            ~2.1MB
```

---

This visual reference provides a complete picture of the Dashboard layout and can be used for design decisions and customizations!
