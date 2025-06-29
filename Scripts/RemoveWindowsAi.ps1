#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Remove Windows AI components including Copilot, Recall, and related features.
.DESCRIPTION
    This script removes Windows AI components by:
    - Killing AI processes
    - Disabling registry keys and policies
    - Removing AppX packages
    - Cleaning up files and scheduled tasks
    - Disabling optional features
.PARAMETER Force
    Skip confirmation prompts
.EXAMPLE
    .\RemoveWindowsAi.ps1
.EXAMPLE
    .\RemoveWindowsAi.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch]$Force
)

#region Configuration
$script:Config = @{
    AIProcesses = @(
        'ai.exe'
        'Copilot.exe'
        'aihost.exe'
        'aicontext.exe'
        'ClickToDo.exe'
        'aixhost.exe'
        'WorkloadsSessionHost.exe'
        'wsaifabricsvc.exe'
    )
    
    AIPackages = @(
        'MicrosoftWindows.Client.Photon'
        'MicrosoftWindows.Client.AIX'
        'MicrosoftWindows.Client.CoPilot'
        'Microsoft.Windows.Ai.Copilot.Provider'
        'Microsoft.Copilot'
        'Microsoft.MicrosoftOfficeHub'
        'MicrosoftWindows.Client.CoreAI'
        # AI component packages installed on Copilot+ PCs
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
    
    MachineLearningDLLs = @(
        "$env:SystemRoot\System32\Windows.AI.MachineLearning.dll"
        "$env:SystemRoot\SysWOW64\Windows.AI.MachineLearning.dll"
        "$env:SystemRoot\System32\Windows.AI.MachineLearning.Preview.dll"
        "$env:SystemRoot\SysWOW64\Windows.AI.MachineLearning.Preview.dll"
    )
}
#endregion

#region Helper Functions

function Write-Status {
    param(
        [string]$Message,
        [bool]$IsError = $false
    )
    
    $color = if ($IsError) { 'Red' } else { 'Cyan' }
    $prefix = if ($IsError) { '!' } else { '-' }
    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-TrustedCommand {
    param([string]$Command, [int]$TimeoutSeconds = 300)
    
    try {
        Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop
    }
    catch {
        taskkill /im trustedinstaller.exe /f *>$null
    }
    
    # Get original binary path
    $service = Get-WmiObject -Class Win32_Service -Filter "Name='TrustedInstaller'"
    $defaultBinPath = $service.PathName
    $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
    
    if ($defaultBinPath -ne $trustedInstallerPath) {
        $defaultBinPath = $trustedInstallerPath
    }
    
    # Convert command to base64 to avoid errors with spaces
    $bytes = [System.Text.Encoding]::Unicode.GetBytes($Command)
    $base64Command = [Convert]::ToBase64String($bytes)
    
    # Change binary path to command
    sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
    
    # Run the command with timeout
    $job = Start-Job -ScriptBlock {
        param($serviceName)
        sc.exe start $serviceName
    } -ArgumentList "TrustedInstaller"
    
    # Wait for job completion with timeout
    if (Wait-Job -Job $job -Timeout $TimeoutSeconds) {
        Receive-Job -Job $job
    } else {
        Write-Status -Message "Command timed out after $TimeoutSeconds seconds" -IsError $true
        Stop-Job -Job $job -ErrorAction SilentlyContinue
    }
    
    Remove-Job -Job $job -ErrorAction SilentlyContinue
    
    # Restore original binary path
    sc.exe config TrustedInstaller binpath= "`"$defaultBinPath`"" | Out-Null
    
    try {
        Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop
    }
    catch {
        taskkill /im trustedinstaller.exe /f *>$null
    }
}

function Remove-FileWithOwnership {
    param([string]$FilePath)
    
    takeown /f $FilePath *>$null
    icacls $FilePath /grant administrators:F /t *>$null
    
    try {
        Remove-Item -Path $FilePath -Force -ErrorAction Stop
    }
    catch {
        # If takeown didn't work, remove with system privileges
        $command = "Remove-Item -Path '$FilePath' -Force"
        Invoke-TrustedCommand -Command $command
    }
}

#endregion

#region Main Functions

function Stop-AIProcesses {
    Write-Status -Message 'Killing AI Processes...'
    
    foreach ($processName in $Config.AIProcesses) {
        try {
            taskkill /im $processName /f *>$null
        }
        catch {
            # Process may not be running, continue silently
        }
    }
}

function Set-RegistryKeys {
    Write-Status -Message 'Disabling Copilot and Recall...'
    
    # Set registry keys for both HKLM and HKCU
    $hives = @('HKLM', 'HKCU')
    
    foreach ($hive in $hives) {
        Reg.exe add "$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot" /v 'TurnOffWindowsCopilot' /t REG_DWORD /d '1' /f *>$null
        Reg.exe add "$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" /v 'DisableAIDataAnalysis' /t REG_DWORD /d '1' /f *>$null
        Reg.exe add "$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" /v 'AllowRecallEnablement' /t REG_DWORD /d '0' /f *>$null
        Reg.exe add "$hive\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" /v 'DisableClickToDo' /t REG_DWORD /d '1' /f *>$null
        Reg.exe add "$hive\SOFTWARE\Microsoft\Windows\Shell\Copilot\BingChat" /v 'IsUserEligible' /t REG_DWORD /d '0' /f *>$null
        Reg.exe add "$hive\SOFTWARE\Microsoft\Windows\Shell\Copilot" /v 'IsCopilotAvailable' /t REG_DWORD /d '0' /f *>$null
        Reg.exe add "$hive\SOFTWARE\Microsoft\Windows\Shell\Copilot" /v 'CopilotDisabledReason' /t REG_SZ /d 'FeatureIsDisabled' /f *>$null
    }
    
    # User-specific registry keys
    Reg.exe add 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' /v 'ShowCopilotButton' /t REG_DWORD /d '0' /f *>$null
    Reg.exe add 'HKCU\Software\Microsoft\input\Settings' /v 'InsightsEnabled' /t REG_DWORD /d '0' /f *>$null
    
    # Additional registry keys
    Reg.exe add 'HKCU\SOFTWARE\Policies\Microsoft\Windows\Explorer' /v 'DisableSearchBoxSuggestions' /t REG_DWORD /d '1' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Edge' /v 'CopilotCDPPageContext' /t REG_DWORD /d '0' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Edge' /v 'CopilotPageContext' /t REG_DWORD /d '0' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Edge' /v 'HubsSidebarEnabled' /t REG_DWORD /d '0' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings' /v 'AutoOpenCopilotLargeScreens' /t REG_DWORD /d '0' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\generativeAI' /v 'Value' /t REG_SZ /d 'Deny' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' /v 'LetAppsAccessGenerativeAI' /t REG_DWORD /d '2' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' /v 'LetAppsAccessSystemAIModels' /t REG_DWORD /d '2' /f *>$null
    Reg.exe add 'HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsCopilot' /v 'AllowCopilotRuntime' /t REG_DWORD /d '0' /f *>$null
    
    # Disable AI image creator in Paint
    Write-Status -Message 'Disabling Image Creator In Paint...'
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'Behavior' /t REG_DWORD /d '1056800' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'highrange' /t REG_DWORD /d '1' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'lowrange' /t REG_DWORD /d '0' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'mergealgorithm' /t REG_DWORD /d '1' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'policytype' /t REG_DWORD /d '4' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'RegKeyPathRedirect' /t REG_SZ /d 'Software\Microsoft\Windows\CurrentVersion\Policies\Paint' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'RegValueNameRedirect' /t REG_SZ /d 'DisableImageCreator' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\PolicyManager\default\WindowsAI\DisableImageCreator' /v 'value' /t REG_DWORD /d '1' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint' /v 'DisableImageCreator' /t REG_DWORD /d '1' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint' /v 'DisableCocreator' /t REG_DWORD /d '1' /f *>$null
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint' /v 'DisableGenerativeFill' /t REG_DWORD /d '1' /f *>$null
    
    # Disable WSAIFabricSvc service
    Reg.exe add "HKLM\SYSTEM\CurrentControlSet\Services\WSAIFabricSvc" /v "Start" /t REG_DWORD /d "4" /f *>$null
    Stop-Service -Name WSAIFabricSvc -Force -ErrorAction SilentlyContinue
    
    Write-Status -Message 'Applying Registry Changes...'
    gpupdate /force *>$null
}

function Remove-CopilotNudges {
    Write-Status -Message 'Removing Copilot Nudges Registry Keys...'
    
    $nudgeKeys = @(
        'registry::HKCR\Extensions\ContractId\Windows.BackgroundTasks\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.AppX*.wwa'
        'registry::HKCR\Extensions\ContractId\Windows.Launch\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.wwa'
        'registry::HKCR\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\Applications\MicrosoftWindows.Client.Core_cw5n1h2txyewy!Global.CopilotNudges'
        'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\Applications\MicrosoftWindows.Client.Core_cw5n1h2txyewy!Global.CopilotNudges'
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications\Backup\MicrosoftWindows.Client.Core_cw5n1h2txyewy!Global.CopilotNudges'
        'HKLM:\SOFTWARE\Classes\Extensions\ContractId\Windows.BackgroundTasks\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.AppX*.wwa'
        'HKLM:\SOFTWARE\Classes\Extensions\ContractId\Windows.BackgroundTasks\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.AppX*.mca'
        'HKLM:\SOFTWARE\Classes\Extensions\ContractId\Windows.Launch\PackageId\MicrosoftWindows.Client.Core_*.*.*.*_x64__cw5n1h2txyewy\ActivatableClassId\Global.CopilotNudges.wwa'
    )
    
    foreach ($key in $nudgeKeys) {
        try {
            $fullKeys = Get-Item -Path $key -ErrorAction Stop
            if ($null -eq $fullKeys) { continue }
            
            if ($fullKeys.Length -gt 1) {
                foreach ($multiKey in $fullKeys) {
                    $command = "Remove-Item -Path 'registry::$multiKey' -Force -Recurse"
                    Invoke-TrustedCommand -Command $command
                    Start-Sleep 1
                    Remove-Item -Path "registry::$multiKey" -Force -Recurse -ErrorAction SilentlyContinue
                }
            }
            else {
                $command = "Remove-Item -Path 'registry::$fullKeys' -Force -Recurse"
                Invoke-TrustedCommand -Command $command
                Start-Sleep 1
                Remove-Item -Path "registry::$fullKeys" -Force -Recurse -ErrorAction SilentlyContinue
            }
        }
        catch {
            continue
        }
    }
}

function Update-IntegratedServicesPolicy {
    $jsonPath = "$env:windir\System32\IntegratedServicesRegionPolicySet.json"
    
    if (-not (Test-Path $jsonPath)) { return }
    
    Write-Status -Message 'Disabling CoPilot Policies in IntegratedServicesRegionPolicySet.json...'
    
    # Take ownership
    takeown /f $jsonPath *>$null
    icacls $jsonPath /grant administrators:F /t *>$null
    
    try {
        $jsonContent = Get-Content $jsonPath | ConvertFrom-Json
        $copilotPolicies = $jsonContent.policies | Where-Object { $_.'$comment' -like '*CoPilot*' }
        
        foreach ($policy in $copilotPolicies) {
            $policy.defaultState = 'disabled'
        }
        
        $newJsonContent = $jsonContent | ConvertTo-Json -Depth 100
        Set-Content $jsonPath -Value $newJsonContent -Force
        Write-Status -Message "$($copilotPolicies.Count) Copilot Policies Disabled"
    }
    catch {
        Write-Status -Message 'CoPilot Not Found in IntegratedServicesRegionPolicySet' -IsError $true
    }
}

function Remove-AIPackages {
    Write-Status -Message 'Preparing for AI Appx Package Removal...'
    
    # Initialize variables
    $skipServiceManagement = $false
    $originalStartupType = $null
    
    # Safety check: Ensure Windows Update services are in a good state before proceeding
    Write-Status -Message 'Checking Windows Update service status...'
    try {
        $wuauserv = Get-Service -Name wuauserv -ErrorAction Stop
        $bits = Get-Service -Name BITS -ErrorAction Stop
        
        # If services are stuck in a problematic state, try to reset them
        if ($wuauserv.Status -eq 'StartPending' -or $wuauserv.Status -eq 'StopPending') {
            Write-Status -Message 'Windows Update service is in a pending state, attempting to reset...' -IsError $true
            taskkill /im wuauserv.exe /f *>$null 2>&1
            Start-Sleep 5
        }
        
        if ($bits.Status -eq 'StartPending' -or $bits.Status -eq 'StopPending') {
            Write-Status -Message 'BITS service is in a pending state, attempting to reset...' -IsError $true
            taskkill /im svchost.exe /f *>$null 2>&1
            Start-Sleep 5
        }
        
        # Check if services are completely stuck
        if ($wuauserv.Status -eq 'StartPending' -or $wuauserv.Status -eq 'StopPending') {
            Write-Status -Message 'Windows Update service appears to be stuck, attempting reset...' -IsError $true
            if (-not (Reset-StuckServices)) {
                Write-Status -Message 'Service reset failed, skipping service management...' -IsError $true
                $skipServiceManagement = $true
            }
        }
    }
    catch {
        Write-Status -Message 'Could not check Windows Update service status, skipping service management...' -IsError $true
        $skipServiceManagement = $true
    }
    
    # Only manage services if they're not stuck
    if (-not $skipServiceManagement) {
        # Instead of disabling Windows Update service completely, use a more targeted approach
        Write-Status -Message 'Configuring Windows Update to prevent interference...'
        try {
            # Temporarily set Windows Update to manual start to prevent automatic interference
            $originalStartupType = (Get-Service -Name wuauserv).StartupType
            Set-Service -Name wuauserv -StartupType Manual -ErrorAction SilentlyContinue
            
            # Stop the service if it's running with timeout and force kill if needed
            if ((Get-Service -Name wuauserv).Status -eq 'Running') {
                Write-Status -Message 'Stopping Windows Update service...'
                
                # Try graceful stop first
                Stop-Service -Name wuauserv -Force -ErrorAction SilentlyContinue
                
                # Wait for service to stop with timeout
                $timeout = 30
                $elapsed = 0
                while ((Get-Service -Name wuauserv).Status -eq 'Running' -and $elapsed -lt $timeout) {
                    Start-Sleep 1
                    $elapsed++
                }
                
                # If service is still running, force kill the process
                if ((Get-Service -Name wuauserv).Status -eq 'Running') {
                    Write-Status -Message 'Windows Update service stuck, force killing...' -IsError $true
                    taskkill /im wuauserv.exe /f *>$null 2>&1
                    taskkill /im svchost.exe /f *>$null 2>&1
                    Start-Sleep 3
                }
                
                # Final check and wait
                if ((Get-Service -Name wuauserv).Status -eq 'Running') {
                    Write-Status -Message 'Windows Update service still running, continuing anyway...' -IsError $true
                } else {
                    Write-Status -Message 'Windows Update service stopped successfully'
                }
            }
            
            # Also stop related services that might interfere with timeout handling
            $relatedServices = @('BITS', 'DoSvc', 'UsoSvc')
            foreach ($service in $relatedServices) {
                if ((Get-Service -Name $service -ErrorAction SilentlyContinue).Status -eq 'Running') {
                    Write-Status -Message "Stopping $service service..."
                    Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
                    
                    # Wait with timeout
                    $timeout = 15
                    $elapsed = 0
                    while ((Get-Service -Name $service -ErrorAction SilentlyContinue).Status -eq 'Running' -and $elapsed -lt $timeout) {
                        Start-Sleep 1
                        $elapsed++
                    }
                    
                    # Force kill if still running
                    if ((Get-Service -Name $service -ErrorAction SilentlyContinue).Status -eq 'Running') {
                        Write-Status -Message "$service service stuck, force killing..." -IsError $true
                        taskkill /im svchost.exe /f *>$null 2>&1
                        Start-Sleep 2
                    }
                }
            }
        }
        catch {
            Write-Status -Message 'Could not configure Windows Update services' -IsError $true
        }
    } else {
        Write-Status -Message 'Skipping Windows Update service management due to stuck services...'
    }
    
    # Clear package cache to remove stuck packages
    Write-Status -Message 'Clearing package cache...'
    try {
        Remove-Item "$env:LOCALAPPDATA\Packages\*" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:ProgramData\Packages\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Status -Message 'Could not clear all package cache' -IsError $true
    }
    
    # Set non-removable policies for all AI packages before removal
    Write-Status -Message 'Setting package removal policies...'
    $provisioned = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
    $appxpackage = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    
    foreach ($package in $Config.AIPackages) {
        # Handle provisioned packages
        $provisionedPackages = $provisioned | Where-Object { $_.PackageName -like "*$package*" }
        foreach ($provPkg in $provisionedPackages) {
            $matchingAppx = $appxpackage | Where-Object { $_.Name -eq $provPkg.DisplayName }
            if ($matchingAppx) {
                try {
                    Set-NonRemovableAppsPolicy -Online -PackageFamilyName $matchingAppx.PackageFamilyName -NonRemovable 0 -ErrorAction SilentlyContinue
                }
                catch {
                    # Continue if this fails
                }
            }
        }
        
        # Handle installed packages
        $installedPackages = $appxpackage | Where-Object { $_.PackageFullName -like "*$package*" }
        foreach ($instPkg in $installedPackages) {
            try {
                Set-NonRemovableAppsPolicy -Online -PackageFamilyName $instPkg.PackageFamilyName -NonRemovable 0 -ErrorAction SilentlyContinue
            }
            catch {
                # Continue if this fails
            }
        }
    }
    
    Write-Status -Message 'Removing AI Appx Packages...'
    
    # Create package removal script
    $packageRemovalPath = "$env:TEMP\aiPackageRemoval.ps1"
    $packageRemovalCode = @"
`$aipackages = @(
$(($Config.AIPackages | ForEach-Object { "    '$_'" }) -join "`n")
)

`$provisioned = Get-AppxProvisionedPackage -Online 
`$appxpackage = Get-AppxPackage -AllUsers
`$store = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore'
`$users = @('S-1-5-18'); if (Test-Path `$store) { `$users += `$((Get-ChildItem `$store -ErrorAction 0 | Where-Object { `$_ -like '*S-1-5-21*' }).PSChildName) }

foreach (`$choice in `$aipackages) {
    foreach (`$appx in `$(`$provisioned | Where-Object { `$_.PackageName -like "*`$choice*" })) {
        `$PackageName = `$appx.PackageName 
        `$PackageFamilyName = (`$appxpackage | Where-Object { `$_.Name -eq `$appx.DisplayName }).PackageFamilyName

        New-Item "`$store\Deprovisioned\`$PackageFamilyName" -Force | Out-Null
        Set-NonRemovableAppsPolicy -Online -PackageFamilyName `$PackageFamilyName -NonRemovable 0
       
        foreach (`$sid in `$users) { 
            New-Item "`$store\EndOfLife\`$sid\`$PackageName" -Force | Out-Null
        }  
        Remove-AppxProvisionedPackage -PackageName `$PackageName -Online -AllUsers
    }
    
    foreach (`$appx in `$(`$appxpackage | Where-Object { `$_.PackageFullName -like "*`$choice*" })) {
        `$PackageFullName = `$appx.PackageFullName
        `$PackageFamilyName = `$appx.PackageFamilyName
        New-Item "`$store\Deprovisioned\`$PackageFamilyName" -Force | Out-Null
        
        Set-NonRemovableAppsPolicy -Online -PackageFamilyName `$PackageFamilyName -NonRemovable 0
       
        `$inboxApp = "`$store\InboxApplications\`$PackageFullName"
        Remove-Item -Path `$inboxApp -Force -ErrorAction SilentlyContinue
       
        foreach (`$user in `$appx.PackageUserInformation) { 
            `$sid = `$user.UserSecurityID.SID
            if (`$users -notcontains `$sid) {
                `$users += `$sid
            }
            New-Item "`$store\EndOfLife\`$sid\`$PackageFullName" -Force | Out-Null
            Remove-AppxPackage -Package `$PackageFullName -User `$sid 
        } 
        Remove-AppxPackage -Package `$PackageFullName -AllUsers
    }
}
"@
    
    Set-Content -Path $packageRemovalPath -Value $packageRemovalCode -Force
    
    # Set execution policy
    try {
        Set-ExecutionPolicy Unrestricted -Force -ErrorAction Stop
    }
    catch {
        $script:originalExecutionPolicy = Get-ItemPropertyValue -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell' -Name 'ExecutionPolicy' -ErrorAction SilentlyContinue
        Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\PowerShell' /v 'EnableScripts' /t REG_DWORD /d '1' /f *>$null
        Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\PowerShell' /v 'ExecutionPolicy' /t REG_SZ /d 'Unrestricted' /f *>$null
    }
    
    # Execute package removal with improved retry logic
    $command = "&'$packageRemovalPath'"
    $maxRetries = 3
    $retryCount = 0
    
    do {
        $retryCount++
        Write-Status -Message "Package removal attempt $retryCount of $maxRetries..."
        
        # Try direct execution first, then fall back to trusted command if needed
        try {
            $job = Start-Job -ScriptBlock {
                param($scriptPath)
                & $scriptPath
            } -ArgumentList $packageRemovalPath
            
            if (Wait-Job -Job $job -Timeout 120) {
                Receive-Job -Job $job
            } else {
                Write-Status -Message "Direct execution timed out, trying trusted command..." -IsError $true
                Stop-Job -Job $job -ErrorAction SilentlyContinue
                Invoke-TrustedCommand -Command $command -TimeoutSeconds 180
            }
            
            Remove-Job -Job $job -ErrorAction SilentlyContinue
        }
        catch {
            Write-Status -Message "Direct execution failed, using trusted command..." -IsError $true
            Invoke-TrustedCommand -Command $command -TimeoutSeconds 180
        }
        
        # Check if packages still exist
        Start-Sleep (2 * $retryCount)  # Progressive delay: 2s, 4s, 6s
        $packages = Get-AppxPackage -AllUsers | Where-Object { $Config.AIPackages -contains $_.Name }
        
        if ($packages -and $retryCount -lt $maxRetries) {
            Write-Status -Message "Some packages remain, retrying..." -IsError $true
        }
    } while ($packages -and $retryCount -lt $maxRetries)
    
    if ($packages) {
        Write-Status -Message "Some packages could not be removed after $maxRetries attempts" -IsError $true
        Write-Status -Message "Continuing with other removal methods..."
    } else {
        Write-Status -Message 'Packages Removed Successfully...'
    }
    
    # Cleanup
    Remove-Item $packageRemovalPath -Force -ErrorAction SilentlyContinue
    
    # Restore execution policy
    if ($script:originalExecutionPolicy) {
        Reg.exe add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\PowerShell' /v 'ExecutionPolicy' /t REG_SZ /d $script:originalExecutionPolicy /f *>$null
    }
    
    # Restore Windows Update services more safely
    if (-not $skipServiceManagement) {
        Write-Status -Message 'Restoring Windows Update services...'
        try {
            # Restore related services first
            $relatedServices = @('BITS', 'DoSvc', 'UsoSvc')
            foreach ($service in $relatedServices) {
                if ((Get-Service -Name $service -ErrorAction SilentlyContinue).StartupType -eq 'Disabled') {
                    Set-Service -Name $service -StartupType Automatic -ErrorAction SilentlyContinue
                }
                
                # Try to start the service with timeout
                if ((Get-Service -Name $service -ErrorAction SilentlyContinue).Status -ne 'Running') {
                    Write-Status -Message "Starting $service service..."
                    Start-Service -Name $service -ErrorAction SilentlyContinue
                    
                    # Wait for service to start
                    $timeout = 20
                    $elapsed = 0
                    while ((Get-Service -Name $service -ErrorAction SilentlyContinue).Status -ne 'Running' -and $elapsed -lt $timeout) {
                        Start-Sleep 1
                        $elapsed++
                    }
                    
                    if ((Get-Service -Name $service -ErrorAction SilentlyContinue).Status -ne 'Running') {
                        Write-Status -Message "$service service failed to start properly" -IsError $true
                    }
                }
            }
            
            # Restore Windows Update service with better handling
            if ($originalStartupType -and $originalStartupType -ne 'Manual') {
                Set-Service -Name wuauserv -StartupType $originalStartupType -ErrorAction SilentlyContinue
            } else {
                # Default to Automatic if we can't determine the original
                Set-Service -Name wuauserv -StartupType Automatic -ErrorAction SilentlyContinue
            }
            
            # Try to start Windows Update service with timeout
            if ((Get-Service -Name wuauserv -ErrorAction SilentlyContinue).Status -ne 'Running') {
                Write-Status -Message 'Starting Windows Update service...'
                Start-Service -Name wuauserv -ErrorAction SilentlyContinue
                
                # Wait for service to start with longer timeout
                $timeout = 45
                $elapsed = 0
                while ((Get-Service -Name wuauserv -ErrorAction SilentlyContinue).Status -ne 'Running' -and $elapsed -lt $timeout) {
                    Start-Sleep 1
                    $elapsed++
                }
                
                if ((Get-Service -Name wuauserv -ErrorAction SilentlyContinue).Status -ne 'Running') {
                    Write-Status -Message 'Windows Update service failed to start properly' -IsError $true
                    Write-Status -Message 'You may need to restart your computer to restore Windows Update' -IsError $true
                } else {
                    Write-Status -Message 'Windows Update service started successfully'
                }
            }
            
            Write-Status -Message 'Windows Update services restored successfully'
        }
        catch {
            Write-Status -Message 'Could not fully restore Windows Update services' -IsError $true
            Write-Status -Message 'You may need to manually restart Windows Update service or restart your computer' -IsError $true
        }
    } else {
        Write-Status -Message 'Skipped Windows Update service restoration due to previous issues'
        Write-Status -Message 'You may need to restart your computer to restore Windows Update services' -IsError $true
    }
}

function Remove-EndOfLifeKeys {
    Write-Status -Message 'Cleaning up End of Life registry keys...'
    
    $eolPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\EndOfLife'
    if (Test-Path $eolPath) {
        $eolKeys = (Get-ChildItem $eolPath).Name
        foreach ($path in $eolKeys) {
            Remove-Item "registry::$path" -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Remove-RecallFeature {
    Write-Status -Message 'Removing Recall Optional Feature...'
    
    try {
        $state = (Get-WindowsOptionalFeature -Online -FeatureName 'Recall').State
        if ($state -and $state -ne 'DisabledWithPayloadRemoved') {
            $ProgressPreference = 'SilentlyContinue'
            Disable-WindowsOptionalFeature -Online -FeatureName 'Recall' -Remove -NoRestart -ErrorAction Stop *>$null
        }
    }
    catch {
        # Feature may not be available
    }
}

function Remove-PackageFiles {
    Write-Status -Message 'Removing Appx Package Files...'
    
    $appsPaths = @(
        'C:\Windows\SystemApps'
        'C:\Program Files\WindowsApps'
    )
    
    $packagePaths = @()
    
    foreach ($appsPath in $appsPaths) {
        if (Test-Path $appsPath) {
            $paths = (Get-ChildItem -Path $appsPath -Directory -Force).FullName
            
            foreach ($package in $Config.AIPackages) {
                foreach ($path in $paths) {
                    if ($path -like "*$package*") {
                        $packagePaths += $path
                    }
                }
            }
        }
    }
    
    foreach ($path in $packagePaths) {
        if ($path -like '*Photon*') {
            # Only remove DLLs from Photon to prevent Start Menu from breaking
            $command = "`$dlls = (Get-ChildItem -Path '$path' -Filter *.dll).FullName; foreach(`$dll in `$dlls){ Remove-Item `"`$dll`" -Force }"
            Invoke-TrustedCommand -Command $command
        }
        else {
            $command = "Remove-Item '$path' -Force -Recurse"
            Invoke-TrustedCommand -Command $command
        }
        Start-Sleep 1
    }
}

function Remove-MachineLearningDLLs {
    Write-Status -Message 'Removing Machine Learning DLLs...'
    
    foreach ($dllPath in $Config.MachineLearningDLLs) {
        if (Test-Path $dllPath) {
            Remove-FileWithOwnership -FilePath $dllPath
        }
    }
}

function Remove-CopilotInstallers {
    Write-Status -Message 'Removing Hidden Copilot Installers...'
    
    $dir = "${env:ProgramFiles(x86)}\Microsoft"
    $folders = @('Edge', 'EdgeCore', 'EdgeWebView')
    
    foreach ($folder in $folders) {
        if ($folder -eq 'EdgeCore') {
            $fullPath = (Get-ChildItem -Path "$dir\$folder\*.*.*.*\copilot_provider_msix" -ErrorAction SilentlyContinue).FullName
        }
        else {
            $fullPath = (Get-ChildItem -Path "$dir\$folder\Application\*.*.*.*\copilot_provider_msix" -ErrorAction SilentlyContinue).FullName
        }
        
        if ($fullPath) {
            Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    
    # Remove additional installers
    $inboxApps = 'C:\Windows\InboxApps'
    if (Test-Path $inboxApps) {
        $installers = Get-ChildItem -Path $inboxApps -Filter '*Copilot*'
        foreach ($installer in $installers) {
            Remove-FileWithOwnership -FilePath $installer.FullName
        }
    }
}

function Set-AdditionalSettings {
    Write-Status -Message 'Hiding AI Components in Settings...'
    Reg.exe add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer' /v 'SettingsPageVisibility' /t REG_SZ /d 'hide:aicomponents;' /f *>$null
    
    Write-Status -Message 'Disabling Rewrite AI Feature for Notepad...'
    
    # Load Notepad settings
    reg load HKU\TEMP "$env:LOCALAPPDATA\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\Settings\settings.dat" *>$null
    
    # Add disable rewrite
    $regContent = @'
Windows Registry Editor Version 5.00

[HKEY_USERS\TEMP\LocalState]
"RewriteEnabled"=hex(5f5e10b):00,e0,d1,c5,7f,ee,83,db,01
'@
    
    $tempRegFile = "$env:TEMP\DisableRewrite.reg"
    New-Item $tempRegFile -Value $regContent -Force | Out-Null
    regedit.exe /s $tempRegFile
    Start-Sleep 1
    reg unload HKU\TEMP *>$null
    Remove-Item $tempRegFile -Force -ErrorAction SilentlyContinue
    
    # Modern method to disable AI in Notepad
    Reg.exe add 'HKLM\SOFTWARE\Policies\WindowsNotepad' /v 'DisableAIFeatures' /t REG_DWORD /d '1' /f *>$null
}

function Remove-RecallData {
    Write-Status -Message 'Removing Any Screenshots By Recall...'
    Remove-Item -Path "$env:LOCALAPPDATA\CoreAIPlatform*" -Force -Recurse -ErrorAction SilentlyContinue
    
    Write-Status -Message 'Removing Recall Scheduled Tasks...'
    
    $recallTaskCode = @"
Get-ScheduledTask -TaskPath "*Recall*" | Disable-ScheduledTask -ErrorAction SilentlyContinue
Remove-Item "`$env:Systemroot\System32\Tasks\Microsoft\Windows\WindowsAI" -Recurse -Force -ErrorAction SilentlyContinue

try {
    `$initConfigID = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\WindowsAI\Recall\InitialConfiguration" -Name 'Id'
    Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\`$initConfigID" -Recurse -Force -ErrorAction SilentlyContinue
} catch { }

try {
    `$policyConfigID = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\WindowsAI\Recall\PolicyConfiguration" -Name 'Id'
    Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tasks\`$policyConfigID" -Recurse -Force -ErrorAction SilentlyContinue
} catch { }

Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\Microsoft\Windows\WindowsAI" -Force -Recurse -ErrorAction SilentlyContinue
"@
    
    $subScript = "$env:TEMP\RemoveRecallTasks.ps1"
    Set-Content $subScript -Value $recallTaskCode -Force
    
    Invoke-TrustedCommand -Command "&'$subScript'"
    Start-Sleep 1
    
    Remove-Item $subScript -Force -ErrorAction SilentlyContinue
}

function Reset-StuckServices {
    Write-Status -Message 'Attempting to reset stuck Windows Update services...'
    
    # Kill all related processes
    $processes = @('wuauserv.exe', 'svchost.exe', 'trustedinstaller.exe')
    foreach ($proc in $processes) {
        taskkill /im $proc /f *>$null 2>&1
    }
    
    # Wait for processes to terminate
    Start-Sleep 5
    
    # Reset service state using sc.exe
    sc.exe stop wuauserv *>$null 2>&1
    sc.exe stop bits *>$null 2>&1
    sc.exe stop dosvc *>$null 2>&1
    sc.exe stop usosvc *>$null 2>&1
    
    Start-Sleep 3
    
    # Try to start services fresh
    sc.exe start bits *>$null 2>&1
    sc.exe start wuauserv *>$null 2>&1
    
    Start-Sleep 5
    
    # Check if services are now responsive
    try {
        $wuauserv = Get-Service -Name wuauserv -ErrorAction Stop
        if ($wuauserv.Status -ne 'StartPending' -and $wuauserv.Status -ne 'StopPending') {
            Write-Status -Message 'Windows Update services reset successfully'
            return $true
        }
    }
    catch {
        # Service still not responding
    }
    
    Write-Status -Message 'Could not reset Windows Update services' -IsError $true
    return $false
}

#endregion

#region Main Execution

function Main {
    # Check for administrator privileges
    if (-not (Test-Administrator)) {
        Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
        Exit
    }
    
    try {
        # Execute removal steps
        Stop-AIProcesses
        Set-RegistryKeys
        Remove-CopilotNudges
        Update-IntegratedServicesPolicy
        Remove-AIPackages
        Remove-EndOfLifeKeys
        Remove-RecallFeature
        Remove-PackageFiles
        Remove-MachineLearningDLLs
        Remove-CopilotInstallers
        Set-AdditionalSettings
        Remove-RecallData
        
        Write-Status -Message 'Windows AI removal completed successfully!'
    }
    catch {
        Write-Status -Message "Error during AI removal: $($_.Exception.Message)" -IsError $true
        Write-Status -Message 'Attempting emergency service restoration...' -IsError $true
        
        # Emergency service restoration
        try {
            if (-not (Reset-StuckServices)) {
                # Fallback to manual restoration
                $relatedServices = @('BITS', 'DoSvc', 'UsoSvc', 'wuauserv')
                foreach ($service in $relatedServices) {
                    if ((Get-Service -Name $service -ErrorAction SilentlyContinue).StartupType -eq 'Disabled') {
                        Set-Service -Name $service -StartupType Automatic -ErrorAction SilentlyContinue
                    }
                    Start-Service -Name $service -ErrorAction SilentlyContinue
                }
            }
            Write-Status -Message 'Emergency service restoration completed'
        }
        catch {
            Write-Status -Message 'Emergency service restoration failed' -IsError $true
        }
    }
    finally {
        # Completion
        if (-not $Force) {
            $input = Read-Host 'Done! Press Any Key to Exit'
            if ($input) { exit }
        }
    }
}

#endregion

# Execute main function
Main
