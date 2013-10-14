using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using MarkdownSharp;

using NUnit.Framework;


namespace MarkdownSharpTests 
{
    [TestFixture]
    public class MDTestTests : BaseTest
    {
        const string folder = "testfiles.mdtest_1._1";


        private static IEnumerable<TestCaseData> GetTests()
        {
            Markdown m = new Markdown();
            Assembly assembly = Assembly.GetAssembly(typeof(BaseTest));
            string namespacePrefix = String.Concat(assembly.GetName().Name, '.', folder);
            string[] resourceNames = assembly.GetManifestResourceNames();
            
            Func<string, string> getResourceFileContent = filename =>
            {
                using (Stream stream = assembly.GetManifestResourceStream(filename))
                {
                    if (stream == null)
                        return null;

                    using (StreamReader streamReader = new StreamReader(stream))
                        return streamReader.ReadToEnd();
                }
            };

            return from name in resourceNames
                   // Skip resource names that aren't within the namespace (folder) we want
                   // and doesn't have the extension '.html'.
                   where name.StartsWith(namespacePrefix) && name.EndsWith(".html")
                   let actualName = Path.ChangeExtension(name, "text")
                   let actualContent = getResourceFileContent(actualName)
                   let actual = Program.RemoveWhitespace(m.Transform(actualContent))
                   let expectedContent = getResourceFileContent(name)
                   let expected = Program.RemoveWhitespace(expectedContent)
                   select new TestCaseData(actualName, name, actual, expected);
        }


        [Test, TestCaseSource("GetTests")]
        public void Test(string actualName, string expectedName, string actual, string expected)
        {
            Assert.That(actual,
                        Is.EqualTo(expected),
                        "Mismatch between '{0}' and the transformed '{1}'.",
                        actualName,
                        expectedName);
        }
    }
}
