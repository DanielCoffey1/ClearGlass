# Clear Glass

[![License: Custom Non-Commercial](https://img.shields.io/badge/License-Custom%20Non--Commercial-blue.svg)](LICENSE)
[![Download Latest](https://img.shields.io/github/downloads/daniel1017/ClearGlass/latest/total?label=Download%20Latest)](https://github.com/daniel1017/ClearGlass/releases/latest)

Clear Glass is a modern Windows optimization application designed to enhance system performance and privacy. Built with a sleek glass-effect UI, it provides powerful tools for Windows system optimization and customization.

## Installation

### Pre-built Installer

1. Download the latest `ClearGlassSetup.exe` from the releases page
2. Run the installer with administrator privileges
3. Follow the installation wizard
4. Launch Clear Glass from the Start menu

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/DanielCoffey1/ClearGlass.git
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
- Enhance privacy settings:
  - Control advertising and personalization
  - Manage app tracking and telemetry
  - Configure inking and typing privacy
  - Disable unwanted features (Copilot, suggestions)
- Improve system performance
- Remove Windows bloatware (with protected apps support)
- **Optional Windows 11 start menu clearing** (user choice to remove all pinned applications and disable recommendations for all users)
- **Optional Microsoft Edge removal** (with detailed warnings about potential system impacts)
- Safely remove pre-installed software:
  - Microsoft 365 and Office products (all installation types)
  - Microsoft OneNote (Program Files and Store versions)
  - McAfee products and services
  - Norton and Norton 360 products
  - Thorough cleanup of related services and registry entries
  - Multiple removal methods (setup.exe, ODT, registry, winget)

### Application Uninstaller

Built-in application management that allows you to:

- List all installed applications on your system from multiple sources:
  - Windows Package Manager (winget) registered applications
  - Applications installed in Program Files directories
  - Applications registered in Windows uninstall registry
- Select multiple applications for batch uninstallation
- View detailed information about each application:
  - Application name
  - Package ID
  - Version number
- Advanced uninstallation features:
  - Creates system restore points before uninstallation
  - Takes system snapshot to track file changes
  - Uses native uninstallers when available
  - Performs deep scanning for leftover files:
    - Tracks new and modified files during uninstallation
    - Scans common system directories
    - Detects various file types (.exe, .dll, .sys, .ini, etc.)
    - Identifies related files using smart name matching
  - Thorough registry scanning
  - Optional removal of all detected leftovers
- Uninstall applications using Windows Package Manager (winget)
- Modern glass-effect interface matching Clear Glass design
- Easy-to-use checkbox selection system
- Confirmation dialogs to prevent accidental uninstallation
- Detailed progress feedback during uninstallation process

**Note on Steam Games:**  
Steam games are detected automatically by their app ID (e.g., "Steam App 1091500") and are always uninstalled using the Steam client (via `steam://uninstall/<AppID>`). They are not uninstalled using winget or other methods. This ensures proper removal and avoids errors.

### Keep Apps Feature

- Select which apps to protect during bloatware removal
- Temporarily store protected apps list during the session
- Reset to defaults when application restarts
- Easy-to-use interface with checkboxes for selection
- Automatically detects and lists installed Windows apps
- Remembers essential system apps by default

### Microsoft Edge Removal

Optional Microsoft Edge removal during bloatware cleanup:

- **User Choice**: Prompts user whether to remove Microsoft Edge
- **Clear Warnings**: Detailed information about potential system impacts:
  - Windows Update may fail to download updates
  - Some Windows features may not work properly
  - Applications may have compatibility issues
  - System stability may be affected
- **Safe Execution**: Runs after main bloatware removal completes
- **Comprehensive Removal**: Uses advanced removal techniques:
  - Standard WinGet removal (recommended)
  - Force removal if standard method fails
  - Registry cleanup and file removal
  - Startup entry removal
- **User Feedback**: Clear success/warning messages
- **Logging**: Full operation logging for troubleshooting

**Note**: Microsoft Edge is no longer protected by default and can be removed during cleanup. Users are strongly advised to consider the potential system impacts before choosing to remove Edge.

### Windows AI Components Removal

Comprehensive removal of Windows AI features and components:

- **Prerequisite Warning**: Before removing AI components, you **MUST** first remove Windows bloatware:
  - Certain bloatware components can interfere with AI removal
  - Some AI features are tied to bloatware packages
  - Removing bloatware first ensures cleaner AI removal
  - **Recommended order**: Run 'Remove Windows Bloatware' → Restart PC → Run 'Remove AI Components'
- **Targeted AI Removal**: Removes Windows AI components including:
  - Windows Copilot and related features
  - Recall functionality and data
  - AI-powered suggestions and recommendations
  - Machine learning components and DLLs
  - AI-enhanced features in applications (Paint, Notepad, etc.)
- **Process Management**: Kills running AI processes before removal
- **Registry Cleanup**: Disables AI-related registry keys and policies
- **Package Removal**: Removes AI AppX packages and components
- **File Cleanup**: Removes AI-related files, scheduled tasks, and data
- **User Guidance**: Provides clear restart recommendations for complete removal
- **Safe Execution**: Runs with elevated privileges and proper error handling
- **Visual Feedback**: Custom-styled confirmation dialogs and success messages

**Note**: Some AI components may require a system restart to be fully removed. If removal appears incomplete, restart your computer and run the removal again.

### Clear Glass Theme

A modern, minimalist theme that includes:

- Dark mode with glass effect
- Left-aligned taskbar option
- Task View visibility toggle
- Search icon visibility toggle
- Desktop icons visibility toggle
- Custom wallpaper that complements the glass effect
- Proper theme application sequencing for reliability
- Automatic wallpaper download and caching

### Enhanced Clear Glass Experience

The complete Clear Glass experience now includes optional user choices:

- **Optional System Tweaks**: Choose to apply additional system optimizations:
  - Remove Microsoft OneDrive completely
  - Disable search suggestions and Bing integration
  - Disable privacy permissions and tracking
  - Enable End Task in taskbar context menu
  - Set File Explorer to show "This PC" by default
  - Restore classic right-click context menu
- **Optional Microsoft Edge Removal**: Choose whether to remove Microsoft Edge (with detailed warnings about potential system impacts)
- **Optional Start Menu Clearing**: Choose whether to clear the Windows start menu and disable recommendations
- **User Control**: All major changes are now user-controlled with clear explanations
- **Progressive Enhancement**: Tweaks are applied before main Clear Glass functions for optimal system preparation

### Recommended Addons

Curated selection of privacy-focused and performance-enhancing applications:

- LibreWolf Browser (privacy-focused web browser)
- Revo Uninstaller (thorough application removal)
- Automatic installation and updates through winget
- Silent installation without user interaction
- Bundle installation option for all recommended apps

### Windows Tweaks

Additional system customization options:

- Enhanced Privacy Controls:
  - Disable advertising ID and personalized ads
  - Turn off website language list access
  - Disable app launch tracking
  - Remove suggested content in Settings
  - Disable Settings app notifications
  - Turn off custom inking and typing dictionary
  - Disable Windows Copilot (preview)
- Disable search box suggestions and Bing integration
- Remove Microsoft OneDrive:
  - Multiple removal methods for different installation types
  - Registry cleanup
  - Process termination
  - Service disablement
- Access to Windows Image Backup
- Quick access to system tools:
  - Windows Settings
  - Control Panel
  - Registry Editor
  - User Accounts
- Enable End Task in taskbar context menu
  - Adds End Task option when right-clicking app icons
  - Quickly terminate unresponsive applications
  - No need to open Task Manager
- Set This PC as default File Explorer view
  - Removes Home and Gallery views
  - Provides cleaner, traditional file management
  - Automatically restarts Explorer to apply changes

### Safety Features

- Automatically creates system restore points before making changes
- Includes error handling for all operations
- Provides progress feedback during optimization
- Shows success/warning messages for all operations
- Protected apps list to prevent removal of essential software
- Confirmation dialogs for important actions
- Rollback capability for failed operations

### Modern UI Features

- Glass-effect interface
- Animated overlay panels
- Smooth transitions
- User-friendly button layout
- Progress indicators
- Clear success/error messaging
- Intuitive app protection interface
- Dark mode support
- Responsive design
- Accessibility features

## Requirements

- Windows 10/11
- Administrator privileges (required for system modifications)
- .NET 6.0 or later
- Internet connection for addon installations and updates
- Winget package manager (automatically installed if needed)
- Minimum 4GB RAM
- 100MB free disk space

## Usage

1. Run the application with administrator privileges
2. (Optional) Configure protected apps using the "Keep Apps" button
3. Choose your optimization approach:
   - Click "Clear Glass Theme" for visual customization only
   - Click "Windows Optimization" for system optimization options
   - Click "Run Clear Glass" for the complete experience:
     - **Optional system tweaks**: Choose to apply additional tweaks (OneDrive removal, privacy settings, context menu, etc.)
     - **Optional start menu clearing**: Choose whether to clear the Windows start menu and disable recommendations
     - Windows settings optimization
     - Bloatware removal with protected apps
     - Clear Glass theme application
4. Wait for the process to complete
5. Review the success message for confirmation

### Installing Recommended Addons

You can install recommended applications in two ways:

1. **Individual Installation**:

   - Click "Recommended Addons"
   - Choose an application
   - Click "Download" to install/update via winget
   - The application will automatically:
     - Install winget if not present
     - Check if the app is already installed
     - Update existing installations
     - Install new applications silently

2. **Bundle Installation**:
   - Click "Recommended Addons"
   - Click "Download Bundle" to install all apps
   - The process continues even if individual apps fail
   - Provides detailed feedback for any failed installations

### Automatic Updates

Clear Glass includes automatic update checking for:

- The application itself (checks for new versions on startup)
  - Optional updates with user confirmation
  - Continue using current version if preferred
- Installed recommended applications
- Winget package manager
- Application installations and updates run silently without user interaction
- Progress indication through button text updates
- Detailed success/error messaging for all operations
- Automatic download and installation of new versions when available

## Development

The application is built using:

- C# / .NET 6.0
- Modern WPF UI
- PowerShell for system modifications
- Windows Registry manipulation
- System service management
- Winget package manager integration
- Serilog for logging
- Microsoft.Extensions.Logging

### Logging

The application uses Serilog for comprehensive logging:

- File-based logging for troubleshooting
- Console output for development
- Structured logging format
- Different log levels for various operations
- Automatic log rotation
- Error tracking and reporting

## Support

If you find Clear Glass useful, consider supporting its development:

- [Support on Ko-fi](https://ko-fi.com/daniel1017)
- Report issues on GitHub
- Contribute to the codebase
- Share with others

## License

This project is licensed under a custom non-commercial license. See the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.
