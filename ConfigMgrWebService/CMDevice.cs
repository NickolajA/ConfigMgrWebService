using System.Collections.Generic;

namespace ConfigMgrWebService
{
    public class CMDevice
    {
        public string Name { get; set; }
        public List<CMDeviceVariable> Variable { get; set; }
    }

    public class CMDeviceByID
    {
        public string ResourceID { get; set; }
        public List<CMDeviceVariable> Variable { get; set; }
    }

    public class CMDeviceVariable
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}