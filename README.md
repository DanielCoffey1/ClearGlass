# ClearGlass

ClearGlass is a modern Windows optimization application designed to enhance system performance and privacy. Built with a sleek glass-effect UI, it provides powerful tools for Windows system optimization and customization.

## Installation

### Pre-built Installer

1. Download the latest `Setup.msi` from the releases page
2. Run the installer with administrator privileges
3. Follow the installation wizard
4. Launch ClearGlass from the Start menu

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/ClearGlass.git
   cd ClearGlass
   ```
2. Ensure you have the following prerequisites:
   - Visual Studio 2022 or later
   - .NET 6.0 SDK
   - Windows SDK
3. Open the solution in Visual Studio
4. Build the solution (Release configuration recommended)
5. Run the application with administrator privileges

## Features

### Windows Optimization

Comprehensive system optimization tools that:

- Create system restore points for safety
- Clean temporary files and perform disk cleanup
- Optimize Windows services
- Enhance privacy settings
- Improve system performance
- Remove Windows bloatware (with protected apps support)

### Keep Apps Feature

- Select which apps to protect during bloatware removal
- Temporarily store protected apps list during the session
- Reset to defaults when application restarts
- Easy-to-use interface with checkboxes for selection

### Clear Glass Theme

A modern, minimalist theme that includes:

- Dark mode with glass effect
- Left-aligned taskbar option
- Task View visibility toggle
- Search icon visibility toggle
- Desktop icons visibility toggle
- Custom wallpaper that complements the glass effect
- Proper theme application sequencing for reliability

### Recommended Addons

The application includes a recommended addons section featuring:

- **LibreWolf Browser**: A privacy-focused Firefox-based browser
- **Revo Uninstaller**: Advanced software removal tool
- Automatic installation via winget package manager
- Option to download manually from official websites
- "Download Bundle" feature to install all recommended apps at once

### Bloatware Removal Process

Intelligent removal of unnecessary Windows apps with:

- Color-coded status messages in the terminal
- "Skipped - System Required" (green) for core components
- "Skipped - Protected Component" (yellow) for user-selected apps
- Progress tracking and summary statistics
- Protected apps list support

### Additional Features

- **Auto Login**: Easy access to Windows Auto Login configuration
- **Support Us**: Future support options for the project
- **Modern UI**: Glass-effect interface with smooth animations
- **Safety Features**: System restore points and error handling

#### Optimization Details

The application performs the following optimizations:

1. **Service Optimization**

   - Sets unnecessary services to Manual and stops them:
     - DiagTrack (Connected User Experiences and Telemetry)
     - dmwappushservice (Device Management)
     - lfsvc (Geolocation)
     - MapsBroker (Downloaded Maps Manager)
     - NetTcpPortSharing
     - RemoteAccess
     - RemoteRegistry
     - SharedAccess
     - TrkWks (Distributed Link Tracking)
     - WbioSrvc (Windows Biometric)
     - WMPNetworkSvc (Media Player Network)
     - WSearch (Windows Search)

2. **Privacy Enhancements**

   - Disables Windows telemetry and data collection
   - Disables consumer features
   - Turns off activity history
   - Disables location tracking
   - Removes Wi-Fi Sense
   - Disables Storage Sense
   - Opts out of PowerShell telemetry

3. **Performance Optimization**

   - Disables GameDVR for better gaming performance
   - Disables hibernation
   - Removes unnecessary background processes
   - Optimizes explorer settings
   - Enables quick access features (like End Task with right-click)

4. **System Cleanup**
   - Removes temporary files
   - Performs disk cleanup
   - Cleans Windows temporary folders

### Safety Features

- Automatically creates system restore points before making changes
- Includes error handling for all operations
- Provides progress feedback during optimization
- Shows success/warning messages for all operations
- Protected apps list to prevent removal of essential software

### Modern UI Features

- Glass-effect interface
- Animated overlay panels
- Smooth transitions
- User-friendly button layout
- Progress indicators
- Clear success/error messaging
- Intuitive app protection interface

## Requirements

- Windows 10/11
- Administrator privileges (required for system modifications)
- .NET 6.0 or later
- Winget package manager (optional, for automatic addon installation)

## Usage

1. Run the application with administrator privileges
2. (Optional) Configure protected apps using the "Keep Apps" button
3. Choose your optimization approach:
   - Click "Clear Glass Theme" for visual customization only
   - Click "Windows Optimization" for system optimization options
   - Click "Clear Glass" for the complete experience
4. Wait for the process to complete
5. Review the success message for confirmation

### Installing Recommended Addons

You can install recommended applications in two ways:

1. **Individual Installation**:

   - Click "Recommended Addons"
   - Choose an application
   - Click "Download" to install via winget or visit the website

2. **Bundle Installation**:
   - Click "Recommended Addons"
   - Click "Download Bundle" to install all apps via winget

## Development

The application is built using:

- C# / .NET
- Modern WPF UI
- PowerShell automation
- Windows Registry manipulation
- System service management
- Winget package manager integration

## Safety Notes

- Always ensure you have important files backed up before running system optimizations
- The application creates automatic restore points, but additional backups are recommended
- Some optimizations may need to be re-applied after major Windows updates
- Use the "Keep Apps" feature to protect essential applications
- Protected apps list resets to defaults on application restart for safety

## Technical Details

### Project Structure

- `MainWindow.xaml` - Main UI layout and styling
- `MainWindow.xaml.cs` - Core application logic
- `Services/` - Service layer for system operations
- `Models/` - Data models and business logic
- `Images/` - Application resources and icons

### Dependencies

- .NET 6.0 Windows Runtime
- Windows Presentation Foundation (WPF)
- Windows Management Instrumentation (WMI)
- PowerShell Core
- Windows Registry API

### Build Configuration

The project uses the following build settings:

- Target Framework: .NET 6.0-windows
- Output Type: Windows Executable
- Nullable Reference Types: Enabled
- Application Manifest: Required for admin privileges
- Application Icon: Custom ClearGlass icon

### Development Environment Setup

1. Install Visual Studio 2022 with:
   - .NET Desktop Development workload
   - Windows SDK
   - C# development tools
2. Install .NET 6.0 SDK
3. Clone the repository
4. Open the solution in Visual Studio
5. Restore NuGet packages
6. Build the solution

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

### Building the Installer

The installer is built using WiX Toolset:

1. Install WiX Toolset v4
2. Build the solution in Release mode
3. Run the WiX build command:
   ```bash
   candle Setup.wxs
   light Setup.wixobj
   ```

## Troubleshooting

### Common Issues

1. **Application requires admin rights**

   - Right-click the application and select "Run as administrator"
   - Ensure UAC is not blocking the application

2. **Optimization fails**

   - Check if Windows Defender or antivirus is blocking the application
   - Ensure all Windows services are running
   - Create a system restore point manually before running

3. **Theme not applying correctly**
   - Ensure Windows is up to date
   - Check if any third-party themes are installed
   - Restart Windows Explorer

### Support

For issues and feature requests, please:

1. Check the existing issues
2. Create a new issue with:
   - Windows version
   - ClearGlass version
   - Steps to reproduce
   - Expected vs actual behavior
   - Screenshots if applicable

## License

This project is licensed under the MIT License - see the LICENSE file for details.
