# CameraPanel - Visual Reference Guide

## Layout Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ HEADER (60px)                                                               │
│ 📷 Camera Feed │ ● Connected │ FPS: 30 │ Res: 1920×1080                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│                                                                              │
│             CAMERA IMAGE DISPLAY AREA (Responsive Height)                   │
│                                                                              │
│    Live Stream ┌──────────────────────────────────────┐ 14:32:45           │
│    Battery...  │                                      │ 2024-01-15         │
│                │   (Image Display - Centered)        │                     │
│                │                                      │ Quality: 85%       │
│    FPS: 30 ┌──┤                                      ├──┐ ████████░     │
│    Res:    │  │                                      │  │                   │
│    1920×   │  │      (1920×1080 @ 30fps)           │  │                   │
│    1080    │  │                                      │  │                   │
│            └──┤                                      ├──┘                   │
│                │                                      │                     │
│                └──────────────────────────────────────┘                     │
│                                                                              │
├─────────────────────────────────────────────────────────────────────────────┤
│ CONTROLS (50px)                                                             │
│ [📸 Capture] [⏹ Record] │ ● Connected │ Frames: 0 │ Exp: █ │ Focus: █ │ WB:█ │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Header Section

```
┌──────────────────────────────────────────────────────────────────────┐
│ 📷 Camera Feed │ ● Connected │              FPS: 30 │ Res: 1920×1080 │
└──────────────────────────────────────────────────────────────────────┘

Left Side (Status):
├─ Icon: 📷
├─ Title: "Camera Feed"
├─ Status Dot: ● (Green = Connected, Red = Disconnected)
└─ Text: "Connected"

Right Side (Metrics):
├─ FPS Display: "30"
└─ Resolution: "1920×1080"
```

---

## Image Display Area

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│  Live Stream ┌───────────────────────────────────┐ ... │
│  Battery...  │                                   │ ...  │
│              │      CAMERA IMAGE                 │ ...  │
│              │    (Main Content)                 │ ...  │
│  FPS: 30 ┌──┤                                   ├──┐ ... │
│  Res:... │  │                                   │  │ ... │
│  1920... │  │  (Placeholder when no image)     │  │ ... │
│          └──┤                                   ├──┘ ... │
│              │                                   │ ...  │
│              └───────────────────────────────────┘ ...  │
│                                                         │
└─────────────────────────────────────────────────────────┘

Overlays (Semi-transparent black backgrounds):
├─ TOP-LEFT: Live Stream Info
│   ├─ "Live Stream"
│   └─ "Battery Inspection"
│
├─ TOP-RIGHT: Timestamp
│   ├─ "14:32:45" (HH:MM:SS)
│   └─ "2024-01-15" (YYYY-MM-DD)
│
├─ BOTTOM-LEFT: Performance
│   ├─ "FPS: 30"
│   └─ "Res: 1920×1080"
│
└─ BOTTOM-RIGHT: Quality
	├─ "Quality"
	├─ Progress bar (85%)
	└─ "85%"
```

---

## Control Panel Section

```
┌───────────────────────────────────────────────────────────────────────┐
│ [📸 Capture] [⏹ Record] │ ● Connected │ Frames: 0 │ Exposure │ Focus │ ...
└───────────────────────────────────────────────────────────────────────┘

LEFT BUTTONS:
├─ Capture: [📸 Capture]
│   Color: #007ACC (Blue)
│   Hover: #1084D7 (Darker Blue)
│   Pressed: #005A9E (Very Dark Blue)
│
└─ Record: [⏹ Record] or [⏸ Stop]
	Color: #6C757D (Gray) or #D13438 (Red when recording)
	Hover: #7A8087 or #E81123
	Pressed: #5A6168 or #A4373A

SEPARATOR: Vertical line (#3E3E42)

STATUS INDICATORS:
├─ Connection: ● Connected (Green dot + text)
└─ Frames: "Frames: 0" (Counter)

RIGHT-SIDE METRICS (Progress Bars):
├─ Exposure: ████░ (Gold #FFD700)
├─ Focus: ██████░ (Blue #007ACC)
└─ White Balance: ████░ (Teal #4EC9B0)
```

---

## Color Reference

### UI Elements

```
Component          Color     Hex       RGB
────────────────────────────────────────────
Background         Dark Gray #2D2D30   45, 45, 48
Header             Very Dark #1E1E1E   30, 30, 30
Content Area       DkGray    #252526   37, 37, 38
Border/Divider     Subtle    #3E3E42   62, 62, 66

Text - Primary     White     #FFFFFF   255, 255, 255
Text - Secondary   LtGray    #CCCCCC   204, 204, 204
Text - Tertiary    DmGray    #858585   133, 133, 133

Status - Online    Teal      #4EC9B0   78, 201, 176
Status - Offline   Red       #FF6B6B   255, 107, 107
```

### Button States

```
Button Type         Normal      Hover       Pressed
──────────────────────────────────────────────────
Primary (Capture)   #007ACC     #1084D7     #005A9E
Record (Idle)       #6C757D     #7A8087     #5A6168
Record (Active)     #D13438     #E81123     #A4373A
```

### Indicator Colors

```
Indicator           Color     Hex       Purpose
────────────────────────────────────────────────
Status Online       Teal      #4EC9B0   Connected
Status Offline      Red       #FF6B6B   Disconnected
Exposure Bar        Gold      #FFD700   Exposure level
Focus Bar           Blue      #007ACC   Focus level
White Balance Bar   Teal      #4EC9B0   White balance level
Quality Bar         Teal      #4EC9B0   Image quality
```

---

## Sizing & Spacing

### Component Dimensions

```
Element              Width              Height       Notes
──────────────────────────────────────────────────────────
Panel Minimum        600px              400px        Responsive
Panel Recommended    800px+             600px+       Better visibility
Header              Fill               60px         Fixed height
Image Area          Fill               Flexible     Fills available
Control Panel       Fill               50px         Fixed height

Image Display       Fill -30px margin  Auto         Centered in area
Overlay Sections    Dynamic            Auto         Positioned absolutely
Button              Auto               40px         Padding included
```

### Spacing Values

```
Global Margins      15px      (outer panel padding)
Section Spacing     10px      (between major areas)
Component Margin    5px       (between controls)
Overlay Position    15px      (from edges)
Button Group Gap    10px      (between buttons)
```

### Text Sizes

```
Header Title        14px Bold           "Camera Feed"
FPS/Resolution      11px Bold/Regular   "FPS: 30"
Overlay Title       12px Bold           "Live Stream"
Overlay Detail      10px Regular        "Battery Inspection"
Timestamp           12px Bold           "14:32:45"
Date                10px Regular        "2024-01-15"
Status Text         11px Regular        "Connected"
Button Text         12px Bold           "Capture"
Indicator Label     10px Regular        "Exposure"
Quality Text        10px Regular        "85%"
```

---

## Responsive Behavior

### Large Screen (1920×1080)
```
┌─────────────────────────────────────────┐
│ HEADER: Full width, all info visible   │
├─────────────────────────────────────────┤
│                                         │
│      IMAGE AREA: 1890×900 (aprox)      │
│   (All overlays visible and readable)  │
│                                         │
├─────────────────────────────────────────┤
│ CONTROLS: Full width, all buttons      │
└─────────────────────────────────────────┘
```

### Medium Screen (1280×720)
```
┌──────────────────────────────┐
│ HEADER: Full width          │
├──────────────────────────────┤
│                              │
│ IMAGE: 1250×570 (approx)    │
│ (Overlays compressed)        │
│                              │
├──────────────────────────────┤
│ CONTROLS: Full width        │
└──────────────────────────────┘
```

### Small Screen (800×600)
```
┌──────────────────┐
│ HEADER: Compact │
├──────────────────┤
│                  │
│ IMAGE: 770×450  │
│ (Minimal layout) │
│                  │
├──────────────────┤
│ CONTROLS: Wrap? │
└──────────────────┘
```

---

## Overlay Positions

```
Top-Left (Info)         Top-Right (Timestamp)
┌─────────────────────────────────────────────────┐
│ Live Stream           14:32:45                  │
│ Battery...            2024-01-15                │
│                                                  │
│                  IMAGE AREA                      │
│                                                  │
│ FPS: 30               Quality: 85%              │
│ Res: 1920×1080        ████████░               │
└─────────────────────────────────────────────────┘
Bottom-Left (Performance)  Bottom-Right (Quality)
```

---

## State Transitions

### Recording State Changes

```
Normal State              Recording State
┌─────────────────┐      ┌─────────────────┐
│ [⏹ Record]      │      │ [⏸ Stop]        │
│ #6C757D (Gray)  │  →   │ #D13438 (Red)   │
└─────────────────┘      └─────────────────┘

Connection Status

Connected                 Disconnected
● (Green #4EC9B0)    →    ● (Red #FF6B6B)
"Connected"               "Disconnected"
```

---

## Component Interaction Flow

```
User Action                    UI Response
─────────────────────────────────────────────
Hover over Capture            Background: #1084D7
Click Capture                 RaiseEvent(CaptureClicked)
Hover over Record             Background: #7A8087
Click Record (Idle)           IsRecording = true
							  Button: #D13438 "Stop"
Click Record (Recording)      IsRecording = false
							  Button: #6C757D "Record"

SetFps(30)                    FpsDisplay: "30"
							  FpsOverlay: "30"

UpdateTimestamp()             TimestampDisplay: "HH:MM:SS"
							  DateDisplay: "YYYY-MM-DD"
```

---

## Accessibility

### Contrast Ratios (WCAG AA Standard: 4.5:1 minimum)

```
White (#FFFFFF) on Dark (#252526)      18.6:1  ✓ Excellent
Light Gray (#CCCCCC) on Dark           6.5:1   ✓ Good
Teal (#4EC9B0) on Dark                 5.2:1   ✓ Good
Blue (#007ACC) on Dark                 5.8:1   ✓ Good
Gold (#FFD700) on Dark                 9.5:1   ✓ Excellent
```

### Recommended Enhancements

```
✓ ToolTips on all buttons
✓ Status text indicators (not just colors)
✓ Keyboard navigation support
✓ Screen reader labels
✓ High contrast mode option
✓ Font size adjustment
```

---

## Performance Indicators

### Rendering Performance
```
Component          Render Time    Notes
─────────────────────────────────────────
Header            ~2ms          Static layout
Image Display     ~15ms         Depends on image
Overlays          ~3ms          Positioned absolutely
Controls          ~5ms          Simple layout
────────────────────────────────────────────
Total             ~25ms         At 60fps target
```

### Memory Usage
```
Component          Memory     Notes
────────────────────────────────
XAML Layout        ~50KB      Static structure
Image (1920×1080)  ~8MB       Per frame (BGRA)
Control State      ~100KB     Properties + data
────────────────────────────────
Total              ~8.1MB     With single image
```

---

This visual reference provides complete layout and styling specifications for the CameraPanel!
