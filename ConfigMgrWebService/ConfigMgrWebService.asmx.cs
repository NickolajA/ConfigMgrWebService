﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.DirectoryServices;
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
using System.Reflection;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Security;
using System.Runtime.InteropServices;
using System.Globalization;
using SqlExtensions;

namespace SqlExtensions
{
    public static class SqlExtension
    {
        /// <summary>
        ///  Extension method for reading int's that potentially may be NULL. Source from: https://stackoverflow.com/questions/18530627/nullable-integer-values-from-reader
        /// </summary>
        public static int GetDBInt(this SqlDataReader reader, string colName)
        {
            var colIndex = reader.GetOrdinal(colName);
            return !reader.IsDBNull(colIndex) ? reader.GetInt32(colIndex) : default(int);
        }
    }
}

namespace ConfigMgrWebService
{
    [WebService(Name = "ConfigMgr WebService", Description = "Web service for ConfigMgr Current Branch developed by Nickolaj Andersen (1.8.0)", Namespace = "http://www.scconfigmgr.com")]
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
        private string monitorDatabase = WebConfigurationManager.AppSettings["OSDMonitorDatabase"];

        //' Enums
        public enum ADObjectClass
        {
            Group,
            Computer,
            DomainController,
            User
        }

        public enum ADObjectType
        {
            distinguishedName,
            objectGuid
        }

        public enum CMObjectType
        {
            System = 5,
            User = 4
        }

        public enum CMCollectionType
        {
            UserCollection = 1,
            DeviceCollection = 2
        }

        public enum CMResourceProperty
        {
            Name,
            ResourceID,
            SMBIOSGUID,
            CollectionID
        }

        //' Initialize event logging
        public static EventLog eventLog;
        public static Stopwatch timer = new Stopwatch();

        public void InitializeComponent()
        {
            eventLog = new EventLog();
            ((System.ComponentModel.ISupportInitialize)(eventLog)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(eventLog)).EndInit();
        }

        [WebMethod(Description = "Get web service version")]
        public string GetCWVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            return version.ToString(3);
        }
        
        [WebMethod(Description = "Get primary user(s) for a specific device by name")]
        public List<string> GetCMPrimaryUserByDeviceName(string secret, string deviceName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct relation list
            var relations = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Query for user relationship instances
                SelectQuery relationQuery = new SelectQuery("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceName like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, relationQuery);

                if (managementObjectSearcher != null)
                {
                    foreach (var userRelation in managementObjectSearcher.Get())
                    {
                        //' Get user name
                        string userName = (string)userRelation.GetPropertyValue("UniqueUserName");
                        relations.Add(userName);
                    }
                }
            }

            MethodEnd(method);
            return relations;
        }

        [WebMethod(Description = "Get primary user(s) for a specific device by resourceId")]
        public List<CMUserDeviceRelation> GetCMPrimaryUserByDeviceResourceId(string secret, string resourceId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct relation list
            List<CMUserDeviceRelation> relations = new List<CMUserDeviceRelation>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get all user device relationsship for specified resource id
                    string relQuery = String.Format("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceID = '{0}'", resourceId);
                    IResultObject relResults = connection.QueryProcessor.ExecuteQuery(relQuery);

                    if (relResults != null)
                    {
                        foreach (IResultObject result in relResults)
                        {
                            if (result["IsActive"].BooleanValue == true)
                            {
                                try
                                {
                                    //' Get display name for user
                                    string fullUserName = string.Empty;
                                    string userQuery = String.Format("SELECT * FROM SMS_R_User WHERE UniqueUserName = '{0}'", result["UniqueUserName"].StringValue.Replace("\\", "\\\\"));
                                    IResultObject userResults = connection.QueryProcessor.ExecuteQuery(userQuery);

                                    if (userResults != null)
                                    {
                                        foreach (IResultObject user in userResults)
                                        {
                                            fullUserName = user["FullUserName"].StringValue;
                                        }
                                    }

                                    //' Construct new relation object for return value
                                    CMUserDeviceRelation relation = new CMUserDeviceRelation()
                                    {
                                        CreationTime = result["CreationTime"].DateTimeValue,
                                        ResourceId = result["ResourceID"].StringValue,
                                        ResourceName = result["ResourceName"].StringValue,
                                        UniqueUserName = result["UniqueUserName"].StringValue,
                                        FullUserName = fullUserName
                                    };

                                    relations.Add(relation);
                                }
                                catch (Exception ex)
                                {
                                    WriteEventLog($"An error occurred while querying unique user name. Error message: { ex.Message } ", EventLogEntryType.Error);
                                }
                            }
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for user machine relations returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for user machine relations. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return relations;
        }

        [WebMethod(Description = "Get primary device(s) for a specific user")]
        public List<string> GetCMPrimaryDeviceByUser(string secret, string userName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct relation list
            var relations = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Query for device relationship instances
                SelectQuery relationQuery = new SelectQuery("SELECT * FROM SMS_UserMachineRelationship WHERE UniqueUserName like '" + userName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, relationQuery);

                if (managementObjectSearcher != null)
                {
                    foreach (var deviceRelation in managementObjectSearcher.Get())
                    {
                        //' Get device name
                        string deviceName = (string)deviceRelation.GetPropertyValue("ResourceName");
                        relations.Add(deviceName);
                    }
                }
            }

            MethodEnd(method);
            return relations;
        }

        [WebMethod(Description = "Get deployed applications for a specific user")]
        public List<CMApplication> GetCMDeployedApplicationsByUser(string secret, string userName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct applications list
            var applicationNames = new List<CMApplication>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Query for specified user
                SelectQuery userQuery = new SelectQuery("SELECT * FROM SMS_R_User WHERE UserName like '" + userName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, userQuery);

                if (managementObjectSearcher.Get() != null)
                {
                    if (managementObjectSearcher.Get().Count == 1)
                    {
                        foreach (ManagementObject user in managementObjectSearcher.Get())
                        {
                            //' Define properties from user
                            string userNameProperty = (string)user.GetPropertyValue("UserName");
                            var resourceIDProperty = user.GetPropertyValue("ResourceId");

                            //' Query for collection memberships relations for user
                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                            {
                                foreach (ManagementObject collUser in collMembershipSearcher.Get())
                                {
                                    //' Define properties for collection
                                    string collectionId = (string)collUser.GetPropertyValue("CollectionID");

                                    //' Query for collection
                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                    {
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            //' Define properties for collection
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            //' Query for deployment info for collection
                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                            {
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    //' Return application object
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    string collectionName = (string)deployment.GetPropertyValue("CollectionName");
                                                    CMApplication targetApplication = new CMApplication();
                                                    targetApplication.ApplicationName = targetName;
                                                    targetApplication.CollectionName = collectionName;
                                                    applicationNames.Add(targetApplication);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MethodEnd(method);
            return applicationNames;
        }

        [WebMethod(Description = "Get deployed applications for a specific device")]
        public List<string> GetCMDeployedApplicationsByDevice(string secret, string deviceName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct applications list
            List<string> applicationNames = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Query for specified device name
                SelectQuery deviceQuery = new SelectQuery("SELECT * FROM SMS_R_System WHERE Name like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, deviceQuery);

                if (managementObjectSearcher.Get() != null)
                {
                    if (managementObjectSearcher.Get().Count == 1)
                    {
                        foreach (ManagementObject device in managementObjectSearcher.Get())
                        {
                            //' Define property variables from device
                            string deviceNameProperty = (string)device.GetPropertyValue("Name");
                            var resourceIDProperty = device.GetPropertyValue("ResourceId");

                            //' Query for collection membership relations for device
                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                            {
                                foreach (ManagementObject collDevice in collMembershipSearcher.Get())
                                {
                                    //' Define property variables for collection
                                    string collectionId = (string)collDevice.GetPropertyValue("CollectionID");

                                    //' Query for collection
                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                    {
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            //' Define collection properties
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            //' Query for deployment info for collection
                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                            {
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    //' Return application name
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    applicationNames.Add(targetName);
                                                }
                                            }
                                        }
                                    }                                       
                                }
                            }
                        }
                    }
                }
            }

            MethodEnd(method);
            return applicationNames;
        }

        [WebMethod(Description = "Get required applications for a specific device")]
        public List<string> GetCMRequiredApplicationsByDevice(string secret, string deviceName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct applications list
            List<string> applicationNames = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Query for specified device name
                SelectQuery deviceQuery = new SelectQuery("SELECT * FROM SMS_R_System WHERE Name like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, deviceQuery);

                if (managementObjectSearcher.Get() != null)
                {
                    if (managementObjectSearcher.Get().Count == 1)
                    {
                        foreach (ManagementObject device in managementObjectSearcher.Get())
                        {
                            //' Define property variables from device
                            string deviceNameProperty = (string)device.GetPropertyValue("Name");
                            var resourceIDProperty = device.GetPropertyValue("ResourceId");

                            //' Query for collection membership relations for device
                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                            {
                                foreach (ManagementObject collDevice in collMembershipSearcher.Get())
                                {
                                    //' Define property variables for collection
                                    string collectionId = (string)collDevice.GetPropertyValue("CollectionID");

                                    //' Query for collection
                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                    {
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            //' Define collection properties
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            //' Query for deployment info for collection
                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31 AND DeploymentIntent = 0");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                            {
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    //' Return application name
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    applicationNames.Add(targetName);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MethodEnd(method);
            return applicationNames;
        }

        [WebMethod(Description = "Get all hidden task sequence deployments")]
        public List<CMTaskSequence> GetCMHiddenTaskSequenceDeployments(string secret)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct hidden task sequences list
            List<CMTaskSequence> hiddenTaskSequences = new List<CMTaskSequence>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for task sequence package
                    string query = "SELECT * FROM SMS_AdvertisementInfo WHERE PackageType = 4";
                    IResultObject queryResults = connection.QueryProcessor.ExecuteQuery(query);

                    foreach (IResultObject queryResult in queryResults)
                    {
                        //' Construct new taskSequence class object and define properties
                        CMTaskSequence taskSequence = new CMTaskSequence
                        {
                            PackageName = queryResult["PackageName"].StringValue,
                            AdvertFlags = queryResult["AdvertFlags"].IntegerValue,
                            AdvertisementId = queryResult["AdvertisementId"].StringValue
                        };

                        //' Add object to list if bit exists
                        if ((taskSequence.AdvertFlags & 0x20000000) != 0)
                            hiddenTaskSequences.Add(taskSequence);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for hidden task sequence deployments. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return hiddenTaskSequences;
        }

        [WebMethod(Description = "Get resource id for device by UUID (SMSBIOSGUID)")]
        public string GetCMDeviceResourceIDByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for device resource
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE SMBIOSGUID like '{0}'", uuid);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    string resourceId = string.Empty;

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            int id = device["ResourceId"].IntegerValue;
                            returnValue = id.ToString();
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device resource id returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying device resource id. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get resource id for device by MAC Address")]
        public string GetCMDeviceResourceIDByMACAddress(string secret, string macAddress)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for device resource
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE MacAddresses like '{0}'", macAddress);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    string resourceId = string.Empty;

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            int id = device["ResourceId"].IntegerValue;
                            returnValue = id.ToString();
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device resource id returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device resource id. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get resource id for a built-in unknown computer object by UUID (SMSBIOSGUID)")]
        public string GetCMUnknownComputerResourceIDByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for unknown computer resource
                    string query = String.Format("SELECT * FROM SMS_R_UnknownSystem WHERE SMSUniqueIdentifier like '{0}'", uuid);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    string resourceId = string.Empty;

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            int id = device["ResourceId"].IntegerValue;
                            returnValue = id.ToString();
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for unknown computer resource id returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for unknown computer resource id. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the name of a specific device by UUID (SMBIOS GUID)")]
        public string GetCMDeviceNameByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for device name
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE SMBIOSGUID like '{0}'", uuid);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            returnValue = device["Name"].StringValue;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device name returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device name. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the name of a specific device by ResourceID")]
        public string GetCMDeviceNameByResourceID(string secret, string resourceId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for device name
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE ResourceID like '{0}'", resourceId);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            returnValue = device["Name"].StringValue;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device name returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device name. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the name of a specific device by MACAddress")]
        public string GetCMDeviceNameByMACAddress(string secret, string macAddress)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for device name
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE MACAddresses like '{0}'", macAddress);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            returnValue = device["Name"].StringValue;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device name returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device name. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the UUID (SMBIOS GUID) of a specific device by name")]
        public string GetCMDeviceUUIDByName(string secret, string computerName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for device name
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE Name like '{0}'", computerName);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    if (result != null)
                    {
                        foreach (IResultObject device in result)
                        {
                            returnValue = device["SMBIOSGUID"].StringValue;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device UUID returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device UUID. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get hidden task sequence deployments for a specific resource id")]
        public List<CMTaskSequence> GetCMHiddenTaskSequenceDeploymentsByResourceId(string secret, string resourceId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct hidden task sequences list
            List<CMTaskSequence> hiddenTaskSequences = new List<CMTaskSequence>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
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
                                    string packageId = tsDeployment["PackageID"].StringValue;
                                    string advertId = tsDeployment["AdvertisementId"].StringValue;
                                    int advertFlags = tsDeployment["AdvertFlags"].IntegerValue;

                                    //' Construct taskSequence object
                                    CMTaskSequence ts = new CMTaskSequence { AdvertFlags = advertFlags, AdvertisementId = advertId, PackageName = packageName, PackageID = packageId };

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
            }

            MethodEnd(method);
            return hiddenTaskSequences;
        }

        [WebMethod(Description = "Get Boot Image source version")]
        public string GetCMBootImageSourceVersion(string secret, string packageId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for Boot Image instance
                    IResultObject queryResult = connection.GetInstance("SMS_BootImagePackage.PackageID='" + packageId + "'");

                    //' Return SourceVersion property from instance
                    if (queryResult != null)
                    {
                        int sourceVersion = queryResult["SourceVersion"].IntegerValue;
                        returnValue = sourceVersion.ToString();
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to retrieve boot image source version. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get details about a specific device collection by name")]
        public List<CMCollection> GetCMCollectionByName(string secret, string name)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct list of CMCollection objects for return value
            List<CMCollection> collList = new List<CMCollection>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for collection object
                    string query = String.Format("SELECT * FROM SMS_Collection WHERE Name like '%{0}%'", name);
                    IResultObject result = connection.QueryProcessor.ExecuteQuery(query);

                    if (result != null)
                    {
                        foreach (IResultObject collection in result)
                        {
                            CMCollection coll = new CMCollection
                            {
                                Name = collection["Name"].StringValue,
                                CollectionID = collection["CollectionID"].StringValue,
                                CollectionType = collection["CollectionType"].StringValue,
                                LimitingCollectionID = collection["LimitToCollectionID"].StringValue,
                                ObjectPath = collection["ObjectPath"].StringValue,
                                RefreshType = collection["RefreshType"].StringValue,
                                ServiceWindowsCount = collection["ServiceWindowsCount"].StringValue
                            };
                            collList.Add(coll);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for collections returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for collections. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return collList;
        }

        [WebMethod(Description = "Get all discovered users")]
        public List<CMUser> GetCMDiscoveredUsers(string secret)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct users list
            var userList = new List<CMUser>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
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
                    WriteEventLog(String.Format("An error occured when an attempt to query for user data from SMS Provider. Error message: {0}", ex.Message), EventLogEntryType.Error);
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
                            CMUser user = new CMUser();
                            user.UniqueUserName = uniqueUserName;
                            user.ResourceId = resourceId;
                            user.WindowsNTDomain = windowsNTDomain;
                            user.FullDomainName = fullDomainName;

                            //' Add user object to user list
                            userList.Add(user);
                        }
                }
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("An error occured while constructing list of user instances. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return userList;
        }

        [WebMethod(Description = "Get the unique username for a specific user (useful for setting a value for SMSTSUdaUsers)")]
        public string GetCMUniqueUserName(string secret, string userName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for unique username
                    IResultObject queryResults = null;
                    string query = "SELECT * FROM SMS_R_User";
                    queryResults = connection.QueryProcessor.ExecuteQuery(query);

                    if (queryResults != null)
                    {
                        foreach (IResultObject queryResult in queryResults)
                        {
                            //' Collect property values from instance
                            string uName = queryResult["UserName"].StringValue;
                            string uniqueUserName = queryResult["UniqueUserName"].StringValue;
                            if (uName.ToLower() == userName.ToLower())
                                returnValue = uniqueUserName;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for unique user name returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for unique user name. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get a user object with the most commonly referenced properties")]
        public CMUser GetCMUser(string secret, string samAccountName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            CMUser returnValue = null;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Query for unique username
                    string query = String.Format("SELECT * FROM SMS_R_User WHERE UserName like '{0}'", samAccountName);
                    IResultObject results = connection.QueryProcessor.ExecuteQuery(query);

                    if (results != null)
                    {
                        foreach (IResultObject result in results)
                        {
                            //' Construct new user object
                            returnValue = new CMUser();

                            //' Set user object properties from query results
                            returnValue.ResourceId = result["ResourceID"].StringValue;
                            returnValue.DistinguishedName = result["DistinguishedName"].StringValue;
                            returnValue.FullDomainName = result["FullDomainName"].StringValue;
                            returnValue.FullUserName = result["FullUserName"].StringValue;
                            returnValue.Name = result["Name"].StringValue;
                            returnValue.UniqueUserName = result["UniqueUserName"].StringValue;
                            returnValue.UserPrincipalName = result["UserPrincipalName"].StringValue;
                            returnValue.WindowsNTDomain = result["WindowsNTDomain"].StringValue;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for a given user object returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for user objects. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Import a computer by MAC Address")]
        public string ImportCMComputerByMacAddress(string secret, string computerName, string macAddress)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Construct method parameters
                Dictionary<string, object> methodParameters = new Dictionary<string, object>();
                methodParameters.Add("NetBIOSName", computerName);
                methodParameters.Add("MacAddress", macAddress);
                methodParameters.Add("OverWriteExistingRecord", true);

                //' Import computer
                string resourceId = ImportCMComputer(methodParameters);

                returnValue = resourceId;
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Import a computer by UUID (SMBIOS GUID)")]
        public string ImportCMComputerByUUID(string secret, string computerName, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Construct method parameters
                Dictionary<string, object> methodParameters = new Dictionary<string, object>();
                methodParameters.Add("NetBIOSName", computerName);
                methodParameters.Add("SMBIOSGuid", uuid);
                methodParameters.Add("OverWriteExistingRecord", true);

                //' Import computer
                string resourceId = ImportCMComputer(methodParameters);

                returnValue = resourceId;
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add a computer to a specific device collection (creates a direct membership rule)")]
        public bool AddCMComputerToCollection(string secret, string resourceName, string collectionId)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get resource id for given computer name
                    string resourceId = GetCMComputerResourceId(resourceName);
                    if (!String.IsNullOrEmpty(resourceId))
                    {
                        //' Initiate collection object
                        WqlResultObject collection = null;

                        //' Attempt to get collection
                        string query = String.Format("SELECT * FROM SMS_Collection WHERE CollectionID LIKE '{0}' AND CollectionType = 2", collectionId);
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

                                returnValue = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding computer to collection. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get all or a filtered list of device collections")]
        public List<CMCollection> GetCMDeviceCollections(string secret, string filter = null)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct list object
            List<CMCollection> collectionList = new List<CMCollection>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
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
                            CMCollection coll = new CMCollection
                            {
                                Name = collection["Name"].StringValue,
                                CollectionID = collection["CollectionID"].StringValue
                            };
                            collectionList.Add(coll);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device collections returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device collections. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return collectionList;
        }

        [WebMethod(Description = "Update membership of a specific collection")]
        public bool UpdateCMCollectionMembership(string secret, string collectionId)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
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
                                returnValue = true;
                            }
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for collection returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to update collection membership. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get Driver Package information by computer model")]
        public List<CMDriverPackage> GetCMDriverPackageByModel(string secret, string model)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct list for driver package ids
            List<CMDriverPackage> pkgList = new List<CMDriverPackage>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
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
                            CMDriverPackage drvPkg = new CMDriverPackage { PackageName = packageName, PackageID = packageId };
                            pkgList.Add(drvPkg);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for driver package returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for driver package. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return pkgList;
        }

        [WebMethod(Description = "Get a filtered list of packages")]
        public List<CMPackage> GetCMPackage(string secret, string filter)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct list for package ids
            List<CMPackage> pkgList = new List<CMPackage>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
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
                            string packageDescription = package["Description"].StringValue;
                            string packageManufacturer = package["Manufacturer"].StringValue;
                            string packageLanguage = package["Language"].StringValue;
                            string packageVersion = package["Version"].StringValue;
                            DateTime packageCreated = package["SourceDate"].DateTimeValue;

                            //' Add new package object to list
                            CMPackage pkg = new CMPackage
                            {
                                PackageName = packageName,
                                PackageID = packageId,
                                PackageDescription = packageDescription,
                                PackageManufacturer = packageManufacturer,
                                PackageLanguage = packageLanguage,
                                PackageVersion = packageVersion,
                                PackageCreated = packageCreated
                            };
                            pkgList.Add(pkg);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for packages returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for packages. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return pkgList;
        }

        [WebMethod(Description = "Check for 'Unknown' device record by UUID (SMBIOS GUID)")]
        public List<string> GetCMUnknownDeviceByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct list for unknown device resource ids
            List<string> resourceIds = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get unknown device records
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE Name like 'Unknown' AND SMBIOSGUID like '{0}'", uuid);
                    IResultObject unknownRecords = connection.QueryProcessor.ExecuteQuery(query);

                    if (unknownRecords != null)
                    {
                        foreach (IResultObject record in unknownRecords)
                        {
                            string resourceId = record["ResourceID"].StringValue;
                            resourceIds.Add(resourceId);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for unknown device records returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for unknown device records. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return resourceIds;
        }

        [WebMethod(Description = "Retrieve the associated OS Image version for a specific task sequence (supports multiple images)")]
        public List<string> GetCMOSImageVersionForTaskSequence(string secret, string tsPackageId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct variable for OS Image version
            List<string> osVersionList = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get OS image property info
                    List<string> propsInfo = GetOSImageProperty("OSVersion", tsPackageId);

                    if (propsInfo != null)
                    {
                        foreach (string prop in propsInfo)
                        {
                            osVersionList.Add(prop);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for OS image version from task sequence. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return osVersionList;
        }

        [WebMethod(Description = "Retrieve the associated OS Image architecture for a specific task sequence (supports multiple images)")]
        public List<string> GetCMOSImageArchitectureForTaskSequence(string secret, string tsPackageId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct variable for OS Image version
            List<string> osArchitectureList = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get OS image property info
                    List<string> propsInfo = GetOSImageProperty("Architecture", tsPackageId);

                    if (propsInfo != null)
                    {
                        foreach (string prop in propsInfo)
                        {
                            osArchitectureList.Add(prop);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for OS image architecture from task sequence. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return osArchitectureList;
        }

        [WebMethod(Description = "Retrieve the associated OS Image object details for a specific task sequence (supports multiple images)")]
        public List<CMOSImage> GetCMOSImageForTaskSequence(string secret, string tsPackageId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct variable for OS Image version
            List<CMOSImage> osImageList = new List<CMOSImage>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get OS images
                    List<CMOSImage> osImages = GetOSImage(tsPackageId);

                    if (osImages != null)
                    {
                        foreach (CMOSImage image in osImages)
                        {
                            osImageList.Add(image);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for OS image details from task sequence. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return osImageList;
        }

        [WebMethod(Description = "Delete 'Unknown' device record by UUID (SMBIOS GUID)")]
        public int RemoveCMUnknownDeviceByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for amount of removed records
            int records = 0;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get unknown device records
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE Name like 'Unknown' AND SMBIOSGUID like '{0}'", uuid);
                    IResultObject unknownRecord = connection.QueryProcessor.ExecuteQuery(query);

                    //' Remove all unknown device records matching uuid
                    if (unknownRecord != null)
                    {
                        foreach (IResultObject record in unknownRecord)
                        {
                            record.Delete();
                            records++;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for unknown device record returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while removing unknown device records. Erro message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return records;
        }

        [WebMethod(Description = "Delete specific device record by UUID (SMBIOS GUID)")]
        public int RemoveCMDeviceByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for amount of removed records
            int records = 0;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get device records
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE SMBIOSGUID like '{0}'", uuid);
                    IResultObject deviceRecords = connection.QueryProcessor.ExecuteQuery(query);

                    //' Remove all device records matching uuid
                    if (deviceRecords != null)
                    {
                        foreach (IResultObject record in deviceRecords)
                        {
                            record.Delete();
                            records++;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device object returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove device object. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return records;
        }

        [WebMethod(Description = "Delete specific device record by name")]
        public int RemoveCMDeviceByName(string secret, string name)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for amount of removed records
            int records = 0;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get device records
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE Name like '{0}'", name);
                    IResultObject deviceRecords = connection.QueryProcessor.ExecuteQuery(query);

                    //' Remove all device records matching name
                    if (deviceRecords != null)
                    {
                        foreach (IResultObject record in deviceRecords)
                        {
                            record.Delete();
                            records++;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device object returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove device object. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return records;
        }

        [WebMethod(Description = "Delete specific device record by resource id")]
        public int RemoveCMDeviceByResourceID(string secret, string resourceId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for amount of removed records
            int records = 0;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get device records
                    string query = String.Format("SELECT * FROM SMS_R_System WHERE ResourceID like '{0}'", resourceId);
                    IResultObject deviceRecords = connection.QueryProcessor.ExecuteQuery(query);

                    //' Remove all device records matching resource id
                    if (deviceRecords != null)
                    {
                        foreach (IResultObject record in deviceRecords)
                        {
                            record.Delete();
                            records++;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device object returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove device object. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return records;
        }

        [WebMethod(Description = "Remove a device from a specific collection (only for Direct Membership rules)")]
        public bool RemoveCMDeviceFromCollection(string secret, string deviceName, string collectionId)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get the device resource instance to be removed from collection
                    CMResource resource = GetCMResource(deviceName, CMObjectType.System, CMResourceProperty.Name);

                    if (resource != null)
                    {
                        try
                        {
                            //' Get the collection instance where the device resource will be removed from
                            IResultObject collection = GetCMCollection(collectionId, CMCollectionType.DeviceCollection);

                            if (collection != null)
                            {
                                try
                                {
                                    //' Construct dictionary for removal parameters
                                    Dictionary<string, object> removeParams = new Dictionary<string, object>();

                                    //' Construct new direct rule instance and add as param
                                    IResultObject removalRule = connection.CreateInstance("SMS_CollectionRuleDirect");
                                    removalRule["ResourceID"].StringValue = resource.ResourceID.ToString();
                                    removeParams.Add("collectionRule", removalRule);

                                    //' Remove direct rule from collection
                                    IResultObject execute = collection.ExecuteMethod("DeleteMembershipRule", removeParams);

                                    if (execute["ReturnValue"].IntegerValue == 0)
                                    {
                                        Dictionary<string, object> refreshParams = new Dictionary<string, object>();
                                        IResultObject exec = collection.ExecuteMethod("RequestRefresh", refreshParams);

                                        returnValue = true;
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    WriteEventLog(String.Format("An error occured when attempting to remove device resource from collection. Error message: {0}", ex.Message), EventLogEntryType.Error);
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            WriteEventLog(String.Format("An error occured when attempting to locate device collection. Error message: {0}", ex.Message), EventLogEntryType.Error);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to locate device resource. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove last PXE advertisement for a specific device")]
        public bool RemoveCMLastPXEAdvertisementForDevice(string secret, string deviceName)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get resource id for device
                    CMResource resource = GetCMResource(deviceName, CMObjectType.System, CMResourceProperty.Name);

                    //' Construct list of resource ids
                    List<string> resourceList = new List<string>
                    {
                        resource.ResourceID.ToString()
                    };

                    //' Construct in params for method execution
                    Dictionary<string, object> execParams = new Dictionary<string, object>
                    {
                        { "ResourceIDs", resourceList.ToArray() }
                    };

                    //' Clear last PXE advertisement for device
                    IResultObject execute = connection.ExecuteMethod("SMS_Collection", "ClearLastNBSAdvForMachines", execParams);

                    if (execute["ReturnValue"].IntegerValue == 0)
                    {
                        returnValue = true;
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove PXE advertisement for device. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove last PXE advertisement for a specific collection")]
        public bool RemoveCMLastPXEAdvertisementForCollection(string secret, string collectionId)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get collection object
                    IResultObject collection = GetCMCollection(collectionId, CMCollectionType.DeviceCollection);

                    //' Construct in params for method execution
                    Dictionary<string, object> execParams = new Dictionary<string, object>();

                    //' Clear last PXE advertisement for device
                    if (collection != null)
                    {
                        IResultObject execute = collection.ExecuteMethod("ClearLastNBSAdvForCollection", execParams);

                        if (execute["ReturnValue"].IntegerValue == 0)
                        {
                            returnValue = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove PXE advertisement for collection. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add a computer association between a source and destination device and capture user state for a single user")]
        public bool AddCMComputerAssociationForUser(string secret, string sourceName, string destinationName, string userName)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get source resource
                    CMResource sourceResource = GetCMResource(sourceName, CMObjectType.System, CMResourceProperty.Name);

                    //' Get destination resource
                    CMResource destinationResource = GetCMResource(destinationName, CMObjectType.System, CMResourceProperty.Name);

                    if (sourceResource != null && destinationResource != null)
                    {
                        //' Construct in params for execution
                        UInt32 migBehavior = 2;
                        Dictionary<string, object> execParams = new Dictionary<string, object>();
                        execParams.Add("SourceClientResourceID", sourceResource.ResourceID);
                        execParams.Add("RestoreClientResourceID", destinationResource.ResourceID);
                        execParams.Add("MigrationBehavior", migBehavior);

                        //' Construct a list for holding embedded instances
                        List<IResultObject> userList = new List<IResultObject>();

                        //' Construct embedded instance for user name param info and add to list
                        IResultObject userInstance = connection.CreateEmbeddedObjectInstance("SMS_StateMigrationUserNames");
                        userInstance["UserName"].StringValue = userName;
                        userInstance["LocaleID"].IntegerValue = 0;
                        userList.Add(userInstance);

                        //' Add list of embedded instances to params
                        execParams.Add("UserNames", userList);

                        try
                        {
                            IResultObject execute = connection.ExecuteMethod("SMS_StateMigration", "AddAssociationEx", execParams);
                            if (execute["ReturnValue"].IntegerValue == 0)
                            {
                                returnValue = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteEventLog($"An error occured while executing AddAssociationEx method. Error message: { ex.Message }", EventLogEntryType.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to create a computer association. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add a computer association between a source and destination device and capture user state for all users")]
        public bool AddCMComputerAssociationForAllUsers(string secret, string sourceName, string destinationName)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get source resource
                    CMResource sourceResource = GetCMResource(sourceName, CMObjectType.System, CMResourceProperty.Name);

                    //' Get destination resource
                    CMResource destinationResource = GetCMResource(destinationName, CMObjectType.System, CMResourceProperty.Name);

                    if (sourceResource != null && destinationResource != null)
                    {
                        //' Construct in params for execution
                        UInt32 migBehavior = 0;
                        Dictionary<string, object> execParams = new Dictionary<string, object>();
                        execParams.Add("SourceClientResourceID", sourceResource.ResourceID);
                        execParams.Add("RestoreClientResourceID", destinationResource.ResourceID);
                        execParams.Add("MigrationBehavior", migBehavior);

                        try
                        {
                            IResultObject execute = connection.ExecuteMethod("SMS_StateMigration", "AddAssociationEx", execParams);
                            if (execute["ReturnValue"].IntegerValue == 0)
                            {
                                returnValue = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteEventLog($"An error occured while executing AddAssociationEx method. Error message: { ex.Message }", EventLogEntryType.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to create a computer association. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove a computer association between a source and destination device")]
        public bool RemoveCMComputerAssociation(string secret, string sourceName, string destinationName)
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

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get source resource
                    CMResource sourceResource = GetCMResource(sourceName, CMObjectType.System, CMResourceProperty.Name);

                    //' Get destination resource
                    CMResource destinationResource = GetCMResource(destinationName, CMObjectType.System, CMResourceProperty.Name);

                    if (sourceResource != null && destinationResource != null)
                    {
                        //' Construct in params for execution
                        Dictionary<string, object> execParams = new Dictionary<string, object>();
                        execParams.Add("SourceClientResourceID", sourceResource.ResourceID);
                        execParams.Add("RestoreClientResourceID", destinationResource.ResourceID);

                        try
                        {
                            IResultObject execute = connection.ExecuteMethod("SMS_StateMigration", "DeleteAssociation", execParams);
                            if (execute["ReturnValue"].IntegerValue == 0)
                            {
                                returnValue = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteEventLog($"An error occured while attempting to execute DeleteAssociation method. Error message: { ex.Message }", EventLogEntryType.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove computer association. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get deployed applications by collection ID")]
        public List<string> GetCMApplicationDeploymentsByCollectionID(string secret, string collId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct new list for application deployments
            List<string> appDeployments = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get application deployments by collection id
                    string query = String.Format("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '{0}' AND DeploymentTypeID like '2'", collId);
                    IResultObject deployments = connection.QueryProcessor.ExecuteQuery(query);

                    if (deployments != null)
                    {
                        foreach (IResultObject deployment in deployments)
                        {
                            string appName = deployment["TargetName"].StringValue;
                            appDeployments.Add(appName);
                        }
                        appDeployments.Sort();
                    }
                    else
                    {
                        WriteEventLog("Query for application deployments returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for application deployments. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return appDeployments;
        }

        [WebMethod(Description = "Get all collections a specific device is a member of by UUID (SMBIOS GUID)")]
        public List<CMCollection> GetCMCollectionsForDeviceByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<CMCollection> returnValue = new List<CMCollection>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get resource
                    CMResource resource = GetCMResource(uuid, CMObjectType.System, CMResourceProperty.SMBIOSGUID);

                    //' Get all collections for device
                    string query = String.Format("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID = {0} AND ResourceType = {1:D}", resource.ResourceID, CMObjectType.System);
                    IResultObject collections = connection.QueryProcessor.ExecuteQuery(query);

                    if (collections != null)
                    {
                        foreach (IResultObject collection in collections)
                        {
                            //' Construct new collection object
                            CMCollection coll = new CMCollection();

                            //' Get collection name from collection ID
                            IResultObject collInstance = GetCMCollection(collection["CollectionID"].StringValue, CMCollectionType.DeviceCollection);

                            //' Add properties to collection object
                            coll.CollectionID = collection["CollectionID"].StringValue;
                            coll.Name = collInstance["Name"].StringValue;
                            returnValue.Add(coll);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device collections returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device collections. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get all collections a specific device is a member of by device name")]
        public List<CMCollection> GetCMCollectionsForDeviceByName(string secret, string deviceName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<CMCollection> returnValue = new List<CMCollection>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get resource
                    CMResource resource = GetCMResource(deviceName, CMObjectType.System, CMResourceProperty.Name);

                    //' Get all collections for device
                    string query = String.Format("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID = {0} AND ResourceType = {1:D}", resource.ResourceID, CMObjectType.System);
                    IResultObject collections = connection.QueryProcessor.ExecuteQuery(query);

                    if (collections != null)
                    {
                        foreach (IResultObject collection in collections)
                        {
                            //' Construct new collection object
                            CMCollection coll = new CMCollection();

                            //' Get collection name from collection ID
                            IResultObject collInstance = GetCMCollection(collection["CollectionID"].StringValue, CMCollectionType.DeviceCollection);

                            //' Add properties to collection object
                            coll.CollectionID = collection["CollectionID"].StringValue;
                            coll.Name = collInstance["Name"].StringValue;
                            returnValue.Add(coll);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device collections returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device collections. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get all collections a specific device is a member of by ResourceID")]
        public List<CMCollection> GetCMCollectionsForDeviceByResourceID(string secret, string resourceId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<CMCollection> returnValue = new List<CMCollection>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get resource
                    CMResource resource = GetCMResource(resourceId, CMObjectType.System, CMResourceProperty.ResourceID);

                    //' Get all collections for device
                    string query = String.Format("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID = {0} AND ResourceType = {1:D}", resource.ResourceID, CMObjectType.System);
                    IResultObject collections = connection.QueryProcessor.ExecuteQuery(query);

                    if (collections != null)
                    {
                        foreach (IResultObject collection in collections)
                        {
                            //' Construct new collection object
                            CMCollection coll = new CMCollection();

                            //' Get collection name from collection ID
                            IResultObject collInstance = GetCMCollection(collection["CollectionID"].StringValue, CMCollectionType.DeviceCollection);

                            //' Add properties to collection object
                            coll.CollectionID = collection["CollectionID"].StringValue;
                            coll.Name = collInstance["Name"].StringValue;
                            returnValue.Add(coll);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for device collections returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for device collections. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get all applications and filter by administrative category")]
        public List<CMApplication> GetCMApplicationByCategory(string secret, string categoryName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<CMApplication> appList = new List<CMApplication>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get applications matching category name
                    string appQuery = String.Format("SELECT * FROM SMS_ApplicationLatest WHERE (IsHidden = 0) AND (LocalizedCategoryInstanceNames = '{0}') AND (IsExpired = 0)", categoryName);
                    IResultObject appInstances = connection.QueryProcessor.ExecuteQuery(appQuery);

                    if (appInstances != null)
                    {
                        foreach (IResultObject app in appInstances)
                        {
                            //' Construct a new CMApplication object
                            CMApplication application = new CMApplication
                            {
                                ApplicationName = app["LocalizedDisplayName"].StringValue
                            };
                            appList.Add(application);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for application category returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for application category. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return appList;
        }

        [WebMethod(Description = "Get all applications or a filtered list")]
        public List<CMApplication> GetCMApplication(string secret, string filter)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<CMApplication> appList = new List<CMApplication>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Determine query for application objects
                    string appQuery = null;
                    if (!String.IsNullOrEmpty(filter))
                    {
                        appQuery = String.Format("SELECT * FROM SMS_ApplicationLatest WHERE (IsHidden = 0) AND (LocalizedDisplayName = '{0}')", filter);
                    }
                    else
                    {
                        appQuery = "SELECT * FROM SMS_ApplicationLatest WHERE (IsHidden = 0)";
                    }

                    //' Get applications objects
                    IResultObject appInstances = connection.QueryProcessor.ExecuteQuery(appQuery);

                    if (appInstances != null)
                    {
                        foreach (IResultObject app in appInstances)
                        {
                            //' Construct a new CMApplication object
                            CMApplication application = new CMApplication
                            {
                                ApplicationName = app["LocalizedDisplayName"].StringValue,
                                ApplicationDescription = app["LocalizedDescription"].StringValue,
                                ApplicationManufacturer = app["Manufacturer"].StringValue,
                                ApplicationVersion = app["SoftwareVersion"].StringValue,
                                ApplicationCreated = app["DateCreated"].DateTimeValue,
                                ApplicationExecutionContext = app["ExecutionContext"].StringValue
                            };
                            appList.Add(application);
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for applications returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for applications. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return appList;
        }

        [WebMethod(Description = "Remove all primary user relations for a specific device by resource id")]
        public int RemoveCMPrimaryUserByDeviceResourceId(string secret, string resourceId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            int returnValue = 0;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get primary user relations for device resource id
                    string relQuery = String.Format("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceID = '{0}'", resourceId);
                    IResultObject relResult = connection.QueryProcessor.ExecuteQuery(relQuery);

                    if (relResult != null)
                    {
                        foreach (IResultObject relation in relResult)
                        {
                            try
                            {
                                //' Delete relation instance
                                relation.Delete();
                                returnValue++;
                                WriteEventLog(String.Format("Successfully removed primary user relation for ResourceID '{0}': {1}", resourceId, relation["UniqueUserName"].StringValue), EventLogEntryType.Information);
                            }
                            catch (Exception ex)
                            {
                                WriteEventLog(String.Format("Unable to remove primary user relation for ResourceID '{1}' with user name '{2}'. Error message: {0}", ex.Message, resourceId, relation["UniqueUserName"].StringValue), EventLogEntryType.Warning);
                            }
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for user machine relations returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while attempting to remove primary user for device. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Find the first available number from computer name")]
        public string GetCMFirstAvailableNameSequence(string secret, int suffixLength, string computerNamefilter)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = null;

            //' Create list for suffix numbers for all computers or those matching the filter
            List<int> suffixList = new List<int>();

            string suffixMask = null;
            switch (suffixLength)
            {
                case 1:
                    suffixMask = "0";
                    break;
                case 2:
                    suffixMask = "00";
                    break;
                case 3:
                    suffixMask = "000";
                    break;
            }

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                //' Determine query for computer objects
                string resQuery = null;
                if (!String.IsNullOrEmpty(computerNamefilter))
                {
                    resQuery = String.Format("SELECT * FROM SMS_R_System WHERE Name like '%{0}%'", computerNamefilter);
                }
                else
                {
                    resQuery = "SELECT * FROM SMS_R_System";
                }

                //' Get all computer objects suffix sequence
                IResultObject resResult = connection.QueryProcessor.ExecuteQuery(resQuery);
                if (resResult != null)
                {
                    try
                    {
                        foreach (IResultObject res in resResult)
                        {
                            int nameLength = res["Name"].StringValue.Length;
                            int nameNumber = int.Parse(res["Name"].StringValue.Substring(nameLength - suffixLength));
                            suffixList.Add(nameNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("Unable to parse computer name suffix from string to integer. Error message: {0}", ex.Message), EventLogEntryType.Warning);
                    }
                }

                //' Locate next available number in the suffix sequence
                if (suffixList.Count >= 1)
                {
                    int missingNumber = FindMissingNumber(suffixList);
                    returnValue = missingNumber.ToString(suffixMask);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get details about a specific task sequence")]
        public CMTaskSequence GetCMTaskSequence(string secret, string packageID)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            CMTaskSequence taskSequence = new CMTaskSequence();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Connect to SMS Provider
                SmsProvider smsProvider = new SmsProvider();
                WqlConnectionManager connection = smsProvider.Connect(siteServer);

                try
                {
                    //' Get task sequence object
                    string tsQuery = String.Format("SELECT * FROM SMS_TaskSequencePackage WHERE PackageID = '{0}'", packageID);
                    IResultObject tsResult = connection.QueryProcessor.ExecuteQuery(tsQuery);

                    if (tsResult != null)
                    {
                        foreach (IResultObject ts in tsResult)
                        {
                            taskSequence.PackageID = ts["PackageID"].StringValue;
                            taskSequence.Description = ts["Description"].StringValue;
                            taskSequence.PackageName = ts["Name"].StringValue;
                            taskSequence.BootImageID = ts["BootImageID"].StringValue;
                        }
                    }
                    else
                    {
                        WriteEventLog("Query for task sequence returned empty", EventLogEntryType.Information);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for task sequence. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return taskSequence;
        }

        [WebMethod(Description = "Move a computer in Active Directory to a specific organizational unit")]
        public bool SetADOrganizationalUnitForComputer(string secret, string organizationalUnitLocation, string computerName)
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

                //' Determine if ldap prefix needs to be appended
                if (organizationalUnitLocation.StartsWith("LDAP://") == false)
                {
                    organizationalUnitLocation = String.Format("LDAP://{0}", organizationalUnitLocation);
                }

                //' Get AD object distinguished name
                string currentDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(currentDistinguishedName))
                {
                    try
                    {
                        //' Move current object to new location
                        DirectoryEntry currentObject = new DirectoryEntry(currentDistinguishedName);
                        DirectoryEntry newLocation = new DirectoryEntry(organizationalUnitLocation);
                        currentObject.MoveTo(newLocation, currentObject.Name);

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to move Active Directory object. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Set ManagedBy attribute for a specific computer with specified user name")]
        public bool SetADComputerManagedBy(string secret, string computerName, string userName)
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

                //' Get AD computer and user object distinguished names
                string computerDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName);
                string userDistinguishedName = (GetADObject(userName, ADObjectClass.User, ADObjectType.distinguishedName)).Remove(0, 7);

                if (!String.IsNullOrEmpty(computerDistinguishedName) && !String.IsNullOrEmpty(userDistinguishedName))
                {
                    try
                    {
                        //' Add user to ManagedBy attribute and commit
                        DirectoryEntry computerEntry = new DirectoryEntry(computerDistinguishedName);
                        computerEntry.Properties["ManagedBy"].Clear();
                        computerEntry.Properties["ManagedBy"].Add(userDistinguishedName);
                        computerEntry.CommitChanges();

                        //' Dispose object
                        computerEntry.Dispose();

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to add a user as ManagedBy for a computer object in Active Directory. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get a specific attribute value for specific an Active Directory computer object")]
        public string GetADComputerAttributeValue(string secret, string computerName, string attributeName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD computer object distinguished name
                string computerDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(computerDistinguishedName))
                {
                    try
                    {
                        //' Retrieve specific attribute value
                        DirectoryEntry computerEntry = new DirectoryEntry(computerDistinguishedName);
                        returnValue = computerEntry.Properties[attributeName].Value.ToString();

                        //' Dispose object
                        computerEntry.Dispose();
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format($"An error occured when attempting to retrieve value for attribute '{ attributeName }' from computer object '{ computerName }' in Active Directory. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get a specific attribute value for specific an Active Directory user object")]
        public string GetADUserAttributeValue(string secret, string userName, string attributeName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD user object distinguished name
                string computerDistinguishedName = GetADObject(userName, ADObjectClass.User, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(computerDistinguishedName))
                {
                    try
                    {
                        //' Retrieve specific attribute value
                        DirectoryEntry userEntry = new DirectoryEntry(computerDistinguishedName);
                        returnValue = userEntry.Properties[attributeName].Value.ToString();

                        //' Dispose object
                        userEntry.Dispose();
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format($"An error occured when attempting to retrieve value for attribute '{ attributeName }' from user object '{ userName }' in Active Directory. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add a computer in Active Directory to a specific group")]
        public bool AddADComputerToGroup(string secret, string groupName, string computerName)
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
                string computerDistinguishedName = (GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName)).Remove(0, 7);
                string groupDistinguishedName = GetADObject(groupName, ADObjectClass.Group, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(computerDistinguishedName) && !String.IsNullOrEmpty(groupDistinguishedName))
                {
                    try
                    {
                        //' Add computer to group and commit
                        DirectoryEntry groupEntry = new DirectoryEntry(groupDistinguishedName);
                        groupEntry.Properties["member"].Add(computerDistinguishedName);
                        groupEntry.CommitChanges();

                        //' Dispose object
                        groupEntry.Dispose();

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to add a computer object in Active Directory to a group. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add a user in Active Directory to a specific group")]
        public bool AddADUserToGroup(string secret, string groupName, string userName)
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

        [WebMethod(Description = "Remove a computer in Active Directory from a specific group")]
        public bool RemoveADComputerFromGroup(string secret, string groupName, string computerName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Set return value variable
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD object distinguished name for computer and group
                string computerDistinguishedName = (GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName)).Remove(0,7);
                string groupDistinguishedName = GetADObject(groupName, ADObjectClass.Group, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(computerDistinguishedName) && !String.IsNullOrEmpty(groupDistinguishedName))
                {
                    try
                    {
                        //' Check if computer is member of group
                        DirectoryEntry groupEntry = new DirectoryEntry(groupDistinguishedName);
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
                        WriteEventLog(String.Format("An error occured when attempting to remove a computer object in Active Directory from a group. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get all members of an Active Directory group")]
        public List<string> GetADGroupMembers(string secret, string groupName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Set return value variable
            List<string> returnValue = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD group object
                string groupDistinguishedName = GetADObject(groupName, ADObjectClass.Group, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(groupDistinguishedName))
                {
                    try
                    {
                        DirectoryEntry groupEntry = new DirectoryEntry(groupDistinguishedName);
                        returnValue = GetADGroupMemberList(groupEntry);
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when retrieving Active Directory group members. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get Active Directory groups for a specific user")]
        public List<ADGroup> GetADGroupsByUser(string secret, string userName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Set return value variable
            List<ADGroup> returnValue = new List<ADGroup>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get AD user object
                    string userDistinguishedName = GetADObject(userName, ADObjectClass.User, ADObjectType.distinguishedName);

                    //' Get AD groups for user distinguished name
                    ArrayList groupMemberships = new ArrayList();
                    ArrayList groups = GetADAttributeValues("memberOf", userDistinguishedName, groupMemberships, true);

                    foreach (string group in groups)
                    {
                        string attributeValue = GetADAttributeValue(group, "samAccountName");
                        returnValue.Add(new ADGroup() { DistinguishedName = group, samAccountName = attributeValue });
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while retrieving Active Directory group memberships for user. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Set the description field for a computer in Active Directory")]
        public bool SetADComputerDescription(string secret, string computerName, string description)
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

                //' Get AD object distinguished name for computer
                string computerDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName);
                
                if (!String.IsNullOrEmpty(computerDistinguishedName))
                {
                    try
                    {
                        //' Set computer object description
                        DirectoryEntry computerEntry = new DirectoryEntry(computerDistinguishedName);
                        computerEntry.Properties["description"].Value = description;
                        computerEntry.CommitChanges();

                        //' Dispose object
                        computerEntry.Dispose();

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to remove a computer object in Active Directory from a group. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the description field for a computer in Active Directory")]
        public string GetADComputerDescription(string secret, string computerName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD object distinguished name for computer
                string computerDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName);

                if (!String.IsNullOrEmpty(computerDistinguishedName))
                {
                    try
                    {
                        //' Set computer object description
                        DirectoryEntry computerEntry = new DirectoryEntry(computerDistinguishedName);
                        returnValue = computerEntry.Properties["description"].Value.ToString();

                        //' Dispose object
                        computerEntry.Dispose();
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to remove a computer object in Active Directory from a group. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the Active Directory site name by IP address")]
        public string GetADSiteNameByIPAddress(string secret, string forestName, string ipAddress)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for site name
            string siteName = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get all subnets for specified forest
                    Dictionary<string, string> subnets = GetADSubnets(forestName);

                    foreach (KeyValuePair<string, string> subnet in subnets)
                    {
                        var ipData = IPAddresses.GetSubnetAndMaskFromCidr(subnet.Key);
                        bool result = IPAddresses.IsAddressOnSubnet(IPAddress.Parse(ipAddress), ipData.Item1, ipData.Item2);
                        if (result == true)
                        {
                            siteName = subnet.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while retrieving Active Directory site name. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return siteName;
        }

        [WebMethod(Description = "Validates if user is a member of a specific group")]
        public bool GetADGroupMemberByUser(string secret, string userName, string groupName, string domain)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            bool memberState = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Check if user is member of group
                    using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
                    using (UserPrincipal user = UserPrincipal.FindByIdentity(context, userName))
                    using (GroupPrincipal group = GroupPrincipal.FindByIdentity(context, groupName))
                    {
                        if (user != null)
                        {
                            WriteEventLog("Successfully located user object", EventLogEntryType.Information);
                            if (group != null)
                            {
                                WriteEventLog("Successfully located group object", EventLogEntryType.Information);

                                //' Check if user is member of group from param value, also check nested groups
                                memberState = GetADGroupNestedMemberOf(user, group);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while verifying user group membership. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return memberState;
        }

        [WebMethod(Description = "Validates if computer is a member of a specific group")]
        public bool GetADGroupMemberByComputer(string secret, string computerName, string groupName, string domain)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            bool memberState = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Check if computer is member of group
                    using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
                    using (ComputerPrincipal computer = ComputerPrincipal.FindByIdentity(context, computerName))
                    using (GroupPrincipal group = GroupPrincipal.FindByIdentity(context, groupName))
                    {
                        if (computer != null)
                        {
                            WriteEventLog("Successfully located computer object", EventLogEntryType.Information);
                            if (group != null)
                            {
                                WriteEventLog("Successfully located group object", EventLogEntryType.Information);

                                //' Check if computer is member of group from param value, also check nested groups
                                memberState = GetADGroupNestedMemberOf(computer, group);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while verifying computer group membership. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return memberState;
        }

        [WebMethod(Description = "Check if a computer object exists in Active Directory")]
        public ADComputer GetADComputer(string secret, string computerName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            ADComputer returnValue = new ADComputer();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Set empty value for search result
                SearchResult searchResult = null;
                DirectoryEntry directoryObject = null;

                //' Get default naming context of current domain
                string defaultNamingContext = GetADDefaultNamingContext();
                string currentDomain = String.Format("GC://{0}", defaultNamingContext);

                //' Construct directory entry for directory searcher
                DirectoryEntry domain = new DirectoryEntry(currentDomain);
                DirectorySearcher directorySearcher = new DirectorySearcher(domain)
                {
                    Filter = String.Format("(&(objectClass=computer)((sAMAccountName={0}$)))", computerName)
                };
                directorySearcher.PropertiesToLoad.Add("distinguishedName");
                directorySearcher.PropertiesToLoad.Add("sAMAccountName");
                directorySearcher.PropertiesToLoad.Add("cn");
                directorySearcher.PropertiesToLoad.Add("dNSHostName");

                //' Invoke directory searcher
                try
                {
                    searchResult = directorySearcher.FindOne();
                    if (searchResult != null)
                    {
                        //' Get computer object from search result
                        directoryObject = searchResult.GetDirectoryEntry();

                        if (directoryObject != null)
                        {
                            returnValue.SamAccountName = (string)directoryObject.Properties["sAMAccountName"].Value;
                            returnValue.CanonicalName = (string)directoryObject.Properties["cn"].Value;
                            returnValue.DistinguishedName = (string)directoryObject.Properties["distinguishedName"].Value;
                            returnValue.DnsHostName = (string)directoryObject.Properties["dNSHostName"].Value;

                            // Dispose directory object
                            directoryObject.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to locate Active Directory object. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }

                //' Dispose objects
                directorySearcher.Dispose();
                domain.Dispose();
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove a computer object from Active Directory (Prohibits removal of domain controllers)")]
        public bool RemoveADComputer(string secret, string samAccountName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Construct list for all domain controllers
                List<string> domainControllers = new List<string>();

                //' Configure domain context
                Domain currentDomain = Domain.GetCurrentDomain();
                PrincipalContext principalContext = new PrincipalContext(ContextType.Domain, currentDomain.Name, null, ContextOptions.Negotiate);
                WriteEventLog(String.Format("Using current domain for domain controller lookup: {0}", currentDomain.Name), EventLogEntryType.Information);

                //' Get a list of all domain controllers in the current domain
                try
                {
                    foreach (DomainController domainController in currentDomain.DomainControllers)
                    {
                        //' Add domain controller distinguished name to list
                        DirectoryEntry domainControllerEntry = domainController.GetDirectoryEntry();
                        string domainControllerName = (string)domainControllerEntry.Properties["name"].Value;

                        //' Debug
                        WriteEventLog(String.Format("Detected domain controller name: {0}", domainControllerName), EventLogEntryType.Information);

                        ComputerPrincipal dcPrincipal = ComputerPrincipal.FindByIdentity(principalContext, IdentityType.Name, domainControllerName);
                        domainControllers.Add(dcPrincipal.DistinguishedName);

                        //' Dispose objects
                        domainControllerEntry.Dispose();
                        dcPrincipal.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("Unable to detect domain controllers in current domain. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    returnValue = false;
                }

                if (domainControllers.Count >= 1)
                {
                    //' Get computer principal eligible for removal
                    ComputerPrincipal computerPrincipal = ComputerPrincipal.FindByIdentity(principalContext, samAccountName);

                    if (computerPrincipal != null)
                    {
                        if (domainControllers.Contains(computerPrincipal.DistinguishedName) == false)
                        {
                            try
                            {
                                //' Delete computer object including any leaf objects
                                DirectoryEntry subEntry = (DirectoryEntry)computerPrincipal.GetUnderlyingObject();

                                if (subEntry != null)
                                {
                                    subEntry.DeleteTree();
                                    subEntry.CommitChanges();

                                    WriteEventLog(String.Format("Successfully removed computer object named '{0}'", computerPrincipal.Name), EventLogEntryType.Information);
                                }

                                //' Dispose object
                                subEntry.Dispose();

                                returnValue = true;
                            }
                            catch (Exception ex)
                            {
                                WriteEventLog(String.Format("Unable to remove computer object '{0}'. Error message: {1}", samAccountName, ex.Message), EventLogEntryType.Error);
                                returnValue = false;
                            }
                        }

                        //' Dispose object
                        computerPrincipal.Dispose();
                    }
                    else
                    {
                        WriteEventLog(String.Format("Unable to find a computer object named '{0}'", computerPrincipal.Name), EventLogEntryType.Information);
                    }
                }

                //' Dispose objects
                currentDomain.Dispose();
                principalContext.Dispose();
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get the domain details for current domain")]
        public ADDomain GetADDomain(string secret)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            ADDomain domain = new ADDomain();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    domain.DefaultNamingContext = GetADDefaultNamingContext();
                    domain.DomainName = GetADDomainName();
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for Active Directory domain details. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return domain;
        }

        [WebMethod(Description = "Get a list of all organizational units from a specific container level")]
        public List<ADOrganizationalUnit> GetADOrganizationalUnits(string secret, string distinguishedName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<ADOrganizationalUnit> containers = new List<ADOrganizationalUnit>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Determine if ldap prefix needs to be appended
                    if (distinguishedName.StartsWith("LDAP://") == false)
                    {
                        distinguishedName = String.Format("LDAP://{0}", distinguishedName);
                    }

                    //' Get the base OU directory entry and start a one level search
                    using (DirectorySearcher directorySearcher = new DirectorySearcher(new DirectoryEntry(distinguishedName)))
                    {
                        //' Define filter options for searcher
                        directorySearcher.Filter = "(objectCategory=organizationalUnit)";
                        directorySearcher.SearchScope = SearchScope.OneLevel;
                        directorySearcher.PropertiesToLoad.Add("name");
                        directorySearcher.PropertiesToLoad.Add("path");

                        //' Enumerate top level containers
                        foreach (SearchResult container in directorySearcher.FindAll())
                        {
                            //' Search for children
                            SearchResult childResult = null;
                            using (DirectorySearcher childDirectorySearcher = new DirectorySearcher(new DirectoryEntry(container.Path)))
                            {
                                //' Define filter options for searcher
                                childDirectorySearcher.Filter = "(objectCategory=organizationalUnit)";
                                childDirectorySearcher.SearchScope = SearchScope.OneLevel;
                                childDirectorySearcher.PropertiesToLoad.Add("name");
                                childResult = childDirectorySearcher.FindOne();
                            }

                            //' Determine if child exist
                            bool childPresence = false;
                            if (childResult != null)
                            {
                                childPresence = true;
                            }

                            //' Construct a new object to hold container information
                            ADOrganizationalUnit orgUnit = new ADOrganizationalUnit()
                            {
                                HasChildren = childPresence,
                                Name = container.Properties["name"][0].ToString(),
                                DistinguishedName = container.Path
                            };
                            containers.Add(orgUnit);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("Could not enumerate OUs. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }
            }
            MethodEnd(method);
            return containers;
        }
        
        [WebMethod(Description = "Write event to web service log")]
        public bool NewCWEventLogEntry(string secret, string value)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Log user attempted to invoke deployment
                WriteEventLog(String.Format("{0}", value), EventLogEntryType.Information);
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get MDT roles from database (Application Pool identity needs access permissions to the specified MDT database)")]
        public List<string> GetMDTRoles(string secret)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct list object
            List<string> roleList = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

                try
                {
                    //' Connect to SQL server instance
                    SqlConnection connection = new SqlConnection
                    {
                        ConnectionString = connectionString.ConnectionString
                    };
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

            MethodEnd(method);
            return roleList;
        }

        [WebMethod(Description = "Get computer by asset tag from MDT database")]
        public string GetMDTComputerByAssetTag(string secret, string assetTag)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "AssetTag", assetTag);
                if (!String.IsNullOrEmpty(identity))
                {
                    returnValue = identity;
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Check if a computer with a specific MAC address exists in MDT database")]
        public string GetMDTComputerByMacAddress(string secret, string macAddress)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "MacAddress", macAddress);
                if (!String.IsNullOrEmpty(identity))
                {
                    returnValue = identity;
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get computer by serial number from MDT database")]
        public string GetMDTComputerBySerialNumber(string secret, string serialNumber)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "SerialNumber", serialNumber);
                if (!String.IsNullOrEmpty(identity))
                {
                    returnValue = identity;
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Check if a computer with a specific UUID exists in MDT database")]
        public string GetMDTComputerByUUID(string secret, string uuid)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

                //' Get computer identity
                string identity = GetMDTComputerIdentity(connectionString, "UUID", uuid);
                if (!String.IsNullOrEmpty(identity))
                {
                    returnValue = identity;
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get MDT roles with detailed information for a specific computer")]
        public List<MDTRole> GetMDTDetailedComputerRoleMembership(string secret, string identity)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct List to hold all roles
            List<MDTRole> roleList = new List<MDTRole>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get connection string
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to get role memberships. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }

                    //' Cleanup and disconnect SQL connection
                    command.Dispose();
                    connection.Close();
                }
                catch (SqlException ex)
                {
                    WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return roleList;
        }

        [WebMethod(Description = "Get a list of MDT roles for a specific computer")]
        public List<string> GetMDTComputerRoleMembership(string secret, string id)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Construct List to hold all roles
            List<string> roleList = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get connection string
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(String.Format("An error occured when attempting to get role memberships. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }

                    //' Cleanup and disconnect SQL connection
                    command.Dispose();
                    connection.Close();
                }
                catch (SqlException ex)
                {
                    WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return roleList;
        }

        [WebMethod(Description = "Get MDT computer name by computer identity")]
        public string GetMDTComputerNameByIdentity(string secret, string identity)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            string returnValue = string.Empty;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    string computerIdentity = GetMDTComputerName(identity);
                    if (!String.IsNullOrEmpty(computerIdentity))
                    {
                        returnValue = computerIdentity;
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying MDT computer identity. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add computer identified by an asset tag to a specific MDT role")]
        public bool AddMDTRoleMemberByAssetTag(string secret, string roleName, string computerName, string assetTag, bool createComputer, string identity = null)
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

                try
                {
                    string description = computerName + " - ConfigMgr OSD FrontEnd " + DateTime.Now.ToString("yyyy-MM-dd");

                    Dictionary<string, string> dictionary = new Dictionary<string, string>
                    {
                        { "AssetTag", assetTag },
                        { "Description", description }
                    };

                    if (createComputer == true)
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName);
                    }
                    else
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding MDT role member. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add computer identified by a serial number to a specific MDT role")]
        public bool AddMDTRoleMemberBySerialNumber(string secret, string roleName, string computerName, string serialNumber, bool createComputer, string identity = null)
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

                try
                {
                    string description = computerName + " - ConfigMgr OSD FrontEnd " + DateTime.Now.ToString("yyyy-MM-dd");

                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    dictionary.Add("SerialNumber", serialNumber);
                    dictionary.Add("Description", description);

                    if (createComputer == true)
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName);
                    }
                    else
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding MDT role member. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add computer identified by a MAC address to a specific MDT role")]
        public bool AddMDTRoleMemberByMacAddress(string secret, string roleName, string computerName, string macAddress, bool createComputer, string identity = null)
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

                try
                {
                    string description = computerName + " - ConfigMgr OSD FrontEnd " + DateTime.Now.ToString("yyyy-MM-dd");

                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    dictionary.Add("MacAddress", macAddress);
                    dictionary.Add("Description", description);

                    if (createComputer == true)
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName);
                    }
                    else
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding MDT role member. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add computer identified by an UUID to a specific MDT role")]
        public bool AddMDTRoleMemberByUUID(string secret, string roleName, string computerName, string uuid, bool createComputer, string identity = null)
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

                try
                {
                    string description = computerName + " - ConfigMgr OSD FrontEnd " + DateTime.Now.ToString("yyyy-MM-dd");

                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    dictionary.Add("UUID", uuid);
                    dictionary.Add("Description", description);

                    if (createComputer == true)
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName);
                    }
                    else
                    {
                        returnValue = BeginMDTRoleMember(dictionary, computerName, roleName, false, identity);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding MDT role member. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Add computer to a given MDT role (supports multiple indentification types)")]
        public bool AddMDTRoleMember(string secret, string computerName, string role, string assetTag = null, string serialNumber = null, string macAddress = null, string uuid = null, string description = null)
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

                try
                {
                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    dictionary.Add("AssetTag", assetTag);
                    dictionary.Add("SerialNumber", serialNumber);
                    dictionary.Add("MacAddress", macAddress);
                    dictionary.Add("UUID", uuid);
                    dictionary.Add("Description", description);

                    returnValue = BeginMDTRoleMember(dictionary, computerName, role);
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding MDT role member. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove MDT computer from all associated roles")]
        public bool RemoveMDTComputerFromRoles(string secret, string identity)
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

                try
                {
                    returnValue = RemoveMDTComputerRoles(identity);
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while removing MDT computer identity from roles. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove MDT computer identity by MacAddress")]
        public bool RemoveMDTComputerByMacAddress(string secret, string macAddress)
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

                try
                {
                    //' Get computer identity from in param value
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);
                    string identity = GetMDTComputerIdentity(connectionString, "MacAddress", macAddress);

                    if (!String.IsNullOrEmpty(identity))
                    {
                        returnValue = RemoveMDTComputer(connectionString, identity);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while removing MDT computer identity. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Remove MDT computer identity by serial number")]
        public bool RemoveMDTComputerBySerialNumber(string secret, string serialNumber)
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

                try
                {
                    //' Get computer identity from in param value
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);
                    string identity = GetMDTComputerIdentity(connectionString, "SerialNumber", serialNumber);

                    if (!String.IsNullOrEmpty(identity))
                    {
                        returnValue = RemoveMDTComputer(connectionString, identity);
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while removing MDT computer identity. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        [WebMethod(Description = "Get a MDT computer identity by computer name")]
        public List<MDTComputer> GetMDTComputerByName(string secret, string computerName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<MDTComputer> computerList = new List<MDTComputer>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get computer identity from in param value
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);
                    computerList = GetMDTComputerByName(connectionString, computerName);
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while retrieving MDT computer identity. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return computerList;
        }

        [WebMethod(Description = "Get an OSD monitoring object for a specific computer by computer name")]
        public List<CMOSDMonitor> GetCMOSDMonitorDataByComputer(string secret, string computerName)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            List<CMOSDMonitor> monitorList = new List<CMOSDMonitor>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Construct the connection string for SQL
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(monitorDatabase);

                    //' Retrieve monitoring objects by computer name
                    monitorList = GetCMOSDMonitorOjectByComputer(connectionString, computerName);
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for ConfigMgr OSD Monitor data. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return monitorList;
        }

        [WebMethod(Description = "Get an OSD monitoring object for a specific computer by unique id")]
        public CMOSDMonitor GetCMOSDMonitorDataByUniqueID(string secret, string uniqueId)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            CMOSDMonitor monitorObject = new CMOSDMonitor();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Construct the connection string for SQL
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(monitorDatabase);

                    //' Retrieve monitoring objects by unique id
                    monitorObject = GetCMOSDMonitorObject(connectionString, uniqueId);
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while querying for ConfigMgr OSD Monitor data. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return monitorObject;
        }

        [WebMethod(Description = "Add a new monitoring object or update an existing one for a specific computer")]
        public CMOSDMonitor AddCMOSDMonitorData(string secret, string uniqueId, string computerName, string uuid, string macAddress, int severity, string modifiedTime, string deploymentId = null, string stepName = null, string currentStep = null, string totalSteps = null, string startTime = null, string endTime = null, string details = null, string dartIp = null, string dartPort = null, string dartTicket = null)
        {
            MethodBase method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            CMOSDMonitor returnValue = null;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Instatiate converted input parameter objects
                    int currentStepInt = 0;
                    int totalStepsInt = 0;

                    //' Handle integer parameter input conversion
                    if (currentStep != null && !String.IsNullOrEmpty(currentStep))
                    {
                        currentStepInt = Convert.ToInt32(currentStep);
                    }
                    if (totalSteps != null && !String.IsNullOrEmpty(totalSteps))
                    {
                        totalStepsInt = Convert.ToInt32(totalSteps);
                    }

                    //' Handle string datetime parameter input conversion
                    object cModifiedTime = null;
                    object cStartTime = null;
                    object cEndTime = null;
                    if (modifiedTime != null)
                    {
                        if (!String.IsNullOrEmpty(modifiedTime))
                        {
                            cModifiedTime = DateTime.ParseExact(modifiedTime, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        }
                    }
                    if (startTime != null)
                    {
                        if (!String.IsNullOrEmpty(startTime))
                        {
                            cStartTime = DateTime.ParseExact(startTime, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        }
                    }
                    if (endTime != null)
                    {
                        if (!String.IsNullOrEmpty(endTime))
                        {
                            cEndTime = DateTime.ParseExact(endTime, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        }
                    }

                    //' Construct dictionary of parameter input
                    Dictionary<string, object> inputParams = new Dictionary<string, object>
                    {
                        { "UniqueID", uniqueId },
                        { "ComputerName", computerName },
                        { "SMBIOSGUID", uuid },
                        { "MacAddress", macAddress },
                        { "Severity", severity },
                        { "ModifiedTime", cModifiedTime },
                        { "DeploymentID", deploymentId },
                        { "StepName", stepName },
                        { "CurrentStep", currentStepInt },
                        { "TotalSteps", totalStepsInt },
                        { "StartTime", cStartTime },
                        { "EndTime", cEndTime },
                        { "Details", details },
                        { "DartIP", dartIp },
                        { "DartPort", dartPort },
                        { "DartTicket", dartTicket }
                    };

                    //' Construct the connection string for SQL
                    SqlConnectionStringBuilder connectionString = GetSqlConnectionString(monitorDatabase);

                    //' Check for exisitng database row matching unique id, else add a row entry
                    CMOSDMonitor monitorObject = GetCMOSDMonitorObject(connectionString, uniqueId);
                    if (monitorObject != null)
                    {
                        //' Update object
                        bool updateResult = UpdateCMOSDMonitorObject(connectionString, inputParams, uniqueId);
                        if (updateResult == true)
                        {
                            returnValue = GetCMOSDMonitorObject(connectionString, uniqueId);
                        }
                        else
                        {
                            returnValue = monitorObject;
                        }
                    }
                    else
                    {
                        bool insertResult = AddCMOSDMonitorObject(connectionString, inputParams, uniqueId);
                        if (insertResult == true)
                        {
                            returnValue = GetCMOSDMonitorObject(connectionString, uniqueId);
                        }
                        else
                        {
                            returnValue = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while adding ConfigMgr OSD Monitor entry to database. Error message: { ex.Message } ", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }

        private bool AddCMOSDMonitorObject(SqlConnectionStringBuilder connectionString, Dictionary<string, object> inputProps, string uniqueId)
        {
            //' Construct variable for return value
            bool result = false;

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection
            {
                ConnectionString = connectionString.ConnectionString
            };
            connection.Open();

            //' Build start of SQL command for INSERT command
            SqlCommand cmdMonitor = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append("INSERT INTO MonitoringData (");

            //' Determine count for non-null values
            Dictionary<string, object> inputParams = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> prop in inputProps)
            {
                if (prop.Value != null)
                {
                    if (prop.Value.GetType() == typeof(string))
                    {
                        string modProp = prop.Value.ToString();
                        if (!String.IsNullOrEmpty(modProp))
                        {
                            inputParams.Add(prop.Key, prop.Value);
                        }
                    }
                    else if (prop.Value.GetType() == typeof(int))
                    {
                        if ((int)prop.Value >= 1)
                        {
                            inputParams.Add(prop.Key, prop.Value);
                        }
                    }
                    else if (prop.Value.GetType() == typeof(DateTime))
                    {
                        inputParams.Add(prop.Key, prop.Value);
                    }
                }
            }

            //' Append command with columns
            int currCount = 1;
            int paramCount = inputParams.Count;
            foreach (KeyValuePair<string, object> prop in inputParams)
            {
                if (prop.Value != null)
                {
                    if (currCount < paramCount)
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
            foreach (KeyValuePair<string, object> prop in inputParams)
            {
                if (prop.Value != null)
                {
                    if (currCount < paramCount)
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
            sqlString.Append(") SELECT @UniqueID");

            //' Add SQL command parameters
            foreach (KeyValuePair<string, object> prop in inputParams)
            {
                if (prop.Value != null)
                {
                    cmdMonitor.Parameters.Add(String.Format("@{0}", prop.Key), SqlDbType.NVarChar).Value = prop.Value;
                }
                else
                {
                    cmdMonitor.Parameters.Add(String.Format("@{0}", prop.Key), SqlDbType.NVarChar).Value = string.Empty;
                }
            }

            //' Add SQL command text string
            cmdMonitor.CommandText = sqlString.ToString();

            //' Invoke SQL command to insert monitoring object
            try
            {
                object resultMonitor = cmdMonitor.ExecuteScalar();
                if (resultMonitor != null)
                {
                    result = true;
                }

                //' Cleanup and disconnect SQL connection
                cmdMonitor.Dispose();
                connection.Close();

                return result;
            }
            catch (Exception ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to insert OSD monitoring data. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return result;
            }
        }

        private bool UpdateCMOSDMonitorObject(SqlConnectionStringBuilder connectionString, Dictionary<string, object> inputProps, string uniqueId)
        {
            //' Construct variable for return value
            bool result = false;

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = connectionString.ConnectionString;
            connection.Open();

            //' Build start of SQL command for INSERT command
            SqlCommand cmdMonitor = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append("UPDATE MonitoringData SET ");

            //' Remove unique identifier from params
            inputProps.Remove("UniqueID");

            //' Determine count for non-null values
            Dictionary<string, object> inputParams = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> prop in inputProps)
            {
                if (prop.Value != null)
                {
                    if (prop.Value.GetType() == typeof(string))
                    {
                        string modProp = prop.Value.ToString();
                        if (!String.IsNullOrEmpty(modProp))
                        {
                            inputParams.Add(prop.Key, prop.Value);
                        }
                    }
                    else if (prop.Value.GetType() == typeof(int))
                    {
                        if ((int)prop.Value >= 1)
                        {
                            inputParams.Add(prop.Key, prop.Value);
                        }
                    }
                    else if (prop.Value.GetType() == typeof(DateTime))
                    {
                        inputParams.Add(prop.Key, prop.Value);
                    }
                }
            }

            //' Append command with columns
            int currCount = 1;
            int paramCount = inputParams.Count;
            foreach (KeyValuePair<string, object> prop in inputParams)
            {
                if (prop.Value != null)
                {
                    if (currCount < paramCount)
                    {
                        sqlString.Append(String.Format("{0} = '{1}', ", prop.Key, prop.Value));
                    }
                    else
                    {
                        sqlString.Append(String.Format("{0} = '{1}'", prop.Key, prop.Value));
                    }
                    currCount++;
                }
            }

            //' Append command
            sqlString.Append(String.Format(" WHERE UniqueID = '{0}' SELECT @UniqueID", uniqueId));

            //' Add SQL command parameters
            cmdMonitor.Parameters.Add(String.Format("@{0}", "UniqueID"), SqlDbType.NVarChar).Value = uniqueId;

            //' Add SQL command text string
            cmdMonitor.CommandText = sqlString.ToString();

            //' Invoke SQL command to update monitoring object
            try
            {
                object resultMonitor = cmdMonitor.ExecuteScalar();
                if (resultMonitor != null)
                {
                    result = true;
                }

                //' Cleanup and disconnect SQL connection
                cmdMonitor.Dispose();
                connection.Close();

                return result;
            }
            catch (Exception ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to update OSD monitoring data. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return result;
            }
        }

        private List<CMOSDMonitor> GetCMOSDMonitorOjectByComputer(SqlConnectionStringBuilder connectionString, string computerName)
        {
            //' Construct variable for return value
            List<CMOSDMonitor> monitorList = new List<CMOSDMonitor>();

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = connectionString.ConnectionString;
            connection.Open();

            //' Construct SQL statement
            SqlCommand command = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append(String.Format("SELECT * FROM MonitoringData WHERE ComputerName like @ComputerName"));

            //' Add parameters to command
            command.Parameters.Add("@ComputerName", SqlDbType.NVarChar).Value = computerName;
            command.CommandText = sqlString.ToString();

            //' Invoke SQL command to retrieve monitoring objects
            try
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows == true)
                    {
                        while (reader.Read())
                        {
                            //' Construct new monitoring object
                            CMOSDMonitor monitorObject = new CMOSDMonitor();

                            //' Read string object from database
                            monitorObject.UniqueID = reader["UniqueID"].ToString();
                            monitorObject.ComputerName = reader["ComputerName"].ToString();
                            monitorObject.SMBIOSGUID = reader["SMBIOSGUID"].ToString();
                            monitorObject.MacAddress = reader["MacAddress"].ToString();
                            monitorObject.DeploymentID = reader["DeploymentID"].ToString();
                            monitorObject.StepName = reader["StepName"].ToString();
                            monitorObject.Details = reader["Details"].ToString();
                            monitorObject.DartIP = reader["DartIP"].ToString();
                            monitorObject.DartPort = reader["DartPort"].ToString();
                            monitorObject.DartTicket = reader["DartTicket"].ToString();

                            //' Read DateTime objects from database
                            int modColIndex = reader.GetOrdinal("ModifiedTime");
                            if (!reader.IsDBNull(modColIndex))
                            {
                                monitorObject.ModifiedTime = reader.GetDateTime(modColIndex);
                            }
                            int startColIndex = reader.GetOrdinal("StartTime");
                            if (!reader.IsDBNull(startColIndex))
                            {
                                monitorObject.StartTime = reader.GetDateTime(startColIndex);
                            }
                            int endColIndex = reader.GetOrdinal("EndTime");
                            if (!reader.IsDBNull(endColIndex))
                            {
                                monitorObject.EndTime = reader.GetDateTime(endColIndex);
                            }

                            //' Read integers from database
                            monitorObject.Severity = reader.GetDBInt("Severity");
                            monitorObject.CurrentStep = reader.GetDBInt("CurrentStep");
                            monitorObject.TotalSteps = reader.GetDBInt("TotalSteps");

                            //' Update list of monitoring objects
                            monitorList.Add(monitorObject);
                        }
                    }
                }

                //' Cleanup and disconnect SQL connection
                command.Dispose();
                connection.Close();
            }
            catch (Exception ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to get OSD monitoring data objects. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return null;
            }

            return monitorList;

        }

        private CMOSDMonitor GetCMOSDMonitorObject(SqlConnectionStringBuilder connectionString, string uniqueId)
        {
            //' Construct variable for return value
            CMOSDMonitor monitorObject = null;

            //' Connect to SQL server instance
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = connectionString.ConnectionString;
            connection.Open();

            //' Construct SQL statement
            SqlCommand command = connection.CreateCommand();
            StringBuilder sqlString = new StringBuilder();
            sqlString.Append(String.Format("SELECT * FROM MonitoringData WHERE UniqueID like @ID"));

            //' Add parameters to command
            command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = uniqueId;
            command.CommandText = sqlString.ToString();

            //' Invoke SQL command retrieve monitoring object
            try
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows == true)
                    {
                        monitorObject = new CMOSDMonitor();
                        while (reader.Read())
                        {
                            //' Read string object from database
                            monitorObject.UniqueID = reader["UniqueID"].ToString();
                            monitorObject.ComputerName = reader["ComputerName"].ToString();
                            monitorObject.SMBIOSGUID = reader["SMBIOSGUID"].ToString();
                            monitorObject.MacAddress = reader["MacAddress"].ToString();
                            monitorObject.DeploymentID = reader["DeploymentID"].ToString();
                            monitorObject.StepName = reader["StepName"].ToString();
                            monitorObject.Details = reader["Details"].ToString();
                            monitorObject.DartIP = reader["DartIP"].ToString();
                            monitorObject.DartPort = reader["DartPort"].ToString();
                            monitorObject.DartTicket = reader["DartTicket"].ToString();

                            //' Read DateTime objects from database
                            int modColIndex = reader.GetOrdinal("ModifiedTime");
                            if (!reader.IsDBNull(modColIndex))
                            {
                                monitorObject.ModifiedTime = reader.GetDateTime(modColIndex);
                            }
                            int startColIndex = reader.GetOrdinal("StartTime");
                            if (!reader.IsDBNull(startColIndex))
                            {
                                monitorObject.StartTime = reader.GetDateTime(startColIndex);
                            }
                            int endColIndex = reader.GetOrdinal("EndTime");
                            if (!reader.IsDBNull(endColIndex))
                            {
                                monitorObject.EndTime = reader.GetDateTime(endColIndex);
                            }

                            //' Read integers from database
                            monitorObject.Severity = reader.GetDBInt("Severity");
                            monitorObject.CurrentStep = reader.GetDBInt("CurrentStep");
                            monitorObject.TotalSteps = reader.GetDBInt("TotalSteps");
                        }
                    }
                }

                //' Cleanup and disconnect SQL connection
                command.Dispose();
                connection.Close();
            }
            catch (Exception ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to get OSD monitoring data object. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return null;
            }

            return monitorObject;
        }

        private bool RemoveMDTComputer(SqlConnectionStringBuilder connectionString, string identity)
        {
            //' Construct variable for return value
            bool returnValue = false;

            try
            {
                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append(String.Format("DELETE FROM ComputerIdentity WHERE ID like @ID"));

                command.Parameters.Add("@ID", SqlDbType.NVarChar).Value = identity;
                command.CommandText = sqlString.ToString();

                //' Invoke SQL command to remove computer identity
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
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to computer identity from MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return false;
                }

                //' Cleanup and disconnect SQL connection
                command.Dispose();
                connection.Close();
            }
            catch (SqlException ex)
            {
                WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
            }

            return returnValue;
        }

        private bool BeginMDTRoleMember(Dictionary<string, string> dictionary, string computerName, string roleName, bool createComputer = true, string identity = null)
        {
            //' Variable for return value
            bool returnValue = false;

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
                    }
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
            }

            return returnValue;
        }

        private string AddMDTComputerIdentity(Dictionary<string, string> dictionary)
        {
            //' Get connection string
            SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
            catch (Exception ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to create computer identity. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return string.Empty;
            }
        }

        private bool AddMDTComputerSetting(string identity, string computerName)
        {
            //' Get connection string
            SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
            catch (Exception ex)
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
            SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
            catch (Exception ex)
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
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
                catch (Exception ex)
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

        private List<MDTComputer> GetMDTComputerByName(SqlConnectionStringBuilder connectionString, string computerName)
        {
            List<MDTComputer> computerList = new List<MDTComputer>();

            try
            {
                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append("SELECT Settings.ID, Settings.OSDComputerName, ComputerIdentity.AssetTag, ComputerIdentity.MacAddress, ComputerIdentity.SerialNumber, ComputerIdentity.UUID FROM Settings JOIN ComputerIdentity ON Settings.ID = ComputerIdentity.ID WHERE OSDComputerName like @ComputerName");

                //' Add parameters with value to command
                command.Parameters.Add("@ComputerName", SqlDbType.NVarChar).Value = computerName;
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
                            MDTComputer computerItem = new MDTComputer()
                            {
                                ComputerName = reader["OSDComputerName"].ToString(),
                                ComputerIdentity = reader["ID"].ToString(),
                                AssetTag = reader["AssetTag"].ToString(),
                                SerialNumber = reader["SerialNumber"].ToString(),
                                UUID = reader["UUID"].ToString(),
                                MacAddress = reader["MacAddress"].ToString()
                            };
                            computerList.Add(computerItem);
                        }
                    }

                    //' Cleanup and disconnect SQL connection
                    command.Dispose();
                    connection.Close();

                    return computerList;
                }
                catch (Exception ex)
                {
                    WriteEventLog(String.Format("An error occured when attempting to get MDT computer objects. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    return null;
                }
            }
            catch (SqlException ex)
            {
                WriteEventLog(String.Format("An error occured while connecting to SQL server hosting MDT database. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return null;
            }
        }

        private string GetMDTComputerName(string identity)
        {
            string computerIdentity = string.Empty;

            try
            {
                //' Get connection string
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

                //' Connect to SQL server instance
                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = connectionString.ConnectionString;
                connection.Open();

                //' Construct SQL statement
                SqlCommand command = connection.CreateCommand();
                StringBuilder sqlString = new StringBuilder();
                sqlString.Append(String.Format("SELECT ID, OSDComputerName FROM Settings WHERE ID like @ID AND OSDComputerName IS NOT NULL"));

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
                catch (Exception ex)
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
                SqlConnectionStringBuilder connectionString = GetSqlConnectionString(mdtDatabase);

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
                catch (Exception ex)
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

        private CMResource GetCMResource(string identification, CMObjectType resourceType, CMResourceProperty resourceProperty)
        {
            CMResource resource = new CMResource();

            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            //' Get resource instance
            string query = string.Empty;
            switch (resourceType)
            {
                case CMObjectType.System:
                    query = String.Format("SELECT * FROM SMS_R_System WHERE {0} like '{1}'", resourceProperty, identification);
                    break;
                case CMObjectType.User:
                    query = String.Format("SELECT * FROM SMS_R_User WHERE {0} like '{1}'", resourceProperty, identification);
                    break;
            }

            IResultObject instances = connection.QueryProcessor.ExecuteQuery(query);

            if (instances != null)
            {
                foreach (IResultObject res in instances)
                {
                    resource.Name = res["Name"].StringValue;
                    resource.ResourceID = res["ResourceID"].IntegerValue;
                }
            }

            return resource;
        }

        private IResultObject GetCMCollection(string collectionId, CMCollectionType collectionType)
        {
            IResultObject collection = null;

            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            //' Get collection instance
            string query = String.Format("SELECT * FROM SMS_Collection WHERE CollectionID like '{0}' AND CollectionType like '{1:D}'", collectionId, collectionType);
            IResultObject instances = connection.QueryProcessor.ExecuteQuery(query);

            if (instances != null)
            {
                foreach (IResultObject coll in instances)
                {
                    collection = coll;
                }
            }

            return collection;
        }

        private List<string> GetOSImageProperty(string property, string tsPackageId)
        {
            List<string> osPropertyList = new List<string>();

            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            //' Get all task sequence references for specific task sequence
            string query = String.Format("SELECT * FROM SMS_TaskSequencePackageReference WHERE PackageID like '{0}'", tsPackageId);
            IResultObject tsReferences = connection.QueryProcessor.ExecuteQuery(query);

            if (tsReferences != null)
            {
                List<string> osImageIds = new List<string>();
                UInt32 osImageType = 257;
                UInt32 osImageInstallType = 259;

                //' Process all task sequence references to determine the OS image package ID
                foreach (IResultObject reference in tsReferences)
                {
                    if (reference["ObjectType"].IntegerValue == osImageType || reference["ObjectType"].IntegerValue == osImageInstallType)
                    {
                        osImageIds.Add(reference["ObjectID"].StringValue);
                    }
                }

                //' Get image information for detected OS image
                if (osImageIds != null)
                {
                    foreach (string osImageId in osImageIds)
                    {
                        //' Get the ImageIndex property from task sequence step
                        IResultObject tsPackage = connection.GetInstance(String.Format("SMS_TaskSequencePackage.PackageID='{0}'", tsPackageId));
                        string imageIndex = GetTSIndexSelection(tsPackage, osImageId);

                        string imageQuery = String.Format("SELECT * FROM SMS_ImageInformation WHERE PackageID like '{0}' AND Index like '{1}'", osImageId, imageIndex);
                        IResultObject osImageProps = connection.QueryProcessor.ExecuteQuery(imageQuery);

                        if (osImageProps != null)
                        {
                            foreach (IResultObject prop in osImageProps)
                            {
                                osPropertyList.Add(prop[property].StringValue);
                            }
                        }
                    }
                }
            }

            return osPropertyList;
        }

        private List<CMOSImage> GetOSImage(string tsPackageId)
        {
            List<CMOSImage> osImageList = new List<CMOSImage>();

            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            //' Get all task sequence references for specific task sequence
            string query = String.Format("SELECT * FROM SMS_TaskSequencePackageReference WHERE PackageID like '{0}'", tsPackageId);
            IResultObject tsReferences = connection.QueryProcessor.ExecuteQuery(query);

            if (tsReferences != null)
            {
                List<string> osImageIds = new List<string>();
                UInt32 osImageType = 257;
                UInt32 osImageInstallType = 259;

                //' Process all task sequence references to determine the OS image package ID
                foreach (IResultObject reference in tsReferences)
                {
                    if (reference["ObjectType"].IntegerValue == osImageType || reference["ObjectType"].IntegerValue == osImageInstallType)
                    {
                        osImageIds.Add(reference["ObjectID"].StringValue);
                    }
                }

                //' Get image information for detected OS image
                if (osImageIds != null)
                {
                    foreach (string osImageId in osImageIds)
                    {
                        //' Get the ImageIndex property from task sequence step
                        IResultObject tsPackage = connection.GetInstance(String.Format("SMS_TaskSequencePackage.PackageID='{0}'", tsPackageId));
                        string imageIndex = GetTSIndexSelection(tsPackage, osImageId);

                        string imageQuery = String.Format("SELECT * FROM SMS_ImageInformation WHERE PackageID like '{0}' AND Index like '{1}'", osImageId, imageIndex);
                        IResultObject osImageProps = connection.QueryProcessor.ExecuteQuery(imageQuery);

                        if (osImageProps != null)
                        {
                            foreach (IResultObject prop in osImageProps)
                            {
                                CMOSImage osImage = new CMOSImage()
                                {
                                    Version = prop["OSVersion"].StringValue,
                                    Architecture = prop["Architecture"].StringValue,
                                    PackageID = prop["PackageID"].StringValue
                                };
                                osImageList.Add(osImage);
                            }
                        }
                    }
                }
            }

            return osImageList;
        }

        private string GetTSIndexSelection(IResultObject taskSequencePackage, string imageId)
        {
            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
            WqlConnectionManager connection = smsProvider.Connect(siteServer);

            IResultObject taskSequence = null;

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskSequencePackage", taskSequencePackage);

            var outParams = connection.ExecuteMethod("SMS_TaskSequencePackage", "GetSequence", parameters);
            taskSequence = outParams.GetSingleItem("TaskSequence");

            string imageIndex = GetTSSteps(taskSequence, imageId);

            return imageIndex;

        }

        private string GetTSSteps(IResultObject taskSequence, string imageId)
        {
            List<IResultObject> tsSteps = taskSequence.GetArrayItems("Steps");

            foreach (IResultObject step in tsSteps)
            {
                if (step["__CLASS"].StringValue == "SMS_TaskSequence_ApplyOperatingSystemAction")
                {
                    if (step["ImagePackageID"].StringValue == imageId)
                    {
                        int imageIndex = step["ImageIndex"].IntegerValue;
                        return imageIndex.ToString();
                    }
                }

                if (step["__CLASS"].StringValue == "SMS_TaskSequence_UpgradeOperatingSystemAction")
                {
                    if (step["InstallPackageID"].StringValue == imageId)
                    {
                        int imageIndex = step["InstallEditionIndex"].IntegerValue;
                        return imageIndex.ToString();
                    }
                }

                if (step["__CLASS"].StringValue == "SMS_TaskSequence_Group")
                {
                    string result = GetTSSteps(step, imageId);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private string ImportCMComputer(Dictionary<string, object> methodParameters)
        {
            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
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

        private string GetCMComputerResourceId(string computerName)
        {
            //' Connect to SMS Provider
            SmsProvider smsProvider = new SmsProvider();
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

        private SqlConnectionStringBuilder GetSqlConnectionString(string databaseName)
        {
            //' Set database connection string
            SqlConnectionStringBuilder connectionString = new SqlConnectionStringBuilder();
            if (sqlInstance == null || sqlInstance == string.Empty)
            {
                connectionString.DataSource = sqlServer;
                connectionString.InitialCatalog = databaseName;
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

        private string GetADDefaultNamingContext()
        {
            string defaultNamingContext;
            using (DirectoryEntry rootDSE = new DirectoryEntry("LDAP://RootDSE"))
            {
                defaultNamingContext = rootDSE.Properties["defaultNamingContext"].Value.ToString();
            }

            return defaultNamingContext;
        }

        private string GetADDomainName()
        {
            return Domain.GetComputerDomain().Name;
        }

        private string GetADObject(string name, ADObjectClass objectClass, ADObjectType objectType)
        {
            //' Set empty value for return object and search result
            string returnValue = string.Empty;
            SearchResult searchResult = null;

            //' Get default naming context of current domain
            string defaultNamingContext = GetADDefaultNamingContext();
            string currentDomain = String.Format("GC://{0}", defaultNamingContext);

            //' Construct directory entry for directory searcher
            DirectoryEntry domain = new DirectoryEntry(currentDomain);
            DirectorySearcher directorySearcher = new DirectorySearcher(domain);
            directorySearcher.PropertiesToLoad.Add("distinguishedName");

            switch (objectClass)
            {
                case ADObjectClass.DomainController:
                    directorySearcher.Filter = String.Format("(&(objectClass=computer)((dNSHostName={0})))", name);
                    break;
                case ADObjectClass.Computer:
                    directorySearcher.Filter = String.Format("(&(objectClass=computer)((sAMAccountName={0}$)))", name);
                    break;
                case ADObjectClass.Group:
                    directorySearcher.Filter = String.Format("(&(objectClass=group)((sAMAccountName={0})))", name);
                    break;
                case ADObjectClass.User:
                    directorySearcher.Filter = String.Format("(&(objectClass=user)((sAMAccountName={0})))", name);
                    break;
            }

            //' Invoke directory searcher
            try
            {
                searchResult = directorySearcher.FindOne();
            }
            catch (Exception ex)
            {
                WriteEventLog(String.Format("An error occured when attempting to locate Active Directory object. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return returnValue;
            }

            //' Return selected object type value
            if (searchResult != null)
            {
                DirectoryEntry directoryObject = searchResult.GetDirectoryEntry();

                if (objectType.Equals(ADObjectType.objectGuid))
                {
                    returnValue = directoryObject.Guid.ToString();
                }

                if (objectType.Equals(ADObjectType.distinguishedName))
                {
                    returnValue = String.Format("LDAP://{0}", directoryObject.Properties["distinguishedName"].Value);
                }
            }

            //' Dispose objects
            directorySearcher.Dispose();
            domain.Dispose();

            return returnValue;
        }

        /// <summary>
        ///  Code adjusted from: https://itq.nl/get-more-than-1500-members-from-an-active-directory-group/
        /// </summary>
        private List<string> GetADGroupMemberList(DirectoryEntry groupEntry)
        {
            //' Construct a list for housing all group members
            List<string> groupMembers = new List<string>();

            //' Define range variables
            const int rangeIncrement = 999;
            int rangeStart = 0;

            //' Continue if there are atleast one member
            if (groupEntry.Properties["member"].Count >= 1)
            {
                while (true)
                {
                    //' Define the end of the range
                    int rangeEnd = rangeStart + rangeIncrement - 1;

                    //' Attach a range option to the properties to load, for example: range=0-999.
                    string[] properties = new[] { String.Format("member;range={0}-{1}", rangeStart, rangeEnd) };

                    //' Perform a search using the group entry as the base
                    string filter = "(objectClass=*)";
                    using (DirectorySearcher memberSearcher = new DirectorySearcher(groupEntry, filter, properties, SearchScope.Base))
                    {
                        try
                        {
                            //' Find all members of the group
                            SearchResultCollection memberResults = memberSearcher.FindAll();
                            foreach (SearchResult memberResult in memberResults)
                            {
                                ResultPropertyCollection membersProperties = memberResult.Properties;
                                IEnumerable<string> membersPropertyNames = membersProperties.PropertyNames.OfType<string>().Where(n => n.StartsWith("member;"));
                                foreach (string propertyName in membersPropertyNames)
                                {
                                    //' Get all members from the ranged result
                                    var members = membersProperties[propertyName];
                                    foreach (string memberDn in members)
                                    {
                                        groupMembers.Add(memberDn);
                                    }
                                }
                            }
                        }
                        catch (DirectoryServicesCOMException)
                        {
                            //' When the start of the range exceeds the number of available results, an exception is thrown and we exit the loop
                            break;
                        }
                    }

                    //' Increment for the next range
                    rangeStart += rangeIncrement;
                }
            }

            return groupMembers;
        }

        /// <summary>
        ///     Check if user if member of a group including nested group - https://stackoverflow.com/questions/5312744/how-to-determine-all-the-groups-a-user-belongs-to-including-nested-groups-in-a/31725157#31725157
        /// </summary>
        private bool GetADGroupNestedMemberOf(Principal principal, GroupPrincipal group)
        {
            //' LDAP query for memberOf including nested
            string filter = String.Format("(&(sAMAccountName={0})(memberOf:1.2.840.113556.1.4.1941:={1}))", principal.SamAccountName, group.DistinguishedName);
            WriteEventLog(String.Format("Using LDAP filter for user validation: {0}", filter), EventLogEntryType.Information);

            DirectorySearcher searcher = new DirectorySearcher(filter);
            SearchResult result = searcher.FindOne();

            return result != null;
        }

        private ArrayList GetADAttributeValues(string attributeName, string distinguishedName, ArrayList valuesCollection, bool recursive)
        {
            //' Construct directory entry for object
            DirectoryEntry directoryEntry = new DirectoryEntry(distinguishedName);

            //' Add properties for value collection
            PropertyValueCollection ValueCollection = directoryEntry.Properties[attributeName];
            IEnumerator enumerator = ValueCollection.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current != null)
                {
                    if (!valuesCollection.Contains(enumerator.Current.ToString()))
                    {
                        valuesCollection.Add(enumerator.Current.ToString());
                        if (recursive)
                        {
                            GetADAttributeValues(attributeName, "LDAP://" + enumerator.Current.ToString(), valuesCollection, true);
                        }
                    }
                }
            }

            //' Dispose objects
            directoryEntry.Close();
            directoryEntry.Dispose();

            return valuesCollection;
        }

        private string GetADAttributeValue(string distinguishedName, string attributeName)
        {
            //' Construct return value variable
            string returnValue = null;

            //' Get attribute value
            DirectoryEntry directoryEntry = new DirectoryEntry(String.Format("LDAP://{0}", distinguishedName));
            returnValue = (string)directoryEntry.Properties[attributeName].Value;

            //' Dispose objects
            directoryEntry.Dispose();

            return returnValue;
        }

        public static Dictionary<string, string> GetADSubnets(string forestName)
        {
            //' Construct dictionary to hold detected subnets
            Dictionary<string, string> subnetList = new Dictionary<string, string>();

            //' Construct a read-only collection for all site objects
            ReadOnlySiteCollection siteCollection = default(ReadOnlySiteCollection);

            //' Get active directory forst context
            DirectoryContext context = new DirectoryContext(DirectoryContextType.Forest, forestName);
            Forest forest = Forest.GetForest(context);

            if (forest != null)
            {
                //' Process each site in forest to get subnets
                siteCollection = forest.Sites;
                for (int i = 0; i <= siteCollection.Count - 1; i++)
                {
                    ActiveDirectorySite currentSite = siteCollection[i];
                    foreach (ActiveDirectorySubnet subnet in currentSite.Subnets)
                    {
                        subnetList.Add(subnet.Name, subnet.Site.ToString());
                    }

                }
            }

            return subnetList;
        }

        private static int FindMissingNumber(List<int> list)
        {
            //' Missing number return value
            int firstMissingNumber = 0;

            //' Sorting the list
            list.Sort();

            //' First number of the list
            int firstNumber = list.First();

            //' Last number of the list
            int lastNumber = list.Last();

            //' Range that contains all numbers in the interval
            if (firstNumber == 1 && lastNumber == 1)
            {
                firstMissingNumber = 2;
            }
            else
            {
                IEnumerable<int> range = Enumerable.Range(firstNumber, lastNumber - firstNumber);

                if (range.Contains<int>(1) == false)
                {
                    firstMissingNumber = 1;
                }
                else
                {
                    if (range != null)
                    {
                        //' Getting the set difference
                        IEnumerable<int> setDifference = range.Except(list);
                        if (!IsNullOrEmpty<int>(setDifference))
                        {
                            firstMissingNumber = range.Except(list).First();
                        }
                        else
                        {
                            firstMissingNumber = FindNextNumber(list);
                        }
                    }
                }
            }

            return firstMissingNumber;
        }

        private static int FindNextNumber(List<int> list)
        {
            //' Sorting the list
            list.Sort();

            //' Next number of the list
            int lastNumber = list.Last<int>();
            int nextNumber = lastNumber + 1;

            return nextNumber;
        }

        public static bool IsNullOrEmpty<T>(IEnumerable<T> enumerable)
        {
            return enumerable == null || !enumerable.Any();
        }

        private static string GetUserHostAddress()
        {
            string address = HttpContext.Current.Request.UserHostAddress;

            return address;
        }

        private static void MethodBegin(MethodBase methodBase)
        {
            StartTimer();
            WriteEventLog(String.Format("Web service method {0} was triggered from {1}", methodBase.Name, GetUserHostAddress()), EventLogEntryType.Information);
        }

        private static void MethodEnd(MethodBase methodBase)
        {
            TimeSpan time = StopTimer();
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}", time.Hours, time.Minutes, time.Seconds);
            WriteEventLog(String.Format("Web service method {0} completed. Elapsed time: {1}", methodBase.Name, elapsedTime), EventLogEntryType.Information);
        }

        private static void StartTimer()
        {
            timer.Start();
        }

        private static TimeSpan StopTimer()
        {
            TimeSpan timeSpan = timer.Elapsed;
            timer.Reset();

            return timeSpan;
        }

        private string ConvertFromSecureString(SecureString secureString)
        {
            IntPtr unsecureString = IntPtr.Zero;
            try
            {
                unsecureString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(unsecureString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unsecureString);
            }
        }

        public static void WriteEventLog(string logEntry, EventLogEntryType entryType)
        {
            using (eventLog = new EventLog())
            {
                //' Check if event log exists, otherwise create new event log
                if (!EventLog.SourceExists("ConfigMgr Web Service"))
                {
                    EventLog.CreateEventSource("ConfigMgr Web Service", "ConfigMgr Web Service Activity");
                }

                //' Determine event log number
                int eventNumber = 0;
                switch (entryType)
                {
                    case EventLogEntryType.Information:
                        eventNumber = 1000;
                        break;
                    case EventLogEntryType.Warning:
                        eventNumber = 1001;
                        break;
                    case EventLogEntryType.Error:
                        eventNumber = 1002;
                        break;
                }

                //' Set event log source and write new entry
                eventLog.Source = "ConfigMgr Web Service";
                eventLog.Log = "ConfigMgr Web Service Activity";
                eventLog.WriteEntry(logEntry, entryType, eventNumber);
            }
        }
    }
}
