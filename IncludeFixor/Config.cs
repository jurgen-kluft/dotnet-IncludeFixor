// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using CppRelativeIncludes;
//
//    var config = Config.FromJson(jsonString);

using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace IncludeFixor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Globalization;

    public partial class Config
    {
        [JsonProperty("settings")] public Settings Settings { get; set; }

        [JsonProperty("includes")] public List<Include> Includes { get; set; } = new List<Include>();

        [JsonProperty("sources")] public List<Source> Sources { get; set; } = new List<Source>();
    }

    public partial class Include
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("readonly")] public bool ReadOnly { get; set; } = false;

        [JsonProperty("scanner_path")] public string ScannerPath { get; set; }

        [JsonProperty("include_path")] public string IncludePath { get; set; }

        [JsonProperty("extensions")] public string[] Extensions { get; set; } = new string[0];

        [JsonProperty("file-renames")] public List<Rename> FileRenames { get; set; } = new List<Rename>();

        [JsonProperty("folder-renames")] public List<Rename> FolderRenames { get; set; } = new List<Rename>();

        [JsonProperty("include-guards")] public IncludeGuards IncludeGuards { get; set; } = new IncludeGuards();
    }

    public partial class IncludeGuards
    {
        // Use the filename to replace the include guard string
        // e.g. before: #ifndef STB_INCLUDE_STB_RECT_PACK_H
        //      after:  #ifndef __STB_INCLUDE_IMSTB_RECTPACK_H__
        [JsonProperty("use_filename")] public bool UseFilename { get; set; } = false;
        [JsonProperty("prefix")] public string Prefix { get; set; } = "";
        [JsonProperty("postfix")] public string Postfix { get; set; } = "";

        [JsonProperty("prefix_remove")] public List<string> PrefixRemoval { get; set; } = new List<string>();

        public Regex IncludeGuardIfNotDefined { get; set; } = new Regex("\\s*#\\s*ifndef\\s*([ ])([^ ;/]+)", RegexOptions.Compiled);
        public Regex IncludeGuardDefine { get; set; } = new Regex("\\s*#\\s*define\\s*([ ])([^ ;/]+)", RegexOptions.Compiled);

        private static string FilenameToIncludeGuard(string filename)
        {
            filename = filename.Trim(' ', '_');

            // Replace all non-alphanumeric characters with an underscore
            var guard = new StringBuilder();
            foreach (var c in filename)
            {
                if (char.IsLetterOrDigit(c))
                    guard.Append(char.ToUpper(c));
                else
                    guard.Append('_');
            }

            return guard.ToString();
        }

        public string HandleIncludeGuard(string includeGuard, string filename)
        {
            if (UseFilename)
            {
                return Prefix + FilenameToIncludeGuard(filename) + Postfix;
            }

            includeGuard = includeGuard.Trim(' ', '_');

            // Remove any of the prefixes that are in the list
            foreach (var prefix in PrefixRemoval)
            {
                if (includeGuard.StartsWith(prefix))
                {
                    includeGuard = includeGuard.Substring(prefix.Length);
                    break;
                }
            }

            // Keep removing the prefix if it exists in the current include guard
            while (includeGuard.StartsWith(Prefix))
                includeGuard = includeGuard.Substring(Prefix.Length);

            // Keep removing the postfix if it exists in the current include guard
            while (includeGuard.EndsWith(Postfix))
                includeGuard = includeGuard.Substring(0, includeGuard.Length - Postfix.Length);

            return Prefix + includeGuard + Postfix;
        }
    }

    public partial class Rename
    {
        [JsonProperty("from")] public string From { get; set; }

        [JsonProperty("to")] public string To { get; set; }
    }

    public partial class Settings
    {
        [JsonProperty("path-separator")] public char PathSeparator { get; set; }
        [JsonProperty("log")] public bool Log { get; set; }
        [JsonProperty("dry-run")] public bool DryRun { get; set; }
        [JsonProperty("verbose")] public bool Verbose { get; set; }
        [JsonProperty("include-regex")] public string IncludeRegex { get; set; } = "\\s*#\\s*include\\s*([<\"])([^>\"]+)([>\"])";

        public char OtherPathSeparator => PathSeparator == '/' ? '\\' : '/';
    }

    public partial class Source
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("scanner_path")] public string ScannerPath { get; set; }

        [JsonProperty("extensions")] public string[] Extensions { get; set; } = new string[0];
    }

    public partial class Config
    {
        private static Config FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Config>(json, IncludeFixor.Converter.sSettings);
        }

        public static bool Read(string filepath, out Config config)
        {
            config = null;

            if (!File.Exists(filepath)) return false;

            try
            {
                var json = File.ReadAllText(filepath);
                config = FromJson(json);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: your json configuration file has an issue (\"\")", e.Message);
            }

            return true;
        }

        public void Fixup()
        {
            // Fixup all the include and scanner paths, they need to end with '/'

            foreach (var include in Includes)
            {
                include.ScannerPath = include.ScannerPath.Replace(Settings.OtherPathSeparator, Settings.PathSeparator);
                include.IncludePath = include.IncludePath.Replace(Settings.OtherPathSeparator, Settings.PathSeparator);

                if (!string.IsNullOrEmpty(include.ScannerPath) && !include.ScannerPath.EndsWith('/'))
                    include.ScannerPath += Path.DirectorySeparatorChar;
                if (!string.IsNullOrEmpty(include.IncludePath) && !include.IncludePath.EndsWith('/'))
                    include.IncludePath += Path.DirectorySeparatorChar;
            }

        }
    }

    public static class Serialize
    {
        public static string ToJson(this Config self)
        {
            return JsonConvert.SerializeObject(self, IncludeFixor.Converter.sSettings);
        }
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings sSettings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}
