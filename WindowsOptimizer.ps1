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

# Helper function to ensure registry paths exist
function Ensure-RegistryPath {
    param($Path)
    if (!(Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }
}

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

# Create animation timer
$script:animationTimer = New-Object System.Windows.Forms.Timer
$script:animationTimer.Interval = 16  # ~60 FPS for smooth animation
$script:currentAngle = 0
$script:animationTimer.Add_Tick({
    try {
        if ($progressWheel -ne $null) {
            $script:currentAngle = ($script:currentAngle + 3) % 360  # Smaller increment for smoother rotation
            $progressWheel.UpdateAngle($script:currentAngle)
        }
    } catch {
        # Silently handle any animation errors
    }
})

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
$flowLayout.Padding = New-Object System.Windows.Forms.Padding(10)
$contentPanel.Controls.Add($flowLayout)

# Welcome Label with glass effect
$welcomeLabel = New-Object System.Windows.Forms.Label
$welcomeLabel.Text = 'Welcome to Windows Optimizer! This tool will help you optimize your Windows system with just a few clicks. Please make sure to save all your work before proceeding.'
$welcomeLabel.Font = New-Object System.Drawing.Font("Segoe UI Light", 10)
$welcomeLabel.ForeColor = [System.Drawing.Color]::FromArgb(240, 240, 240)
$welcomeLabel.BackColor = [System.Drawing.Color]::Transparent
$welcomeLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$welcomeLabel.AutoSize = $true
$welcomeLabel.MaximumSize = New-Object System.Drawing.Size(400, 0)
$welcomeLabel.MinimumSize = New-Object System.Drawing.Size(400, 120)
$welcomeLabel.Margin = New-Object System.Windows.Forms.Padding(40, 20, 40, 20)
$welcomeLabel.UseMnemonic = $false
$flowLayout.Controls.Add($welcomeLabel)

# Create container panel for button
$buttonContainer = New-Object System.Windows.Forms.Panel
$buttonContainer.BackColor = [System.Drawing.Color]::Transparent
$buttonContainer.Size = New-Object System.Drawing.Size(500, 80)
$buttonContainer.Margin = New-Object System.Windows.Forms.Padding(0)
$buttonContainer.Add_Paint({
    param($sender, $e)
    try {
        $graphics = $e.Graphics
        $rect = $sender.ClientRectangle
        $gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(150, 20, 20, 20),
            [System.Drawing.Color]::FromArgb(150, 30, 30, 30),
            45
        )
        $graphics.FillRectangle($gradientBrush, $rect)
        $gradientBrush.Dispose()
    }
    catch {
        Write-Warning "Failed to paint button container: $_"
    }
})
$flowLayout.Controls.Add($buttonContainer)

# Create Optimize Button
$optimizeButton = New-ModernButton "Start Optimization"
$optimizeButton.Location = New-Object System.Drawing.Point(140, 20)
$buttonContainer.Controls.Add($optimizeButton)

# Progress Label with glass effect
$progressLabel = New-Object System.Windows.Forms.Label
$progressLabel.Text = 'Click the button above to start optimization...'
$progressLabel.Font = New-Object System.Drawing.Font("Segoe UI Light", 9)
$progressLabel.ForeColor = [System.Drawing.Color]::FromArgb(220, 220, 220)
$progressLabel.BackColor = [System.Drawing.Color]::Transparent
$progressLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$progressLabel.AutoSize = $true
$progressLabel.MaximumSize = New-Object System.Drawing.Size(400, 0)
$progressLabel.MinimumSize = New-Object System.Drawing.Size(400, 80)
$progressLabel.Margin = New-Object System.Windows.Forms.Padding(40, 20, 40, 20)
$progressLabel.UseMnemonic = $false
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
    try {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $radius = 20
        $rect = $form.ClientRectangle

        # Top left corner
        $path.AddArc($rect.X, $rect.Y, $radius * 2, $radius * 2, 180, 90)
        # Top edge
        $path.AddLine($rect.X + $radius, $rect.Y, $rect.Right - $radius, $rect.Y)
        # Top right corner
        $path.AddArc($rect.Right - ($radius * 2), $rect.Y, $radius * 2, $radius * 2, 270, 90)
        # Right edge
        $path.AddLine($rect.Right, $rect.Y + $radius, $rect.Right, $rect.Bottom - $radius)
        # Bottom right corner
        $path.AddArc($rect.Right - ($radius * 2), $rect.Bottom - ($radius * 2), $radius * 2, $radius * 2, 0, 90)
        # Bottom edge
        $path.AddLine($rect.Right - $radius, $rect.Bottom, $rect.X + $radius, $rect.Bottom)
        # Bottom left corner
        $path.AddArc($rect.X, $rect.Bottom - ($radius * 2), $radius * 2, $radius * 2, 90, 90)
        # Left edge
        $path.AddLine($rect.X, $rect.Bottom - $radius, $rect.X, $rect.Y + $radius)

        $path.CloseFigure()
        $form.Region = [System.Drawing.Region]::new($path)
    }
    catch {
        Write-Warning "Failed to create rounded corners: $_"
    }
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

# Function to update progress with UI refresh
function Update-Progress {
    param($message)
    $progressLabel.Text = $message
    [System.Windows.Forms.Application]::DoEvents()
}

# Function to ensure UI stays responsive during operations
function Invoke-WithProgress {
    param(
        [ScriptBlock]$Action,
        [string]$TaskName
    )
    
    Update-Progress "Working: $TaskName..."
    
    # Execute the action in small chunks with UI updates
    $job = Start-Job -ScriptBlock {
        param($action)

        # Define helper function in the job scope
        function Ensure-RegistryPath {
            param($Path)
            if (!(Test-Path $Path)) {
                New-Item -Path $Path -Force | Out-Null
            }
        }

        # Execute the passed action
        . ([ScriptBlock]::Create($action))
    } -ArgumentList $Action.ToString()
    
    while ($job.State -eq 'Running') {
        Start-Sleep -Milliseconds 50  # Shorter sleep for more responsive UI
        [System.Windows.Forms.Application]::DoEvents()
    }
    
    Receive-Job -Job $job
    Remove-Job -Job $job
}

# Function to run disk cleanup
function Run-DiskCleanup {
    param($TaskName)
    
    Update-Progress "Working: $TaskName..."
    
    # Start cleanmgr in a separate process without waiting
    $process = Start-Process cleanmgr -ArgumentList "/sagerun:1" -PassThru -WindowStyle Hidden
    
    # Wait for a maximum of 30 seconds while keeping UI responsive
    $timeout = 30
    $elapsed = 0
    while (!$process.HasExited -and $elapsed -lt $timeout) {
        Start-Sleep -Milliseconds 500
        [System.Windows.Forms.Application]::DoEvents()
        $elapsed++
    }
    
    # If process is still running after timeout, we'll continue anyway
    if (!$process.HasExited) {
        Write-Warning "Disk cleanup is still running in background"
    }
}

# Function to safely remove a provisioned package
function Remove-StoreApps {
    param($TaskName)
    
    Update-Progress "Working: $TaskName..."
    
    # Essential Windows apps that should not be removed
    $keepApps = @(
        "Microsoft.WindowsStore",              # Microsoft Store
        "Microsoft.WindowsCalculator",         # Calculator
        "Microsoft.Windows.Photos",            # Photos
        "Microsoft.ScreenSketch",              # Snipping Tool
        "Microsoft.WindowsNotepad",            # Notepad
        "Microsoft.DesktopAppInstaller",       # App Installer
        "Microsoft.SecHealthUI",               # Windows Security
        "Microsoft.WindowsTerminal",           # Windows Terminal
        "Microsoft.WindowsCamera",             # Camera
        "Microsoft.HEIFImageExtension",        # HEIF Image Extensions
        "Microsoft.WebpImageExtension",        # Webp Image Extensions
        "Microsoft.VP9VideoExtensions",        # VP9 Video Extensions
        "Microsoft.WebMediaExtensions",        # Web Media Extensions
        "Microsoft.RawImageExtension",         # Raw Image Extension
        "Microsoft.MicrosoftEdge",             # Edge Browser Components
        "Microsoft.UI.Xaml",                   # UI Framework
        "Microsoft.VCLibs",                    # Visual C++ Libraries
        "Microsoft.Services.Store.Engagement", # Store Services
        "Microsoft.XboxIdentityProvider"       # Xbox Identity (required by Store)
    )

    # Pattern matches for system apps that should be kept
    $keepPatterns = @(
        "Microsoft.WindowsStore",
        "Microsoft.Windows.Shell",
        "Microsoft.SecHealth",
        "Microsoft.Windows.Cloud",
        "Microsoft.AAD.Broker",
        "Microsoft.AccountsControl",
        "Microsoft.AsyncTextService",
        "Microsoft.CredDialogHost",
        "Microsoft.ECApp",
        "Microsoft.LockApp",
        "Microsoft.Win32WebViewHost",
        "Microsoft.Windows.Apprep",
        "Microsoft.Windows.AssignedAccessLockApp",
        "Microsoft.Windows.CallingShellApp",
        "Microsoft.Windows.CapturePicker",
        "Microsoft.Windows.ContentDeliveryManager",
        "Microsoft.Windows.NarratorQuickStart",
        "Microsoft.Windows.OOBENetworkCaptivePortal",
        "Microsoft.Windows.OOBENetworkConnectionFlow",
        "Microsoft.Windows.ParentalControls",
        "Microsoft.Windows.PeopleExperienceHost",
        "Microsoft.Windows.PinningConfirmationDialog",
        "Microsoft.Windows.SecHealthUI",
        "Microsoft.Windows.SecureAssessmentBrowser",
        "Microsoft.Windows.ShellExperienceHost",
        "Microsoft.Windows.StartMenuExperienceHost",
        "Microsoft.Windows.System"
    )
    
    # Get all installed apps for the current user
    $installedApps = Get-AppxPackage -AllUsers | Where-Object { 
        $app = $_
        $isSystemApp = $false
        
        # Check if app matches any keep pattern
        foreach ($pattern in $keepPatterns) {
            if ($app.Name -like "$pattern*") {
                $isSystemApp = $true
                break
            }
        }
        
        # Keep if not system app and not in keepApps list
        -not $isSystemApp -and
        $_.Name -notlike "Windows.Client.WebExperience*" -and
        $keepApps -notcontains $_.Name.Split('_')[0]
    }
    
    foreach ($app in $installedApps) {
        try {
            Write-Host "Removing package: $($app.PackageFullName)"
            Remove-AppxPackage -Package $app.PackageFullName -ErrorAction SilentlyContinue
        }
        catch {
            Write-Warning "Failed to remove package: $($app.PackageFullName)"
        }
    }
    
    # Get all provisioned packages
    $provisionedApps = Get-AppxProvisionedPackage -Online | Where-Object {
        $app = $_
        $isSystemApp = $false
        
        # Check if app matches any keep pattern
        foreach ($pattern in $keepPatterns) {
            if ($app.DisplayName -like "$pattern*") {
                $isSystemApp = $true
                break
            }
        }
        
        # Keep if not system app and not in keepApps list
        -not $isSystemApp -and
        $app.DisplayName -notlike "Windows.Client.WebExperience*" -and
        $keepApps -notcontains $app.DisplayName.Split('_')[0]
    }
    
    foreach ($app in $provisionedApps) {
        try {
            Write-Host "Removing provisioned package: $($app.PackageName)"
            Remove-AppxProvisionedPackage -PackageName $app.PackageName -Online -AllUsers -ErrorAction SilentlyContinue
        }
        catch {
            Write-Warning "Failed to remove provisioned package: $($app.PackageName)"
        }
    }
}

# Main optimization function
function Start-Optimization {
    try {
        # Disable button during optimization
        $optimizeButton.Enabled = $false
        $optimizeButton.Text = "Optimizing..."

        # Run optimization tasks
        $tasks = @(
            @{ 
                Name = "Creating System Restore Point"
                Action = {
                    # Create registry key to allow more frequent restore points
                    if (!(Test-Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore")) {
                        New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Force | Out-Null
                    }
                    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -Name "SystemRestorePointCreationFrequency" -Value 0 -Type DWord -Force
                    Enable-ComputerRestore -Drive "C:\"
                    Checkpoint-Computer -Description "Before Windows Optimization" -RestorePointType "MODIFY_SETTINGS"
                }
            },
            @{ 
                Name = "Cleaning Temporary Files"
                Action = {
                    Remove-Item -Path "C:\Windows\Temp\*" -Force -Recurse -ErrorAction SilentlyContinue
                    Remove-Item -Path "$env:TEMP\*" -Force -Recurse -ErrorAction SilentlyContinue
                }
            },
            @{ 
                Name = "Running Disk Cleanup"
                Action = { Run-DiskCleanup -TaskName "Running Disk Cleanup" }
            },
            @{
                Name = "Disabling Consumer Features"
                Action = {
                    if (!(Test-Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent")) {
                        New-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent" -Force | Out-Null
                    }
                    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent" -Name "DisableWindowsConsumerFeatures" -Value 1 -Type DWord -Force
                }
            },
            @{
                Name = "Disabling Telemetry"
                Action = {
                    foreach ($path in @(
                        "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"
                    )) {
                        if (!(Test-Path $path)) {
                            New-Item -Path $path -Force | Out-Null
                        }
                        Set-ItemProperty -Path $path -Name "AllowTelemetry" -Value 0 -Type DWord -Force
                    }
                }
            },
            @{
                Name = "Disabling Activity History"
                Action = {
                    if (!(Test-Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System")) {
                        New-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Force | Out-Null
                    }
                    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "EnableActivityFeed" -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "PublishUserActivities" -Value 0 -Type DWord -Force
                }
            },
            @{
                Name = "Disabling Automatic Folder Discovery"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Automatic Folder Discovery" -Action {
                        Ensure-RegistryPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
                        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "NavPaneExpandToCurrentFolder" -Value 0 -Type DWord -Force
                    }
                }
            },
            @{
                Name = "Disabling Game DVR"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Game DVR" -Action {
                        Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR"
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Name "AllowGameDVR" -Value 0 -Type DWord -Force
                    }
                }
            },
            @{
                Name = "Disabling Hibernation"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Hibernation" -Action {
                        powercfg /hibernate off
                    }
                }
            },
            @{
                Name = "Disabling HomeGroup"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling HomeGroup" -Action {
                        if (Get-Service "HomeGroupProvider" -ErrorAction SilentlyContinue) {
                            Stop-Service "HomeGroupProvider" -Force -ErrorAction SilentlyContinue
                            Set-Service "HomeGroupProvider" -StartupType Disabled -ErrorAction SilentlyContinue
                        }
                    }
                }
            },
            @{
                Name = "Disabling Location Tracking"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Location Tracking" -Action {
                        Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" -Name "Value" -Value "Deny" -Force
                    }
                }
            },
            @{
                Name = "Disabling Storage Sense"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Storage Sense" -Action {
                        Remove-Item -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy" -Force -ErrorAction SilentlyContinue
                    }
                }
            },
            @{
                Name = "Disabling Wi-Fi Sense"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Wi-Fi Sense" -Action {
                        Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi"
                        Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting"
                        Ensure-RegistryPath "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots"
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting" -Name "Value" -Value 0 -Type DWord -Force
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots" -Name "Value" -Value 0 -Type DWord -Force
                    }
                }
            },
            @{
                Name = "Enabling End Task Right Click"
                Action = {
                    Invoke-WithProgress -TaskName "Enabling End Task Right Click" -Action {
                        Ensure-RegistryPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
                        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "TaskbarRightClickMenu" -Value 1 -Type DWord -Force
                    }
                }
            },
            @{
                Name = "Disabling PowerShell 7 Telemetry"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling PowerShell 7 Telemetry" -Action {
                        [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine')
                    }
                }
            },
            @{
                Name = "Configuring Services"
                Action = {
                    Invoke-WithProgress -TaskName "Configuring Services" -Action {
                        $services = @(
                            "DiagTrack",
                            "dmwappushservice",
                            "RetailDemo",
                            "diagnosticshub.standardcollector.service"
                        )
                        foreach ($service in $services) {
                            if (Get-Service $service -ErrorAction SilentlyContinue) {
                                Set-Service -Name $service -StartupType Disabled -ErrorAction SilentlyContinue
                                Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
                            }
                        }
                    }
                }
            },
            @{
                Name = "Disabling Recall Feature"
                Action = {
                    Invoke-WithProgress -TaskName "Disabling Recall Feature" -Action {
                        Ensure-RegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat"
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat" -Name "DisableUAR" -Value 1 -Type DWord -Force
                    }
                }
            },
            @{
                Name = "Removing Store Apps"
                Action = { Remove-StoreApps -TaskName "Removing Store Apps" }
            },
            # New task for Winget and Revo Uninstaller
            @{
                Name = "Installing Revo Uninstaller"
                Action = {
                    Invoke-WithProgress -TaskName "Installing Revo Uninstaller" -Action {
                        # Check if winget is installed
                        $wingetTest = Get-Command winget -ErrorAction SilentlyContinue
                        if (-not $wingetTest) {
                            Update-Progress "Winget not found. Installing Microsoft.DesktopAppInstaller..."
                            # Add the Microsoft Store source
                            Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
                        }
                        
                        # Now check again for winget
                        $wingetTest = Get-Command winget -ErrorAction SilentlyContinue
                        if ($wingetTest) {
                            Update-Progress "Installing Revo Uninstaller..."
                            # Install Revo Uninstaller silently
                            winget install --id RevoUninstaller.RevoUninstaller --silent --accept-source-agreements --accept-package-agreements
                        } else {
                            Update-Progress "Failed to install Winget. Skipping Revo Uninstaller installation."
                        }
                    }
                }
            }
        )

        foreach ($task in $tasks) {
            try {
                Update-Progress "Working: $($task.Name)..."
                & $task.Action
                [System.Windows.Forms.Application]::DoEvents()
            }
            catch {
                Write-Warning "Task '$($task.Name)' encountered an error: $_"
                # Continue with next task
            }
        }

        # Update status
        Update-Progress "Optimization completed successfully!`n`nPlease restart your computer for all changes to take effect."
        $optimizeButton.Enabled = $false
        $optimizeButton.Text = "Optimization Complete"
        $optimizeButton.BackColor = [System.Drawing.Color]::FromArgb(180, 40, 167, 69)
    }
    catch {
        # Show error
        $optimizeButton.Enabled = $true
        $optimizeButton.Text = "Start Optimization"
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

# Clean up when form closes
$form.Add_FormClosing({
    if ($progressWheel -ne $null) {
        $progressWheel.StopSpinning()
    }
})

# Show the form
$form.ShowDialog() 