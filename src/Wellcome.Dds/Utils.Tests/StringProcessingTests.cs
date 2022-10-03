using System.Collections.Generic;
using FluentAssertions;
using Wellcome.Dds.Repositories.Presentation.LicencesAndRights;
using Xunit;

namespace Utils.Tests;

public class StringProcessingTests
{
    private static readonly Dictionary<string, string> LicenseMap  = new()
    {
        ["PDM"] = "https://creativecommons.org/publicdomain/mark/1.0/",
        ["CC0"] = "https://creativecommons.org/publicdomain/zero/1.0/",
        ["CC-BY"] = "https://creativecommons.org/licenses/by/4.0/",
        ["CC-BY-NC"] = "https://creativecommons.org/licenses/by-nc/4.0/",
        ["CC-BY-NC-ND"] = "https://creativecommons.org/licenses/by-nc-nd/4.0/",
        ["CC-BY-ND"] = "https://creativecommons.org/licenses/by-nd/4.0/",
        ["CC-BY-SA"] = "https://creativecommons.org/licenses/by-sa/4.0/",
        ["CC-BY-NC-SA"] = "https://creativecommons.org/licenses/by-nc-sa/4.0/",
        ["OGL"] = "http://www.nationalarchives.gov.uk/doc/open-government-licence/version/2/",
        ["OPL"] = "http://www.parliament.uk/site-information/copyright/open-parliament-licence/",
        ["ARR"] = "https://en.wikipedia.org/wiki/All_rights_reserved",
        ["All Rights Reserved"] = "https://en.wikipedia.org/wiki/All_rights_reserved",
    };

    [Theory]
    [InlineData("This is CC-BY", @"This is <a href=""https://creativecommons.org/licenses/by/4.0/"">CC-BY</a>")]
    [InlineData("This is CC-BY hello", @"This is <a href=""https://creativecommons.org/licenses/by/4.0/"">CC-BY</a> hello")]
    [InlineData("This is CC-BY-NC hello", @"This is <a href=""https://creativecommons.org/licenses/by-nc/4.0/"">CC-BY-NC</a> hello")]
    [InlineData("This is CC-BY-NC hello and CC-BY", @"This is <a href=""https://creativecommons.org/licenses/by-nc/4.0/"">CC-BY-NC</a> hello and <a href=""https://creativecommons.org/licenses/by/4.0/"">CC-BY</a>")]
    [InlineData("This is OGL and CC-BY-NC-SA and CC-BY-NC and OGL again", 
        @"This is <a href=""http://www.nationalarchives.gov.uk/doc/open-government-licence/version/2/"">OGL</a> and <a href=""https://creativecommons.org/licenses/by-nc-sa/4.0/"">CC-BY-NC-SA</a> and <a href=""https://creativecommons.org/licenses/by-nc/4.0/"">CC-BY-NC</a> and <a href=""http://www.nationalarchives.gov.uk/doc/open-government-licence/version/2/"">OGL</a> again")]
    public void LicenseMap_Codes_Are_Replaced(string raw, string expected)
    {
        var processed = LicenceHelpers.GetUsageWithHtmlLinks(raw);

        processed.Should().Be(expected);
    }
}