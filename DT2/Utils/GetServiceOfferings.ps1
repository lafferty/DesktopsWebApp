<#

.SYNOPSIS

Return description of service offerings available to the user in their cloudstack account.


.DESCRIPTION

Uses Get-ChildItem cmdlet to retrieve array of objects corresponding to available service offerings.


.PARAMETER path

Path for hosting unit, e.g. 'XDHyp:\HostingUnits\CloudPlatformHost'

E.g. 
Request results for a specific catalog. Optional


(c)opyright Citrix Systems
Author: Donal Lafferty
Date: 02.01.2014
Version 0.1.0

You need to run this function as a user with admin access to XenDesktop

#>

Param (
    [string]$path
)

Add-pssnapin citrix.*

Get-ChildItem -Path $path | Where-Object {$_.ObjectType -eq "ServiceOffering"}
