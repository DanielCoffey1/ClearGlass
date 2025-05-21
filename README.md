# ClearGlass

[![License: Custom Non-Commercial](https://img.shields.io/badge/License-Custom%20Non--Commercial-blue.svg)](LICENSE)

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

Curated selection of privacy-focused and performance-enhancing applications:

- LibreWolf Browser (privacy-focused web browser)
- Revo Uninstaller (thorough application removal)
- Easy installation through winget or direct download links

### Windows Tweaks

Additional system customization options:

- Disable search box suggestions and Bing integration
- Remove Microsoft OneDrive
- Access to Windows Image Backup
- Quick access to system tools:
  - Windows Settings
  - Control Panel
  - Registry Editor
  - User Accounts

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
   - Click "Run Clear Glass" for the complete experience
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
- PowerShell for system modifications
- Windows Registry manipulation
- System service management

## Support

If you find ClearGlass useful, consider supporting its development:

- [Support on Ko-fi](https://ko-fi.com/daniel1017)

## License

This project is licensed under a custom non-commercial license. See the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
