using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ConfigMgrWebService
{

    public class CMCollectionVariables
    {
        public string Name { get; set; }
        public string CollectionID { get; set; }
        public List<CMVariables> Variable { get; set; }
    }

    public class CMCollectionAdvanced
    {
        public string Name { get; set; }
        public string CollectionID { get; set; }
        public List<CMVariables> Variable { get; set; }
        public System.Collections.ArrayList AdhesionRules { get; set; }
    }

    public class CMVariablesSettings
    {
        public string CollectionID { get; set; }
        public List<CMVariables> Variable { get; set; }
    }

    public class CMVariables
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

}