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

    [DataContract(Name = "QueryRule")]
    public class CMQueryRule
    {
        [DataMember]
        public string RuleName { get; set; }

        [DataMember]
        public string RuleType { get; set; }

        [DataMember]
        public string QueryID { get; set; }

        [DataMember]
        public string QueryExpression { get; set; }

    }

    [DataContract(Name = "DirectRule")]
    public class CMDirectRule
    {
        [DataMember]
        public string RuleName { get; set; }

        [DataMember]
        public string RuleType { get; set; }

        [DataMember]
        public string ResourceClassName { get; set; }

        [DataMember]
        public string ResourceID { get; set; }
    }

    [DataContract(Name = "IncludeRule")]
    public class CMIncludeRule
    {
        [DataMember]
        public string RuleName { get; set; }

        [DataMember]
        public string RuleType { get; set; }

        [DataMember]
        public string IncludeCollectionID { get; set; }
    }

    [DataContract(Name = "ExcludeRule")]
    public class CMExcludeRule
    {
        [DataMember]
        public string RuleName { get; set; }

        [DataMember]
        public string RuleType { get; set; }

        [DataMember]
        public string ExcludeCollectionID { get; set; }
    }
}
