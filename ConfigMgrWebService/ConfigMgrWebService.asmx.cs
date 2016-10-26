using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections;
using System.Diagnostics;
using System.Web.Services;
using System.Management;
using System.Web.Configuration;
using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;

namespace ConfigMgrWebService
{
    [WebService(Name = "ConfigMgr Web Service 1.1.0", Description = "Web service for ConfigMgr Current Branch developed by Nickolaj Andersen", Namespace = "http://www.scconfigmgr.com")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]

    public class ConfigMgrWebService : System.Web.Services.WebService
    {
        //' Read required application settings from web.config
        private string secretKey = WebConfigurationManager.AppSettings["SecretKey"];
        private string siteServer = WebConfigurationManager.AppSettings["SiteServer"];
        private string siteCode = WebConfigurationManager.AppSettings["SiteCode"];

        [WebMethod(Description = "Get primary user(s) for a specific device")]
        public List<string> GetPrimaryUserByDevice(string deviceName, string secret)
        {
            //' Construct relation list
            var relations = new List<string>();

            //' Validate secret key
            if (secret != secretKey)
            {
                relations.Add("A secret key was not specified or cannot be validated");
                return relations;
            }
            else
            {
                //' Query for user relationship instances
                SelectQuery relationQuery = new SelectQuery("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceName like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, relationQuery);

                if (managementObjectSearcher != null)
                    foreach (var userRelation in managementObjectSearcher.Get())
                    {
                        //' Return user name
                        string userName = (string) userRelation.GetPropertyValue("UniqueUserName");
                        relations.Add(userName);
                    }
                //' Return empty
                return relations;
            }
        }

        [WebMethod(Description = "Get primary device(s) for a specific user")]
        public List<string> GetPrimaryDeviceByUser(string userName, string secret)
        {
            //' Construct relation list
            var relations = new List<string>();

            //' Validate secret key
            if (secret != secretKey)
            {
                relations.Add("A secret key was not specified or cannot be validated");
                return relations;
            }
            else
            {
                //' Query for device relationship instances
                SelectQuery relationQuery = new SelectQuery("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceName like '" + userName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, relationQuery);

                if (managementObjectSearcher != null)
                    foreach (var deviceRelation in managementObjectSearcher.Get())
                    {
                        //' Return device name
                        string deviceName = (string) deviceRelation.GetPropertyValue("ResourceName");
                        relations.Add(deviceName);
                    }
                //' Return empty
                return relations;
            }
        }

        [WebMethod(Description = "Get deployed applications for a specific user")]
        public List<Application> GetDeployedApplicationsByUser(string userName, string secret)
        {
            //' Construct applications list
            var applicationNames = new List<Application>();

            //' Validate secret key
            if (secret != secretKey)
            {
                return null;
            }
            else
            {
                //' Query for specified user
                SelectQuery userQuery = new SelectQuery("SELECT * FROM SMS_R_User WHERE UserName like '" + userName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, userQuery);

                if (managementObjectSearcher.Get() != null)
                    if (managementObjectSearcher.Get().Count == 1)
                        foreach (ManagementObject user in managementObjectSearcher.Get())
                        {
                            //' Define properties from user
                            string userNameProperty = (string)user.GetPropertyValue("UserName");
                            var resourceIDProperty = user.GetPropertyValue("ResourceId");

                            //' Query for collection memberships relations for user
                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                                foreach (ManagementObject collUser in collMembershipSearcher.Get())
                                {
                                    //' Define properties for collection
                                    string collectionId = (string)collUser.GetPropertyValue("CollectionID");

                                    //' Query for collection
                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            //' Define properties for collection
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            //' Query for deployment info for collection
                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    //' Return application object
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    string collectionName = (string)deployment.GetPropertyValue("CollectionName");
                                                    Application targetApplication = new Application();
                                                    targetApplication.ApplicationName = targetName;
                                                    targetApplication.CollectionName = collectionName;
                                                    applicationNames.Add(targetApplication);
                                                }
                                        }
                                }
                        }
                //' Return empty
                return applicationNames;
            }
        }

        [WebMethod(Description = "Get deployed applications for a specific device")]
        public List<string> GetDeployedApplicationsByDevice(string deviceName, string secret)
        {
            //' Construct applications list
            var applicationNames = new List<string>();

            //' Validate secret key
            if (secret != secretKey)
            {
                applicationNames.Add("A secret key was not specified or cannot be validated");
                return applicationNames;
            }
            else
            {
                //' Query for specified device name
                SelectQuery deviceQuery = new SelectQuery("SELECT * FROM SMS_R_System WHERE Name like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, deviceQuery);

                if (managementObjectSearcher.Get() != null)
                    if (managementObjectSearcher.Get().Count == 1)
                        foreach (ManagementObject device in managementObjectSearcher.Get())
                        {
                            //' Define property variables from device
                            string deviceNameProperty = (string)device.GetPropertyValue("Name");
                            var resourceIDProperty = device.GetPropertyValue("ResourceId");

                            //' Query for collection membership relations for device
                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                                foreach (ManagementObject collDevice in collMembershipSearcher.Get())
                                {
                                    //' Define property variables for collection
                                    string collectionId = (string)collDevice.GetPropertyValue("CollectionID");

                                    //' Query for collection
                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            //' Define collection properties
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            //' Query for deployment info for collection
                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    //' Return application name
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    applicationNames.Add(targetName);
                                                }
                                        }
                                }
                        }
                //' Return empty
                return applicationNames;
            }
        }

        [WebMethod(Description = "Get hidden task sequence deployments")]
        public List<taskSequence> GetHiddenTaskSequenceDeployments(string secret)
        {
            //' Construct hidden task sequences list
            var hiddenTaskSequences = new List<taskSequence>();

            //' Validate secret key
            if (secret != secretKey)
            {
                return hiddenTaskSequences;
            }
            else
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Define query string
                string query = "SELECT * FROM SMS_AdvertisementInfo WHERE PackageType = 4";

                //' Query for Task Sequence package
                IResultObject queryResults = connection.QueryProcessor.ExecuteQuery(query);

                foreach (IResultObject queryResult in queryResults)
                {
                    //' Collect property values from instance
                    string taskSequenceName = queryResult["PackageName"].StringValue;
                    string advertId = queryResult["AdvertisementId"].StringValue;
                    int advertFlags = queryResult["AdvertFlags"].IntegerValue;

                    //' Construct new taskSequence class object and define properties
                    taskSequence returnObject = new taskSequence();
                    returnObject.PackageName = taskSequenceName;
                    returnObject.AdvertFlags = advertFlags;
                    returnObject.AdvertisementId = advertId;

                    //' Add object to list if bit exists
                    if ((advertFlags & 0x20000000) != 0)
                        hiddenTaskSequences.Add(returnObject);
                }
                return hiddenTaskSequences;
            }
        }

        [WebMethod(Description = "Get Boot Image source version")]
        public string GetBootImageSourceVersion(string packageId, string secret)
        {
            //' Validate secret key
            if (secret != secretKey)
            {
                return "A secret key was not specified or cannot be validated";
            }
            else
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for Boot Image instance
                    IResultObject queryResult = connection.GetInstance("SMS_BootImagePackage.PackageID='" + packageId + "'");

                    if (queryResult != null)
                    {
                        //' Return SourceVersion property from instance
                        int sourceVersion = queryResult["SourceVersion"].IntegerValue;
                        return sourceVersion.ToString();
                    }
                    else
                    {
                        return "Unable to find any Boot Images";
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(DateTime.Now + ": Unhandled exception occured: " + ex.ToString());
                    return "Unhandled exception occured";
                }
            }
        }

        [WebMethod(Description = "Get all discovered users")]
        public List<user> GetDiscoveredUsers(string secret)
        {
            //' Construct users list
            var userList = new List<user>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for all discovered users
                string query = "SELECT * FROM SMS_R_User";
                IResultObject queryResults = connection.QueryProcessor.ExecuteQuery(query);

                if (queryResults != null)
                    foreach (IResultObject queryResult in queryResults)
                    {
                        //' Collect property values from instance
                        string fullUserName = queryResult["FullUserName"].StringValue;
                        string uniqueUserName = queryResult["UniqueUserName"].StringValue;
                        string userPrincipalName = queryResult["UserPrincipalName"].StringValue;
                        string userName = queryResult["UserName"].StringValue;
                        string resourceId = queryResult["ResourceId"].StringValue;
                        string windowsNTDomain = queryResult["WindowsNTDomain"].StringValue;
                        string fullDomainName = queryResult["FullDomainName"].StringValue;

                        //' Construct new user object
                        user user = new user();
                        user.fullUserName = fullUserName;
                        user.uniqueUserName = uniqueUserName;
                        user.userPrincipalName = userPrincipalName;
                        user.userName = userName;
                        user.resourceId = resourceId;
                        user.windowsNTDomain = windowsNTDomain;
                        user.fullDomainName = fullDomainName;

                        //' Add user object to user list
                        userList.Add(user);
                    }
            }

            //' Return list of users
            return userList;
        }
    }
}
