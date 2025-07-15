Get-AppxPackage -AllUsers |
    Where-Object {
        -not $_.IsFramework -and
        -not $_.IsResourcePackage -and
        -not $_.NonRemovable
    } |
    Select-Object Name, DisplayName, PackageFullName |
    ConvertTo-Json 