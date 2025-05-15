# Windows Optimizer Script
# Requires elevation (Run as Administrator)

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process PowerShell -Verb RunAs "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Add required type for transparency
Add-Type -TypeDefinition @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32 {
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int LWA_ALPHA = 0x2;
        public const int LWA_COLORKEY = 0x1;
    }
"@

# Create modern button style
function New-ModernButton {
    param($text, $width = 200, $height = 40)
    $button = New-Object System.Windows.Forms.Button
    $button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $button.BackColor = [System.Drawing.Color]::FromArgb(180, 0, 122, 204)
    $button.ForeColor = [System.Drawing.Color]::FromArgb(240, 240, 240)
    $button.Size = New-Object System.Drawing.Size($width, $height)
    $button.Text = $text
    $button.Font = New-Object System.Drawing.Font("Segoe UI Light", 10)
    $button.FlatAppearance.BorderSize = 1
    $button.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(0, 142, 234)
    $button.Cursor = [System.Windows.Forms.Cursors]::Hand
    
    # Add hover effect
    $button.Add_MouseEnter({
        $this.BackColor = [System.Drawing.Color]::FromArgb(200, 0, 142, 234)
        $this.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(0, 162, 254)
    })
    $button.Add_MouseLeave({
        $this.BackColor = [System.Drawing.Color]::FromArgb(180, 0, 122, 204)
        $this.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(0, 142, 234)
    })
    
    return $button
}

# Create the form
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Windows Optimizer'
$form.Size = New-Object System.Drawing.Size(500,400)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'None'
$form.BackColor = [System.Drawing.Color]::FromArgb(18, 18, 18)
$form.Opacity = 0.95

# Enable transparency with error handling
$form.Add_HandleCreated({
    try {
        $handle = $form.Handle
        $initialStyle = [Win32]::GetWindowLong($handle, [Win32]::GWL_EXSTYLE)
        $result = [Win32]::SetWindowLong($handle, [Win32]::GWL_EXSTYLE, $initialStyle -bor [Win32]::WS_EX_LAYERED)
        if ($result -eq 0) {
            Write-Warning "Failed to set window style"
            return
        }
        [Win32]::SetLayeredWindowAttributes($handle, 0, 230, [Win32]::LWA_ALPHA)
    }
    catch {
        Write-Warning "Failed to apply transparency: $_"
    }
})

# Create main container panel with glass effect
$mainPanel = New-Object System.Windows.Forms.Panel
$mainPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$mainPanel.BackColor = [System.Drawing.Color]::FromArgb(10, 10, 10)
$mainPanel.Add_Paint({
    param($sender, $e)
    try {
        $graphics = $e.Graphics
        $rect = $sender.ClientRectangle
        $gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(150, 20, 20, 20),  # Reduced alpha for more transparency
            [System.Drawing.Color]::FromArgb(150, 30, 30, 30),  # Reduced alpha for more transparency
            45
        )
        $graphics.FillRectangle($gradientBrush, $rect)
        $gradientBrush.Dispose()
    }
    catch {
        Write-Warning "Failed to paint main panel: $_"
    }
})
$form.Controls.Add($mainPanel)

# Add title bar with glass effect
$titleBar = New-Object System.Windows.Forms.Panel
$titleBar.Size = New-Object System.Drawing.Size(500, 40)
$titleBar.BackColor = [System.Drawing.Color]::FromArgb(150, 40, 40, 40)  # Reduced alpha
$titleBar.Dock = [System.Windows.Forms.DockStyle]::Top
$titleBar.Add_Paint({
    param($sender, $e)
    try {
        $graphics = $e.Graphics
        $rect = $sender.ClientRectangle
        $gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(150, 30, 30, 30),  # Reduced alpha
            [System.Drawing.Color]::FromArgb(150, 40, 40, 40),  # Reduced alpha
            0
        )
        $graphics.FillRectangle($gradientBrush, $rect)
        $gradientBrush.Dispose()
    }
    catch {
        Write-Warning "Failed to paint title bar: $_"
    }
})
$mainPanel.Controls.Add($titleBar)

# Add title label with glow effect
$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = "Windows Optimizer"
$titleLabel.ForeColor = [System.Drawing.Color]::FromArgb(240, 240, 240)
$titleLabel.Font = New-Object System.Drawing.Font("Segoe UI Light", 12, [System.Drawing.FontStyle]::Regular)
$titleLabel.AutoSize = $true
$titleLabel.BackColor = [System.Drawing.Color]::Transparent

# Calculate center position for title
$centerX = [int](($titleBar.Width - $titleLabel.PreferredWidth) / 2)
$centerY = [int](($titleBar.Height - $titleLabel.PreferredHeight) / 2)
$titleLabel.Location = New-Object System.Drawing.Point($centerX, $centerY)
$titleBar.Controls.Add($titleLabel)

# Add close button with glass effect
$closeButton = New-Object System.Windows.Forms.Button
$closeButton.Text = "x"
$closeButton.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$closeButton.ForeColor = [System.Drawing.Color]::FromArgb(240, 240, 240)
$closeButton.BackColor = [System.Drawing.Color]::FromArgb(150, 40, 40, 40)
$closeButton.Size = New-Object System.Drawing.Size(40, 40)
$closeButton.Location = New-Object System.Drawing.Point(($titleBar.Width - 40), 0)
$closeButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$closeButton.FlatAppearance.BorderSize = 0
$closeButton.FlatAppearance.MouseOverBackColor = [System.Drawing.Color]::FromArgb(232, 17, 35)
$closeButton.Cursor = [System.Windows.Forms.Cursors]::Hand
$titleBar.Controls.Add($closeButton)

$closeButton.Add_Click({ $form.Close() })

# Create content panel with glass effect
$contentPanel = New-Object System.Windows.Forms.Panel
$contentPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$contentPanel.BackColor = [System.Drawing.Color]::Transparent
$contentPanel.Padding = New-Object System.Windows.Forms.Padding(20)
$contentPanel.Add_Paint({
    param($sender, $e)
    $graphics = $e.Graphics
    $rect = $sender.ClientRectangle
    $gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(100, 20, 20, 20),
        [System.Drawing.Color]::FromArgb(100, 30, 30, 30),
        45
    )
    $graphics.FillRectangle($gradientBrush, $rect)
    $gradientBrush.Dispose()
})
$mainPanel.Controls.Add($contentPanel)

# Create a FlowLayoutPanel for centered content
$flowLayout = New-Object System.Windows.Forms.FlowLayoutPanel
$flowLayout.Dock = [System.Windows.Forms.DockStyle]::Fill
$flowLayout.BackColor = [System.Drawing.Color]::Transparent
$flowLayout.FlowDirection = [System.Windows.Forms.FlowDirection]::TopDown
$flowLayout.WrapContents = $false
$flowLayout.AutoSize = $false
$flowLayout.Padding = New-Object System.Windows.Forms.Padding(20)
$contentPanel.Controls.Add($flowLayout)

# Welcome Label with glass effect
$welcomeLabel = New-Object System.Windows.Forms.Label
$welcomeLabel.Text = 'Welcome to Windows Optimizer! This tool will help you optimize your Windows system with just a few clicks. Please make sure to save all your work before proceeding.'
$welcomeLabel.Font = New-Object System.Drawing.Font("Segoe UI Light", 10)
$welcomeLabel.ForeColor = [System.Drawing.Color]::FromArgb(240, 240, 240)
$welcomeLabel.BackColor = [System.Drawing.Color]::Transparent
$welcomeLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$welcomeLabel.AutoSize = $false
$welcomeLabel.Size = New-Object System.Drawing.Size(460, 60)
$welcomeLabel.Margin = New-Object System.Windows.Forms.Padding(0, 20, 0, 20)
$flowLayout.Controls.Add($welcomeLabel)

# Create Optimize Button
$optimizeButton = New-ModernButton "Start Optimization"
$optimizeButton.Margin = New-Object System.Windows.Forms.Padding(130, 20, 130, 20)
$flowLayout.Controls.Add($optimizeButton)

# Progress Label with glass effect
$progressLabel = New-Object System.Windows.Forms.Label
$progressLabel.Text = 'Click the button above to start optimization...'
$progressLabel.Font = New-Object System.Drawing.Font("Segoe UI Light", 9)
$progressLabel.ForeColor = [System.Drawing.Color]::FromArgb(220, 220, 220)
$progressLabel.BackColor = [System.Drawing.Color]::Transparent
$progressLabel.TextAlign = [System.Drawing.ContentAlignment]::TopCenter
$progressLabel.AutoSize = $false
$progressLabel.Size = New-Object System.Drawing.Size(460, 60)
$progressLabel.Margin = New-Object System.Windows.Forms.Padding(0, 20, 0, 20)
$flowLayout.Controls.Add($progressLabel)

# Center the FlowLayoutPanel contents
$flowLayout.Add_SizeChanged({
    foreach ($control in $flowLayout.Controls) {
        $control.Left = [Math]::Max(0, ($flowLayout.ClientSize.Width - $control.Width) / 2)
    }
})

# Add resize handler to keep title centered
$titleBar.Add_SizeChanged({
    $centerX = [int](($titleBar.Width - $titleLabel.PreferredWidth) / 2)
    $centerY = [int](($titleBar.Height - $titleLabel.PreferredHeight) / 2)
    $titleLabel.Location = New-Object System.Drawing.Point($centerX, $centerY)
    $closeButton.Location = New-Object System.Drawing.Point(($titleBar.Width - 40), 0)
})

# Make form draggable
$lastFormLocation = $null
$isDragging = $false

$titleBar.Add_MouseDown({
    $script:isDragging = $true
    $script:lastFormLocation = $form.PointToScreen([System.Drawing.Point]::new($_.X, $_.Y))
})

$titleBar.Add_MouseMove({
    if ($script:isDragging) {
        $currentPos = $form.PointToScreen([System.Drawing.Point]::new($_.X, $_.Y))
        $offset = [System.Drawing.Point]::new(
            $currentPos.X - $script:lastFormLocation.X,
            $currentPos.Y - $script:lastFormLocation.Y
        )
        $form.Location = [System.Drawing.Point]::new(
            $form.Location.X + $offset.X,
            $form.Location.Y + $offset.Y
        )
        $script:lastFormLocation = $currentPos
    }
})

$titleBar.Add_MouseUp({ $script:isDragging = $false })

# Add rounded corners to form
$form.Add_Load({
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 20, 20, 180, 90)
    $path.AddArc($form.Width - 20, 0, 20, 20, 270, 90)
    $path.AddArc($form.Width - 20, $form.Height - 20, 20, 20, 0, 90)
    $path.AddArc(0, $form.Height - 20, 20, 20, 90, 90)
    $form.Region = [System.Drawing.Region]::new($path)
})

# Add shadow effect
$form.Add_Paint({
    $graphics = $_.Graphics
    $rect = $form.ClientRectangle
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(40, 40, 40),
        [System.Drawing.Color]::FromArgb(18, 18, 18),
        45
    )
    $graphics.FillRectangle($brush, $rect)
    $brush.Dispose()
})

# Helper function to create registry paths if they don't exist
function Ensure-RegistryPath {
    param($Path)
    if (!(Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }
}

# Function to update progress
function Update-Progress {
    param($message)
    $progressLabel.Text = $message
    $form.Refresh()
}

# Function to create restore point
function Create-RestorePoint {
    Update-Progress "Creating system restore point..."
    
    # Create registry key to allow more frequent restore points
    Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Name "SystemRestorePointCreationFrequency" -Value 0 -Type DWord -Force
    
    Enable-ComputerRestore -Drive "C:\"
    Checkpoint-Computer -Description "Before Windows Optimization" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue
}

# Function to clean temporary files
function Clean-TempFiles {
    Update-Progress "Cleaning temporary files..."
    Remove-Item -Path "C:\Windows\Temp\*" -Force -Recurse -ErrorAction SilentlyContinue
    Remove-Item -Path "$env:TEMP\*" -Force -Recurse -ErrorAction SilentlyContinue
}

# Function to disable consumer features
function Disable-ConsumerFeatures {
    Update-Progress "Disabling consumer features..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent" -Name "DisableWindowsConsumerFeatures" -Value 1 -Type DWord -Force
}

# Function to disable telemetry
function Disable-Telemetry {
    Update-Progress "Disabling telemetry..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
    Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name "AllowTelemetry" -Value 0 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" -Name "AllowTelemetry" -Value 0 -Type DWord -Force
}

# Function to disable activity history
function Disable-ActivityHistory {
    Update-Progress "Disabling activity history..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "EnableActivityFeed" -Value 0 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "PublishUserActivities" -Value 0 -Type DWord -Force
}

# Function to disable automatic folder discovery
function Disable-AutomaticFolderDiscovery {
    Update-Progress "Disabling automatic folder discovery..."
    Ensure-RegistryPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "NavPaneExpandToCurrentFolder" -Value 0 -Type DWord -Force
}

# Function to disable Game DVR
function Disable-GameDVR {
    Update-Progress "Disabling Game DVR..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Name "AllowGameDVR" -Value 0 -Type DWord -Force
}

# Function to disable hibernation
function Disable-Hibernation {
    Update-Progress "Disabling hibernation..."
    powercfg /hibernate off
}

# Function to disable HomeGroup
function Disable-HomeGroup {
    Update-Progress "Disabling HomeGroup..."
    if (Get-Service "HomeGroupProvider" -ErrorAction SilentlyContinue) {
        Stop-Service "HomeGroupProvider" -Force -ErrorAction SilentlyContinue
        Set-Service "HomeGroupProvider" -StartupType Disabled -ErrorAction SilentlyContinue
    }
}

# Function to disable location tracking
function Disable-LocationTracking {
    Update-Progress "Disabling location tracking..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" -Name "Value" -Value "Deny" -Force
}

# Function to disable Storage Sense
function Disable-StorageSense {
    Update-Progress "Disabling Storage Sense..."
    Remove-Item -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy" -Force -ErrorAction SilentlyContinue
}

# Function to disable Wi-Fi Sense
function Disable-WiFiSense {
    Update-Progress "Disabling Wi-Fi Sense..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi"
    Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting"
    Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots"
    
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting" -Name "Value" -Value 0 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots" -Name "Value" -Value 0 -Type DWord -Force
}

# Function to enable End Task with right click
function Enable-EndTaskRightClick {
    Update-Progress "Enabling End Task with right click..."
    Ensure-RegistryPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "TaskbarRightClickMenu" -Value 1 -Type DWord -Force
}

# Function to run disk cleanup
function Run-DiskCleanup {
    Update-Progress "Running disk cleanup..."
    cleanmgr /sagerun:1
}

# Function to disable PowerShell 7 telemetry
function Disable-PS7Telemetry {
    Update-Progress "Disabling PowerShell 7 telemetry..."
    [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine')
}

# Function to set services to manual
function Set-ServicesToManual {
    Update-Progress "Setting services to manual..."
    $services = @(
        "DiagTrack",
        "dmwappushservice",
        "RetailDemo",
        "diagnosticshub.standardcollector.service"
    )
    
    foreach ($service in $services) {
        if (Get-Service $service -ErrorAction SilentlyContinue) {
            Set-Service -Name $service -StartupType Manual -ErrorAction SilentlyContinue
            Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
        }
    }
}

# Function to disable recall
function Disable-Recall {
    Update-Progress "Disabling recall feature..."
    Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat"
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat" -Name "DisableUAR" -Value 1 -Type DWord -Force
}

# Function to remove Microsoft Store apps
function Remove-StoreApps {
    Update-Progress "Removing Microsoft Store apps..."
    
    # List of apps to keep (essential Windows apps)
    $keepApps = @(
        "Microsoft.WindowsStore",              # Microsoft Store itself
        "Microsoft.WindowsCalculator",         # Calculator
        "Microsoft.Windows.Photos",            # Photos
        "Microsoft.ScreenSketch",             # Snipping Tool
        "Microsoft.WindowsNotepad"            # Notepad
    )
    
    # Get all installed apps for the current user
    $installedApps = Get-AppxPackage -AllUsers | Where-Object { $_.Name -notlike "Windows.Client.WebExperience" }
    
    foreach ($app in $installedApps) {
        if ($keepApps -notcontains $app.Name) {
            try {
                Update-Progress "Removing app: $($app.Name)"
                Remove-AppxPackage -Package $app.PackageFullName -ErrorAction SilentlyContinue
                Remove-AppxProvisionedPackage -Online -PackageName $app.Name -ErrorAction SilentlyContinue
            }
            catch {
                Update-Progress "Failed to remove $($app.Name)"
            }
        }
    }
    
    # Remove provisioned packages
    $provisionedApps = Get-AppxProvisionedPackage -Online
    foreach ($app in $provisionedApps) {
        if ($keepApps -notcontains $app.DisplayName) {
            try {
                Update-Progress "Removing provisioned app: $($app.DisplayName)"
                Remove-AppxProvisionedPackage -Online -PackageName $app.PackageName -ErrorAction SilentlyContinue
            }
            catch {
                Update-Progress "Failed to remove provisioned package $($app.DisplayName)"
            }
        }
    }
}

# Main optimization function
function Start-Optimization {
    try {
        Create-RestorePoint
        Clean-TempFiles
        Disable-ConsumerFeatures
        Disable-Telemetry
        Disable-ActivityHistory
        Disable-AutomaticFolderDiscovery
        Disable-GameDVR
        Disable-Hibernation
        Disable-HomeGroup
        Disable-LocationTracking
        Disable-StorageSense
        Disable-WiFiSense
        Enable-EndTaskRightClick
        Run-DiskCleanup
        Disable-PS7Telemetry
        Set-ServicesToManual
        Disable-Recall
        Remove-StoreApps

        Update-Progress "Optimization completed successfully!`n`nPlease restart your computer for all changes to take effect."
        $optimizeButton.Enabled = $false
    }
    catch {
        Update-Progress "An error occurred during optimization:`n$($_.Exception.Message)"
    }
}

# Add click event to the optimize button
$optimizeButton.Add_Click({
    $result = [System.Windows.Forms.MessageBox]::Show(
        "This will make several changes to your Windows settings. A system restore point will be created before proceeding. Do you want to continue?",
        "Confirm Optimization",
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Warning
    )
    
    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
        Start-Optimization
    }
})

# Show the form
$form.ShowDialog() 