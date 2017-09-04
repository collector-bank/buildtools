using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class Program
{
    public static int Main(string[] args)
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
            return 0;
        }


        if (branchname == "master" || branchname == "refs/heads/master")
        {
            Log($"On master branch: '{branchname}', keeping build number.");
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
                return 0;
            }


            string buildnumber = $"0.0.{buildcounter}";
            Log($"Setting build number: '{buildnumber}'");

            if (args.Length != 1 || args[0] != "-dryrun")
            {
                Log($"##teamcity[buildNumber '{buildnumber}']");
            }
        }

        return 0;
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

return Program.Main(Environment.GetCommandLineArgs().Skip(2).ToArray());
