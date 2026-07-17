# Project structure & integration points for device connectivity

This document summarizes the project layout and explains which files/components are responsible for device connections (camera, robot arm, conveyor). Use this to craft prompts for GPT about what to modify to connect new devices or protocols.

## High-level layers
- UI (Views, Controls): XAML files and code-behind under Views/ and Controls/. Responsible for presentation and user interaction.
- ViewModels: ViewModels/* - glue between UI and services. They call services and expose observable state to Views.
- Services: Services/* - contains device integration logic, business processes, and persistence.
- Models: Models/* - DTOs and configuration objects used by services and viewmodels.
- Helpers & Resources: Helpers/* and Resources/* - configuration loader, logging, service provider, converters, and styles.

## Key folders and files
- Services/CameraService.cs - current camera access, capture loop, frame distribution to UI or AI service
- Services/RobotArmService.cs - robot arm commands, status polling
- Services/ConveyorService.cs - conveyor control and animation logic
- Services/AIDetectionService.cs - runs detection on frames, consumes CameraService
- Services/DataRepository.cs - persistence and storing results/configs
- Models/CameraConfiguration.cs - camera connection settings (IP, port, device id)
- Models/ApplicationSettings.cs & Helpers/ConfigurationHelper.cs - central app settings and config loading
- ViewModels/*ViewModel.cs (CameraViewModel, RobotArmViewModel, ConveyorViewModel) - where services get injected and used
- Controls/CameraPanel.xaml(.cs), RobotPanel.xaml(.cs), ConveyorAnimation.xaml(.cs) - UI components that show device data or send user commands

## Where to change to connect to other devices
- Implement or extend Services/*.cs for the target transport/protocol (e.g., TCP, Serial, HTTP, gRPC, OPC UA). Prefer adding an interface (ICameraService, IRobotArmService) and keep implementation behind the interface.
- Update Models/*Configuration.cs to include new connection parameters (credentials, endpoints, protocol flags).
- Load new settings via ConfigurationHelper and ApplicationSettings so they are editable in SettingsViewModel / SettingsView.
- Wire the new service into Helpers/ServiceProvider.cs (or the composition root) so ViewModels get the correct implementation.
- Update ViewModels to expose any new commands or status properties required by the new device.
- Update Controls and Views to show new UI elements or controls for device-specific controls/feedback.

## Suggested minimal integration steps
1. Create interface in Services/Interfaces (e.g., ICameraService) and a concrete implementation for the new device.
2. Add configuration model and Settings UI entries.
3. Register the implementation in ServiceProvider.cs.
4. Update CameraViewModel to consume the interface and adapt binding in CameraPanel.
5. Test with a mock implementation (simple simulator) before hardware integration.

## Example prompts to give GPT
- "How do I implement TCP-based camera streaming in Services/CameraService.cs using System.Net.Sockets? Show code skeleton and points to adapt in CameraViewModel and CameraPanel.xaml.cs."
- "Create an IRobotArmService interface and a new RobotArmTcpService that sends commands over TCP. Explain where to register it and how to update RobotArmViewModel."
- "What changes are needed to add serial COM port support for conveyor control? List files to edit and provide code snippets for reading/writing SerialPort."

## Next actions
- Decide transport protocol(s) for each device (e.g., ONVIF/RTSP for cameras, TCP/JSON for robot, Serial for conveyor).
- Choose whether to add new interfaces or extend existing services.
- Provide one example device/protocol and I can generate the concrete code changes and tests.
