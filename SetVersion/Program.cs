using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public class Program
{
    public static int Main(string[] args)
    {
        bool updateprojectfiles = args.Contains("-updateprojectfiles");
        bool dryrun = args.Contains("-dryrun");

        string version = SetTeamcityVersion(dryrun);

        if (updateprojectfiles && version != null)
        {
            UpdateProjectFiles(version, dryrun);
        }

        return 0;
    }

    private static void UpdateProjectFiles(string version, bool dryrun)
    {
        string[] files = Directory.GetFiles(".", "*.csproj", SearchOption.AllDirectories)
            .Select(f => f.StartsWith(@".\") ? f.Substring(2) : f)
            .ToArray();

        Log($"Found {files.Length} projects.");

        foreach (string filename in files)
        {
            bool modified = false;
            Log($"Reading: '{filename}'");
            var xdoc = XDocument.Load(filename);

            XNamespace ns = xdoc.Root.Name.Namespace;

            var groups = xdoc
                .Elements(ns + "Project")
                .Elements(ns + "PropertyGroup")
                .ToList();

            Log($"Found {groups.Count} PropertyGroups.");

            foreach (var group in groups)
            {
                var nodes = group
                    .Elements()
                    .Where(e => e.Name.LocalName == "Version" || e.Name.LocalName == "AssemblyVersion" || e.Name.LocalName == "FileVersion" || e.Name.LocalName == "ProductVersion")
                    .ToList();

                foreach (var node in nodes)
                {
                    if (node.Value != version)
                    {
                        node.Value = version;
                        modified = true;
                    }
                }

                if (group.Elements(ns + "Version").Count() == 0)
                {
                    group.Add(new XElement(ns + "Version", version));
                    modified = true;
                }
            }

            if (modified)
            {
                Log($"Saving: '{filename}'");
                xdoc.Save(filename);
            }
        }
    }

    static string SetTeamcityVersion(bool dryrun)
    {
        Dictionary<string, string> tcprops = GetTeamcityVariables();


        string branchname;
        if (tcprops.ContainsKey("teamcity.build.branch"))
        {
            branchname = tcprops["teamcity.build.branch"];
            Log($"Found teamcity.build.branch: '{branchname}'");
        }
        else if (tcprops.ContainsKey("vcsroot.branch"))
        {
            branchname = tcprops["vcsroot.branch"];
            Log($"Found vcsroot.branch: '{branchname}'");
        }
        else
        {
            LogColor("Couldn't find any branch name.", ConsoleColor.Yellow);
            return null;
        }

        string tcisdefault = tcprops.ContainsKey("teamcity.build.branch.is_default") ? tcprops["teamcity.build.branch.is_default"] : null;
        Log("teamcity.build.branch.is_default: " + (tcisdefault == null ? "<null>" : $"'{tcisdefault}'"));

        bool isdefaultbranch = tcisdefault == "true" ? true : false;


        if (branchname == "master" || branchname == "refs/heads/master" || isdefaultbranch)
        {
            Log($"On master/default branch: '{branchname}', keeping build number.");
            string buildnumber = tcprops["build.number"];
            return buildnumber;
        }
        else
        {
            string buildcounter;
            if (tcprops.ContainsKey("build.counter"))
            {
                buildcounter = tcprops["build.counter"];
                Log($"Found build.counter: '{buildcounter}'");
            }
            else
            {
                LogColor("Couldn't find any build counter.", ConsoleColor.Yellow);
                return null;
            }


            string buildnumber = $"0.0.{buildcounter}";
            Log($"Setting build number: '{buildnumber}'");

            if (!dryrun)
            {
                Log($"##teamcity[buildNumber '{buildnumber}']");
            }

            return buildnumber;
        }
    }

    private static Dictionary<string, string> GetTeamcityVariables()
    {
        Dictionary<string, string> empty = new Dictionary<string, string>();

        string buildpropfile = Environment.GetEnvironmentVariable("TEAMCITY_BUILD_PROPERTIES_FILE");
        if (string.IsNullOrEmpty(buildpropfile))
        {
            LogColor("Couldn't find Teamcity build properties file.", ConsoleColor.Yellow);
            return empty;
        }
        if (!File.Exists(buildpropfile))
        {
            LogColor($"Couldn't find Teamcity build properties file: '{buildpropfile}'", ConsoleColor.Yellow);
            return empty;
        }

        Log($"Reading Teamcity build properties file: '{buildpropfile}'");
        string[] rows = File.ReadAllLines(buildpropfile);

        var valuesBuild = GetPropValues(rows);

        string configpropfile = valuesBuild["teamcity.configuration.properties.file"];
        if (string.IsNullOrEmpty(configpropfile))
        {
            LogColor("Couldn't find Teamcity config properties file.", ConsoleColor.Yellow);
            return empty;
        }
        if (!File.Exists(configpropfile))
        {
            LogColor($"Couldn't find Teamcity config properties file: '{configpropfile}'", ConsoleColor.Yellow);
            return empty;
        }

        Log($"Reading Teamcity config properties file: '{configpropfile}'");
        rows = File.ReadAllLines(configpropfile);

        var valuesConfig = GetPropValues(rows);

        return valuesConfig;
    }

    private static Dictionary<string, string> GetPropValues(string[] rows)
    {
        Dictionary<string, string> dic = new Dictionary<string, string>();

        foreach (string row in rows)
        {
            int index = row.IndexOf('=');
            if (index != -1)
            {
                string key = row.Substring(0, index);
                string value = Regex.Unescape(row.Substring(index + 1));
                dic[key] = value;
            }
        }

        return dic;
    }

    private static void Log(string message)
    {
        Console.WriteLine(message);
    }

    private static void LogColor(string message, ConsoleColor color)
    {
        ConsoleColor oldcolor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = oldcolor;
        }
    }
}
