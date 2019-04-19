using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Remove a computer object from Active Directory on the specified domain controller (Prohibits removal of domain controllers)")]
        public bool RemoveADComputerByDC(string secret, string samAccountName, string dc)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            bool returnValue = false;
            ComputerPrincipal compPrin = null;
            DirectoryEntry dirEntry = null;
            string respondingDC = null;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                using (ADDomain domain = Domain.GetComputerDomain())
                {
                    if (!domain.IsDC(samAccountName))
                    {
                        WriteEventLog(string.Format("{0} is not a domain controller.  Continuing with removal.", samAccountName), EventLogEntryType.Information);
                        compPrin = FindComputerObject(samAccountName, dc, out respondingDC);
                        if (compPrin == null)
                        {
                            WriteEventLog(string.Format("{0} was not found in active directory!", samAccountName), EventLogEntryType.Error);
                        }
                        else
                        {
                            dirEntry = (DirectoryEntry)compPrin.GetUnderlyingObject();
                            try
                            {
                                dirEntry.DeleteTree();
                                dirEntry.CommitChanges();
                                returnValue = true;
                                WriteEventLog(string.Format("{0} was successfully deleted from Active Directory on {1}.", samAccountName, respondingDC), EventLogEntryType.Information);
                            }
                            catch (DirectoryServicesCOMException comEx)
                            {
                                WriteEventLog(string.Format("{0}:{1}{1}{2}", comEx.Message, Environment.NewLine, comEx.ExtendedErrorMessage), EventLogEntryType.Error);
                            }
                            catch (AppDomainUnloadedException unloadEx)
                            {
                                WriteEventLog(string.Format("{0}{1}{1}{2}", unloadEx.Message, Environment.NewLine, unloadEx.StackTrace), EventLogEntryType.Error);
                            }
                            catch (Exception e)
                            {
                                WriteEventLog(e.Message, EventLogEntryType.Error);
                            }
                        }
                    }
                    else
                    {
                        WriteEventLog(string.Format("{0} matches the name of an existing domain controller!  We are not allowed to remove domain controllers.  Stopping the execution.", samAccountName), EventLogEntryType.Error);
                    }
                }
            }

            dirEntry.Dispose();
            compPrin.Dispose();

            MethodEnd(method);
            return returnValue;
        }
    }
}