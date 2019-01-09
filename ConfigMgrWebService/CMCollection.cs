using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMCollection
    {
        public string Name { get; set; }
        public string CollectionID { get; set; }
        public string CollectionType { get; set; }
        public string LimitingCollectionID { get; set; }
        public string ObjectPath { get; set; }
        public string RefreshType { get; set; }
        public string ServiceWindowsCount { get; set; }
    }
}