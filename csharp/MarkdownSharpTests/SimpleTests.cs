using MarkdownSharp;
using NUnit.Framework;

namespace MarkdownSharpTests
{
    [TestFixture]
    public class SimpleTests : BaseTest
    {
        private Markdown m = new Markdown();

        [Test]
        public void Bold()
        {
            string input = "This is **bold**. This is also __bold__.";
            string expected = "<p>This is <strong>bold</strong>. This is also <strong>bold</strong>.</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Italic()
        {
            string input = "This is *italic*. This is also _italic_.";
            string expected = "<p>This is <em>italic</em>. This is also <em>italic</em>.</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Link()
        {
            string input = "This is [a link][1].\n\n  [1]: http://www.example.com";
            string expected = "<p>This is <a href=\"http://www.example.com\">a link</a>.</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void LinkBracket()
        {
            string input = "Have you visited <http://www.example.com> before?";
            string expected = "<p>Have you visited <a href=\"http://www.example.com\">http://www.example.com</a> before?</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void LinkBare_withoutAutoHyperLink()
        {
            string input = "Have you visited http://www.example.com before?";
            string expected = "<p>Have you visited http://www.example.com before?</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        /*
        [Test]
        public void LinkBare_withAutoHyperLink()
        {
            //TODO: implement some way of setting AutoHyperLink programmatically
            //to run this test now, just change the _autoHyperlink constant in Markdown.cs
            string input = "Have you visited http://www.example.com before?";
            string expected = "<p>Have you visited <a href=\"http://www.example.com\">http://www.example.com</a> before?</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }*/

        [Test]
        public void LinkAlt()
        {
            string input = "Have you visited [example](http://www.example.com) before?";
            string expected = "<p>Have you visited <a href=\"http://www.example.com\">example</a> before?</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Image()
        {
            string input = "An image goes here: ![alt text][1]\n\n  [1]: http://www.google.com/intl/en_ALL/images/logo.gif";
            string expected = "<p>An image goes here: <img src=\"http://www.google.com/intl/en_ALL/images/logo.gif\" alt=\"alt text\" /></p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Blockquote()
        {
            string input = "Here is a quote\n\n> Sample blockquote\n";
            string expected = "<p>Here is a quote</p>\n\n<blockquote>\n  <p>Sample blockquote</p>\n</blockquote>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void NumberList()
        {
            string input = "A numbered list:\n\n1. a\n2. b\n3. c\n";
            string expected = "<p>A numbered list:</p>\n\n<ol>\n<li>a</li>\n<li>b</li>\n<li>c</li>\n</ol>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void BulletList()
        {
            string input = "A bulleted list:\n\n- a\n- b\n- c\n";
            string expected = "<p>A bulleted list:</p>\n\n<ul>\n<li>a</li>\n<li>b</li>\n<li>c</li>\n</ul>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Header1()
        {
            string input = "#Header 1\nHeader 1\n========";
            string expected = "<h1>Header 1</h1>\n\n<h1>Header 1</h1>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Header2()
        {
            string input = "##Header 2\nHeader 2\n--------";
            string expected = "<h2>Header 2</h2>\n\n<h2>Header 2</h2>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CodeBlock()
        {
            string input = "code sample:\n\n    <head>\n    <title>page title</title>\n    </head>\n";
            string expected = "<p>code sample:</p>\n\n<pre><code>&lt;head&gt;\n&lt;title&gt;page title&lt;/title&gt;\n&lt;/head&gt;\n</code></pre>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CodeSpan()
        {
            string input = "HTML contains the `<blink>` tag";
            string expected = "<p>HTML contains the <code>&lt;blink&gt;</code> tag</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void HtmlPassthrough()
        {
            string input = "<div>\nHello World!\n</div>\n";
            string expected = "<div>\nHello World!\n</div>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Escaping()
        {
            string input = @"\`foo\`";
            string expected = "<p>`foo`</p>\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void HorizontalRule()
        {
            string input = "* * *\n\n***\n\n*****\n\n- - -\n\n---------------------------------------\n\n";
            string expected = "<hr />\n\n<hr />\n\n<hr />\n\n<hr />\n\n<hr />\n";

            string actual = m.Transform(input);

            Assert.AreEqual(expected, actual);
        }
    }
}
