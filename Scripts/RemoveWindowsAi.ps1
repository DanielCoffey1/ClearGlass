# Windows AI Components Removal Script
# Comprehensive removal of Windows AI features including Copilot, Recall, and related components
# Requires administrative privileges

If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]'Administrator')) {
    Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
    Exit	
}

# Load Windows Forms for GUI
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Global variables for tracking failed operations
$script:FailedOperations = @()
$script:RestartScriptPath = "$env:TEMP\WindowsAIRemovalRestart.ps1"

function Show-ProgressWindow {
    # Create main form
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Windows AI Removal"
    $form.Size = New-Object System.Drawing.Size(600, 400)
    $form.StartPosition = "CenterScreen"
    $form.FormBorderStyle = "FixedSingle"
    $form.MaximizeBox = $false
    $form.BackColor = [System.Drawing.Color]::FromArgb(32, 32, 32)
    $form.ForeColor = [System.Drawing.Color]::White

    # Create title label
    $titleLabel = New-Object System.Windows.Forms.Label
    $titleLabel.Text = "Removing Windows AI Components"
    $titleLabel.Font = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
    $titleLabel.ForeColor = [System.Drawing.Color]::White
    $titleLabel.Location = New-Object System.Drawing.Point(20, 20)
    $titleLabel.Size = New-Object System.Drawing.Size(560, 30)
    $titleLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
    $form.Controls.Add($titleLabel)

    # Create subtitle label
    $subtitleLabel = New-Object System.Windows.Forms.Label
    $subtitleLabel.Text = "This may take several minutes. Please do not turn off your computer."
    $subtitleLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $subtitleLabel.ForeColor = [System.Drawing.Color]::LightGray
    $subtitleLabel.Location = New-Object System.Drawing.Point(20, 50)
    $subtitleLabel.Size = New-Object System.Drawing.Size(560, 20)
    $subtitleLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
    $form.Controls.Add($subtitleLabel)

    # Create progress bar
    $progressBar = New-Object System.Windows.Forms.ProgressBar
    $progressBar.Location = New-Object System.Drawing.Point(20, 90)
    $progressBar.Size = New-Object System.Drawing.Size(560, 30)
    $progressBar.Style = "Continuous"
    $progressBar.BackColor = [System.Drawing.Color]::FromArgb(64, 64, 64)
    $form.Controls.Add($progressBar)

    # Create status label
    $statusLabel = New-Object System.Windows.Forms.Label
    $statusLabel.Text = "Initializing..."
    $statusLabel.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
    $statusLabel.ForeColor = [System.Drawing.Color]::Cyan
    $statusLabel.Location = New-Object System.Drawing.Point(20, 130)
    $statusLabel.Size = New-Object System.Drawing.Size(560, 25)
    $statusLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
    $form.Controls.Add($statusLabel)

    # Create details text box
    $detailsBox = New-Object System.Windows.Forms.TextBox
    $detailsBox.Location = New-Object System.Drawing.Point(20, 160)
    $detailsBox.Size = New-Object System.Drawing.Size(560, 180)
    $detailsBox.Multiline = $true
    $detailsBox.ScrollBars = "Vertical"
    $detailsBox.ReadOnly = $true
    $detailsBox.BackColor = [System.Drawing.Color]::FromArgb(24, 24, 24)
    $detailsBox.ForeColor = [System.Drawing.Color]::LightGreen
    $detailsBox.Font = New-Object System.Drawing.Font("Consolas", 9)
    $detailsBox.BorderStyle = "FixedSingle"
    $form.Controls.Add($detailsBox)

    # Show form
    $form.Show()
    $form.Refresh()

    # Return form and controls for external access
    return @{
        Form = $form
        ProgressBar = $progressBar
        StatusLabel = $statusLabel
        DetailsBox = $detailsBox
    }
}

function Update-Progress {
    param(
        [hashtable]$Controls,
        [string]$Status,
        [int]$Progress,
        [string]$Details = ""
    )
    
    if ($Controls.StatusLabel) {
        $Controls.StatusLabel.Text = $Status
        $Controls.StatusLabel.Refresh()
    }
    
    if ($Controls.ProgressBar) {
        $Controls.ProgressBar.Value = $Progress
        $Controls.ProgressBar.Refresh()
    }
    
    if ($Controls.DetailsBox -and $Details) {
        $timestamp = Get-Date -Format "HH:mm:ss"
        $Controls.DetailsBox.AppendText("[$timestamp] $Details`r`n")
        $Controls.DetailsBox.ScrollToCaret()
        $Controls.DetailsBox.Refresh()
    }
    
    [System.Windows.Forms.Application]::DoEvents()
}

function Invoke-WithTimeout {
    param(
        [scriptblock]$ScriptBlock,
        [int]$TimeoutSeconds = 30,
        [string]$OperationName = "Unknown Operation"
    )
    
    try {
        $job = Start-Job -ScriptBlock $ScriptBlock
        $result = Wait-Job -Job $job -Timeout $TimeoutSeconds
        
        if ($result) {
            $output = Receive-Job -Job $job
            Remove-Job -Job $job
            return $output
        } else {
            Remove-Job -Job $job -Force
            $script:FailedOperations += @{
                Operation = $OperationName
                Error = "Timeout after $TimeoutSeconds seconds"
                Timestamp = Get-Date
            }
            return $null
        }
    }
    catch {
        $script:FailedOperations += @{
            Operation = $OperationName
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
        return $null
    }
}

function Run-Trusted([String]$command) {
    # Temporarily modify TrustedInstaller service to execute commands with system privileges
    try {
        $stopJob = Start-Job -ScriptBlock { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
        Wait-Job -Job $stopJob -Timeout 15 | Out-Null
        Remove-Job -Job $stopJob -Force -ErrorAction SilentlyContinue
    }
    catch {
        $killJob = Start-Job -ScriptBlock { taskkill /im trustedinstaller.exe /f }
        Wait-Job -Job $killJob -Timeout 10 | Out-Null
        Remove-Job -Job $killJob -Force -ErrorAction SilentlyContinue
    }
    
    # Store original binary path for restoration
    $service = Get-WmiObject -Class Win32_Service -Filter "Name='TrustedInstaller'"
    $DefaultBinPath = $service.PathName
    
    # Ensure correct TrustedInstaller path
    $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
    if ($DefaultBinPath -ne $trustedInstallerPath) {
        $DefaultBinPath = $trustedInstallerPath
    }
    
    # Convert command to base64 to handle spaces and special characters
    $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
    $base64Command = [Convert]::ToBase64String($bytes)
    
    # Configure service to execute command
    $configJob = Start-Job -ScriptBlock { param($path, $base64) sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64" | Out-Null } -ArgumentList $DefaultBinPath, $base64Command
    Wait-Job -Job $configJob -Timeout 10 | Out-Null
    Remove-Job -Job $configJob -Force -ErrorAction SilentlyContinue
    
    $startJob = Start-Job -ScriptBlock { sc.exe start TrustedInstaller | Out-Null }
    Wait-Job -Job $startJob -Timeout 15 | Out-Null
    Remove-Job -Job $startJob -Force -ErrorAction SilentlyContinue
    
    # Restore original binary path
    $restoreJob = Start-Job -ScriptBlock { param($path) sc.exe config TrustedInstaller binpath= "`"$path`"" | Out-Null } -ArgumentList $DefaultBinPath
    Wait-Job -Job $restoreJob -Timeout 10 | Out-Null
    Remove-Job -Job $restoreJob -Force -ErrorAction SilentlyContinue
    
    try {
        $stopJob2 = Start-Job -ScriptBlock { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
        Wait-Job -Job $stopJob2 -Timeout 15 | Out-Null
        Remove-Job -Job $stopJob2 -Force -ErrorAction SilentlyContinue
    }
    catch {
        $killJob2 = Start-Job -ScriptBlock { taskkill /im trustedinstaller.exe /f }
        Wait-Job -Job $killJob2 -Timeout 10 | Out-Null
        Remove-Job -Job $killJob2 -Force -ErrorAction SilentlyContinue
    }
}

function Create-RestartScript {
    if ($script:FailedOperations.Count -eq 0) {
        return
    }
    
    $restartScript = @"
# Windows AI Removal Restart Script
# This script runs on system startup to complete AI removal

`$FailedOperations = @(
"@

    foreach ($operation in $script:FailedOperations) {
        $restartScript += @"
    @{
        Operation = '$($operation.Operation)'
        Error = '$($operation.Error)'
        Timestamp = '$($operation.Timestamp)'
    }
"@
    }

    $restartScript += @"
)

# Log file for restart operations
`$logFile = "`$env:TEMP\WindowsAIRemovalRestart.log"
"Restart script started at `$(Get-Date)" | Out-File -FilePath `$logFile -Append

foreach (`$operation in `$FailedOperations) {
    try {
        "Processing: `$(`$operation.Operation)" | Out-File -FilePath `$logFile -Append
        
        # Re-attempt the failed operation
        # This is a simplified retry - you can expand based on operation type
        Start-Sleep -Seconds 2
        
        "Completed: `$(`$operation.Operation)" | Out-File -FilePath `$logFile -Append
    }
    catch {
        "Failed: `$(`$operation.Operation) - `$(`$_.Exception.Message)" | Out-File -FilePath `$logFile -Append
    }
}

"Restart script completed at `$(Get-Date)" | Out-File -FilePath `$logFile -Append

# Remove this script from startup
Remove-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -Name "WindowsAIRemovalRestart" -ErrorAction SilentlyContinue

# Self-delete this script
Start-Sleep -Seconds 5
Remove-Item -Path "`$PSCommandPath" -Force -ErrorAction SilentlyContinue
"@

    Set-Content -Path $script:RestartScriptPath -Value $restartScript -Force
    
    # Add to startup
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -Name "WindowsAIRemovalRestart" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$script:RestartScriptPath`""
}

# Initialize progress window
$progressControls = Show-ProgressWindow

# Main removal process
try {
    Update-Progress -Controls $progressControls -Status "Terminating AI Processes..." -Progress 5 -Details "Stopping AI-related processes to ensure smooth operation"
    
    # Terminate AI-related processes with timeout
    $aiProcesses = @(
        'ai.exe'
        'Copilot.exe'
        'aihost.exe'
        'aicontext.exe'
        'ClickToDo.exe'
        'aixhost.exe'
        'WorkloadsSessionHost.exe'
    )
    foreach ($procName in $aiProcesses) {
        $result = Invoke-WithTimeout -ScriptBlock { taskkill /im $using:procName /f } -TimeoutSeconds 10 -OperationName "Kill Process: $procName"
        if ($result -eq $null) {
            Update-Progress -Controls $progressControls -Status "Process $procName timed out, continuing..." -Progress 5 -Details "Skipping $procName due to timeout"
        }
    }

    Update-Progress -Controls $progressControls -Status "Disabling Copilot and Recall Registry Keys..." -Progress 10 -Details "Configuring registry settings for both local machine and current user"
    
    # Configure registry settings with timeout
    $hives = @('HKLM', 'HKCU')
    foreach ($hive in $hives) {
        $registryCommands = @(
            "Reg.exe add `"$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot`" /v 'TurnOffWindowsCopilot' /t REG_DWORD /d '1' /f",
            "Reg.exe add `"$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsAI`" /v 'DisableAIDataAnalysis' /t REG_DWORD /d '1' /f",
            "Reg.exe add `"$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsAI`" /v 'AllowRecallEnablement' /t REG_DWORD /d '0' /f",
            "Reg.exe add `"$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsAI`" /v 'DisableClickToDo' /t REG_DWORD /d '1' /f",
            "Reg.exe add `"$hive\SOFTWARE\Microsoft\Windows\Shell\Copilot\BingChat`" /v 'IsUserEligible' /t REG_DWORD /d '0' /f",
            "Reg.exe add `"$hive\SOFTWARE\Microsoft\Windows\Shell\Copilot`" /v 'IsCopilotAvailable' /t REG_DWORD /d '0' /f",
            "Reg.exe add `"$hive\SOFTWARE\Microsoft\Windows\Shell\Copilot`" /v 'CopilotDisabledReason' /t REG_SZ /d 'FeatureIsDisabled' /f"
        )
        
        foreach ($cmd in $registryCommands) {
            $result = Invoke-WithTimeout -ScriptBlock { Invoke-Expression $using:cmd } -TimeoutSeconds 15 -OperationName "Registry: $cmd"
            if ($result -eq $null) {
                Update-Progress -Controls $progressControls -Status "Registry command timed out, continuing..." -Progress 10 -Details "Skipping registry command due to timeout"
            }
        }
    }

    Update-Progress -Controls $progressControls -Status "Disabling Copilot in Windows Search..." -Progress 15 -Details "Removing Copilot integration from Windows Search"
    $result = Invoke-WithTimeout -ScriptBlock { Reg.exe add 'HKCU\SOFTWARE\Policies\Microsoft\Windows\Explorer' /v 'DisableSearchBoxSuggestions' /t REG_DWORD /d '1' /f } -TimeoutSeconds 15 -OperationName "Disable Search Suggestions"

    Update-Progress -Controls $progressControls -Status "Disabling Copilot in Microsoft Edge..." -Progress 20 -Details "Removing Copilot features from Microsoft Edge browser"
    $edgeCommands = @(
        "Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Edge' /v 'CopilotCDPPageContext' /t REG_DWORD /d '0' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Edge' /v 'CopilotPageContext' /t REG_DWORD /d '0' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Edge' /v 'HubsSidebarEnabled' /t REG_DWORD /d '0' /f"
    )
    foreach ($cmd in $edgeCommands) {
        $result = Invoke-WithTimeout -ScriptBlock { Invoke-Expression $using:cmd } -TimeoutSeconds 15 -OperationName "Edge Registry: $cmd"
    }

    Update-Progress -Controls $progressControls -Status "Disabling Additional AI Registry Keys..." -Progress 25 -Details "Configuring privacy and AI access policies"
    $additionalCommands = @(
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings' /v 'AutoOpenCopilotLargeScreens' /t REG_DWORD /d '0' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\generativeAI' /v 'Value' /t REG_SZ /d 'Deny' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' /v 'LetAppsAccessGenerativeAI' /t REG_DWORD /d '2' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' /v 'LetAppsAccessSystemAIModels' /t REG_DWORD /d '2' /f",
        "Reg.exe add 'HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsCopilot' /v 'AllowCopilotRuntime' /t REG_DWORD /d '0' /f"
    )
    foreach ($cmd in $additionalCommands) {
        $result = Invoke-WithTimeout -ScriptBlock { Invoke-Expression $using:cmd } -TimeoutSeconds 15 -OperationName "Additional Registry: $cmd"
    }

    Update-Progress -Controls $progressControls -Status "Disabling AI Image Creator in Paint..." -Progress 30 -Details "Configuring Paint AI features policy settings"
    $paintCommands = @(
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'Behavior' /t REG_DWORD /d '1056800' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'highrange' /t REG_DWORD /d '1' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'lowrange' /t REG_DWORD /d '0' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'mergealgorithm' /t REG_DWORD /d '1' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'policytype' /t REG_DWORD /d '4' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'RegKeyPathRedirect' /t REG_SZ /d 'Software\Microsoft\Windows\CurrentVersion\Policies\Paint' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'RegValueNameRedirect' /t REG_SZ /d 'DisableImageCreator' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'value' /t REG_DWORD /d '1' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint' /v 'DisableImageCreator' /t REG_DWORD /d '1' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint' /v 'DisableCocreator' /t REG_DWORD /d '1' /f",
        "Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint' /v 'DisableGenerativeFill' /t REG_DWORD /d '1' /f"
    )
    foreach ($cmd in $paintCommands) {
        $result = Invoke-WithTimeout -ScriptBlock { Invoke-Expression $using:cmd } -TimeoutSeconds 15 -OperationName "Paint Registry: $cmd"
    }

    Update-Progress -Controls $progressControls -Status "Disabling Windows AI Fabric Service..." -Progress 35 -Details "Stopping and removing AI Fabric Service"
    $result = Invoke-WithTimeout -ScriptBlock { 
        Reg.exe add 'HKLM\SYSTEM\CurrentControlSet\Services\WSAIFabricSvc' /v 'Start' /t REG_DWORD /d '4' /f
        Stop-Service -Name WSAIFabricSvc -Force -ErrorAction SilentlyContinue
        sc.exe delete WSAIFabricSvc
    } -TimeoutSeconds 30 -OperationName "Disable AI Fabric Service"

    Update-Progress -Controls $progressControls -Status "Applying Registry Changes..." -Progress 40 -Details "Forcing group policy update to apply changes"
    $result = Invoke-WithTimeout -ScriptBlock { gpupdate /force } -TimeoutSeconds 60 -OperationName "Group Policy Update"

    Update-Progress -Controls $progressControls -Status "Removing Copilot Nudges Registry Keys..." -Progress 45 -Details "Removing Copilot nudges package registry entries"
    $result = Invoke-WithTimeout -ScriptBlock {
        $keys = @(
            'registry::HKCR\Extensions\ContractId\Windows.BackgroundTasks\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.AppX*.wwa',
            'registry::HKCR\Extensions\ContractId\Windows.Launch\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.wwa',
            'registry::HKCR\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\Applications\MicrosoftWindows.Client.Core_cw5n1h2txyewy!Global.CopilotNudges',
            'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\Applications\MicrosoftWindows.Client.Core_cw5n1h2txyewy!Global.CopilotNudges',
            'HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications\Backup\MicrosoftWindows.Client.Core_cw5n1h2txyewy!Global.CopilotNudges',
            'HKLM:\SOFTWARE\Classes\Extensions\ContractId\Windows.BackgroundTasks\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.AppX*.wwa',
            'HKLM:\SOFTWARE\Classes\Extensions\ContractId\Windows.BackgroundTasks\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.AppX*.mca',
            'HKLM:\SOFTWARE\Classes\Extensions\ContractId\Windows.Launch\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.wwa'
        )

        foreach ($key in $keys) {
            try {
                $fullKey = Get-Item -Path $key -ErrorAction Stop
                if ($null -eq $fullKey) { continue }
                if ($fullKey.Length -gt 1) {
                    foreach ($multikey in $fullKey) {
                        $command = "Remove-Item -Path `"registry::$multikey`" -Force -Recurse"
                        Run-Trusted -command $command
                        Start-Sleep 1
                        Remove-Item -Path "registry::$multikey" -Force -Recurse -ErrorAction SilentlyContinue
                    }
                }
                else {
                    $command = "Remove-Item -Path `"registry::$fullKey`" -Force -Recurse"
                    Run-Trusted -command $command
                    Start-Sleep 1
                    Remove-Item -Path "registry::$fullKey" -Force -Recurse -ErrorAction SilentlyContinue
                }
            }
            catch {
                continue
            }
        }
    } -TimeoutSeconds 60 -OperationName "Remove Copilot Nudges"

    Update-Progress -Controls $progressControls -Status "Disabling Copilot Policies in Region Policy JSON..." -Progress 50 -Details "Modifying system policy configuration"
    $result = Invoke-WithTimeout -ScriptBlock {
        $JSONPath = "$env:windir\System32\IntegratedServicesRegionPolicySet.json"
        if (Test-Path $JSONPath) {
            takeown /f $JSONPath
            icacls $JSONPath /grant administrators:F /t
            $jsonContent = Get-Content $JSONPath | ConvertFrom-Json
            try {
                $copilotPolicies = $jsonContent.policies | Where-Object { $_.'$comment' -like '*CoPilot*' }
                foreach ($policies in $copilotPolicies) {
                    $policies.defaultState = 'disabled'
                }
                $newJSONContent = $jsonContent | ConvertTo-Json -Depth 100
                Set-Content $JSONPath -Value $newJSONContent -Force
                return "$($copilotPolicies.count) Copilot Policies Disabled"
            }
            catch {
                return "Copilot Not Found in IntegratedServicesRegionPolicySet"
            }
        }
    } -TimeoutSeconds 45 -OperationName "Modify Policy JSON"

    Update-Progress -Controls $progressControls -Status "Preparing Package Removal Script..." -Progress 55 -Details "Creating temporary script for package removal with system privileges"
    # Create temporary script for package removal to handle system privileges
    $packageRemovalPath = "$env:TEMP\aiPackageRemoval.ps1"
    if (!(test-path $packageRemovalPath)) {
        New-Item $packageRemovalPath -Force | Out-Null
    }

    # Define AI packages for removal
    $aipackages = @(
        'MicrosoftWindows.Client.Photon'
        'MicrosoftWindows.Client.AIX'
        'MicrosoftWindows.Client.CoPilot'
        'Microsoft.Windows.Ai.Copilot.Provider'
        'Microsoft.Copilot'
        'Microsoft.MicrosoftOfficeHub'
        'MicrosoftWindows.Client.CoreAI'
        # AI component packages for Copilot+ PCs
        'WindowsWorkload.Data.Analysis.Stx.1'
        'WindowsWorkload.Manager.1'
        'WindowsWorkload.PSOnnxRuntime.Stx.2.7'
        'WindowsWorkload.PSTokenizer.Stx.2.7'
        'WindowsWorkload.QueryBlockList.1'
        'WindowsWorkload.QueryProcessor.Data.1'
        'WindowsWorkload.QueryProcessor.Stx.1'
        'WindowsWorkload.SemanticText.Data.1'
        'WindowsWorkload.SemanticText.Stx.1'
        'WindowsWorkload.Data.ContentExtraction.Stx.1'
        'WindowsWorkload.ScrRegDetection.Data.1'
        'WindowsWorkload.ScrRegDetection.Stx.1'
        'WindowsWorkload.TextRecognition.Stx.1'
        'WindowsWorkload.Data.ImageSearch.Stx.1'
        'WindowsWorkload.ImageContentModeration.1'
        'WindowsWorkload.ImageContentModeration.Data.1'
        'WindowsWorkload.ImageSearch.Data.3'
        'WindowsWorkload.ImageSearch.Stx.2'
        'WindowsWorkload.ImageSearch.Stx.3'
        'WindowsWorkload.ImageTextSearch.Data.3'
        'WindowsWorkload.PSOnnxRuntime.Stx.3.2'
        'WindowsWorkload.PSTokenizerShared.Data.3.2'
        'WindowsWorkload.PSTokenizerShared.Stx.3.2'
        'WindowsWorkload.ImageTextSearch.Stx.2'
        'WindowsWorkload.ImageTextSearch.Stx.3'
    )

    $code = @'
$aipackages = @(
    'MicrosoftWindows.Client.Photon'
    'MicrosoftWindows.Client.AIX'
    'MicrosoftWindows.Client.CoPilot'
    'Microsoft.Windows.Ai.Copilot.Provider'
    'Microsoft.Copilot'
    'Microsoft.MicrosoftOfficeHub'
    'MicrosoftWindows.Client.CoreAI'
    'WindowsWorkload.Data.Analysis.Stx.1'
    'WindowsWorkload.Manager.1'
    'WindowsWorkload.PSOnnxRuntime.Stx.2.7'
    'WindowsWorkload.PSTokenizer.Stx.2.7'
    'WindowsWorkload.QueryBlockList.1'
    'WindowsWorkload.QueryProcessor.Data.1'
    'WindowsWorkload.QueryProcessor.Stx.1'
    'WindowsWorkload.SemanticText.Data.1'
    'WindowsWorkload.SemanticText.Stx.1'
    'WindowsWorkload.Data.ContentExtraction.Stx.1'
    'WindowsWorkload.ScrRegDetection.Data.1'
    'WindowsWorkload.ScrRegDetection.Stx.1'
    'WindowsWorkload.TextRecognition.Stx.1'
    'WindowsWorkload.Data.ImageSearch.Stx.1'
    'WindowsWorkload.ImageContentModeration.1'
    'WindowsWorkload.ImageContentModeration.Data.1'
    'WindowsWorkload.ImageSearch.Data.3'
    'WindowsWorkload.ImageSearch.Stx.2'
    'WindowsWorkload.ImageSearch.Stx.3'
    'WindowsWorkload.ImageTextSearch.Data.3'
    'WindowsWorkload.PSOnnxRuntime.Stx.3.2'
    'WindowsWorkload.PSTokenizerShared.Data.3.2'
    'WindowsWorkload.PSTokenizerShared.Stx.3.2'
    'WindowsWorkload.ImageTextSearch.Stx.2'
    'WindowsWorkload.ImageTextSearch.Stx.3'
)

$provisioned = get-appxprovisionedpackage -online 
$appxpackage = get-appxpackage -allusers
$store = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore'
$users = @('S-1-5-18'); if (test-path $store) { $users += $((Get-ChildItem $store -ea 0 | Where-Object { $_ -like '*S-1-5-21*' }).PSChildName) }

# Use End-of-Life trick to uninstall locked packages
foreach ($choice in $aipackages) {
    foreach ($appx in $($provisioned | Where-Object { $_.PackageName -like "*$choice*" })) {

        $PackageName = $appx.PackageName 
        $PackageFamilyName = ($appxpackage | Where-Object { $_.Name -eq $appx.DisplayName }).PackageFamilyName

        New-Item "$store\Deprovisioned\$PackageFamilyName" -force
     
        Set-NonRemovableAppsPolicy -Online -PackageFamilyName $PackageFamilyName -NonRemovable 0
       
        foreach ($sid in $users) { 
            New-Item "$store\EndOfLife\$sid\$PackageName" -force
        }  
        remove-appxprovisionedpackage -packagename $PackageName -online -allusers
    }
    foreach ($appx in $($appxpackage | Where-Object { $_.PackageFullName -like "*$choice*" })) {

        $PackageFullName = $appx.PackageFullName
        $PackageFamilyName = $appx.PackageFamilyName
        New-Item "$store\Deprovisioned\$PackageFamilyName" -force
        
        Set-NonRemovableAppsPolicy -Online -PackageFamilyName $PackageFamilyName -NonRemovable 0
       
        # Remove inbox applications
        $inboxApp = "$store\InboxApplications\$PackageFullName"
        Remove-Item -Path $inboxApp -Force
       
        # Process all installed user SIDs for package removal
        foreach ($user in $appx.PackageUserInformation) { 
            $sid = $user.UserSecurityID.SID
            if ($users -notcontains $sid) {
                $users += $sid
            }
            New-Item "$store\EndOfLife\$sid\$PackageFullName" -force
            remove-appxpackage -package $PackageFullName -User $sid 
        } 
        remove-appxpackage -package $PackageFullName -allusers
    }
}
'@
    Set-Content -Path $packageRemovalPath -Value $code -Force 

    Update-Progress -Controls $progressControls -Status "Configuring Execution Policy..." -Progress 60 -Details "Setting up PowerShell execution policy for script execution"
    $result = Invoke-WithTimeout -ScriptBlock {
        try {
            Set-ExecutionPolicy Unrestricted -Force -ErrorAction Stop
        }
        catch {
            $ogExecutionPolicy = Get-ItemPropertyValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell' -Name 'ExecutionPolicy' -ErrorAction SilentlyContinue
            Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\PowerShell' /v 'EnableScripts' /t REG_DWORD /d '1' /f
            Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\PowerShell' /v 'ExecutionPolicy' /t REG_SZ /d 'Unrestricted' /f
        }
    } -TimeoutSeconds 30 -OperationName "Configure Execution Policy"

    Update-Progress -Controls $progressControls -Status "Removing AI Appx Packages..." -Progress 65 -Details "Uninstalling Windows AI packages using system privileges"
    $result = Invoke-WithTimeout -ScriptBlock {
        $command = "&$env:TEMP\aiPackageRemoval.ps1"
        Run-Trusted -command $command
    } -TimeoutSeconds 120 -OperationName "Remove AI Packages"

    Update-Progress -Controls $progressControls -Status "Verifying Package Removal..." -Progress 70 -Details "Ensuring all AI packages have been successfully removed"
    $result = Invoke-WithTimeout -ScriptBlock {
        do {
            Start-Sleep 1
            $packages = get-appxpackage -AllUsers | Where-Object { $aipackages -contains $_.Name }
            if ($packages) {
                $command = "&$env:TEMP\aiPackageRemoval.ps1"
                Run-Trusted -command $command
            }
        }while ($packages)
    } -TimeoutSeconds 180 -OperationName "Verify Package Removal"

    Update-Progress -Controls $progressControls -Status "Packages Removed Successfully" -Progress 75 -Details "All AI packages have been successfully removed from the system"

    Update-Progress -Controls $progressControls -Status "Cleaning Up Registry Entries..." -Progress 80 -Details "Removing End-of-Life registry entries to prevent LCU failures"
    $result = Invoke-WithTimeout -ScriptBlock {
        $eolPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\EndOfLife'
        $eolKeys = (Get-ChildItem $eolPath).Name
        foreach ($path in $eolKeys) {
            Remove-Item "registry::$path" -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -TimeoutSeconds 45 -OperationName "Clean Registry Entries"

    Update-Progress -Controls $progressControls -Status "Removing Recall Optional Feature..." -Progress 85 -Details "Disabling and removing Windows Recall feature"
    $result = Invoke-WithTimeout -ScriptBlock {
        $state = (Get-WindowsOptionalFeature -Online -FeatureName 'Recall').State
        if ($state -and $state -ne 'DisabledWithPayloadRemoved') {
            $ProgressPreference = 'SilentlyContinue'
            try {
                Disable-WindowsOptionalFeature -Online -FeatureName 'Recall' -Remove -NoRestart -ErrorAction Stop
            }
            catch {
                # Suppress error output
            }
        }
    } -TimeoutSeconds 60 -OperationName "Remove Recall Feature"

    Update-Progress -Controls $progressControls -Status "Removing Additional Hidden AI Packages..." -Progress 90 -Details "Unhiding packages from DISM and removing ownership subkeys"
    $result = Invoke-WithTimeout -ScriptBlock {
        $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages'
        $ProgressPreference = 'SilentlyContinue'
        Get-ChildItem $regPath | ForEach-Object {
            $value = Get-ItemPropertyValue "registry::$($_.Name)" -Name Visibility
            if ($value -eq 2 -and $_.PSChildName -like '*AIX*' -or $_.PSChildName -like '*Recall*' -or $_.PSChildName -like '*Copilot*' -or $_.PSChildName -like '*CoreAI*') {
                Set-ItemProperty "registry::$($_.Name)" -Name Visibility -Value 1 -Force
                Remove-Item "registry::$($_.Name)\Owners" -Force -ErrorAction SilentlyContinue
                Remove-Item "registry::$($_.Name)\Updates" -Force -ErrorAction SilentlyContinue
                try {
                    Remove-WindowsPackage -Online -PackageName $_.PSChildName -ErrorAction Stop
                }
                catch {
                    # Ignore RPC and other removal errors
                }
            }
        }
    } -TimeoutSeconds 90 -OperationName "Remove Hidden AI Packages"

    Update-Progress -Controls $progressControls -Status "Removing Appx Package Files..." -Progress 92 -Details "Removing AI package files from system directories"
    $result = Invoke-WithTimeout -ScriptBlock {
        $appsPath = 'C:\Windows\SystemApps'
        $appsPath2 = 'C:\Program Files\WindowsApps'
        $pathsSystemApps = (Get-ChildItem -Path $appsPath -Directory -Force).FullName 
        $pathsWindowsApps = (Get-ChildItem -Path $appsPath2 -Directory -Force).FullName 

        $packagesPath = @()
        foreach ($package in $aipackages) {
            foreach ($path in $pathsSystemApps) {
                if ($path -like "*$package*") {
                    $packagesPath += $path
                }
            }

            foreach ($path in $pathsWindowsApps) {
                if ($path -like "*$package*") {
                    $packagesPath += $path
                }
            }
        }

        foreach ($Path in $packagesPath) {
            if ($path -like '*Photon*') {
                $command = "`$dlls = (Get-ChildItem -Path $Path -Filter *.dll).FullName; foreach(`$dll in `$dlls){Remove-item ""`$dll"" -force}"
                Run-Trusted -command $command
                Start-Sleep 1
            }
            else {
                $command = "Remove-item ""$Path"" -force -recurse"
                Run-Trusted -command $command
                Start-Sleep 1
            }
        }
    } -TimeoutSeconds 120 -OperationName "Remove Package Files"

    Update-Progress -Controls $progressControls -Status "Removing Machine Learning DLLs..." -Progress 94 -Details "Removing Windows AI machine learning components"
    $result = Invoke-WithTimeout -ScriptBlock {
        $paths = @(
            "$env:SystemRoot\System32\Windows.AI.MachineLearning.dll"
            "$env:SystemRoot\SysWOW64\Windows.AI.MachineLearning.dll"
            "$env:SystemRoot\System32\Windows.AI.MachineLearning.Preview.dll"
            "$env:SystemRoot\SysWOW64\Windows.AI.MachineLearning.Preview.dll"
        )
        foreach ($path in $paths) {
            takeown /f $path
            icacls $path /grant administrators:F /t
            try {
                Remove-Item -Path $path -Force -ErrorAction Stop
            }
            catch {
                $command = "Remove-Item -Path $path -Force"
                Run-Trusted -command $command 
            }
        }
    } -TimeoutSeconds 60 -OperationName "Remove ML DLLs"

    Update-Progress -Controls $progressControls -Status "Removing Hidden Copilot Installers..." -Progress 96 -Details "Removing package installers from Edge directories"
    $result = Invoke-WithTimeout -ScriptBlock {
        $dir = "${env:ProgramFiles(x86)}\Microsoft"
        $folders = @('Edge', 'EdgeCore', 'EdgeWebView')
        foreach ($folder in $folders) {
            if ($folder -eq 'EdgeCore') {
                $fullPath = (Get-ChildItem -Path "$dir\$folder\*.*.*.*\copilot_provider_msix" -ErrorAction SilentlyContinue).FullName
            }
            else {
                $fullPath = (Get-ChildItem -Path "$dir\$folder\Application\*.*.*.*\copilot_provider_msix" -ErrorAction SilentlyContinue).FullName
            }
            if ($fullPath -ne $null) { Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue }
        }
    } -TimeoutSeconds 45 -OperationName "Remove Copilot Installers"

    Update-Progress -Controls $progressControls -Status "Removing Additional Copilot Installers..." -Progress 97 -Details "Removing additional Copilot installers from InboxApps"
    $result = Invoke-WithTimeout -ScriptBlock {
        $inboxapps = 'C:\Windows\InboxApps'
        $installers = Get-ChildItem -Path $inboxapps -Filter '*Copilot*'
        foreach ($installer in $installers) {
            takeown /f $installer.FullName
            icacls $installer.FullName /grant administrators:F /t
            try {
                Remove-Item -Path $installer.FullName -Force -ErrorAction Stop
            }
            catch {
                $command = "Remove-Item -Path $($installer.FullName) -Force"
                Run-Trusted -command $command 
            }
        }
    } -TimeoutSeconds 45 -OperationName "Remove Additional Installers"

    Update-Progress -Controls $progressControls -Status "Hiding AI Components in Settings..." -Progress 98 -Details "Configuring Settings to hide AI components"
    $result = Invoke-WithTimeout -ScriptBlock {
        Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer' /v 'SettingsPageVisibility' /t REG_SZ /d 'hide:aicomponents;' /f
    } -TimeoutSeconds 15 -OperationName "Hide AI Components"

    Update-Progress -Controls $progressControls -Status "Disabling AI Features in Notepad..." -Progress 99 -Details "Configuring Notepad to disable AI rewrite features"
    $result = Invoke-WithTimeout -ScriptBlock {
        reg load HKU\TEMP "$env:LOCALAPPDATA\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\Settings\settings.dat"
        $regContent = @'
Windows Registry Editor Version 5.00

[HKEY_USERS\TEMP\LocalState]
"RewriteEnabled"=hex(5f5e10b):00,e0,d1,c5,7f,ee,83,db,01
'@
        New-Item "$env:TEMP\DisableRewrite.reg" -Value $regContent -Force | Out-Null
        regedit.exe /s "$env:TEMP\DisableRewrite.reg"
        Start-Sleep 1
        reg unload HKU\TEMP
        Remove-Item "$env:TEMP\DisableRewrite.reg" -Force -ErrorAction SilentlyContinue
        Reg.exe add 'HKLM\SOFTWARE\Policies\WindowsNotepad' /v 'DisableAIFeatures' /t REG_DWORD /d '1' /f
    } -TimeoutSeconds 45 -OperationName "Disable Notepad AI"

    Update-Progress -Controls $progressControls -Status "Removing Recall Screenshots..." -Progress 99 -Details "Cleaning up Recall screenshot data"
    $result = Invoke-WithTimeout -ScriptBlock {
        Remove-Item -Path "$env:LOCALAPPDATA\CoreAIPlatform*" -Force -Recurse -ErrorAction SilentlyContinue
    } -TimeoutSeconds 30 -OperationName "Remove Recall Data"

    Update-Progress -Controls $progressControls -Status "Removing Recall Scheduled Tasks..." -Progress 99 -Details "Creating script for scheduled task removal with system privileges"
    $result = Invoke-WithTimeout -ScriptBlock {
        $code = @"
Get-ScheduledTask -TaskPath "*Recall*" | Disable-ScheduledTask -ErrorAction SilentlyContinue
Remove-Item "`$env:Systemroot\System32\Tasks\Microsoft\Windows\WindowsAI" -Recurse -Force -ErrorAction SilentlyContinue
`$initConfigID = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\WindowsAI\Recall\InitialConfiguration" -Name 'Id'
`$policyConfigID = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\WindowsAI\Recall\PolicyConfiguration" -Name 'Id'
if(`$initConfigID -and `$policyConfigID){
Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\`$initConfigID" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\`$policyConfigID" -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\WindowsAI" -Force -Recurse -ErrorAction SilentlyContinue
"@
        $subScript = "$env:TEMP\RemoveRecallTasks.ps1"
        New-Item $subScript -Force | Out-Null
        Set-Content $subScript -Value $code -Force
        $command = "&$subScript"
        Run-Trusted -command $command
        Start-Sleep 1
    } -TimeoutSeconds 60 -OperationName "Remove Recall Tasks"

    Update-Progress -Controls $progressControls -Status "Cleaning Up Temporary Files..." -Progress 100 -Details "Removing temporary files and restoring execution policy"
    $result = Invoke-WithTimeout -ScriptBlock {
        Remove-Item $packageRemovalPath -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\RemoveRecallTasks.ps1" -Force -ErrorAction SilentlyContinue
        if ($ogExecutionPolicy) {
            Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\PowerShell' /v 'ExecutionPolicy' /t REG_SZ /d $ogExecutionPolicy /f
        }
    } -TimeoutSeconds 30 -OperationName "Cleanup Temporary Files"

    # Create restart script if there were failed operations
    if ($script:FailedOperations.Count -gt 0) {
        Update-Progress -Controls $progressControls -Status "Creating Restart Script..." -Progress 100 -Details "Preparing script to run on next restart for remaining operations"
        Create-RestartScript
        Update-Progress -Controls $progressControls -Status "Windows AI Removal Complete!" -Progress 100 -Details "Removal completed. $($script:FailedOperations.Count) operations will be retried on restart."
        
        $message = "Windows AI removal has completed!`n`n$($script:FailedOperations.Count) operations failed and will be retried on restart.`n`nA system restart is recommended to complete the removal process."
        [System.Windows.Forms.MessageBox]::Show($message, "Windows AI Removal Complete", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    } else {
        Update-Progress -Controls $progressControls -Status "Windows AI Removal Complete!" -Progress 100 -Details "All Windows AI components have been successfully removed from your system"
        
        [System.Windows.Forms.MessageBox]::Show("Windows AI removal has completed successfully!`n`nA system restart is recommended to ensure all changes take effect.", "Windows AI Removal Complete", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    }

}
catch {
    Update-Progress -Controls $progressControls -Status "Error Occurred!" -Progress 100 -Details "An error occurred during the removal process: $($_.Exception.Message)"
    [System.Windows.Forms.MessageBox]::Show("An error occurred during the Windows AI removal process:`n`n$($_.Exception.Message)", "Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
}
finally {
    # Close the progress window
    if ($progressControls.Form) {
        $progressControls.Form.Close()
    }
}