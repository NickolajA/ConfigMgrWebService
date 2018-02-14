using System.Collections.Generic;

namespace ConfigMgrWebService
{
	public enum CMNodeObjectType
	{
        	SMS_Package = 2,
        	SMS_Advertisement = 3,
        	SMS_Query = 7,
        	SMS_Report = 8,
        	SMS_MeteredProductRule = 9,
        	SMS_ConfigurationItem = 11,
        	SMS_OperatingSystemInstallPackage = 14,
        	SMS_StateMigration = 17,
        	SMS_ImagePackage = 18,
        	SMS_BootImagePackage = 19,
        	SMS_TaskSequencePackage = 20,
        	SMS_DeviceSettingPackage = 21,
        	SMS_DriverPackage = 23,
        	SMS_Driver = 25,
        	SMS_SoftwareUpdate = 1011,
        	SMS_ConfigurationItem_Baseline = 2011,
        	SMS_Collection_Device = 5000,
        	SMS_Collection_User = 5001,
        	SMS_ApplicationLatest = 6000
    	}

	public class CMNode
	{
		public string Name { get; set; }
    		public string ContainerNodeID { get; set; }
        	public List<string> MemberID { get; set; }
        	public List<string> MemberGuid { get; set; }
        	public List<string> InstanceKey { get; set; }
        	public string ParentContainerNodeID { get; set; }
		public string FolderGuid { get; set; }
		public string Path { get; set; }
	}

}
