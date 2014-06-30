<#

.SYNOPSIS

Load log4net assembly so that scripts that have [log4net.ILog] parameter types can be used.


.DESCRIPTION

Load log4net assembly so that scripts that have [log4net.ILog] parameter types can be used.


.NOTES
    Version:        1.0
    Author:			Donal Lafferty
    Creation Date:	2014-07-07
    Purpose/Change: CloudDestkop
 
#>

# Add logging to DLL to allow testing from the commandline
$log4netPath = Resolve-Path "..\bin\log4net.dll"
[void][Reflection.Assembly]::LoadFrom($log4netPath)
