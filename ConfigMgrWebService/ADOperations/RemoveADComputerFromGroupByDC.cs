using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Reflection;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Remove a computer in Active Directory from a specific group on the specified domain controller.")]
        public bool RemoveADComputerFromGroupByDC(string secret, string groupName, string computerName, string domainController)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Set return value variable
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD object distinguished name for computer and group
                string computerDistinguishedName = (GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName, domainController)).Remove(0, 7);
                string groupDistinguishedName = GetADObject(groupName, ADObjectClass.Group, ADObjectType.distinguishedName, domainController);

                if (!string.IsNullOrEmpty(computerDistinguishedName) && !string.IsNullOrEmpty(groupDistinguishedName))
                {
                    try
                    {
                        //' Check if computer is member of group
                        var groupEntry = new DirectoryEntry(groupDistinguishedName);
                        List<string> groupMembers = GetADGroupMemberList(groupEntry);
                        bool memberOf = groupMembers.Contains(computerDistinguishedName);
                        if (memberOf == true)
                        {
                            //' Remove computer from group and commit
                            groupEntry.Properties["member"].Remove(computerDistinguishedName);
                            groupEntry.CommitChanges();

                            returnValue = true;
                        }

                        //' Dispose object
                        groupEntry.Dispose();
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(string.Format("An error occured when attempting to remove a computer object in Active Directory from a group. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}