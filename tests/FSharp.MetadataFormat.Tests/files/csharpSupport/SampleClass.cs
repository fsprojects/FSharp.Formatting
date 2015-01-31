using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace csharpSupport
{
    /// <summary>
    /// My_Static_Sample_Class
    /// </summary>
    public static class SampleStaticClass
    {
        /// <summary>
        /// My_Static_Method
        /// </summary>
        public static void StaticMethod() { }

        
        /// <summary>
        /// My_Static_Property
        /// </summary>
        public static string StaticProperty { get; set; }

        /// <summary>
        /// My_Static_Event
        /// </summary>
        public static event EventHandler StaticEvent;  
    }

    /// <summary>
    /// My_Sample_Class
    /// </summary>
    public class SampleClass
    {
        /// <summary>
        /// My_Static_Method
        /// </summary>
        public static void StaticMethod() { }


        /// <summary>
        /// My_Static_Property
        /// </summary>
        public static string StaticProperty { get; set; }

        /// <summary>
        /// My_Static_Event
        /// </summary>
        public static event EventHandler StaticEvent;

        /// <summary>
        /// My_Constructor
        /// </summary>
        public SampleClass() { }

        /// <summary>
        /// My_Method
        /// </summary>
        public void Method() { }


        /// <summary>
        /// My_Property
        /// </summary>
        public string Property { get; set; }

        /// <summary>
        /// My_Event
        /// </summary>
        public event EventHandler Event;  
    }
}
