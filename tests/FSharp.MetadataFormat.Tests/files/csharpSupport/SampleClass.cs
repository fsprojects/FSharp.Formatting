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

        /// <summary>
        /// My_Private_Static_Method
        /// </summary>
        private static void StaticPrivateMethod() { }


        /// <summary>
        /// My_Private_Static_Property
        /// </summary>
        private static string StaticPrivateProperty { get; set; }

        /// <summary>
        /// My_Private_Static_Event
        /// </summary>
        private static event EventHandler StaticPrivateEvent;  
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
        /// My_Private_Static_Method
        /// </summary>
        private static void StaticPrivateMethod() { }


        /// <summary>
        /// My_Private_Static_Property
        /// </summary>
        private static string StaticPrivateProperty { get; set; }

        /// <summary>
        /// My_Private_Static_Event
        /// </summary>
        private static event EventHandler StaticPrivateEvent;


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


        /// <summary>
        /// My_Private_Constructor
        /// </summary>
        private SampleClass(int dummy) { }


        /// <summary>
        /// My_Private_Method
        /// </summary>
        private void PrivateMethod() { }


        /// <summary>
        /// My_Private_Property
        /// </summary>
        private string PrivateProperty { get; set; }

        /// <summary>
        /// My_Private_Event
        /// </summary>
        private event EventHandler PrivateEvent;

        /// <summary>
        /// Event triggered when WorklistText is required
        /// </summary>
        public delegate void WorklistTextRequested(int medicalRecordId, string accessionNo,
          int templateId, string specificationCode, string specificationText, string aeTitle);

        /// <summary>
        /// Some Event with its own delegate
        /// </summary>
        public event WorklistTextRequested OnWorklistTextRequested;
    }
}
