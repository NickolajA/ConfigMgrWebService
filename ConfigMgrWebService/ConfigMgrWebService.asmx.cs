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
    [WebService(Name = "ConfigMgr WebService", Description = "Web service for ConfigMgr 2012+ developed by Nickolaj Andersen", Namespace = "http://www.scconfigmgr.com")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]

    public class ConfigMgrWebService : System.Web.Services.WebService
    {
        //' Implement logging, normal and debug?

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
                //' Query SMS_UserMachineRelationship SMS Provider class for instances
                SelectQuery relationQuery = new SelectQuery("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceName like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, relationQuery);

                if (managementObjectSearcher != null)
                    foreach (var userRelation in managementObjectSearcher.Get())
                    {
                        string userName = (string) userRelation.GetPropertyValue("UniqueUserName");
                        relations.Add(userName);
                    }
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
                //' Query SMS_UserMachineRelationship SMS Provider class for instances
                SelectQuery relationQuery = new SelectQuery("SELECT * FROM SMS_UserMachineRelationship WHERE ResourceName like '" + userName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, relationQuery);

                if (managementObjectSearcher != null)
                    foreach (var deviceRelation in managementObjectSearcher.Get())
                    {
                        string deviceName = (string) deviceRelation.GetPropertyValue("Device"); //' <--- this property needs to be verified
                        relations.Add(deviceName);
                    }
                return relations;
            }
        }

        [WebMethod(Description = "Get deployed applications for a specific user")]
        public List<string> GetDeployedApplicationsByUser(string userName, string secret)
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
                SelectQuery userQuery = new SelectQuery("SELECT * FROM SMS_R_User WHERE UserName like '" + userName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, userQuery);

                if (managementObjectSearcher.Get() != null)
                    if (managementObjectSearcher.Get().Count == 1)
                        foreach (ManagementObject user in managementObjectSearcher.Get())
                        {
                            string userNameProperty = (string)user.GetPropertyValue("UserName");
                            var resourceIDProperty = user.GetPropertyValue("ResourceId");

                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                                foreach (ManagementObject collUser in collMembershipSearcher.Get())
                                {
                                    string collectionId = (string)collUser.GetPropertyValue("CollectionID");

                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    applicationNames.Add(targetName);
                                                }
                                        }
                                }
                        }
                return applicationNames;
            }
        }

        [WebMethod(Description = "Get edployed applications foro a specific device")]
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
                SelectQuery deviceQuery = new SelectQuery("SELECT * FROM SMS_R_System WHERE Name like '" + deviceName + "'");
                ManagementScope managementScope = new ManagementScope("\\\\" + siteServer + "\\root\\SMS\\site_" + siteCode);
                managementScope.Connect();
                ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, deviceQuery);

                if (managementObjectSearcher.Get() != null)
                    if (managementObjectSearcher.Get().Count == 1)
                        foreach (ManagementObject device in managementObjectSearcher.Get())
                        {
                            string deviceNameProperty = (string)device.GetPropertyValue("Name");
                            var resourceIDProperty = device.GetPropertyValue("ResourceId");

                            SelectQuery collMembershipQuery = new SelectQuery("SELECT * FROM SMS_FullCollectionMembership WHERE ResourceID like '" + resourceIDProperty.ToString() + "'");
                            ManagementObjectSearcher collMembershipSearcher = new ManagementObjectSearcher(managementScope, collMembershipQuery);

                            if (collMembershipSearcher.Get() != null)
                                foreach (ManagementObject collDevice in collMembershipSearcher.Get())
                                {
                                    string collectionId = (string)collDevice.GetPropertyValue("CollectionID");

                                    SelectQuery collectionQuery = new SelectQuery("SELECT * FROM SMS_Collection WHERE CollectionID like '" + collectionId + "'");
                                    ManagementObjectSearcher collectionSearcher = new ManagementObjectSearcher(managementScope, collectionQuery);

                                    if (collectionSearcher.Get() != null)
                                        foreach (ManagementObject collection in collectionSearcher.Get())
                                        {
                                            var collId = collection.GetPropertyValue("CollectionID");

                                            SelectQuery deploymentInfoQuery = new SelectQuery("SELECT * FROM SMS_DeploymentInfo WHERE CollectionID like '" + collId + "' AND DeploymentType = 31");
                                            ManagementObjectSearcher deploymentInfoSearcher = new ManagementObjectSearcher(managementScope, deploymentInfoQuery);

                                            if (deploymentInfoSearcher.Get() != null)
                                                foreach (ManagementObject deployment in deploymentInfoSearcher.Get())
                                                {
                                                    string targetName = (string)deployment.GetPropertyValue("TargetName");
                                                    //'UInt32 deploymentType = (UInt32)deployment.GetPropertyValue("DeploymentType");
                                                    //'if (deploymentType == 31)
                                                    applicationNames.Add(targetName);
                                                }
                                        }
                                }
                        }
                return applicationNames;
            }
        }

        [WebMethod(Description = "Get hidden task sequence deployments")]
        public List<taskSequence> GetHiddenTaskSequenceDeployment(string secret)
        {
            //' Construct applications list
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

                string query = "SELECT * FROM SMS_AdvertisementInfo WHERE PackageType = 4";
                IResultObject queryResults = connection.QueryProcessor.ExecuteQuery(query);

                foreach (IResultObject queryResult in queryResults)
                {
                    string taskSequenceName = queryResult["PackageName"].StringValue;
                    string advertId = queryResult["AdvertisementId"].StringValue;
                    int advertFlags = queryResult["AdvertFlags"].IntegerValue;

                    taskSequence returnObject = new taskSequence();
                    returnObject.Name = taskSequenceName;
                    returnObject.AdvertFlags = advertFlags;
                    returnObject.AdvertisementId = advertId;

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
        }
    }
}
