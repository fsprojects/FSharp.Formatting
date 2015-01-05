using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crefLib4
{
    /// <summary>
    /// <see cref="Class2" />
    /// </summary>
    public class Class1
    {
        /// <summary>
        /// None
        /// </summary>
        public string X { get; set; }
    }

    /// <summary>
    /// <see cref="crefLib1.Class1" />
    /// </summary>
    public class Class2
    {
        /// <summary>
        /// <see cref="Unknown__Reference" />
        /// </summary>
        public string Other {get;set;}
    }

    
    /// <summary>
    /// Test
    /// </summary>
    public class Class3
    {
        /// <summary>
        /// <see cref="Class2.Other" />
        /// </summary>
        public string X {get; set;}
    }

    /// <summary>
    /// Test
    /// </summary>
    public class Class4 {
        /// <summary>
        /// <see cref="System.Reflection.Assembly" />
        /// </summary>
        public string X { get; set; }
    } 
}
