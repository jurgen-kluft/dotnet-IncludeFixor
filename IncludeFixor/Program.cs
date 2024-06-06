using System.Text.RegularExpressions;
using Glob;
using Spectre.Console;

namespace IncludeFixor
{
    using Console = CConsole;

    public static class CConsole
    {
        public static List<TextWriter> sWriters = new List<TextWriter>();
        public static bool Verbose { get; set; }

        // Implement all the possible console functions and being able to route them to different TextWriters as well as Spectre.AnsiConsole.
        public static void Write(string value)
        {
            AnsiConsole.Write(value);
            foreach (var writer in sWriters)
            {
                writer.Write(value);
            }
        }

        public static void WriteLine(string value)
        {
            AnsiConsole.WriteLine(value);
            foreach (var writer in sWriters)
            {
                writer.WriteLine(value);
            }
        }

        public static void WriteLine(string format, params object[] args)
        {
            AnsiConsole.WriteLine(format, args);
            foreach (var writer in sWriters)
            {
                writer.WriteLine(format, args);
            }
        }

        public static void AddWriter(TextWriter writer)
        {
            sWriters.Add(writer);
        }

        public static void Info(string value)
        {
            if (!Verbose)
                return;
            var color = AnsiConsole.Foreground;
            AnsiConsole.Foreground = Spectre.Console.Color.Grey;
            AnsiConsole.WriteLine(value);
            AnsiConsole.Foreground = color;

            foreach (var writer in sWriters)
            {
                writer.WriteLine(value);
            }
        }

        public static void Warning(string value)
        {
            var color = AnsiConsole.Foreground;
            AnsiConsole.Foreground = Spectre.Console.Color.Yellow;
            AnsiConsole.WriteLine(value);
            AnsiConsole.Foreground = color;

            foreach (var writer in sWriters)
            {
                writer.WriteLine(value);
            }
        }

        public static void Warning(string value, params object[] args)
        {
            var color = AnsiConsole.Foreground;
            AnsiConsole.Foreground = Spectre.Console.Color.Yellow;
            AnsiConsole.WriteLine(value, args);
            AnsiConsole.Foreground = color;

            foreach (var writer in sWriters)
            {
                writer.WriteLine(value, args);
            }
        }

        public static void Error(string value)
        {
            var color = AnsiConsole.Foreground;
            AnsiConsole.Foreground = Spectre.Console.Color.Red;
            AnsiConsole.WriteLine(value);
            AnsiConsole.Foreground = color;

            foreach (var writer in sWriters)
            {
                writer.WriteLine(value);
            }
        }

        public static void Error(string value, params object[] args)
        {
            var color = AnsiConsole.Foreground;
            AnsiConsole.Foreground = Spectre.Console.Color.Red;
            AnsiConsole.WriteLine(value, args);
            AnsiConsole.Foreground = color;

            foreach (var writer in sWriters)
            {
                writer.WriteLine(value, args);
            }
        }

    }

    class Program
    {
        // C++ include fixor:
        // Follows standard C/C++ include mechanism
        // User can register folder renames (after the actual physical rename, this script will not rename your files or folders)
        // User can register file renames (after the actual physical rename)
        //
        static bool Verbose { get; set; }
        static char PathSeparator { get; set; }

        static int Main(string[] args)
        {
            // Root folder is ROOT
            // Should we make backups of the cpp/c files that we modify ?
            var includeFixer = new IncludeFixer();

            // Read the configuration
            if (args.Length == 0 || !Config.Read(args[0], out var config))
            {
                Console.WriteLine("IncludeFixor v1.0, 2018, Jurgen");
                Console.WriteLine("   A utility to adjust/fix/manage include directives of a C++ codebase.");
                Console.WriteLine("");
                Console.WriteLine("    IncludeFixor {INPUT FILE}     (e.g. 'IncludeFixor myconfig.json')");
                Console.WriteLine("");
                return -1;
            }
            // Fix all the paths and separators
            config.Fixup();

            Verbose = config.Settings.Verbose;
            PathSeparator = config.Settings.PathSeparator;

            FileStream logFile = null;
            StreamWriter logWriter = null;
            if (config.Settings.Log)
            {
                logFile = new FileStream(args[0] + ".log", FileMode.Create, FileAccess.ReadWrite);
                logWriter = new StreamWriter(logFile);
                logWriter.AutoFlush = true;
                Console.AddWriter(logWriter);
            }

            foreach (var inc in config.Includes)
            {
                includeFixer.RegisterIncludePath(inc);

                foreach (var rn in inc.FileRenames)
                    includeFixer.AddFileRename(inc.ScannerPath, rn.From, rn.To);
                foreach (var rn in inc.FolderRenames)
                    includeFixer.AddFolderRename(inc.ScannerPath, rn.From, rn.To);
            }

            // Build list of source files (*.c, *.cpp)
            var allSourceFiles = new List<KeyValuePair<string, string>>();
            foreach (var src in config.Sources)
            {
                GlobAllSourceFiles(src.ScannerPath, config.Settings.PathSeparator, allSourceFiles, src.Extensions);
            }

            // Compile our regulare expression to find include directives
            var includeRegex = new Regex(config.Settings.IncludeRegex, RegexOptions.Compiled);

            // For every source file:
            foreach (var cppFile in allSourceFiles)
            {
                //   Read in all lines
                var filePath = FixPath(Path.Combine(cppFile.Key, cppFile.Value));
                var basePath = FixPath(Path.GetDirectoryName(cppFile.Value));
                if (!File.Exists(filePath))
                {
                    Console.Error("globbing found this file but now it doesn't exist? ... \"{0}\"", cppFile.Value);
                    break;
                }

                CConsole.WriteLine("Processing source file ... \"{0}\"", cppFile.Value);

                var lines = File.ReadAllLines(filePath);

                if (FixIncludeDirectives(basePath, cppFile.Value, lines, includeFixer, includeRegex, out var newLines))
                {
                    // Write out all lines if there were any modifications
                    if (!config.Settings.DryRun)
                    {
                        File.WriteAllLines(filePath, newLines);
                    }
                }
            }

            // For every header file that is not read-only fix their include directives
            void FixIncludeDirectory(string rootPath, string relativeFilepath)
            {
                CConsole.WriteLine("Processing header file ... \"{0}\"", relativeFilepath);

                //   Read in all lines
                var outlines = new List<string>();
                var filepath = FixPath(Path.Combine(rootPath, relativeFilepath));

                var basePath = FixPath(Path.GetDirectoryName(relativeFilepath));
                var lines = File.ReadAllLines(filepath);

                if (FixIncludeDirectives(basePath, relativeFilepath, lines, includeFixer, includeRegex, out var newlines))
                {
                    // Write out all lines if there were any modifications
                    if (!config.Settings.DryRun)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
            }

            includeFixer.ForeachHeaderFileThatNeedIncludeDirFix(FixIncludeDirectory);

            // For every header file that is not read-only fix their include guards

            void IncludeGuard(string rootPath, string relativeFilepath, bool verbose, IncludeGuards guards)
            {
                CConsole.WriteLine("Processing header file ... \"{0}\"", relativeFilepath);

                //   Read in all lines
                var outlines = new List<string>();
                var filepath = FixPath(Path.Combine(rootPath, relativeFilepath));
                var basePath = FixPath(Path.GetDirectoryName(relativeFilepath));
                var lines = File.ReadAllLines(filepath);

                var numberOfModifiedLines = FixIncludeGuard(basePath, relativeFilepath, lines, includeFixer, guards, verbose, out var newlines);
                if (numberOfModifiedLines > 0)
                {
                    // Write out all lines if there were any modifications
                    if (!config.Settings.DryRun)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
                else
                {
                    CConsole.Info($"       Didn't modify include guards for header file ... \"{relativeFilepath}\"");
                }
            }

            includeFixer.ForeachHeaderFileThatNeedIncludeGuardFix(config.Settings.Verbose, IncludeGuard);

            if (config.Settings.Log)
            {
                logWriter.Close();
                logFile.Close();
            }

            return 0;
        }

        // File being process can have its own base-path:
        // Example:
        //  - Physics/Broadphase.cpp
        //    If this file has the following includes:
        //    - #include "Broadphase.h"
        //    - #include "Collision.h"
        //
        // Where "Collision.h" also exists in the root, we need to actually get the "Collision.h" that exists in his own folder.

        static bool FixIncludeDirectives(string basePath, string filename, string[] lines, IncludeFixer includes, Regex includeRegex, out List<string> outlines)
        {
            outlines = new List<string>();

            var numberOfModifiedLines = 0;
            var lineNumber = 0;

            foreach (var originalLine in lines)
            {
                //   Analyze line for  #include "
                var lineIsModified = false;
                var modifiedLine = originalLine;

                var regexMatch = includeRegex.Match(originalLine);
                if (regexMatch.Success)
                {
                    var groups = regexMatch.Groups;
                    if (groups.Count == 4 && groups[1].Value == "\"" && groups[3].Value == "\"")
                    {
                        var includeHdr = groups[2].Value.Trim();
                        if (includes.FindInclude(basePath, includeHdr, out var relativeIncludeHdr))
                        {
                            relativeIncludeHdr = FixPath(relativeIncludeHdr);

                            const bool ignoreCase = true;
                            lineIsModified = String.Compare(includeHdr, relativeIncludeHdr, ignoreCase) != 0;
                            if (lineIsModified)
                            {
                                modifiedLine = originalLine.Replace(includeHdr, relativeIncludeHdr);
                                if (Verbose)
                                {
                                    CConsole.WriteLine("    changed:\"{0}\", line({1}): \"{2}\"  -->  \"{3}\".", filename, lineNumber, originalLine, modifiedLine);
                                }
                            }
                        }
                        else
                        {
                            Console.Warning("    warning:\"{0}\", line({1}): Could not find matching include for \"{2}\".", filename, lineNumber, includeHdr);
                        }
                    }
                }

                if (lineIsModified)
                {
                    numberOfModifiedLines += 1;
                    outlines.Add(modifiedLine);
                }
                else
                {
                    outlines.Add(originalLine);
                }

                lineNumber += 1;
            }

            return numberOfModifiedLines > 0;
        }


        static int FixIncludeGuard(string basePath, string filename, string[] lines, IncludeFixer includes, IncludeGuards guards, bool verbose, out List<string> outlines)
        {
            outlines = new List<string>(lines.Length + 16);

            var i = 0;
            var numberOfModifiedLines = 0;

            // Skip empty and comment lines at the top of the file
            var line = string.Empty;
            while (i < lines.Length)
            {
                line = lines[i];
                line = line.Trim();
                if (line == "" || line.StartsWith("//") || line.StartsWith(";"))
                {
                    i += 1;
                }
                else if (line.StartsWith("/*"))
                {
                    // Iterate here until we find the closing "*/"
                    i += 1;
                    while (i++ < lines.Length)
                    {
                        line = lines[i];
                        line = line.Trim();
                        if (!line.EndsWith("*/")) continue;

                        ++i;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            // TODO Should we replace "#pragma once" statements and replace it with an include guard ?
            if (!line.Contains("#pragma once"))
            {

                if (lines.Length >= (i + 2))
                {
                    var ifNotDefinedMatch = guards.IncludeGuardIfNotDefined.Match(lines[i]);
                    if (ifNotDefinedMatch.Success)
                    {
                        var defineMatch = guards.IncludeGuardDefine.Match(lines[i + 1]);
                        if (defineMatch.Success)
                        {
                            var ifNotDefinedGroups = ifNotDefinedMatch.Groups;
                            var defineGroups = defineMatch.Groups;
                            if (ifNotDefinedGroups.Count >= 2 && defineGroups.Count >= 2)
                            {
                                var ifNotDefinedSymbol = ifNotDefinedGroups[2].Value.Trim();
                                var defineSymbol = defineGroups[2].Value.Trim();
                                if (ifNotDefinedSymbol == defineSymbol)
                                {
                                    // Ok we have found an include guard, something like:
                                    // #ifndef __SYMBOL__
                                    // #define __SYMBOL__
                                    var newIncludeGuardSymbol = guards.HandleIncludeGuard(ifNotDefinedSymbol, filename);

                                    if (verbose)
                                    {
                                        CConsole.WriteLine("    changed:\"{0}\", line({1}): \"{2}\"  -->  \"{3}\".", filename, i, lines[i], lines[i].Replace(ifNotDefinedSymbol, newIncludeGuardSymbol));
                                        CConsole.WriteLine("    changed:\"{0}\", line({1}): \"{2}\"  -->  \"{3}\".", filename, i + 1, lines[i + 1], lines[i + 1].Replace(ifNotDefinedSymbol, newIncludeGuardSymbol));
                                    }

                                    outlines.Add(lines[i].Replace(ifNotDefinedSymbol, newIncludeGuardSymbol));
                                    outlines.Add(lines[i + 1].Replace(ifNotDefinedSymbol, newIncludeGuardSymbol));
                                    i += 2;
                                    numberOfModifiedLines = 2;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (verbose)
                {
                    CConsole.Info($"    header file has a #pragma once directive, skipping include guard modification for header file ... \"{filename}\"");
                }
            }

            // Copy lines to out, but when we have replaced the include guard start after those 2 lines
            for (; i < lines.Length; ++i)
            {
                outlines.Add(lines[i]);
            }

            return numberOfModifiedLines;
        }


        static string MakeRelative(string root, string filepath)
        {
            filepath = filepath.Substring(root.Length);
            return filepath;
        }

        static char OtherPathSeparator(char pathSeparator)
        {
            switch (pathSeparator)
            {
                case '/': return '\\';
                case '\\': return '/';
            }

            return pathSeparator;
        }

        private static string FixPath(string filepath)
        {
            filepath = filepath.Replace(OtherPathSeparator(PathSeparator), PathSeparator);
            return filepath;
        }

        static void GlobAllSourceFiles(string subPath, char pathSeparator, List<KeyValuePair<string, string>> allSourceFiles, params string[] extensions)
        {
            var rootPath = Path.Join(Environment.CurrentDirectory, subPath);
            var subPathInfo = new DirectoryInfo(rootPath);
            var oldPathSeparator = OtherPathSeparator(pathSeparator);

            foreach (var ext in extensions)
            {
                var globbed = subPathInfo.GlobFiles("**/" + ext);
                foreach (var fi in globbed)
                {
                    var filepath = MakeRelative(rootPath, fi.FullName);
                    filepath = filepath.Replace(oldPathSeparator, pathSeparator);
                    filepath = FixPath(filepath);
                    allSourceFiles.Add(new KeyValuePair<string, string>(subPath, filepath));
                }
            }
        }
    }
}
