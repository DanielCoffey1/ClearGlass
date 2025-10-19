using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ClearGlass.Services
{
    public class OptimizationService
    {
        public async Task TweakWindowsSettings()
        {
            try
            {
                // Create restore point
                await CreateRestorePoint();

                // Run PowerShell commands with elevated privileges
                string script = @"
                    Write-Host 'Creating system restore point...'
                    # Create Restore Point (with override for 24-hour limitation)
                    try {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force
                        Enable-ComputerRestore -Drive 'C:\'
                        Checkpoint-Computer -Description 'Before ClearGlass Optimization' -RestorePointType 'MODIFY_SETTINGS'
                        Write-Host 'Restore point created successfully'
                    } catch {
                        Write-Warning 'Could not create restore point. Continuing with optimization...'
                    }

                    Write-Host 'Cleaning temporary files...'
                    # Delete temporary files (skip locked files)
                    Get-ChildItem -Path 'C:\Windows\Temp\*' -File -Force | ForEach-Object {
                        try {
                            Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                        } catch {}
                    }
                    Get-ChildItem -Path $env:TEMP\* -File -Force | ForEach-Object {
                        try {
                            Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                        } catch {}
                    }
                    Write-Host 'Temporary files cleaned'

                    Write-Host 'Configuring Windows settings...'
                    # Disable Consumer Features
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1 -Type DWord -Force
                    Write-Host 'Consumer features disabled'

                    Write-Host 'Disabling telemetry...'
                    # Disable Telemetry
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    Write-Host 'Telemetry disabled'

                    Write-Host 'Disabling activity history...'
                    # Disable Activity History
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord -Force
                    Write-Host 'Activity history disabled'

                    Write-Host 'Configuring Explorer settings...'
                    # Disable Explorer Automatic Folder Type Discovery
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'AutoCheckSelect' -Value 0 -Type DWord -Force
                    Write-Host 'Explorer settings configured'

                    Write-Host 'Disabling GameDVR...'
                    # Disable GameDVR
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0 -Type DWord -Force
                    Write-Host 'GameDVR disabled'

                    Write-Host 'Disabling hibernation...'
                    # Disable Hibernation
                    powercfg /hibernate off
                    Write-Host 'Hibernation disabled'

                    Write-Host 'Disabling location tracking...'
                    # Disable Location Tracking
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Name 'SensorPermissionState' -Value 0 -Type DWord -Force
                    New-Item -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Name 'Status' -Value 0 -Type DWord -Force
                    Write-Host 'Location tracking disabled'

                    Write-Host 'Disabling Storage Sense...'
                    # Disable Storage Sense
                    Remove-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' -Recurse -ErrorAction SilentlyContinue
                    Write-Host 'Storage Sense disabled'

                    Write-Host 'Disabling Wi-Fi Sense...'
                    # Disable Wi-Fi Sense
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Name 'AutoConnectAllowedOEM' -Value 0 -Type DWord -Force
                    Write-Host 'Wi-Fi Sense disabled'

                    Write-Host 'Configuring taskbar settings...'
                    # Enable End Task with Right Click
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarRightClickMenu' -Value 1 -Type DWord -Force
                    Write-Host 'Taskbar settings configured'

                    Write-Host 'Running disk cleanup...'
                    # Run Disk Cleanup silently
                    Start-Process -FilePath cleanmgr -ArgumentList '/sagerun:1' -NoNewWindow -Wait
                    Write-Host 'Disk cleanup completed'

                    Write-Host 'Disabling PowerShell telemetry...'
                    # Disable PowerShell 7 Telemetry
                    [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine')
                    Write-Host 'PowerShell telemetry disabled'

                    Write-Host 'Configuring ReCall settings...'
                    # Check and Disable ReCall if installed
                    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\ReCall') {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\ReCall' -Name 'Enabled' -Value 0 -Type DWord -Force
                        Write-Host 'ReCall disabled'
                    }

                    Write-Host 'Configuring services...'
                    # Comprehensive service optimization based on Chris Titus Win Util
                    $servicesToModify = @(
                        @{Name='AJRouter'; StartupType='Disabled'; OriginalType='Manual'},
                        @{Name='ALG'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppIDSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppMgmt'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppReadiness'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppVClient'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='AppXSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Appinfo'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AssignedAccessManagerSvc'; StartupType='Disabled'; OriginalType='Manual'},
                        @{Name='AudioEndpointBuilder'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='AudioSrv'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='Audiosrv'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='AxInstSV'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BDESVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BFE'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='BITS'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='BTAGService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BcastDVRUserService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BluetoothUserService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BrokerInfrastructure'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='Browser'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BthAvctpSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='BthHFSrv'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='CDPSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='CDPUserSvc_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='COMSysApp'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CaptureService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CertPropSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ClipSVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ConsentUxUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CoreMessagingRegistrar'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='CredentialEnrollmentManagerUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CryptSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='CscService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DPS'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DcomLaunch'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DcpSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DevQueryBroker'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DeviceAssociationBrokerSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DeviceAssociationService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DeviceInstall'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DevicePickerUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DevicesFlowUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Dhcp'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DiagTrack'; StartupType='Disabled'; OriginalType='Automatic'},
                        @{Name='DialogBlockingService'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='DispBrokerDesktopSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DisplayEnhancementService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DmEnrollmentSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Dnscache'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DoSvc'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='DsSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DsmSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DusmSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='EFS'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='EapHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='EntAppSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='EventLog'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='EventSystem'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='FDResPub'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Fax'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='FontCache'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='FrameServer'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='FrameServerMonitor'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='GraphicsPerfSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='HomeGroupListener'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='HomeGroupProvider'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='HvHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='IEEtwCollectorService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='IKEEXT'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='InstallService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='InventorySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='IpxlatCfgSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='KeyIso'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='KtmRm'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='LSM'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='LanmanServer'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='LanmanWorkstation'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='LicenseManager'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='LxpSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MSDTC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MSiSCSI'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MapsBroker'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='McpManagementService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MessagingService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MicrosoftEdgeElevationService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MixedRealityOpenXRSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MpsSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='MsKeyboardFilter'; StartupType='Manual'; OriginalType='Disabled'},
                        @{Name='NPSMSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NaturalAuthentication'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NcaSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NcbService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NcdAutoSetup'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NetSetupSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NetTcpPortSharing'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='Netlogon'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='Netman'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NgcCtnrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NgcSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NlaSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='OneSyncSvc_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='P9RdrService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PNRPAutoReg'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PNRPsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PcaSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='PeerDistSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PenService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PerfHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PhoneSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PimIndexMaintenanceSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PlugPlay'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PolicyAgent'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Power'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='PrintNotify'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PrintWorkflowUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ProfSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='PushToInstall'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='QWAVE'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RasAuto'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RasMan'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RemoteAccess'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='RemoteRegistry'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='RetailDemo'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RmSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RpcEptMapper'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='RpcLocator'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RpcSs'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SCPolicySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SCardSvr'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SDRSVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SEMgrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SENS'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SNMPTRAP'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SNMPTrap'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SSDPSRV'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SamSs'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='ScDeviceEnum'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Schedule'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SecurityHealthService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Sense'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SensorDataService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SensorService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SensrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SessionEnv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SgrmBroker'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SharedAccess'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SharedRealitySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ShellHWDetection'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SmsRouter'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Spooler'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SstpSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='StateRepository'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='StiSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='StorSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='SysMain'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SystemEventsBroker'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TabletInputService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TapiSrv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TermService'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TextInputManagementService'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='Themes'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TieringEngineService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TimeBroker'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TimeBrokerSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TokenBroker'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TrkWks'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TroubleshootingSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TrustedInstaller'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UI0Detect'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UdkUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UevAgentService'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='UmRdpService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UnistoreSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UserDataSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UserManager'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='UsoSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='VGAuthService'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='VMTools'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='VSS'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='VacSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='VaultSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='W32Time'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WEPHOSTSVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WFDSConMgrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WMPNetworkSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WManSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WPDBusEnum'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WSService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WSearch'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='WaaSMedicSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WalletService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WarpJITSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WbioSrvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Wcmsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WcsPlugInService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WdNisSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WdiServiceHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WdiSystemHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WebClient'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Wecsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WerSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WiaRpc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WinDefend'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WinHttpAutoProxySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WinRM'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Winmgmt'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WlanSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WpcMonSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WpnService'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='WpnUserService_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='XblAuthManager'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='XblGameSave'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='XboxGipSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='XboxNetApiSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='autotimesvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='bthserv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='camsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='cbdhsvc_*'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='cloudidsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='dcsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='defragsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='diagnosticshub.standardcollector.service'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='diagsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='dmwappushservice'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='dot3svc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='edgeupdate'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='edgeupdatem'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='embeddedmode'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='fdPHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='fhsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='gpsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='hidserv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='icssvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='iphlpsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='lfsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='lltdsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='lmhosts'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='mpssvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='msiserver'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='netprofm'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='nsi'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='p2pimsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='p2psvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='perceptionsimulation'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='pla'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='seclogon'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='shpamsvc'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='smphost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='spectrum'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='sppsvc'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='ssh-agent'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='svsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='swprv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='tiledatamodelsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='tzautoupdate'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='uhssvc'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='upnphost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vds'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vm3dservice'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='vmicguestinterface'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicheartbeat'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmickvpexchange'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicrdv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicshutdown'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmictimesync'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicvmsession'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicvss'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmvss'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wbengine'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wcncsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='webthreatdefsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='webthreatdefusersvc_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='wercplsupport'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wisvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wlidsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wlpasvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wmiApSrv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='workfolderssvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wscsvc'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='wuauserv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wudfsvc'; StartupType='Manual'; OriginalType='Manual'}
                    )

                    $modifiedCount = 0
                    $skippedCount = 0
                    $errorCount = 0

                    foreach ($service in $servicesToModify) {
                        try {
                            $serviceName = $service.Name
                            $startupType = $service.StartupType
                            
                            # Handle wildcard services
                            if ($serviceName -like '*_*') {
                                $matchingServices = Get-Service | Where-Object { $_.Name -like $serviceName }
                                if ($matchingServices) {
                                    foreach ($matchingService in $matchingServices) {
                                        try {
                                            Write-Host ""Configuring wildcard service: $($matchingService.Name) -> $startupType""
                                            Stop-Service -Name $matchingService.Name -Force -ErrorAction SilentlyContinue
                                            Set-Service -Name $matchingService.Name -StartupType $startupType -ErrorAction Stop
                                            $modifiedCount++
                                            Write-Host ""Service $($matchingService.Name) configured successfully""
                            } catch {
                                            Write-Warning ""Could not configure wildcard service $($matchingService.Name): $($_.Exception.Message)""
                                            $errorCount++
                                        }
                                    }
                                } else {
                                    Write-Host ""No services found matching pattern: $serviceName""
                                    $skippedCount++
                                }
                            } else {
                                # Handle regular services
                                $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
                                if ($existingService) {
                                    Write-Host ""Configuring service: $serviceName -> $startupType""
                                    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
                                    Set-Service -Name $serviceName -StartupType $startupType -ErrorAction Stop
                                    $modifiedCount++
                                    Write-Host ""Service $serviceName configured successfully""
                                } else {
                                    Write-Host ""Service $serviceName not found, skipping""
                                    $skippedCount++
                                }
                            }
                        } catch {
                            Write-Warning ""Error configuring service $($service.Name): $($_.Exception.Message)""
                            $errorCount++
                        }
                    }
                    
                    Write-Host ""Service configuration completed: $modifiedCount modified, $skippedCount skipped, $errorCount errors""

                    Write-Host 'Resetting restore point settings...'
                    # Reset the restore point creation frequency back to default
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 1440 -Type DWord -Force
                    Write-Host 'Restore point settings reset'

                    Write-Host 'Optimization completed successfully!'
                ";

                // Save the script to a temporary file
                string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassOptimization.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                // Run PowerShell with elevated privileges
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start PowerShell process");
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    CustomMessageBox.Show(
                        "Windows settings have been successfully optimized!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    CustomMessageBox.Show(
                        "Some optimizations may not have completed successfully. Please check the system logs for more information.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Clean up the temporary script file
                File.Delete(scriptPath);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during optimization: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task TweakWindowsSettingsSilent()
        {
            try
            {
                await CreateRestorePoint();

                string script = @"
                    Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force
                    Checkpoint-Computer -Description 'Before ClearGlass Optimization' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction SilentlyContinue
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' -Name 'DisableWindowsConsumerFeatures' -Value 1 -Type DWord -Force
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord -Force
                    
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'AutoCheckSelect' -Value 0 -Type DWord -Force
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR' -Name 'AllowGameDVR' -Value 0 -Type DWord -Force
                    
                    powercfg /hibernate off
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}' -Name 'SensorPermissionState' -Value 0 -Type DWord -Force
                    New-Item -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration' -Name 'Status' -Value 0 -Type DWord -Force
                    
                    Remove-Item -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' -Recurse -ErrorAction SilentlyContinue
                    
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config' -Name 'AutoConnectAllowedOEM' -Value 0 -Type DWord -Force
                    
                    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarRightClickMenu' -Value 1 -Type DWord -Force
                    
                    Start-Process -FilePath cleanmgr -ArgumentList '/sagerun:1' -NoNewWindow -Wait
                    
                    [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Machine')
                    
                    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\ReCall') {
                        Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\ReCall' -Name 'Enabled' -Value 0 -Type DWord -Force
                    }
                    
                    $servicesToModify = @(
                        @{Name='AJRouter'; StartupType='Disabled'; OriginalType='Manual'},
                        @{Name='ALG'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppIDSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppMgmt'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppReadiness'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AppVClient'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='AppXSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Appinfo'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='AssignedAccessManagerSvc'; StartupType='Disabled'; OriginalType='Manual'},
                        @{Name='AudioEndpointBuilder'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='AudioSrv'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='Audiosrv'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='AxInstSV'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BDESVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BFE'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='BITS'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='BTAGService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BcastDVRUserService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BluetoothUserService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BrokerInfrastructure'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='Browser'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='BthAvctpSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='BthHFSrv'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='CDPSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='CDPUserSvc_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='COMSysApp'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CaptureService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CertPropSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ClipSVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ConsentUxUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CoreMessagingRegistrar'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='CredentialEnrollmentManagerUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='CryptSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='CscService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DPS'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DcomLaunch'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DcpSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DevQueryBroker'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DeviceAssociationBrokerSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DeviceAssociationService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DeviceInstall'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DevicePickerUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DevicesFlowUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Dhcp'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DiagTrack'; StartupType='Disabled'; OriginalType='Automatic'},
                        @{Name='DialogBlockingService'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='DispBrokerDesktopSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DisplayEnhancementService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DmEnrollmentSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Dnscache'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='DoSvc'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='DsSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DsmSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='DusmSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='EFS'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='EapHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='EntAppSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='EventLog'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='EventSystem'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='FDResPub'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Fax'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='FontCache'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='FrameServer'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='FrameServerMonitor'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='GraphicsPerfSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='HomeGroupListener'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='HomeGroupProvider'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='HvHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='IEEtwCollectorService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='IKEEXT'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='InstallService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='InventorySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='IpxlatCfgSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='KeyIso'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='KtmRm'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='LSM'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='LanmanServer'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='LanmanWorkstation'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='LicenseManager'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='LxpSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MSDTC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MSiSCSI'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MapsBroker'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='McpManagementService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MessagingService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MicrosoftEdgeElevationService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MixedRealityOpenXRSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='MpsSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='MsKeyboardFilter'; StartupType='Manual'; OriginalType='Disabled'},
                        @{Name='NPSMSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NaturalAuthentication'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NcaSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NcbService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NcdAutoSetup'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NetSetupSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NetTcpPortSharing'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='Netlogon'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='Netman'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NgcCtnrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NgcSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='NlaSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='OneSyncSvc_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='P9RdrService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PNRPAutoReg'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PNRPsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PcaSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='PeerDistSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PenService_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PerfHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PhoneSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PimIndexMaintenanceSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PlugPlay'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PolicyAgent'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Power'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='PrintNotify'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='PrintWorkflowUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ProfSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='PushToInstall'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='QWAVE'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RasAuto'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RasMan'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RemoteAccess'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='RemoteRegistry'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='RetailDemo'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RmSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RpcEptMapper'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='RpcLocator'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='RpcSs'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SCPolicySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SCardSvr'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SDRSVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SEMgrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SENS'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SNMPTRAP'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SNMPTrap'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SSDPSRV'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SamSs'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='ScDeviceEnum'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Schedule'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SecurityHealthService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Sense'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SensorDataService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SensorService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SensrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SessionEnv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SgrmBroker'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SharedAccess'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='SharedRealitySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='ShellHWDetection'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SmsRouter'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Spooler'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SstpSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='StateRepository'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='StiSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='StorSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='SysMain'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='SystemEventsBroker'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TabletInputService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TapiSrv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TermService'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TextInputManagementService'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='Themes'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TieringEngineService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TimeBroker'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TimeBrokerSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TokenBroker'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TrkWks'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='TroubleshootingSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='TrustedInstaller'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UI0Detect'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UdkUserSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UevAgentService'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='UmRdpService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UnistoreSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UserDataSvc_*'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='UserManager'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='UsoSvc'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='VGAuthService'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='VMTools'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='VSS'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='VacSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='VaultSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='W32Time'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WEPHOSTSVC'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WFDSConMgrSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WMPNetworkSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WManSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WPDBusEnum'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WSService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WSearch'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='WaaSMedicSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WalletService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WarpJITSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WbioSrvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Wcmsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WcsPlugInService'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WdNisSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WdiServiceHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WdiSystemHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WebClient'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Wecsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WerSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WiaRpc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WinDefend'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WinHttpAutoProxySvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WinRM'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='Winmgmt'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WlanSvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='WpcMonSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='WpnService'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='WpnUserService_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='XblAuthManager'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='XblGameSave'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='XboxGipSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='XboxNetApiSvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='autotimesvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='bthserv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='camsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='cbdhsvc_*'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='cloudidsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='dcsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='defragsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='diagnosticshub.standardcollector.service'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='diagsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='dmwappushservice'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='dot3svc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='edgeupdate'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='edgeupdatem'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='embeddedmode'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='fdPHost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='fhsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='gpsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='hidserv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='icssvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='iphlpsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='lfsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='lltdsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='lmhosts'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='mpssvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='msiserver'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='netprofm'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='nsi'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='p2pimsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='p2psvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='perceptionsimulation'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='pla'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='seclogon'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='shpamsvc'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='smphost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='spectrum'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='sppsvc'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='ssh-agent'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='svsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='swprv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='tiledatamodelsvc'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='tzautoupdate'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='uhssvc'; StartupType='Disabled'; OriginalType='Disabled'},
                        @{Name='upnphost'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vds'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vm3dservice'; StartupType='Manual'; OriginalType='Automatic'},
                        @{Name='vmicguestinterface'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicheartbeat'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmickvpexchange'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicrdv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicshutdown'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmictimesync'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicvmsession'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmicvss'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='vmvss'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wbengine'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wcncsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='webthreatdefsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='webthreatdefusersvc_*'; StartupType='Automatic'; OriginalType='Automatic'},
                        @{Name='wercplsupport'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wisvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wlidsvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wlpasvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wmiApSrv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='workfolderssvc'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wscsvc'; StartupType='AutomaticDelayedStart'; OriginalType='Automatic'},
                        @{Name='wuauserv'; StartupType='Manual'; OriginalType='Manual'},
                        @{Name='wudfsvc'; StartupType='Manual'; OriginalType='Manual'}
                    )

                    foreach ($service in $servicesToModify) {
                        try {
                            $serviceName = $service.Name
                            $startupType = $service.StartupType
                            
                            if ($serviceName -like '*_*') {
                                $matchingServices = Get-Service | Where-Object { $_.Name -like $serviceName }
                                if ($matchingServices) {
                                    foreach ($matchingService in $matchingServices) {
                                        try {
                                            Stop-Service -Name $matchingService.Name -Force -ErrorAction SilentlyContinue
                                            Set-Service -Name $matchingService.Name -StartupType $startupType -ErrorAction SilentlyContinue
                                        } catch {}
                                    }
                                }
                            } else {
                                $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
                                if ($existingService) {
                                    try {
                                        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
                                        Set-Service -Name $serviceName -StartupType $startupType -ErrorAction SilentlyContinue
                            } catch {}
                        }
                            }
                        } catch {}
                    }
                    
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 1440 -Type DWord -Force
                ";

                string scriptPath = Path.Combine(Path.GetTempPath(), "ClearGlassOptimizationSilent.ps1");
                await File.WriteAllTextAsync(scriptPath, script);

                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start PowerShell process");
                }

                await process.WaitForExitAsync();
                File.Delete(scriptPath);
            }
            catch (Exception ex)
            {
                // Log error but continue with main process
                System.Diagnostics.Debug.WriteLine($"Error during silent optimization: {ex.Message}");
            }
        }

        public async Task RemoveWindowsAIOnly()
        {
            try
            {
                await RemoveWindowsAIComponents();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during AI component removal: {ex.Message}\n\n" +
                    "The script has attempted to restore Windows Update services automatically.\n" +
                    "If you experience issues with Windows Update, you may need to restart your computer.",
                    "AI Removal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task RemoveWindowsAIOnlySilent()
        {
            try
            {
                await RemoveWindowsAIComponents();
            }
            catch (Exception ex)
            {
                // Log error but continue with main process
                System.Diagnostics.Debug.WriteLine($"Error during silent AI component removal: {ex.Message}");
            }
        }

        private async Task RemoveWindowsAIComponents()
        {
            try
            {
                // Get the path to the RemoveWindowsAi.ps1 script
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "RemoveWindowsAi.ps1");
                
                // Check if the script exists
                if (!File.Exists(scriptPath))
                {
                    CustomMessageBox.Show(
                        "Windows AI removal script not found. Skipping AI component removal.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Run the AI removal script with elevated privileges
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Force",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start AI removal process");
                }

                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    CustomMessageBox.Show(
                        "Windows AI removal completed with some warnings. This is normal for systems without AI components.",
                        "AI Removal Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error during AI component removal: {ex.Message}\n\nContinuing with other optimizations...",
                    "AI Removal Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task CreateRestorePoint()
        {
            try
            {
                // Enable System Restore if it's disabled
                using var enableProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Enable-ComputerRestore -Drive 'C:\'\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                enableProcess.Start();
                await enableProcess.WaitForExitAsync();

                // Create the restore point
                using var restoreProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Checkpoint-Computer -Description 'Before ClearGlass Optimization' -RestorePointType 'MODIFY_SETTINGS'\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                restoreProcess.Start();
                await restoreProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                // Log the error but continue with optimization
                System.Diagnostics.Debug.WriteLine($"Error creating restore point: {ex.Message}");
            }
        }
    }
} 