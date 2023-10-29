using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

public class LauncherVersion
{
    public static string ShortVersionString
    {
        get
        {
            string version = GitVersionInformation.MajorMinorPatch;

            if (GitVersionInformation.CommitsSinceVersionSource != "0")
                version += $"+{GitVersionInformation.CommitsSinceVersionSource}";

            if (GitVersionInformation.UncommittedChanges != "0")
                version += " dirty";

            return version;
        }
    }

    public static string DetailedVersionString => GitVersionInformation.InformationalVersion;

    public static bool IsNotMainBranch => GitVersionInformation.CommitsSinceVersionSource != "0" || GitVersionInformation.UncommittedChanges != "0";

    public static bool CheckIfUpdateIsAvailable([NotNullWhen(true)] out UpdateInformation? updateInformation)
    {
        updateInformation = null;

        if (IsNotMainBranch)
        {
            Console.WriteLine("Skip update check because not main branch (or local dev version)");
            return false; // we are probably in a test branch
        }

        var latestLauncherVersion = GitHubApi.LatestReleaseVersion("0blu/WinterspringLauncher");
        if (latestLauncherVersion.TagName == null)
            throw new Exception("No latest version?");

        var myVersion = Version.Parse(GitVersionInformation.MajorMinorPatch);
        var newVersion = Version.Parse(latestLauncherVersion.TagName);
        if (newVersion > myVersion)
        {
            Console.WriteLine($"New launcher update {myVersion.ToString(fieldCount: 2)} => {newVersion.ToString(fieldCount: 2)}");
            updateInformation = new UpdateInformation
            {
                ReleaseDate = latestLauncherVersion.PublishedAt,
                VersionName = latestLauncherVersion.TagName,
                URLLinkToReleasePage = "https://github.com/0blu/WinterspringLauncher/releases",
            };
            return true;
        }

        return false;
    }

    public class UpdateInformation
    {
        public DateTime ReleaseDate;
        public string VersionName;
        public string URLLinkToReleasePage;
    }
}
