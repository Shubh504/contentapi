using System;
using System.Collections.Generic;

namespace contentapi
{
    public class SystemConfig
    {
        public List<long> SuperUsers {get;set;} = new List<long>();
        public bool SuperRootCategories {get;set;}
        public TimeSpan ListenTimeout {get;set;}
        public TimeSpan ListenGracePeriod {get;set;}
    }
}