using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public abstract class BaseADObject
    {
        internal abstract string[] PropertiesToLoad { get; }

        
    }
}