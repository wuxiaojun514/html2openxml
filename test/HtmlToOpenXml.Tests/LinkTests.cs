using NUnit.Framework;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Tests
{
    /// <summary>
    /// Tests hyperlink.
    /// </summary>
    [TestFixture]
    public class LinkTests : HtmlConverterTestBase
    {
        [TestCase("://www.site.com")]
        [TestCase("www.site.com")]
        [TestCase("http://www.site.com")]
        public void ExternalLink_ShouldSucceed (string link)
        {
            var elements = converter.Parse($@"<a href=""{link}"" title=""Test Tooltip"">Test Caption</a>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.Multiple(() => {
                Assert.That(elements[0], Is.TypeOf(typeof(Paragraph)));
                Assert.That(elements[0].HasChild<Hyperlink>(), Is.True);
            });
            var hyperlink = elements[0].GetFirstChild<Hyperlink>()!;
            Assert.That(hyperlink.Tooltip, Is.Not.Null);
            Assert.That(hyperlink.Tooltip.Value, Is.EqualTo("Test Tooltip"));
            Assert.That(hyperlink.HasChild<Run>(), Is.True);
            Assert.That(elements[0].InnerText, Is.EqualTo("Test Caption"));

            Assert.That(hyperlink.Id, Is.Not.Null);
            Assert.That(hyperlink.History?.Value, Is.EqualTo(true));
            Assert.That(mainPart.HyperlinkRelationships.Count(), Is.GreaterThan(0));

            var extLink = mainPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == hyperlink.Id);
            Assert.That(extLink, Is.Not.Null);
            Assert.That(extLink.IsExternal, Is.EqualTo(true));
            Assert.That(extLink.Uri.AbsoluteUri, Is.EqualTo("http://www.site.com/"));
        }

        [TestCase(@"<a href=""javascript:alert()"">Js</a>")]
        [TestCase(@"<a href=""site.com"">Unknown site</a>")]
        [TestCase(@"<a href=''>Empty link</a>")]
        [TestCase(@"<a href='#'>Empty bookmark</a>")]
        public void InvalidLink_ReturnsSimpleRun (string html)
        {
            // invalid link leads to simple Run with no link

            var elements = converter.Parse(html);
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.Multiple(() => {
                Assert.That(elements[0], Is.TypeOf(typeof(Paragraph)));
                Assert.That(elements[0].FirstChild, Is.TypeOf(typeof(Run)));
                Assert.That(elements[0].FirstChild?.FirstChild, Is.TypeOf(typeof(Text)));
            });
        }

        [Test]
        public void TextImageLink_ReturnsHyperlinkWithTextAndImage ()
        {
            var elements = converter.Parse(@"<a href=""www.site.com""><img src=""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg=="" alt=""Red dot"" /> Test Caption</a>");
            Assert.That(elements[0].FirstChild, Is.TypeOf(typeof(Hyperlink)));

            var hyperlink = (Hyperlink) elements[0].FirstChild;
            Assert.That(hyperlink.ChildElements, Has.Count.EqualTo(2));
            Assert.That(hyperlink.FirstChild, Is.TypeOf(typeof(Run)));
            Assert.That(hyperlink.FirstChild.HasChild<Drawing>(), Is.True);
            Assert.That(hyperlink.LastChild?.InnerText, Is.EqualTo(" Test Caption"));
        }

        [Test]
        public void Anchoring_WithUnknownTarget_ReturnsHyperlinkWithBookmark ()
        {
            var elements = converter.Parse(@"<a href=""#anchor1"">Anchor1</a>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf(typeof(Paragraph)));
            Assert.That(elements[0].HasChild<Hyperlink>(), Is.True);

            var hyperlink = elements[0].GetFirstChild<Hyperlink>()!;
            Assert.That(hyperlink.Id, Is.Null);
            Assert.That(hyperlink.Anchor?.Value, Is.EqualTo("anchor1"));
        }

        [Test]
        public void SetExcludeAnchoring_ReturnsSimpleRun ()
        {
            converter.ExcludeLinkAnchor = true;

            // _top is always present and bypass the previous rule
            var elements = converter.Parse(@"<a href=""#_top"">Anchor2</a>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf(typeof(Paragraph)));
            Assert.That(elements[0].HasChild<Hyperlink>(), Is.True);

            var hyperlink = (Hyperlink) elements[0].FirstChild!;
            Assert.That(hyperlink.Anchor?.Value, Is.EqualTo("_top"));

            // this should generate a Run and not an Hyperlink
            elements = converter.Parse(@"<a href=""#_anchor3"">Anchor3</a>");
            Assert.That(elements[0].FirstChild, Is.TypeOf(typeof(Run)));
        }

        [TestCase("h1", "id")]
        [TestCase("div", "id")]
        [TestCase("h1", "name")]
        [TestCase("div", "name")]
        public void AnchoringTag_ReturnsHyperlinkWithBookmark(string tagName, string attributeName)
        {
            string str = @$"<a href=""#heading1"">1. Heading 1</a><{tagName} {attributeName}=""heading1"">Heading 1</${tagName}>";
            var elements = converter.Parse(str);
            Assert.That(elements, Has.Count.EqualTo(2));
            Assert.That(elements, Has.All.TypeOf<Paragraph>());
            Assert.Multiple(() =>
            {
                Assert.That(elements[0].HasChild<Hyperlink>(), Is.True);
                Assert.That(elements[1].HasChild<BookmarkStart>(), Is.True);
                Assert.That(elements[1].HasChild<BookmarkEnd>(), Is.True);
                Assert.That(elements[0].GetFirstChild<Hyperlink>()?.Anchor?.Value, Is.EqualTo("heading1"));
                Assert.That(elements[1].GetFirstChild<BookmarkStart>()?.Name?.Value, Is.EqualTo("heading1"));
            });
        }

        [Test(Description = "Anchor is targeting an empty link inside a heading")]
        public void AnchoringHeading_WithEmptyTarget_ReturnsHyperlinkWithBookmark()
        {
            string str = @$"<a href=""#heading1"">1. Heading 1</a><h1><a name=""heading1""></a>Heading 1</h1>";
            var elements = converter.Parse(str);
            Assert.That(elements, Has.Count.EqualTo(2));
            Assert.That(elements, Has.All.TypeOf<Paragraph>());
            Assert.Multiple(() =>
            {
                Assert.That(elements[0].HasChild<Hyperlink>(), Is.True);
                Assert.That(elements[1].HasChild<BookmarkStart>(), Is.True);
                Assert.That(elements[1].HasChild<BookmarkEnd>(), Is.True);
                Assert.That(elements[0].GetFirstChild<Hyperlink>()?.Anchor?.Value, Is.EqualTo("heading1"));
                Assert.That(elements[1].GetFirstChild<BookmarkStart>()?.Name?.Value, Is.EqualTo("heading1"));
            });
        }

        [Test(Description = "Link inside a paragraph")]
        public void InlineWithText_ReturnsMultipleRunsWithHyperlink()
        {
            var elements = converter.Parse(@"Some <a href=""www.site.com"">inline</a> link.");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf(typeof(Paragraph)));
            Assert.Multiple(() => {
                Assert.That(elements[0].ElementAt(0), Is.TypeOf<Run>());
                Assert.That(elements[0].ElementAt(1), Is.TypeOf<Run>());
                Assert.That(elements[0].ElementAt(2), Is.TypeOf<Hyperlink>());
                Assert.That(elements[0].ElementAt(3), Is.TypeOf<Run>());
            });
        }

        [Test(Description = "Many runs inside the link should respect whitespaces")]
        public void WithMultipleRun_ReturnsHyperlinkWithMultipleRuns()
        {
            var elements = converter.Parse(@"<a href=""https://github.com/onizet/html2openxml""><b>Html</b> to <b>OpenXml</b>!</a>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf(typeof(Paragraph)));
            var h = elements[0].GetFirstChild<Hyperlink>();

            Assert.That(h, Is.Not.Null);
            Assert.That(h.ChildElements, Has.All.TypeOf(typeof(Run)));
            Assert.That(h.InnerText, Is.EqualTo("Html to OpenXml !"));
        }
    }
}