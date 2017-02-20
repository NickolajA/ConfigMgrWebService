using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections;
using System.Diagnostics;
using System.Web.Services;
using System.Management;
using System.Web.Configuration;
using System.Data.SqlClient;
using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;
using System.Text;
using System.Data;

namespace ConfigMgrWebService
{
    [WebService(Name = "ConfigMgr Web Service", Description = "Web service for ConfigMgr Current Branch developed by Nickolaj Andersen (v1.1.0)", Namespace = "http://www.scconfigmgr.com")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]

    public class ConfigMgrWebService : System.Web.Services.WebService
    {
        //' Read required application settings from web.config
        private string secretKey = WebConfigurationManager.AppSettings["SecretKey"];
        private string siteServer = WebConfigurationManager.AppSettings["SiteServer"];
        private string siteCode = WebConfigurationManager.AppSettings["SiteCode"];
        private string sqlServer = WebConfigurationManager.AppSettings["SQLServer"];
        private string sqlInstance = WebConfigurationManager.AppSettings["SQLInstance"];
        private string mdtDatabase = WebConfigurationManager.AppSettings["MDTDatabase"];

        //' Initialize event logging
        private EventLog eventLog;
        private void InitializeComponent()
        {
            this.eventLog = new EventLog();
            ((System.ComponentModel.ISupportInitialize)(this.eventLog)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.eventLog)).EndInit();
        }

        [WebMethod(Description = "Get primary user(s) for a specific device")]
        public List<string> GetCMPrimaryUserByDevice(string deviceName, string secret)
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
        public List<string> GetCMPrimaryDeviceByUser(string userName, string secret)
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
        public List<Application> GetCMDeployedApplicationsByUser(string userName, string secret)
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
        public List<string> GetCMDeployedApplicationsByDevice(string deviceName, string secret)
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

        [WebMethod(Description = "Get all hidden task sequence deployments")]
        public List<taskSequence> GetCMHiddenTaskSequenceDeployments(string secret)
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

        [WebMethod(Description = "Get resource id for device by UUID (SMSBIOSGUID)")]
        public string GetCMDeviceResourceIDByUUID(string secret, string uuid)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for device resource
                string query = String.Format("SELECT * FROM SMS_R_System WHERE SMBIOSGUID like '{0}'", uuid);
                IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                string resourceId = string.Empty;

                if (result != null)
                {
                    foreach (IResultObject device in result)
                    {
                        int id = device["ResourceId"].IntegerValue;
                        resourceId = id.ToString();
                    }
                }

                return resourceId;
            }
            else
            {
                return string.Empty;
            }
        }

        [WebMethod(Description = "Get resource id for device by MAC Address")]
        public string GetCMDeviceResourceIDByMACAddress(string secret, string macAddress)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for device resource
                string query = String.Format("SELECT * FROM SMS_R_System WHERE MacAddresses like '{0}'", macAddress);
                IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                string resourceId = string.Empty;

                if (result != null)
                {
                    foreach (IResultObject device in result)
                    {
                        int id = device["ResourceId"].IntegerValue;
                        resourceId = id.ToString();
                    }
                }

                return resourceId;
            }
            else
            {
                return string.Empty;
            }
        }

        [WebMethod(Description = "Get the name of a specific device by UUID (SMBIOS GUID)")]
        public string GetCMDeviceNameByUUID(string secret, string uuid)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for device name
                string query = String.Format("SELECT * FROM SMS_R_System WHERE SMBIOSGUID like '{0}'", uuid);
                IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                string deviceName = string.Empty;

                if (result != null)
                {
                    foreach (IResultObject device in result)
                    {
                        string name = device["Name"].StringValue;
                        deviceName = name;
                    }
                }

                return deviceName;
            }
            else
            {
                return string.Empty;
            }
        }

        [WebMethod(Description = "Get hidden task sequence deployments for a specific resource id")]
        public List<taskSequence> GetCMHiddenTaskSequenceDeploymentsByResourceId(string secret, string resourceId)
        {
            //' Construct hidden task sequences list
            List<taskSequence> hiddenTaskSequences = new List<taskSequence>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for task sequence deployments
                string deploymentQuery = "SELECT * FROM SMS_AdvertisementInfo WHERE PackageType = 4";
                IResultObject tsDeployments = connection.QueryProcessor.ExecuteQuery(deploymentQuery);

                if (tsDeployments != null)
                {
                    //' Get device collection ids for resource id
                    string collectionQuery = String.Format("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceId like '{0}'", resourceId);
                    IResultObject collections = connection.QueryProcessor.ExecuteQuery(collectionQuery);

                    //' Construct string array for collection ids
                    ArrayList collIdList = new ArrayList();

                    if (collections != null)
                    {
                        //' Process collection memberships for device
                        foreach (IResultObject collection in collections)
                        {
                            string collectionId = collection["CollectionID"].StringValue;
                            collIdList.Add(collectionId);
                        }

                        //' Process task sequence deployments to see if any is deployed to a collection that the device is a member of
                        if (collIdList.Count >= 1)
                        {
                            foreach (IResultObject tsDeployment in tsDeployments)
                            {
                                string deployCollId = tsDeployment["CollectionID"].StringValue;

                                if (collIdList.Contains(deployCollId))
                                {
                                    //' Collect property values from instance
                                    string packageName = tsDeployment["PackageName"].StringValue;
                                    string advertId = tsDeployment["AdvertisementId"].StringValue;
                                    int advertFlags = tsDeployment["AdvertFlags"].IntegerValue;

                                    //' Construct taskSequence object
                                    taskSequence ts = new taskSequence { AdvertFlags = advertFlags, AdvertisementId = advertId, PackageName = packageName };

                                    //' Add object to list if hidden deployment bit exists
                                    if ((advertFlags & 0x20000000) != 0)
                                    {
                                        hiddenTaskSequences.Add(ts);
                                    }
                                }
                            }
                        }
                    }
                }

                return hiddenTaskSequences;
            }
            else
            {
                return hiddenTaskSequences;
            }
        }

        [WebMethod(Description = "Get Boot Image source version")]
        public string GetCMBootImageSourceVersion(string packageId, string secret)
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
        public List<User> GetCMDiscoveredUsers(string secret)
        {
            //' Construct users list
            var userList = new List<User>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for all discovered users
                IResultObject queryResults = null;
                string query = "SELECT UniqueUserName,ResourceId,WindowsNTDomain,FullDomainName FROM SMS_R_User";

                try
                {
                    queryResults = connection.QueryProcessor.ExecuteQuery(query);
                }
                catch (Exception ex)
                {
                    WriteEventLog("An error occured when an attempt to query for user data from SMS Provider was made", EventLogEntryType.Error);
                    WriteEventLog(ex.Message, EventLogEntryType.Error);
                    return userList;
                }
                
                try
                {
                    if (queryResults != null)
                        foreach (IResultObject queryResult in queryResults)
                        {
                            //' Collect property values from instance
                            string uniqueUserName = queryResult["UniqueUserName"].StringValue;
                            string resourceId = queryResult["ResourceId"].StringValue;
                            string windowsNTDomain = queryResult["WindowsNTDomain"].StringValue;
                            string fullDomainName = queryResult["FullDomainName"].StringValue;

                            //' Construct new user object
                            User user = new User();
                            user.uniqueUserName = uniqueUserName;
                            user.resourceId = resourceId;
                            user.windowsNTDomain = windowsNTDomain;
                            user.fullDomainName = fullDomainName;

                            //' Add user object to user list
                            userList.Add(user);
                        }
                }
                catch (Exception ex)
                {
                    WriteEventLog("An error occured while constructing list of user instances", EventLogEntryType.Error);
                    WriteEventLog(ex.Message, EventLogEntryType.Error);
                    return userList;
                }
            }

            //' Return list of users
            return userList;
        }

        [WebMethod(Description = "Get the unique username for a specific user (useful for setting a value for SMSTSUdaUsers)")]
        public string GetCMUniqueUserName(string userName, string secret)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Query for unique username
                IResultObject queryResults = null;
                string query = "SELECT * FROM SMS_R_User";

                queryResults = connection.QueryProcessor.ExecuteQuery(query);
                if (queryResults != null)
                    foreach (IResultObject queryResult in queryResults)
                    {
                        //' Collect property values from instance
                        string uName = queryResult["UserName"].StringValue;
                        string uniqueUserName = queryResult["UniqueUserName"].StringValue;
                        if (uName.ToLower() == userName.ToLower())
                            return uniqueUserName;
                    }
            }

            return string.Empty;
        }

        [WebMethod(Description = "Import a computer by MAC Address")]
        public string ImportCMComputerByMacAddress(string computerName, string macAddress, string secret)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Construct method parameters
                Dictionary<string, object> methodParameters = new Dictionary<string, object>();
                methodParameters.Add("NetBIOSName", computerName);
                methodParameters.Add("MacAddress", macAddress);
                methodParameters.Add("OverWriteExistingRecord", true);

                //' Import computer
                string resourceId = ImportCMComputer(methodParameters);

                return resourceId;
            }
            else
            {
                return string.Empty;
            }
        }

        [WebMethod(Description = "Import a computer by UUID (SMBIOS GUID)")]
        public string ImportCMComputerByUUID(string computerName, string uuid, string secret)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Construct method parameters
                Dictionary<string, object> methodParameters = new Dictionary<string, object>();
                methodParameters.Add("NetBIOSName", computerName);
                methodParameters.Add("SMBIOSGuid", uuid);
                methodParameters.Add("OverWriteExistingRecord", true);

                //' Import computer
                string resourceId = ImportCMComputer(methodParameters);

                return resourceId;
            }
            else
            {
                return string.Empty;
            }
        }

        [WebMethod(Description = "Add a computer to a specific device collection (creates a direct membership rule)")]
        public bool AddCMComputerToCollection(string resourceName, string collectionName, string secret)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Get resource id for given computer name
                string resourceId = GetCMCompterResourceId(resourceName);
                if (!String.IsNullOrEmpty(resourceId))
                {
                    //' Initiate collection object
                    WqlResultObject collection = null;

                    //' Attempt to get collection
                    string query = String.Format("SELECT * FROM SMS_Collection WHERE Name LIKE '{0}' AND CollectionType = 2", collectionName);
                    WqlQueryResultsObject collResult = (WqlQueryResultsObject)connection.QueryProcessor.ExecuteQuery(query);

                    if (collResult != null)
                    {
                        foreach (WqlResultObject coll in collResult)
                        {
                            collection = coll;
                        }

                        //' Construct new direct membership rule
                        IResultObject newRule = connection.CreateInstance("SMS_CollectionRuleDirect");
                        newRule["ResourceClassName"].StringValue = "SMS_R_System";
                        newRule["ResourceID"].StringValue = resourceId;
                        newRule["RuleName"].StringValue = resourceName;

                        //' Construct params dictionary for method execution
                        Dictionary<string, object> methodParams = new Dictionary<string, object>();
                        methodParams.Add("CollectionRule", newRule);

                        //' Execute method to add new direct membership rule
                        IResultObject result = collection.ExecuteMethod("AddMembershipRule", methodParams);

                        //' Refresh collection
                        if (result["ReturnValue"].IntegerValue == 0)
                        {
                            Dictionary<string, object> refreshParams = new Dictionary<string, object>();
                            collection.ExecuteMethod("RequestRefresh", refreshParams);

                            return true;
                        }
                    }
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        [WebMethod(Description = "Get all or a filtered list of device collections")]
        public List<string> GetCMDeviceCollections(string secret, string filter = null)
        {
            //' Construct list object
            List<string> collectionList = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Device collection query
                string deviceQuery = string.Empty;
                if (String.IsNullOrEmpty(filter))
                {
                    deviceQuery = "SELECT * FROM SMS_Collection WHERE CollectionType LIKE '2'";
                }
                else
                {
                    deviceQuery = String.Format("SELECT * FROM SMS_Collection WHERE CollectionType LIKE '2' AND Name like '%{0}%'", filter);
                }

                //' Get all device collections
                IResultObject collections = connection.QueryProcessor.ExecuteQuery(deviceQuery);
                if (collectionList != null)
                {
                    foreach (IResultObject collection in collections)
                    {
                        collectionList.Add(collection["Name"].StringValue);
                    }
                }

                return collectionList;
            }
            else
            {
                return null;
            }
        }

        [WebMethod(Description = "Update membership of a specific collection")]
        public bool UpdateCMCollectionMembership(string secret, string collectionId)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Get collection
                string query = String.Format("SELECT * FROM SMS_Collection WHERE CollectionID LIKE '{0}'", collectionId);
                WqlQueryResultsObject collResult = (WqlQueryResultsObject)connection.QueryProcessor.ExecuteQuery(query);

                if (collResult != null)
                {
                    //' Refresh memberships
                    foreach (WqlResultObject collection in collResult)
                    {
                        Dictionary<string, object> refreshParams = new Dictionary<string, object>();
                        IResultObject exec = collection.ExecuteMethod("RequestRefresh", refreshParams);

                        if (exec["ReturnValue"].IntegerValue == 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        [WebMethod(Description = "Get Driver Package information by computer model")]
        public List<driverPackage> GetCMDriverPackageByModel(string secret, string model)
        {
            //' Construct list for driver package ids
            List<driverPackage> pkgList = new List<driverPackage>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Get driver packages
                string query = String.Format("SELECT * FROM SMS_DriverPackage WHERE Name like '%{0}%' AND PackageType = 3", model);
                IResultObject driverPackages = connection.QueryProcessor.ExecuteQuery(query);

                if (driverPackages != null)
                {
                    foreach (IResultObject driverPackage in driverPackages)
                    {
                        //' Define objects for properties
                        string packageName = driverPackage["Name"].StringValue;
                        string packageId = driverPackage["PackageID"].StringValue;

                        //' Add new driver package object to list
                        driverPackage drvPkg = new driverPackage { PackageName = packageName, PackageID = packageId };
                        pkgList.Add(drvPkg);
                    }
                }

                return pkgList;
            }
            else
            {
                return pkgList;
            }
        }

        [WebMethod(Description = "Get a filtered list of packages")]
        public List<package> GetCMPackage(string secret, string filter)
        {
            //' Construct list for package ids
            List<package> pkgList = new List<package>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Connect to SMS Provider
                smsProvider smsProvider = new smsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Get packages
                string query = String.Format("SELECT * FROM SMS_Package WHERE Name like '%{0}%'", filter);
                IResultObject packages = connection.QueryProcessor.ExecuteQuery(query);

                if (packages != null)
                {
                    foreach (IResultObject package in packages)
                    {
                        //' Define objects for properties
                        string packageName = package["Name"].StringValue;
                        string packageId = package["PackageID"].StringValue;

                        //' Add new package object to list
                        package pkg = new package { PackageName = packageName, PackageID = packageId };
                        pkgList.Add(pkg);
                    }
                }

                return pkgList;
            }
            else
            {
                return pkgList;
            }
        }

        [WebMethod(Description = "Get MDT roles from database (Application Pool identity needs access permissions to the specified MDT database)")]
        public List<string> GetMDTRoles(string server, string database, string instance, string secret)
        {
            //' Construct list object
            List<string> roleList = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                try
                {
                    //' Connect to SQL server instance
                    SqlConnection connection = new SqlConnection();
                    connection.ConnectionString = connectionString.ConnectionString;
                    connection.Open();

                    //' Invoke SQL command
                    SqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT Role FROM RoleIdentity";
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows == true)
                    {
                        while (reader.Read())
                        {
                            roleList.Add(reader["Role"].ToString());
                        }
                        reader.Close();
                        connection.Close();
                        roleList.Sort();
                    }
                }
                catch (SqlException ex)
                {
                    WriteEventLog(String.Format("An error occured while attempting to retrieve MDT Roles. Error message {0}", ex.Message), EventLogEntryType.Error);
                }
            }

            return roleList;
        }

        [WebMethod(Description = "Get computer by asset tag from MDT database")]
        public string GetMDTComputerByAssetTag(string secret, string assetTag)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "AssetTag", assetTag);
                if (!String.IsNullOrEmpty(identity))
                {
                    return identity;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                //' Return null when secret key is not passed correctly
                return null;
            }
        }

        [WebMethod(Description = "Check if a computer with a specific MAC address exists in MDT database")]
        public string GetMDTComputerByMacAddress(string secret, string macAddress)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "MacAddress", macAddress);
                if (!String.IsNullOrEmpty(identity))
                {
                    return identity;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                //' Return null when secret key is not passed correctly
                return null;
            }
        }

        [WebMethod(Description = "Get computer by serial number from MDT database")]
        public string GetMDTComputerBySerialNumber(string secret, string serialNumber)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "SerialNumber", serialNumber);
                if (!String.IsNullOrEmpty(identity))
                {
                    return identity;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                //' Return null when secret key is not passed correctly
                return null;
            }
        }

        [WebMethod(Description = "Check if a computer with a specific UUID exists in MDT database")]
        public string GetMDTComputerByUUID(string secret, string uuid)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "UUID", uuid);
                if (!String.IsNullOrEmpty(identity))
                {
                    return identity;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                //' Return null when secret key is not passed correctly
                return null;
            }
        }

        [WebMethod(Description = "Get MDT roles with detailed information for a specific computer")]
        public List<MDTRole> GetMDTDetailedComputerRoleMembership(string secret, string identity)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                try
                {
                    //' Get connection string
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                    //' Connect to SQL server instance
                    SqlConnection connection = new SqlConnection();
                    connection.ConnectionString = connectionString.ConnectionString;
                    connection.Open();

                    //' Construct SQL statement
                    SqlCommand command = connection.CreateCommand();
                    StringBuilder sqlString = new StringBuilder();
                    sqlString.Append(String.Format("SELECT Roles.Role, RoleIdentity.ID FROM Settings_Roles AS Roles INNER JOIN RoleIdentity ON Roles.Role = RoleIdentity.Role WHERE Roles.ID = @ID AND Roles.Type = 'C'"));

                    command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = identity;
                    command.CommandText = sqlString.ToString();

                    //' Construct List to hold all roles
                    List<MDTRole> roleList = new List<MDTRole>();

                    //' Invoke SQL command to retrieve roles
                    try
                    {
                        SqlDataReader reader = command.ExecuteReader();
                        if (reader.HasRows == true)
                        {
                            while (reader.Read())
                            {
                                MDTRole mdtRole = new MDTRole();
                                mdtRole.RoleName = reader["Role"].ToString();
                                mdtRole.RoleId = reader["ID"].ToString();
                                roleList.Add(mdtRole);
                            }
                        }

                        //' Cleanup and disconnect SQL connection
                        command.Dispose();
                        connection.Close();

                        return roleList;
                    }
                    catch (System.Web.Services.Protocols.SoapException ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to get role memberships. Error message: {0}", ex.Message), EventLogEntryType.Error);
                        return null;
                    }
                }
                catch (SqlException ex)
                {
                    WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return null;
                }
            }
            else
            {
                //' Return null when secret key is not passed correctly
                return null;
            }
        }

        [WebMethod(Description = "Get a list of MDT roles for a specific computer")]
        public List<string> GetMDTComputerRoleMembership(string id, string secret)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                try
                {
                    //' Get connection string
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                    //' Connect to SQL server instance
                    SqlConnection connection = new SqlConnection();
                    connection.ConnectionString = connectionString.ConnectionString;
                    connection.Open();

                    //' Construct SQL statement
                    SqlCommand command = connection.CreateCommand();
                    StringBuilder sqlString = new StringBuilder();
                    sqlString.Append(String.Format("SELECT Role FROM Settings_Roles WHERE ID LIKE @ID"));

                    command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = id;
                    command.CommandText = sqlString.ToString();

                    //' Construct List to hold all roles
                    List<string> roleList = new List<string>();

                    //' Invoke SQL command to retrieve roles
                    try
                    {
                        SqlDataReader reader = command.ExecuteReader();
                        if (reader.HasRows == true)
                        {
                            while (reader.Read())
                            {
                                roleList.Add(reader["Role"].ToString());
                            }
                        }

                        //' Cleanup and disconnect SQL connection
                        command.Dispose();
                        connection.Close();

                        return roleList;
                    }
                    catch (System.Web.Services.Protocols.SoapException ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to get role memberships. Error message: {0}", ex.Message), EventLogEntryType.Error);
                        return null;
                    }
                }
                catch (SqlException ex)
                {
                    WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return null;
                }
            }
            else
            {
                //' Return null when secret key is not passed correctly
                return null;
            }
        }

        [WebMethod(Description = "Get MDT computer name by computer identity")]
        public string GetMDTComputerNameByIdentity(string secret, string identity)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                string computerIdentity = GetMDTComputerName(identity);
                if (!String.IsNullOrEmpty(computerIdentity))
                {
                    return computerIdentity;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                //' Return empty string when secret key is not passed correctly
                return string.Empty;
            }
        }

        [WebMethod(Description = "Add computer identified by an asset tag to a specific MDT role")]
        public bool AddMDTRoleMemberByAssetTag(string roleName, string computerName, string assetTag, string secret, bool createComputer, string identity = null)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary.Add("AssetTag", assetTag);

                if (createComputer == true)
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName);
                    return result;
                }
                else
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    return result;
                }
            }
            else
            {
                //' Return false when secret key is not passed correctly
                return false;
            }
        }

        [WebMethod(Description = "Add computer identified by a serial number to a specific MDT role")]
        public bool AddMDTRoleMemberBySerialNumber(string roleName, string computerName, string serialNumber, string secret, bool createComputer, string identity = null)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary.Add("SerialNumber", serialNumber);

                if (createComputer == true)
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName);
                    return result;
                }
                else
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    return result;
                }

            }
            else
            {
                //' Return false when secret key is not passed correctly
                return false;
            }
        }

        [WebMethod(Description = "Add computer identified by a MAC address to a specific MDT role")]
        public bool AddMDTRoleMemberByMacAddress(string roleName, string computerName, string macAddress, string secret, bool createComputer, string identity = null)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary.Add("MacAddress", macAddress);

                if (createComputer == true)
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName);
                    return result;
                }
                else
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    return result;
                }
            }
            else
            {
                //' Return false when secret key is not passed correctly
                return false;
            }
        }

        [WebMethod(Description = "Add computer identified by an UUID to a specific MDT role")]
        public bool AddMDTRoleMemberByUUID(string roleName, string computerName, string uuid, string secret, bool createComputer, string identity = null)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary.Add("UUID", uuid);

                if (createComputer == true)
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName);
                    return result;
                }
                else
                {
                    bool result = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    return result;
                }
            }
            else
            {
                //' Return false when secret key is not passed correctly
                return false;
            }
        }

        [WebMethod(Description = "Add computer to a given MDT role (supports multiple indentification types)")]
        public bool AddMDTRoleMember(string computerName, string role, string secret, string assetTag = null, string serialNumber = null, string macAddress = null, string uuid = null, string description = null)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary.Add("AssetTag", assetTag);
                dictionary.Add("SerialNumber", serialNumber);
                dictionary.Add("MacAddress", macAddress);
                dictionary.Add("UUID", uuid);
                dictionary.Add("Description", description);

                bool result = BeginMDTRoleMember(dictionary, computerName, role);
                return result;
            }
            else
            {
                //' Return false when secret key is not passed correctly
                return false;
            }
        }

        [WebMethod(Description = "Remove MDT computer from all associated roles")]
        public bool RemoveMDTComputerFromRoles(string secret, string identity)
        {
            //' Validate secret key
            if (secret == secretKey)
            {
                bool removeResult = RemoveMDTComputerRoles(identity);
                return removeResult;
            }
            else
            {
                //' Return false when secret key is not passed correctly
                return false;
            }
        }

        private bool BeginMDTRoleMember(Dictionary<string, string> dictionary, string computerName, string roleName, bool createComputer = true, string identity = null)
        {
            if (createComputer == true)
            {
                //' Create computer identity in MDT database
                string computerIdentity = AddMDTComputerIdentity(dictionary);
                if (!String.IsNullOrEmpty(computerIdentity))
                {
                    //' Create association between computer and identity
                    bool computerSetting = AddMDTComputerSetting(computerIdentity, computerName);
                    if (computerSetting == true)
                    {
                        //' Associate computer with role
                        bool roleAssociation = AddMDTRoleAssociationWithMember(computerIdentity, roleName);
                        if (roleAssociation == true)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //' Associate computer with role
                bool roleAssociation = AddMDTRoleAssociationWithMember(identity, roleName);
                if (roleAssociation == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

        }

        private string AddMDTComputerIdentity(Dictionary<string, string> dictionary)
        {
            //' Get connection string
            SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = connectionString.ConnectionString;
            connection.Open();

            //' Build start of SQL command for identity creation
            SqlCommand cmdIdentity = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append("INSERT INTO ComputerIdentity (");


            //' Determine count for non-null values
            int valueCount = 0;
            foreach (KeyValuePair<string, string> prop in dictionary)
            {
                if (!String.IsNullOrEmpty(prop.Value))
                {
                    valueCount++;
                }
            }

            //' Append command with columns
            int currCount = 1;
            foreach (KeyValuePair<string, string> prop in dictionary)
            {
                if (!String.IsNullOrEmpty(prop.Value))
                {
                    if (currCount < valueCount)
                    {
                        sqlString.Append(String.Format("{0}, ", prop.Key));
                    }
                    else
                    {
                        sqlString.Append(String.Format("{0}", prop.Key));
                    }
                    currCount++;
                }
            }

            //' Append command
            sqlString.Append(") VALUES (");

            //' Append command with parameters for columns
            currCount = 1;
            foreach (KeyValuePair<string, string> prop in dictionary)
            {
                if (!String.IsNullOrEmpty(prop.Value))
                {
                    if (currCount < valueCount)
                    {
                        sqlString.Append(String.Format("@{0}, ", prop.Key));
                    }
                    else
                    {
                        sqlString.Append(String.Format("@{0}", prop.Key));
                    }
                    currCount++;
                }
            }

            //' Append end for command
            sqlString.Append(") SELECT @@IDENTITY");

            //' Add SQL command parameters
            foreach (KeyValuePair<string, string> prop in dictionary)
            {
                if (!String.IsNullOrEmpty(prop.Value))
                {
                    cmdIdentity.Parameters.Add(String.Format("@{0}", prop.Key), SqlDbType.NVarChar).Value = prop.Value;
                }
                else
                {
                    cmdIdentity.Parameters.Add(String.Format("@{0}", prop.Key), SqlDbType.NVarChar).Value = string.Empty;
                }
            }

            //' Add SQL command text string
            cmdIdentity.CommandText = sqlString.ToString();

            //' Invoke SQL command for identity creation
            try
            {
                string identity = string.Empty;
                object resultIdentity = cmdIdentity.ExecuteScalar();
                if (resultIdentity != null)
                {
                    identity = (string)resultIdentity.ToString();
                }

                //' Cleanup and disconnect SQL connection
                cmdIdentity.Dispose();
                connection.Close();

                return identity;
            }
            catch (System.Web.Services.Protocols.SoapException ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to create computer identity. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return string.Empty;
            }
        }

        private bool AddMDTComputerSetting(string identity, string computerName)
        {
            //' Get connection string
            SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = connectionString.ConnectionString;
            connection.Open();

            //' Build SQL command for computer setting
            SqlCommand cmdSetting = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append("INSERT INTO Settings (Type, ID, OSDComputerName) VALUES ('C', @Identity, @OSDComputerName) SELECT @@IDENTITY");

            //' Add parameters for SQL command
            cmdSetting.Parameters.Add("@Identity", SqlDbType.Int).Value = identity;
            cmdSetting.Parameters.Add("@OSDComputerName", SqlDbType.NVarChar).Value = computerName;

            //' Add SQL command text string
            cmdSetting.CommandText = sqlString.ToString();

            //' Invoke SQL command for computer setting
            try
            {
                object resultAssociation = cmdSetting.ExecuteScalar();
                if (resultAssociation != null)
                {
                    //' Cleanup and disconnect SQL connection
                    cmdSetting.Dispose();
                    connection.Close();

                    return true;
                }
            }
            catch (System.Web.Services.Protocols.SoapException ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to create computer setting. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return false;
            }

            //' Cleanup and disconnect SQL connection
            cmdSetting.Dispose();
            connection.Close();

            return false;
        }

        private bool AddMDTRoleAssociationWithMember(string identity, string role)
        {
            //' Get connection string
            SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = connectionString.ConnectionString;
            connection.Open();

            //' Build SQL command for role association
            SqlCommand cmdRole = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append("INSERT INTO Settings_Roles (Type, ID, Sequence, Role) VALUES ('C', @Identity, @Sequence, @Role) SELECT @@IDENTITY");

            //' Add parameters for SQL command
            cmdRole.Parameters.Add("@Identity", SqlDbType.Int).Value = identity;
            cmdRole.Parameters.Add("@Role", SqlDbType.NVarChar).Value = role;

            //' determine if Sequence should be incremented
            int sequenceNumber = GetMDTSequenceNumber(identity);
            if (sequenceNumber >= 1)
            {
                cmdRole.Parameters.Add("@Sequence", SqlDbType.Int).Value = sequenceNumber + 1; ;
            }
            else
            {
                cmdRole.Parameters.Add("@Sequence", SqlDbType.Int).Value = 1;
            }

            //' Add SQL command text string
            cmdRole.CommandText = sqlString.ToString();

            //' Invoke SQL command for role and computer association
            try
            {
                object resultRole = cmdRole.ExecuteScalar();
                if (resultRole != null)
                {
                    //' Cleanup and disconnect SQL connection
                    cmdRole.Dispose();
                    connection.Close();

                    return true;
                }
            }
            catch (System.Web.Services.Protocols.SoapException ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to association computer with role. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return false;
            }

            //' Cleanup and disconnect SQL connection
            cmdRole.Dispose();
            connection.Close();

            return false;
        }

        private int GetMDTSequenceNumber(string identity)
        {
            try
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append(String.Format("SELECT Sequence FROM Settings_Roles WHERE ID LIKE @ID"));

                command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = identity;
                command.CommandText = sqlString.ToString();

                //' Construct List to hold all roles
                List<object> sequenceList = new List<object>();

                //' Invoke SQL command to retrieve roles
                try
                {
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.HasRows == true)
                    {
                        while (reader.Read())
                        {
                            sequenceList.Add(reader["Sequence"]);
                        }
                    }

                    //' Cleanup and disconnect SQL connection
                    command.Dispose();
                    connection.Close();

                    //' Calculate the highest sequence number
                    int maxSequenceNumber = 0;
                    if (sequenceList.Count >= 1)
                    {
                        maxSequenceNumber = (int)sequenceList.Max();
                        return maxSequenceNumber;
                    }
                    else {
                        return 0;
                    }
                }
                catch (System.Web.Services.Protocols.SoapException ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to get sequence numbers from role settings. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return 0;
                }
            }
            catch (SqlException ex)
            {
                WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return 0;
            }
        }

        private string GetMDTComputerName(string identity)
        {
            string computerIdentity = string.Empty;

            try
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append(String.Format("SELECT ID, OSDComputerName FROM Settings WHERE ID like @ID"));

                command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = identity;
                command.CommandText = sqlString.ToString();

                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows == true)
                {
                    while (reader.Read())
                    {
                        computerIdentity = reader["OSDComputerName"].ToString();
                    }
                }

                //' Cleanup and disconnect SQL connection
                command.Dispose();
                connection.Close();

                return computerIdentity;
            }
            catch (SqlException ex)
            {
                WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return computerIdentity;
            }
        }

        private string GetMDTComputerIdentity(SqlConnectionStringBuilder connectionString, string identityType, string identityValue)
        {
            try
            {
                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append(String.Format("SELECT * FROM ComputerIdentity WHERE {0} LIKE @{1}", identityType, identityType));

                command.Parameters.Add(String.Format("@{0}", identityType), SqlDbType.NVarChar).Value = identityValue;
                command.CommandText = sqlString.ToString();

                //' Invoke SQL command to retrieve computer identity
                try
                {
                    string identity = string.Empty;
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.HasRows == true)
                    {
                        while (reader.Read())
                        {
                            identity = reader["Id"].ToString();
                        }
                    }

                    //' Cleanup and disconnect SQL connection
                    command.Dispose();
                    connection.Close();

                    return identity;
                }
                catch (System.Web.Services.Protocols.SoapException ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to get computer identity. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return null;
                }
            }
            catch (SqlException ex)
            {
                WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return null;
            }
        }

        private bool RemoveMDTComputerRoles(string identity)
        {
            try
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString();

                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append(String.Format("DELETE FROM Settings_Roles WHERE ID = @ID AND Type like 'C'"));

                command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = identity;
                command.CommandText = sqlString.ToString();

                //' Invoke SQL command for clearing all associated roles for computer identity
                try
                {
                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected >= 1)
                    {
                        //' Cleanup and disconnect SQL connection
                        command.Dispose();
                        connection.Close();

                        return true;
                    }
                }
                catch (System.Web.Services.Protocols.SoapException ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to clear associated roles for computer. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return false;
                }

                //' Cleanup and disconnect SQL connection
                command.Dispose();
                connection.Close();

                return false;
            }
            catch (SqlException ex)
            {
                WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return false;
            }
        }

        private string ImportCMComputer(Dictionary<string, object> methodParameters)
        {
            //' Connect to SMS Provider
            smsProvider smsProvider = new smsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            //' Initiate string for resourceId of imported computer
            string resourceId = string.Empty;

            // Execute ImportMachineEntry method with in params
            try
            {
                IResultObject importEntry = connection.ExecuteMethod("SMS_Site", "ImportMachineEntry", methodParameters);
                resourceId = importEntry["ResourceID"].StringValue;

                return resourceId;
            }
            catch (SmsException ex)
            {
                WriteEventLog("An error occured while attempting to import computer information. Error message: " + ex.Message, EventLogEntryType.Error);
                return null;
            }
        }

        private string GetCMCompterResourceId(string computerName)
        {
            //' Connect to SMS Provider
            smsProvider smsProvider = new smsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            //' Construct query for resource id
            string query = String.Format("SELECT * FROM SMS_R_System WHERE Name LIKE '{0}'", computerName);

            //' Query for instance
            string resourceId = string.Empty;
            IResultObject instance = connection.QueryProcessor.ExecuteQuery(query);
            if (instance != null)
            {
                foreach (IResultObject item in instance)
                {
                    resourceId = item["ResourceId"].StringValue;
                }
            }

            return resourceId;
        }

        private SqlConnectionStringBuilder GetSqlConnectionString()
        {
            //' Set database connection string
            SqlConnectionStringBuilder connectionString = new SqlConnectionStringBuilder();
            if (sqlInstance == null || sqlInstance == string.Empty)
            {
                connectionString.DataSource = sqlServer;
                connectionString.InitialCatalog = mdtDatabase;
                connectionString.IntegratedSecurity = true;
            }
            else
            {
                connectionString.DataSource = String.Format("{0}\\{1}", sqlServer, sqlInstance);
                connectionString.InitialCatalog = mdtDatabase;
                connectionString.IntegratedSecurity = true;
            }

            //' Set general properties for connection string
            connectionString.ConnectTimeout = 15;

            return connectionString;
        }

        private void WriteEventLog(string logEntry, EventLogEntryType entryType)
        {
            using (eventLog = new EventLog())
            {
                if (!EventLog.SourceExists("ConfigMgr Web Service"))
                {
                    EventLog.CreateEventSource("ConfigMgr Web Service", "ConfigMgr Web Service Activity");
                }
                eventLog.Source = "ConfigMgr Web Service";
                eventLog.Log = "ConfigMgr Web Service Activity";
                eventLog.WriteEntry(logEntry, entryType, 1000);
            }
        }
    }
}
