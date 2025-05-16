# Windows 11 Taskbar Customizer

A simple application to customize Windows 11 taskbar settings.

## Features

- Toggle taskbar alignment (centered/left-aligned)
- Toggle Task View button visibility
- Toggle Widgets button visibility
- Toggle Search visibility
- Modern, user-friendly interface
- Automatic loading of current settings

## Requirements

- Windows 11
- .NET 6.0 Runtime
- Administrative privileges

## How to Run

1. Build the solution:

   ```
   dotnet build
   ```

2. Navigate to the output directory:

   ```
   cd bin\Debug\net6.0-windows
   ```

3. Run `TaskbarCustomizer.exe` as administrator:
   - Right-click on `TaskbarCustomizer.exe`
   - Select "Run as administrator"
   - Click "Yes" on the UAC prompt

## Usage

1. The application will load your current taskbar settings
2. Toggle the desired settings using the buttons
3. Click "Apply Changes" to save your settings
4. Confirm the prompt to restart the Explorer process

Note: The Explorer process will briefly restart to apply the changes, which will cause the taskbar to disappear and reappear.
