using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Glob;

namespace IncludeFixor
{
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
            IncludeFixer includefixer = new IncludeFixer();

            // Read the configuration
            Config config;
            if (args.Length == 0 || !Config.Read(args[0], out config))
            {
                Console.WriteLine("IncludeFixor v1.0, 2018, Virtuos Games");
                Console.WriteLine("   A utility to adjust/fix/manage include directives of a C++ codebase.");
                Console.WriteLine("");
                Console.WriteLine("    IncludeFixor {INPUT FILE}     (e.g. 'IncludeFixor myconfig.json')");
                Console.WriteLine("");
                return -1;
            }
            Verbose = config.Settings.Verbose;
            PathSeparator = config.Settings.PathSeparator;

            foreach (Include inc in config.Includes)
            {
                includefixer.RegisterIncludePath(inc.Path, inc.ReadOnly, inc.Extensions);

                foreach (Rename rn in inc.FileRenames)
                    includefixer.AddFileRename(inc.Path, rn.From, rn.To);
                foreach (Rename rn in inc.FolderRenames)
                    includefixer.AddFolderRename(inc.Path, rn.From, rn.To);
            }

            // Build list of source files (*.c, *.cpp)
            List<KeyValuePair<string, string>> all_source_files = new List<KeyValuePair<string, string>>();
            foreach (Source src in config.Sources)
            {
                GlobAllSourceFiles(src.Path, config.Settings.PathSeparator, all_source_files, src.Extensions);
            }

            // Compile our regulare expression to find include directives
            Regex include_regex = new Regex(config.Settings.IncludeRegex, RegexOptions.Compiled);

            // For every source file:
            foreach (KeyValuePair<string, string> cppfile in all_source_files)
            {
                if (Verbose)
                {
                    Console.WriteLine("Processing source file ... \"{0}\"", cppfile.Value);
                }

                //   Read in all lines
                string filepath = FixPath(Path.Combine(cppfile.Key, cppfile.Value));
                string basepath = FixPath(Path.GetDirectoryName(cppfile.Value));
                string[] lines = File.ReadAllLines(filepath);

                List<string> newlines;
                if (FixIncludes(basepath, cppfile.Value, lines, includefixer, include_regex, out newlines))
                {
                    // Write out all lines if there where any modifications
                    if (!config.Settings.DryRun)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
            }

            // For every header file that is not read-only:
            List<KeyValuePair<string, string>> all_header_files = new List<KeyValuePair<string, string>>();
            Action<string, string, bool> collector = delegate (string rootpath, string relative_filepath, bool isreadonly)
            {
                if (!isreadonly)
                    all_header_files.Add(new KeyValuePair<string, string>(rootpath, relative_filepath));
            };
            includefixer.GetAllHeaderFilesToFix(collector);

            foreach (KeyValuePair<string,string> hdrfile in all_header_files)
            {
                if (Verbose)
                {
                    Console.WriteLine("Processing header file ... \"{0}\"", hdrfile.Value);
                }

                //   Read in all lines
                List<string> outlines = new List<string>();
                string filepath = FixPath(Path.Combine(hdrfile.Key, hdrfile.Value));
                string basepath = FixPath(Path.GetDirectoryName(hdrfile.Value));
                string[] lines = File.ReadAllLines(filepath);

                List<string> newlines;
                if (FixIncludes(basepath, hdrfile.Value, lines, includefixer, include_regex, out newlines))
                {
                    // Write out all lines if there where any modifications
                    if (!config.Settings.DryRun)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
            }

            return 0;
        }

        // File being process can have it's own base-path:
        // Example:
        //  - Physics/Broadphase.cpp
        //    If this file has the following includes:
        //    - #include "Broadphase.h"
        //    - #include "Collision.h"
        //
        // Where "Collision.h" also exists in the root, we need to actually get the "Collision.h" that exists in his own folder.

        static bool FixIncludes(string basepath, string filename, string[] lines, IncludeFixer includes, Regex include_regex, out List<string> outlines)
        {
            outlines = new List<string>();

            int number_of_modified_lines = 0;
            int line_number = 0;

            foreach (string original_line in lines)
            {
                //   Analyze line for  #include "
                bool line_is_modified = false;
                string modified_line = original_line;

                Match regex_match = include_regex.Match(original_line);
                if (regex_match.Success)
                {
                    var groups = regex_match.Groups;
                    if (groups.Count == 4 && groups[1].Value == "\"" && groups[3].Value == "\"")
                    {
                        string include_hdr = groups[2].Value.Trim();
                        if (includes.FindInclude(basepath, include_hdr, out string relative_include_hdr))
                        {
                            relative_include_hdr = FixPath(relative_include_hdr);

                            const bool ignoreCase = true;
                            line_is_modified = String.Compare(include_hdr, relative_include_hdr, ignoreCase) != 0;
                            if (line_is_modified)
                            {
                                modified_line = original_line.Replace(include_hdr, relative_include_hdr);
                                if (Verbose)
                                {
                                    Console.WriteLine("    changed:\"{0}\", line({1}): \"{2}\"  -->  \"{3}\".", filename, line_number, original_line, modified_line);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("    warning:\"{0}\", line({1}): Could not find matching include for \"{2}\".", filename, line_number, include_hdr);
                        }
                    }
                }

                if (line_is_modified)
                {
                    number_of_modified_lines += 1;
                    outlines.Add(modified_line);
                }
                else
                {
                    outlines.Add(original_line);
                }

                line_number += 1;
            }

            return number_of_modified_lines > 0;
        }

        static string MakeRelative(string root, string filepath)
        {
            filepath = filepath.Substring(root.Length);
            return filepath;
        }

        static char OtherPathSeperator(char path_seperator)
        {
            switch (path_seperator)
            {
                case '/': return '\\';
                case '\\': return '/';
            }
            return path_seperator;
        }
        static private string FixPath(string filepath)
        {
            filepath = filepath.Replace(OtherPathSeperator(PathSeparator), PathSeparator);
            return filepath;
        }

        static void GlobAllSourceFiles(string root, char path_seperator, List<KeyValuePair<string, string>> all_source_files, params string[] extensions)
        {
            var rootdirinfo = new DirectoryInfo(root);
            char old_path_seperator = OtherPathSeperator(path_seperator);

            foreach (string ext in extensions)
            {
                var globbed = rootdirinfo.GlobFiles("**/" + ext);
                foreach (FileInfo fi in globbed)
                {
                    string filepath = MakeRelative(root, fi.FullName);
                    filepath = filepath.Replace(old_path_seperator, path_seperator);
                    filepath = FixPath(filepath);
                    all_source_files.Add(new KeyValuePair<string,string>(root, filepath));
                }
            }
        }

    }
}
