<#

.SYNOPSIS

Return description of networks available to the user in a specific availability zone.


.DESCRIPTION

Uses Get-ChildItem cmdlet to retrieve array of objects corresponding to available networks.


.PARAMETER path

Path for availability zone, e.g. 'XDHyp:\HostingUnits\CloudPlatformHost\Zone1.availabilityzone'

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

Get-ChildItem -Path $path | Where-Object {$_.ObjectType -eq "Network"}
