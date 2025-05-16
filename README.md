# ClearGlass

ClearGlass is a modern Windows optimization application designed to enhance system performance and privacy. Built with a sleek glass-effect UI, it provides powerful tools for Windows system optimization and customization.

## Features

### Windows Optimization

Comprehensive system optimization tools that:

- Create system restore points for safety
- Clean temporary files and perform disk cleanup
- Optimize Windows services
- Enhance privacy settings
- Improve system performance

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

### Modern UI Features

- Glass-effect interface
- Animated overlay panels
- Smooth transitions
- User-friendly button layout
- Progress indicators
- Clear success/error messaging

## Recommended Addons

The application includes a recommended addons section with useful tools and utilities that complement ClearGlass's functionality.

## Safety Considerations

- System restore points are automatically created before any changes
- All operations include error handling
- Users can easily reverse changes through Windows System Restore
- Operations that fail are gracefully handled and reported

## Requirements

- Windows 10/11
- Administrator privileges (required for system modifications)
- .NET 6.0 or later

## Usage

1. Run the application with administrator privileges
2. Click the "Windows Optimization" button
3. Click "Run Optimization" in the overlay panel
4. Wait for the optimization process to complete
5. Review the success message for confirmation

## Development

The application is built using:

- C# / .NET
- Modern WPF UI
- PowerShell automation
- Windows Registry manipulation
- System service management

## Safety Notes

- Always ensure you have important files backed up before running system optimizations
- The application creates automatic restore points, but additional backups are recommended
- Some optimizations may need to be re-applied after major Windows updates
