# Windows Optimizer

A modern PowerShell-based Windows optimization tool with a sleek, glass-effect GUI. This tool helps users optimize their Windows system with just a few clicks while maintaining system safety and stability.

![Windows Optimizer Screenshot](screenshots/preview.png) _(Screenshot to be added)_

## Features

### System Optimization

- Creates system restore point before making changes
- Cleans temporary files and performs disk cleanup
- Optimizes system services
- Removes unnecessary Microsoft Store apps while preserving essential ones
- Disables unnecessary Windows features

### Privacy Enhancements

- Disables telemetry and data collection
- Disables activity history
- Disables location tracking
- Disables Wi-Fi Sense
- Disables consumer features

### Performance Improvements

- Disables Game DVR
- Disables hibernation
- Optimizes system services
- Enables "End Task" with right-click
- Disables automatic folder discovery

### Modern User Interface

- Sleek, glass-effect design
- Dark theme
- Real-time progress updates
- Draggable window
- Rounded corners and modern aesthetics
- User-friendly controls

## Requirements

- Windows 10/11
- PowerShell 5.1 or later
- Administrative privileges

## Installation

1. Clone this repository or download the ZIP file

```powershell
git clone https://github.com/yourusername/windows-optimizer.git
```

2. Right-click on `WindowsOptimizer.ps1` and select "Run with PowerShell"
   - The script will automatically request administrative privileges if needed

## Usage

1. Launch the application by running `WindowsOptimizer.ps1`
2. Review the welcome message and optimization information
3. Click the "Start Optimization" button
4. Confirm the optimization when prompted
5. Wait for the optimization process to complete
6. Restart your computer when finished

## Safety Features

- Creates a system restore point before making any changes
- Confirms user consent before starting optimizations
- Preserves essential Windows apps and features
- Includes error handling and progress monitoring
- Shows clear feedback for all operations

## Protected Apps

The following essential Windows apps are preserved during optimization:

- Microsoft Store
- Windows Calculator
- Windows Photos
- Snipping Tool
- Notepad

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This tool makes significant changes to your Windows system. While it creates a system restore point before making any changes, please:

- Save all your work before running the optimization
- Review the changes that will be made
- Use at your own risk
- Consider backing up important data before use

## Acknowledgments

- Thanks to all contributors who have helped with the project
- Special thanks to the PowerShell community for their valuable resources

## Support

If you encounter any issues or have suggestions:

1. Check the [Issues](https://github.com/yourusername/windows-optimizer/issues) page
2. Create a new issue with a detailed description of the problem
3. Include your Windows version and any error messages

---

Made with ❤️ by [Your Name]
