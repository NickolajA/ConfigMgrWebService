# ConfigMgrWebService

This web service was designed to extend the functionality of Operating System Deployment with Configuration Manager 2012 (and above). Included in this web service, the following methods are made available:

- Get Primary User by Device
- Get Primary Device by User
- Get Boot Image Source Version
- Get Deployed Applications by User
- Get Deployed Applications by Device
- Get Hidden Task Sequence Deployments
- Get Discovered Users

## Supported Configurations
This web service has been built to support the following versions of System Center Configuration Manager:

- Configuration Manager 2012 SP1
- Configuration Manager 2012 SP2
- Configuration Manager 2012 R2
- Configuration Manager 2012 R2 SP1
- Configuration Manager Current Branch (all currently supported versions released by Microsoft)

Make sure that .NET Framework 4.5.2 is available on the member server you intend to host this web service on.

## Installation instructions

To successfully run this web service, you'll need to have IIS installed on a member server with ASP.NET enabled. Easiest way to get going is to install the ConfigMgrWebService on the same server as where your Management Point role is hosted. You'll also need to have a service account for the application pool in IIS. It's recommended that you add the service account in ConfigMgr with Full Administrator privileges.

### 1 - Create folder structure
1. Download the project and compile the solution in Visual Studio (you can download the free version called Visual Studio Community Edition)
2. Create a folder in <b>C:\inetpub</b> called <b>ConfigMgrWebService</b>. Inside that folder, create a folder called <b>bin</b>.
3. Copy the compiled <b>ConfigMgrWebService.dll</b> to <b>C:\inetpub\ConfigMgrWebService\bin</b>.
4. Rename Web.Release.config to <b>Web.config</b> and copy it to <b>C:\inetpub\ConfigMgrWebService</b>.
5. Copy <b>ConfigMgrWebService.asmx</b> to <b>C:\inetpub\ConfigMgrWebService</b>.
6. Locate <b>AdminUI.WqlQueryEngine.dll</b> and <b>Microsoft.ConfigurationManagement.ManagementProvider.dll</b> in the ConfigMgr console installation location and copy them to <b>C:\inetpub\ConfigMgrWebService\bin</b>.

### 2 - Add an Application Pool in IIS
1. Open IIS management console, right click on <b>Application Pools</b> and select Add Application Pool.
2. Enter <b>ConfigMgrWebService</b> as name, select the .NET CLR version <b>.NET CLR Version v4.0.30319</b> and click OK.
3. Select the new <b>ConfigMgrWebService</b> application pool and select <b>Advanced Settings</b>.
4. In the <b>Process Model</b> section, specify the service account that will have access to ConfigMgr in the <b>Identity</b> field and click OK.

### 3 - Add an Application to Default Web Site
1. Open IIS management console, expand <b>Sites</b>, right click on <b>Default Web Site</b> and select <b>Add Application</b>.
2. As for <b>Alias</b>, enter <b>ConfigMgrWebService</b>.
3. Select <b>ConfigMgrWebService</b> as application pool.
4. Set the physical path to <b>C:\inetpub\ConfigMgrWebService</b> and click OK.

### 4 - Set Application Settings
1. Open IIS management console, expand <b>Sites</b> and <b>Default Web Site</b>.
2. Select <b>ConfigMgrWebService</b> application and go to <b>Application Settings</b>.
3. Enter values for each application settings, <b>SiteServer</b> being the server where the SMS Provider is installed, <b>SiteCode</b> being the site code of your site and <b>SecretKey</b> being a custom string that you create yourself.

## Documentation

### Application Settings
When calling the web service methods, you'll need to pass along a secret key that matches what's specified for the SecretKey application setting in web.config as a parameter. Without this parameter, the method will be invoked properly. This is a somewhat reasonable security mechanism (at least it's something) that prevents unathorized users to invoke the methods and retrieve data from your ConfigMgr environment. It's recommended that you generate a GUID and enter that as the SecretKey.

### Available Methods

#### GetPrimaryUserByDevice
This method returns all primary user associated for a specified device in Configuration Manager
##### Parameters
deviceName (string), secretKey (string)

#### GetPrimaryDeviceByUser
This methods returns all primary devices associated with a specified user in Configuration Manager
##### Parameters
userName (string), secretKey (string)

#### GetDeployedApplicationsByUser
This methods returns a list of all applications deployed targeting a specified user in Configuration Manager
##### Parameters
userName (string), secretKey (string)

#### GetDeployedApplicationsByDevice
This methods returns a list of all applications deployed targeting a specified device in Configuration Manager
##### Parameters
deviceName (string), secretKey (string)

#### GetHiddenTaskSequenceDeployments
This method returns a list of all task sequences deployed (available and required) as hidden in Configuration Manager
##### Parameters
secretKey (string)

#### GetBootImageSourceVersion
This method returns the source version of a specified Boot Image from Configuration Manager
#### Parameters
packageId (string), secretKey (string)

#### GetDiscoveredUsers
This method returns all discovered users from Configuration Manager
#### Parameters
secretKey (string)

#### GetCMUniqueUserName
This method gets the UniqueUserName property for a specific user in Configuration Manager
#### Parameters
userName (string), secretKey (string) 

#### ImportCMComputerByMacAddress
This method imports a computer by the specified MAC address into Configuration Manager
#### Parameters
computerName (string), macAddress (string), secretKey (string)

#### ImportCMComputerByUUID
This method imports a computer by the specified UUID (SMBIOS GUID) into Configuration Manager
#### Parameters
computerName (string), uuid (string), secretKey (string)

#### AddCMComputerToCollection
This method adds a direct memberships rule for a specific computer to a collection in Configuration Manager
#### Parameters
resourceName (string), collectionName (string), secretKey (string)

#### GetCMDeviceCollections
This method retrieves a list (supports filtering) of device collection in Configuration Manager
#### Parameters
secretKey (string), filter (string, optional)

#### UpdateCMCollectionMembership
This method updates the membership of a collection by it's CollectionID property in Configuration Manager
#### Parameters
secretKey (string), collectionId (string)

#### GetMDTRoles
This method retrieves all roles in the MDT database
#### Parameters
server (string), database (string), instance (string), secretKey (string)

#### GetMDTComputerByAssetTag
This method retrieves a computer in the MDT database by specified asset tag
#### Parameters
secretKey (string), assetTag (string)

#### GetMDTComputerByMacAddress
This method retrieves a computer in the MDT database by specified MAC address
#### Parameters
secretKey (string), macAddress (string)

#### GetMDTComputerBySerialNumber
This method retrieves a computer in the MDT database by specified serial number
#### Parameters
secretKey (string), serialNumber (string)

#### GetMDTComputerByUUID
This method retrieves a computer in the MDT database by specified uuid (SMBIOS GUID)
#### Parameters
secretKey (string), uuid (string)

#### GetMDTComputerRoleMembership
This method retrieves a list of roles associated with a computer by it's ID in the MDT database
#### Parameters
id (string), secretKey (string)

#### AddMDTRoleMemberByAssetTag
This method adds a computer identified by an asset tag to a specific role in the MDT database. Set the parameter value for createComputer parameter to True if you also want the computer to be created, else set the value to False and include the identity parameter with the ID of the computer object to only the association is set.
#### Parameters
roleName (string), computerName (string), assetTag (string), secretKey (string), createComputer (bool), identity (string, optional)

#### AddMDTRoleMemberBySerialNumber
This method adds a computer identified by a serial number to a specific role in the MDT database. Set the parameter value for createComputer parameter to True if you also want the computer to be created, else set the value to False and include the identity parameter with the ID of the computer object to only the association is set.
#### Parameters
roleName (string), computerName (string), serialNumber (string), secretKey (string), createComputer (bool), identity (string, optional)

#### AddMDTRoleMemberByMacAddress
This method adds a computer identified by a MAC address to a specific role in the MDT database. Set the parameter value for createComputer parameter to True if you also want the computer to be created, else set the value to False and include the identity parameter with the ID of the computer object to only the association is set.
#### Parameters
roleName (string), computerName (string), macAddress (string), secretKey (string), createComputer (bool), identity (string, optional)

#### AddMDTRoleMemberByUUID
This method adds a computer identified by an UUID (SMBIOS GUID) to a specific role in the MDT database. Set the parameter value for createComputer parameter to True if you also want the computer to be created, else set the value to False and include the identity parameter with the ID of the computer object to only the association is set.
#### Parameters
roleName (string), computerName (string), uuid (string), secretKey (string), createComputer (bool), identity (string, optional)

#### AddMDTRoleMember
This method adds a computer identified by a single or multiple values, e.g. serial number and MAC address.
#### Parameters
computerName (string), role (string), secretKey (string), assetTag (string, optional), serialNumber (string, optional), macAddress (string, optional), uuid (string, optional), description (string, optional)
