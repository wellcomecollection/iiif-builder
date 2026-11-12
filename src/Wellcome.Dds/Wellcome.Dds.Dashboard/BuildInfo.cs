using System;
using System.Globalization;

namespace Wellcome.Dds.Dashboard
{
    /// <summary>
    /// What this image was built from: the "Build" configuration section. The Dockerfile sets it as Build__*
    /// environment variables from the build arguments that .github/workflows/build.yml passes.
    /// A local build has none of them.
    /// </summary>
    public class BuildInfo
    {
        /// <summary>Full commit SHA</summary>
        public string Commit { get; set; }

        /// <summary>The branch or tag whose push made the build</summary>
        public string Ref { get; set; }

        /// <summary>git describe --tags --always, e.g. v3.2-4-g5678042</summary>
        public string Describe { get; set; }

        public string CommitSubject { get; set; }
        public string CommitAuthor { get; set; }
        public string CommitDate { get; set; }

        /// <summary>Number of the pull request the commit merged, or of an open one with the commit at its head</summary>
        public string PullRequest { get; set; }

        public string PullRequestTitle { get; set; }

        /// <summary>e.g. https://github.com/wellcomecollection/iiif-builder</summary>
        public string Repository { get; set; }

        /// <summary>The GitHub Actions run that built the image</summary>
        public string Run { get; set; }

        /// <summary>When the image was built (UTC, ISO 8601)</summary>
        public string Date { get; set; }

        public bool IsKnown => !string.IsNullOrEmpty(Commit);

        public string ShortCommit => IsKnown ? Commit[..Math.Min(7, Commit.Length)] : null;

        public string CommitUrl => RepositoryLink("commit", Commit);
        public string RefUrl => RepositoryLink("tree", Ref);
        public string PullRequestUrl => RepositoryLink("pull", PullRequest);

        /// <summary>CommitDate in the server's local time (Europe/London in the image); null if absent or unreadable</summary>
        public DateTime? CommitDateLocal => ToLocal(CommitDate);

        /// <summary>Date (when the image was built) in the server's local time; null if absent or unreadable</summary>
        public DateTime? DateLocal => ToLocal(Date);

        private static DateTime? ToLocal(string iso8601) =>
            DateTimeOffset.TryParse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.LocalDateTime
                : null;

        private string RepositoryLink(string kind, string value) =>
            string.IsNullOrEmpty(Repository) || string.IsNullOrEmpty(value) ? null : $"{Repository}/{kind}/{value}";
    }
}
