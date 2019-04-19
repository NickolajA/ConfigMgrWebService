using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Web;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Set ManagedBy attribute for a specific computer with specified user name on the specified domain controller.")]
        public bool SetADComputerManagedByByDC(string secret, string computerName, string userName, string domainController)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD computer and user object distinguished names
                string computerDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName, domainController);
                string userDistinguishedName = (GetADObject(userName, ADObjectClass.User, ADObjectType.distinguishedName, domainController)).Remove(0, 7);

                if (!string.IsNullOrEmpty(computerDistinguishedName) && !string.IsNullOrEmpty(userDistinguishedName))
                {
                    try
                    {
                        //' Add user to ManagedBy attribute and commit
                        var computerEntry = new DirectoryEntry(computerDistinguishedName);
                        computerEntry.Properties["ManagedBy"].Clear();
                        computerEntry.Properties["ManagedBy"].Add(userDistinguishedName);
                        computerEntry.CommitChanges();

                        //' Dispose object
                        computerEntry.Dispose();

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(string.Format("An error occured when attempting to add a user as ManagedBy for a computer object in Active Directory. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}