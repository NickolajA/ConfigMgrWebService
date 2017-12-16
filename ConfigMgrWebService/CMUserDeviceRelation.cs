using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMUserDeviceRelation
    {
        public string UniqueUserName { get; set; }
        public string FullUserName { get; set; }
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public DateTime CreationTime { get; set; }
    }
}