# Battery Inspection System - Project Architecture Guide

## Project Structure Overview

This document describes the clean, MVVM-based architecture for the Battery Inspection System WPF application.

---

## Folder Responsibilities

### 📁 **Views/**
**Responsibility:** UI Presentation Layer
- Contains XAML UserControl definitions for each feature
- **Files:** CameraView, ConveyorView, AIDetectionView, RobotArmView, StatisticsView, EventLogView, SettingsView
- **Purpose:** Purely declarative UI layouts with minimal code-behind (only InitializeComponent)
- **Best Practice:** All business logic and state management should be in ViewModels, not code-behind
- **Pattern:** Each View binds to a corresponding ViewModel through the DataContext property

---

### 📁 **ViewModels/**
**Responsibility:** Presentation Logic Layer (MVVM Core)
- Contains logic that controls and coordinates UI behavior
- **Files:** ViewModelBase (inheritance base), feature-specific ViewModels (CameraViewModel, ConveyorViewModel, etc.)
- **Purpose:** 
  - Implements INotifyPropertyChanged for data binding
  - Coordinates between Views and Services
  - Maintains UI state and handles user interactions
  - Exposes Commands for button clicks and actions
- **Base Class:** ViewModelBase provides property change notification infrastructure
- **Pattern:** ViewModels expose properties and commands that Views bind to; no direct Model access in code-behind

---

### 📁 **Models/**
**Responsibility:** Data Model Layer
- Defines the domain objects and entities
- **Files:** ModelBase (inheritance base), BatteryInspection, InspectionStatistics, SystemEvent, ApplicationSettings, CameraConfiguration, DetectionResult
- **Purpose:**
  - Represents data structures for inspection results, statistics, and configurations
  - Contains only data properties, not business logic
  - Implements INotifyPropertyChanged when used with ObservableCollections
- **Best Practice:** Models should be persistence-agnostic (database/file format independent)

---

### 📁 **Services/**
**Responsibility:** Business Logic & Data Access Layer
- Encapsulates hardware communication and business logic
- **Files:** 
  - ICameraService / CameraService - Camera hardware control
  - IConveyorService / ConveyorService - Conveyor control
  - IAIDetectionService / AIDetectionService - AI model inference
  - IRobotArmService / RobotArmService - Robot arm control
  - IStatisticsService / StatisticsService - Data aggregation
  - IEventLogService / EventLogService - Event persistence
  - ISettingsService / SettingsService - Configuration management
  - IDataRepository / DataRepository - Data persistence abstraction
- **Purpose:**
  - Implements business logic independent of UI
  - Handles communication with hardware, databases, and external APIs
  - Provides clean interfaces for dependency injection
- **Best Practice:** All services should have interfaces for testability and loose coupling

---

### 📁 **Controls/**
**Responsibility:** Reusable UI Components
- Custom UserControls for specialized UI elements
- **Files:** CameraPreview, StatusIndicator, DefectHighlight, ControlPanel
- **Purpose:**
  - Encapsulates complex UI patterns for reuse
  - Reduces code duplication across Views
  - Each control should have a focused responsibility
- **Pattern:** Controls can have limited code-behind for animation/interaction logic

---

### 📁 **Converters/**
**Responsibility:** Value Transformation Layer
- Implements IValueConverter for data binding transformations
- **Files:** BoolToVisibilityConverter, StatusToColorConverter, PercentageConverter, DateTimeFormatConverter, NullToEmptyStringConverter
- **Purpose:**
  - Converts data types for UI display (bool → Visibility, DateTime → formatted string, etc.)
  - Centralizes display logic separately from ViewModels
  - Reusable across multiple Views
- **Best Practice:** Keep converters stateless and focused on single transformations

---

### 📁 **Resources/**
**Responsibility:** XAML Resource Definitions
- Contains ResourceDictionaries for consistent styling and theming
- **Files:**
  - **Colors.xaml** - Centralized color palette (primary, secondary, error, warning, success)
  - **Converters.xaml** - Converter registrations for application-wide use
  - **Styles.xaml** - Reusable Control styles and templates
- **Purpose:**
  - Ensures visual consistency across the application
  - Facilitates theme changes by editing resource values
  - Reduces code duplication in XAML
- **Best Practice:** Use resource keys consistently across Views

---

### 📁 **Helpers/**
**Responsibility:** Utility & Infrastructure
- Provides utility functions and cross-cutting concerns
- **Files:**
  - **LoggerHelper.cs** - Logging infrastructure
  - **ExtensionMethods.cs** - Helper methods for built-in types
  - **ValidationHelper.cs** - Input validation utilities
  - **ConfigurationHelper.cs** - Application constants and configuration
  - **ServiceProvider.cs** - Dependency injection setup
- **Purpose:**
  - Centralizes common functionality
  - Reduces boilerplate code
  - Provides consistent patterns for logging, validation, and configuration
- **Best Practice:** Keep helpers stateless and focused

---

### 📁 **Assets/**
**Responsibility:** Binary Resources
- Contains images, icons, and other media files
- **Subdirectories:**
  - **Icons/** - Application and UI icons
  - **Images/** - Background images, diagrams
  - **Themes/** - Theme-specific assets
- **Purpose:**
  - Organizes all non-code resources
  - Simplifies asset management and updates
  - Enables multi-theme support

---

## Architecture Flow Diagram

```
User Interaction (UI)
		↓
	Views (XAML)
		↓
	ViewModels (Logic Coordination)
		↓
	Services (Business Logic)
		↓
	Models (Data Structures) ← → DataRepository (Persistence)
		↓
	External Hardware/APIs
```

---

## Key Design Principles

### 1. **Separation of Concerns**
- Views handle only presentation
- ViewModels coordinate logic and state
- Services encapsulate business rules
- Models define data structures

### 2. **Loose Coupling**
- Services use interfaces for dependency injection
- Views bind to ViewModels through DataContext
- No direct references between Views or from Views to Services

### 3. **Reusability**
- Controls encapsulate complex UI patterns
- Converters handle all value transformations
- Helpers provide common utilities
- Resources define consistent styling

### 4. **Testability**
- Service interfaces enable unit testing with mocks
- ViewModels can be tested without UI
- Models are pure data structures
- Helpers remain stateless

### 5. **Maintainability**
- Consistent file organization
- Clear naming conventions
- Centralized configuration and styling
- Documented responsibilities

---

## Getting Started

1. **Create ViewModels first** - Design your application's state and interactions
2. **Design Views** - Build XAML layouts that bind to ViewModels
3. **Implement Services** - Write business logic and hardware communication
4. **Define Models** - Create domain objects used by Services
5. **Build Controls** - Develop reusable UI components
6. **Add Converters** - Implement value transformations as needed
7. **Polish Resources** - Refine styling and resources

---

## File Naming Conventions

- **Views:** {Feature}View.xaml / {Feature}View.xaml.cs
- **ViewModels:** {Feature}ViewModel.cs
- **Models:** {Entity}.cs
- **Services:** I{Feature}Service.cs / {Feature}Service.cs
- **Controls:** {ControlName}.xaml / {ControlName}.xaml.cs
- **Converters:** {Transformation}Converter.cs
- **Helpers:** {Purpose}Helper.cs

---

## Next Steps

This architecture is ready for implementation. Begin by:
1. Implementing ViewModels with property binding support
2. Connecting Views to their corresponding ViewModels
3. Creating Service interfaces and stub implementations
4. Building the main navigation structure
5. Integrating hardware communication layer by layer

Maintain this structure as you build out functionality to ensure scalability and maintainability.
