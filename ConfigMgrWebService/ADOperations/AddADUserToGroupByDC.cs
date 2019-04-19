using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.Reflection;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Add a user in Active Directory to a specific group on the specified domain controller")]
        public bool AddADUserToGroupByDC(string secret, string groupName, string userName, string domainController)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD object distinguished name for computer and group
                string userDistinguishedName = (GetADObject(userName, ADObjectClass.User, ADObjectType.distinguishedName)).Remove(0, 7);
                string groupDistinguishedName = GetADObject(groupName, ADObjectClass.Group, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(userDistinguishedName) && !String.IsNullOrEmpty(groupDistinguishedName))
                {
                    try
                    {
                        //' Add user to group and commit
                        DirectoryEntry groupEntry = new DirectoryEntry(groupDistinguishedName);
                        groupEntry.Properties["member"].Add(userDistinguishedName);
                        groupEntry.CommitChanges();

                        //' Dispose object
                        groupEntry.Dispose();

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to add an user object in Active Directory to a group. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}