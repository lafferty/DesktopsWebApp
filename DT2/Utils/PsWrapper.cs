/*
 * Copyright (c) 2014 Citrix Systems, Inc. All Rights Reserved.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using DT2.Models;
using log4net;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace DT2.Utils
{
    public class PsWrapper
    {

        private static ILog logger = LogManager.GetLogger(typeof (PsWrapper));

        private const string DebugPrefix = "[Debug]";

        private string scriptPath;
        private bool debug;

        public PsWrapper(string scriptName) : this(scriptName, false)
        {
        }

        public PsWrapper(string scriptName, bool debug)
        {
            this.scriptPath = GetScriptPath(scriptName);
            this.debug = debug;
            IgnoreExceptions = new List<string>();
        }

        /// <summary>
        /// List of exceptions that may appear in the error output stream from the script that are expected and can
        /// safely be ignored (e.g. "ADIdentityNotFoundException").
        /// </summary>
        public List<string> IgnoreExceptions { get; private set; }

        public Collection<PSObject> RunPowerShell(Dictionary<string, object> arguments, LoginViewModel clientId)
        {
            // Start with identity assigned by IIS Application Pool
            var poolIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();

            // TODO: remove, this is for initial testing
            //scriptPath = Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder, "TestScript.ps1");
            Command command = new Command(scriptPath);

            if (arguments != null)
            {
                foreach (var argument in arguments)
                {
                    command.Parameters.Add(argument.Key, argument.Value);
                }
            }
            if (debug)
            {
                command.Parameters.Add("debug");
            }
            command.Parameters.Add("log", logger);

            // add the ndc context
            string ndcContext = log4net.NDC.Pop();
            log4net.NDC.Push(ndcContext);
            command.Parameters.Add("ndcContext", ndcContext);

            try
            {
                logger.Debug("Application Pool identity is " + poolIdentity.Name + ", but we will impersonate " +
                             clientId.UserName);

                if (System.Security.SecurityContext.IsWindowsIdentityFlowSuppressed())
                {
                    logger.Error("PowerShell calls will fail, because IsWindowsIdentityFlowSuppressed true");
                }

                // A RunSpace defines the operating system environment, which references HKCU\Environment
                // We create it now before impersonating an identity that might not have HKCU\Environment access
                // TODO: it may be possible to improve performance by using a RunSpacePool
                using (PowerShell powerShell = PowerShell.Create())
                {
                    Runspace runspace = RunspaceFactory.CreateRunspace();
                    runspace.Open();
                    powerShell.Runspace = runspace;
                    Collection<PSObject> results;

                    IntPtr handle;
                    SafeTokenHandle _handle;
                    /// Test:  generate primary login token
                    /// LOGON32_PROVIDER_DEFAULT = 0
                    /// 
                    logger.Debug("LogonUser call for " + clientId.UserNameNoDomain);
                    bool logonSuccess = NativeMethods.LogonUser(clientId.UserNameNoDomain, clientId.DomainName,
                        clientId.Password,
                        (int) LogonType.Interactive, 0, out handle);
                    _handle = new SafeTokenHandle(handle);

                    if (!logonSuccess)
                    {
                        string errMsg = "LogonUser() for " + clientId.UserName +
                                        "failed - no handle for user credentials:" +
                                        Marshal.GetLastWin32Error();
                        throw new Win32Exception(errMsg);
                    }


                    // When 'using' block ends, the thread reverts back to previous Windows identity,
                    // because under the hood WindowsImpersonationContext.Undo() is called by Dispose()
                    try
                    {
                        using (
                            WindowsImpersonationContext wic = WindowsIdentity.Impersonate(_handle.DangerousGetHandle()))
                        {
                            // WindowsIdentity will have changed to match clientId
                            var clientIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
                            logger.Debug("Application Pool identity is now " + clientIdentity.Name);

                            powerShell.Commands.AddCommand(command);
                            logger.Debug("Calling " + scriptPath);
                            results = powerShell.Invoke();
                        }
                    }
                    // Back to the original identity
                    finally
                    {
                        // dispose of the LogonUser handle
                        _handle.Dispose();
                        // clean up RunSpace used by the PowerShell object
                        runspace.Close();
                        runspace.Dispose();
                    }

                    // The order is important here. Debug messages are flushed to the log *before* checking for errors
                    // so the debug traces leading up to an error are not lost 
                    Collection<PSObject> filteredResults = new Collection<PSObject>();
                    foreach (PSObject result in results)
                    {
                        string output = result.BaseObject as string;
                        if ((output != null) && output.StartsWith(DebugPrefix))
                        {
                            if (debug)
                            {
                                logger.Info(output.Substring(DebugPrefix.Length));
                            }
                        }
                        else
                        {
                            filteredResults.Add(result);
                        }
                    }
                    foreach (DebugRecord r in powerShell.Streams.Debug)
                    {
                        logger.Info(r.Message);
                    }
                    logger.Debug("Examining powershell error records");
                    foreach (ErrorRecord r in powerShell.Streams.Error)
                    {
                        // If the exception doesn't match a "to be ignored" exception, then throw it
                        if (IgnoreExceptions.SingleOrDefault(i =>
                            i.Equals(r.Exception.GetType().FullName, StringComparison.InvariantCultureIgnoreCase)) ==
                            null)
                        {
                            logger.Error("Powershell reported exception:" + r.ErrorDetails);
                            throw r.Exception;
                        }
                    }
                    return filteredResults;
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
                logger.Error(e.Message);
                logger.Error(e.StackTrace);
                throw;
            }
        }
    

    private static string GetScriptPath(string inputScriptPath)
        {
            if (File.Exists(inputScriptPath))
            {
                if (!Path.IsPathRooted(inputScriptPath))
                {
                    return ".\\" + inputScriptPath;
                }
                return inputScriptPath;
            }

            string scriptPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), inputScriptPath);
            if (File.Exists(scriptPath))
            {
                return scriptPath;
            }
            throw new System.Configuration.ConfigurationErrorsException("Unable to locate script " + inputScriptPath);
        }
    }

    /// <summary>
    /// Specifies the type of login used.
    /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa378184.aspx
    /// </summary>
    /// <remarks>
    /// Source:  http://stackoverflow.com/a/7250145/939250
    /// License:  MIT, see https://github.com/mj1856/SimpleImpersonation/blob/master/LICENSE.txt
    /// </remarks>
    public enum LogonType
    {
        /// <summary>
        /// This logon type is intended for users who will be interactively using the computer, such as a user being logged
        /// on by a terminal server, remote shell, or similar process. This logon type has the additional expense of caching
        /// logon information for disconnected operations; therefore, it is inappropriate for some client/server applications,
        /// such as a mail server.
        /// </summary>
        Interactive = 2,

        /// <summary>
        /// This logon type is intended for high performance servers to authenticate plaintext passwords.
        /// The LogonUser function does not cache credentials for this logon type.
        /// </summary>
        Network = 3,

        /// <summary>
        /// This logon type is intended for batch servers, where processes may be executing on behalf of a user
        /// without their direct intervention. This type is also for higher performance servers that process many
        /// plaintext authentication attempts at a time, such as mail or web servers.
        /// </summary>
        Batch = 4,

        /// <summary>
        /// Indicates a service-type logon. The account provided must have the service privilege enabled. 
        /// </summary>
        Service = 5,

        /// <summary>
        /// GINAs are no longer supported.
        /// Windows Server 2003 and Windows XP:  This logon type is for GINA DLLs that log on users who will be
        /// interactively using the computer. This logon type can generate a unique audit record that shows when
        /// the workstation was unlocked.
        /// </summary>
        Unlock = 7,

        /// <summary>
        /// This logon type preserves the name and password in the authentication package, which allows the server
        /// to make connections to other network servers while impersonating the client. A server can accept plaintext
        /// credentials from a client, call LogonUser, verify that the user can access the system across the network,
        /// and still communicate with other servers.
        /// </summary>
        NetworkCleartext = 8,

        /// <summary>
        /// This logon type allows the caller to clone its current token and specify new credentials for outbound connections.
        /// The new logon session has the same local identifier but uses different credentials for other network connections.
        /// This logon type is supported only by the LOGON32_PROVIDER_WINNT50 logon provider.
        /// </summary>
        NewCredentials = 9,
    }

    /// <remarks>
    /// Source:  http://stackoverflow.com/a/7250145/939250
    /// License:  MIT, see https://github.com/mj1856/SimpleImpersonation/blob/master/LICENSE.txt
    /// </remarks>
    internal sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeTokenHandle(IntPtr handle)
            : base(true)
        {
            this.handle = handle;
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>
    /// Fuller domcumentation on pinvoke.net, which provides similar but not identical imports.
    /// </summary>
    internal class NativeMethods
    {
        [DllImport("advapi32.dll")]
        public static extern bool LogonUser(String lpszUserName,
            String lpszDomain,
            String lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
