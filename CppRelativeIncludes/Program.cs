using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Glob;

namespace CppRelativeIncludes
{
    class Program
    {
        static void Main(string[] args)
        {
            bool write_files = false;

            // Root folder is ROOT
            // Should we make backups of the cpp/c files that we modify ?
            IncludeHandler includes = new IncludeHandler();

            // Build database of include files (*.h, *.hpp, *.inl)
            string hdr_root = @"e:\Dev.Go\src\github.com\jurgen-kluft\TRCDC\source\main\include\cdc\runtime\";
            includes.RegisterIncludePath(hdr_root, "*.h", "*.H", "*.hpp", "*.HPP");

            includes.AddFolderRename(hdr_root, "Animation", "gameAnimation");
            includes.AddFolderRename(hdr_root, "Archive", "gameArchive");
            includes.AddFolderRename(hdr_root, "Camera", "gameCamera");
            includes.AddFolderRename(hdr_root, "Debug", "gameDebug");
            includes.AddFolderRename(hdr_root, "G2", "gameG2");
            includes.AddFolderRename(hdr_root, "Input", "gameInput");
            includes.AddFolderRename(hdr_root, "Local", "gameLocal");
            includes.AddFolderRename(hdr_root, "Monster", "gameMonster");
            includes.AddFolderRename(hdr_root, "Multibody", "gameMultibody");
            includes.AddFolderRename(hdr_root, "Objects", "gameObjects");
            includes.AddFolderRename(hdr_root, "PC", "gamePC");
            includes.AddFolderRename(hdr_root, "Physics", "gamePhysics");
            includes.AddFolderRename(hdr_root, "Player", "gamePlayer");
            includes.AddFolderRename(hdr_root, "Resolve", "gameResolve");
            includes.AddFolderRename(hdr_root, "Scene", "gameScene");
            includes.AddFolderRename(hdr_root, "Stream", "gameStream");

            // Build list of source files (*.c, *.cpp)
            string cpp_root = @"e:\Dev.Go\src\github.com\jurgen-kluft\TRCDC\source\main\cpp\cdc\runtime\";
            List<string> all_source_files = GlobAllSourceFiles(cpp_root, '/', "*.c", "*.C", "*.cpp", "*.CPP");

            // For every source file:
            foreach (string cppfile in all_source_files)
            {
                Console.WriteLine("Processing source file .. \"{0}\"", cppfile);

                //   Read in all lines
                string filepath = FixPath(Path.Combine(cpp_root, cppfile));
                string basepath = FixPath(Path.GetDirectoryName(cppfile));
                string[] lines = File.ReadAllLines(filepath);

                List<string> newlines;
                if (FixIncludes(basepath, lines, includes, out newlines))
                {
                    // Write out all lines if there where any modifications
                    if (write_files)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
            }

            // For every header file:
            List<string> all_header_files = includes.GetAllHeaderFiles();
            foreach (string hdrfile in all_header_files)
            {
                Console.WriteLine("Processing header file .. \"{0}\"", hdrfile);

                //   Read in all lines
                List<string> outlines = new List<string>();
                string filepath = FixPath(Path.Combine(hdr_root, hdrfile));
                string basepath = FixPath(Path.GetDirectoryName(hdrfile));
                string[] lines = File.ReadAllLines(filepath);

                List<string> newlines;
                if (FixIncludes(basepath, lines, includes, out newlines))
                {
                    // Write out all lines if there where any modifications
                    if (write_files)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
            }

            // REPORT
            // Report any header files that could not be detected
        }
        
        // File being process can have it's own base-path:
        // Example:
        //  - Physics/Broadphase.cpp
        //    If this file has the following includes:
        //    - #include "Broadphase.h"
        //    - #include "Collision.h"
        //
        // Where "Collision.h" also exists in the root, we need to actually get the "Collision.h" that exists in his own folder.

        static bool FixIncludes(string basepath, string[] lines, IncludeHandler includes, out List<string> outlines)
        {
            outlines = new List<string>();

            string include = "#include";
            int number_of_modified_lines = 0;

            foreach (string original_line in lines)
            {
                //   Analyze line for  #include "
                bool line_is_modified = false;
                string modified_line = original_line;

                string line = original_line.Trim(' ');
                if (line.StartsWith(include))
                {
                    line = line.Substring(include.Length);
                    line = line.Trim(' ');
                    if (line.StartsWith("\""))
                    {
                        // Skip the '"'
                        line = line.Substring(1);
                        string include_hdr = line.Substring(0, line.IndexOf('"'));
                        string relative_include_hdr;
                        if (includes.FindInclude(basepath, include_hdr, out relative_include_hdr))
                        {
                            const bool ignoreCase = true;
                            line_is_modified = String.Compare(FixPath(include_hdr), FixPath(relative_include_hdr), ignoreCase) != 0;
                            if (line_is_modified)
                            {
                                modified_line = original_line.Replace(include_hdr, relative_include_hdr);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Could not find matching include for \"{0}\".", include_hdr);
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
            filepath = filepath.Replace('\\', '/');
            return filepath;
        }

        static List<string> GlobAllSourceFiles(string root, char path_seperator, params string[] extensions)
        {
            var rootdirinfo = new DirectoryInfo(root);
            char old_path_seperator = OtherPathSeperator(path_seperator);

            List<string> all = new List<string>();
            foreach (string ext in extensions)
            {
                var globbed = rootdirinfo.GlobFiles("**/" + ext);
                foreach (FileInfo fi in globbed)
                {
                    string filepath = MakeRelative(root, fi.FullName);
                    filepath = filepath.Replace(old_path_seperator, path_seperator);
                    filepath = FixPath(filepath);
                    all.Add(filepath);
                }
            }
            return all;
        }

    }
}
