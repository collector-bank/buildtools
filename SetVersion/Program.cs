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
        if (args.Contains("-?") || args.Contains("-h") || args.Contains("--help"))
        {
            Log("Usage: SetVersion -updateprojectfiles -updateassemblyinfofiles -updatenuspecfiles -dryrun");
            return 1;
        }

        bool updateprojectfiles = args.Select(a => a.ToLower()).Contains("-updateprojectfiles");
        bool updateassemblyinfofiles = args.Select(a => a.ToLower()).Contains("-updateassemblyinfofiles");
        bool updatenuspecfiles = args.Select(a => a.ToLower()).Contains("-updatenuspecfiles");
        bool dryrun = args.Select(a => a.ToLower()).Contains("-dryrun");

        string version = SetTeamcityVersion(dryrun);

        if (updateprojectfiles && version != null)
        {
            UpdateProjectFiles(version, dryrun);
        }
        if (updateassemblyinfofiles && version != null)
        {
            UpdateAssemblyinfoFiles(version, dryrun);
        }
        if (updatenuspecfiles && version != null)
        {
            UpdateNuspecFiles(version, dryrun);
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

            Log($"Got {groups.Count} PropertyGroups.");

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
                        Log($"{filename}: {node.Value} -> {version}");
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
                if (dryrun)
                {
                    Log("Not!");
                }
                else
                {
                    xdoc.Save(filename);
                }
            }
        }
    }

    private static void UpdateAssemblyinfoFiles(string version, bool dryrun)
    {
        string[] files = Directory.GetFiles(".", "AssemblyInfo.cs", SearchOption.AllDirectories)
            .Select(f => f.StartsWith(@".\") ? f.Substring(2) : f)
            .ToArray();

        Log($"Found {files.Length} AssemblyInfo files.");

        bool updatedAssemblyVersion = false;
        bool updatedAssemblyFileVersion = false;

        foreach (string filename in files)
        {
            bool modified = false;
            Log($"Reading: '{filename}'");
            List<string> rows = File.ReadAllLines(filename).ToList();

            Log($"Got {rows.Count} rows.");

            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i];
                int offset = row.IndexOf("Version(\"");
                if (row.StartsWith("[assembly: ") && offset >= 0 && row.EndsWith("\")]"))
                {
                    string newRow = row.Substring(0, offset + 9) + version + row.Substring(row.Length - 3);
                    if (row != newRow)
                    {
                        int offsetVersionName = row.LastIndexOf(' ', offset) + 1;
                        string versionName = row.Substring(offsetVersionName, offset - offsetVersionName + 7);
                        string oldVersion = row.Substring(offset + 9, row.Length - offset - 12);
                        Log($"{filename}: {versionName}: '{oldVersion}' -> '{version}'");
                        rows[i] = newRow;
                        modified = true;

                        if (versionName == "AssemblyVersion")
                        {
                            updatedAssemblyVersion = true;
                        }
                        if (versionName == "AssemblyFileVersion")
                        {
                            updatedAssemblyFileVersion = true;
                        }
                    }
                }
            }
            if (!updatedAssemblyVersion)
            {
                Log($"{filename}: AssemblyVersion: -> '{version}'");
                rows.Add($"[assembly: AssemblyVersion(\"{version}\")]");
            }
            if (!updatedAssemblyFileVersion)
            {
                Log($"{filename}: AssemblyFileVersion: -> '{version}'");
                rows.Add($"[assembly: AssemblyFileVersion(\"{version}\")]");
            }

            if (modified)
            {
                Log($"Saving: '{filename}'");
                if (dryrun)
                {
                    Log("Not!");
                }
                else
                {
                    File.WriteAllLines(filename, rows);
                }
            }
        }
    }

    private static void UpdateNuspecFiles(string version, bool dryrun)
    {
        string[] files = Directory.GetFiles(".", "*.nuspec", SearchOption.AllDirectories)
            .Select(f => f.StartsWith(@".\") ? f.Substring(2) : f)
            .ToArray();

        Log($"Found {files.Length} nuspec files.");

        foreach (string filename in files)
        {
            bool modified = false;
            Log($"Reading: '{filename}'");
            var xdoc = XDocument.Load(filename);

            XNamespace ns = xdoc.Root.Name.Namespace;

            var metadatas = xdoc
                .Elements(ns + "package")
                .Elements(ns + "metadata")
                .ToList();

            Log($"Got {metadatas.Count} metadata elements.");

            foreach (var metadata in metadatas)
            {
                var versionElements = metadata
                    .Elements()
                    .Where(e => e.Name.LocalName == "version")
                    .ToList();

                foreach (var versionElement in versionElements)
                {
                    if (versionElement.Value != version)
                    {
                        Log($"{filename}: {versionElement.Value} -> {version}");
                        versionElement.Value = version;
                        modified = true;
                    }
                }

                if (metadatas.Elements(ns + "version").Count() == 0)
                {
                    metadatas.Add(new XElement(ns + "version", version));
                    modified = true;
                }
            }

            if (modified)
            {
                Log($"Saving: '{filename}'");
                if (dryrun)
                {
                    Log("Not!");
                }
                else
                {
                    xdoc.Save(filename);
                }
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
            Log("Couldn't find any branch name.", ConsoleColor.Yellow);
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
                Log("Couldn't find any build counter.", ConsoleColor.Yellow);
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
            Log("Couldn't find Teamcity build properties file.", ConsoleColor.Yellow);
            return empty;
        }
        if (!File.Exists(buildpropfile))
        {
            Log($"Couldn't find Teamcity build properties file: '{buildpropfile}'", ConsoleColor.Yellow);
            return empty;
        }

        Log($"Reading Teamcity build properties file: '{buildpropfile}'");
        string[] rows = File.ReadAllLines(buildpropfile);

        var valuesBuild = GetPropValues(rows);

        string configpropfile = valuesBuild["teamcity.configuration.properties.file"];
        if (string.IsNullOrEmpty(configpropfile))
        {
            Log("Couldn't find Teamcity config properties file.", ConsoleColor.Yellow);
            return empty;
        }
        if (!File.Exists(configpropfile))
        {
            Log($"Couldn't find Teamcity config properties file: '{configpropfile}'", ConsoleColor.Yellow);
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

    private static void Log(string message, ConsoleColor color)
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
