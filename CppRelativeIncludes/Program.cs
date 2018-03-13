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
            IncludeFixer includefixer = new IncludeFixer();

            // The order of registration does matter, includes are search in this order
            string dx9_root = @"e:\trae\cdc\3rdparty\DX9SDK\include\";
            includefixer.RegisterIncludePathAsReadonly(dx9_root, "*.h", "*.H", "*.hpp", "*.HPP");

            // Build database of include files (*.h, *.hpp, *.inl)
            string cdc_root = @"e:\Dev.Go\src\github.com\jurgen-kluft\TRCDC\source\main\include\cdc\runtime\";
            includefixer.RegisterIncludePath(cdc_root, "*.h", "*.H", "*.hpp", "*.HPP");

            bool rename_ok = includefixer.AddFileRename(cdc_root, "cdcMath/math.h", "cdcMath/sysMath.h");

            string game_root = @"e:\Dev.Go\src\github.com\jurgen-kluft\TRCDC\source\main\include\game\";
            includefixer.RegisterIncludePath(game_root, "*.h", "*.H", "*.hpp", "*.HPP");
            includefixer.AddFolderRename(game_root, "Animation", "gameAnimation");
            includefixer.AddFolderRename(game_root, "Archive", "gameArchive");
            includefixer.AddFolderRename(game_root, "Camera", "gameCamera");
            includefixer.AddFolderRename(game_root, "Debug", "gameDebug");
            includefixer.AddFolderRename(game_root, "enemy", "gameEnemy");
            includefixer.AddFolderRename(game_root, "G2", "gameG2");
            includefixer.AddFolderRename(game_root, "Input", "gameInput");
            includefixer.AddFolderRename(game_root, "Local", "gameLocal");
            includefixer.AddFolderRename(game_root, "Monster", "gameMonster");
            includefixer.AddFolderRename(game_root, "Multibody", "gameMultibody");
            includefixer.AddFolderRename(game_root, "Objects", "gameObjects");
            includefixer.AddFolderRename(game_root, "PC", "gamePC");
            includefixer.AddFolderRename(game_root, "Physics", "gamePhysics");
            includefixer.AddFolderRename(game_root, "Player", "gamePlayer");
            includefixer.AddFolderRename(game_root, "Resolve", "gameResolve");
            includefixer.AddFolderRename(game_root, "Scene", "gameScene");
            includefixer.AddFolderRename(game_root, "Stream", "gameStream");
            includefixer.AddFolderRename(game_root, "menu", "gameMenu");
            includefixer.AddFolderRename(game_root, "padshock", "gamePadshock");
            includefixer.AddFolderRename(game_root, "ReaverGUI", "gameReaverGUI");
            includefixer.AddFolderRename(game_root, "Save", "gameSave");
            includefixer.AddFolderRename(game_root, "script", "gameScript");
            includefixer.AddFolderRename(game_root, "sound", "gameSound");
            includefixer.AddFolderRename(game_root, "VehicleSection", "gameVehicleSection");
            includefixer.AddFolderRename(game_root, "WorldRep", "gameWorldRep");

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
                if (FixIncludes(basepath, lines, includefixer, out newlines))
                {
                    // Write out all lines if there where any modifications
                    if (write_files)
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
                Console.WriteLine("Processing header file .. \"{0}\"", hdrfile.Value);

                //   Read in all lines
                List<string> outlines = new List<string>();
                string filepath = FixPath(Path.Combine(hdrfile.Key, hdrfile.Value));
                string basepath = FixPath(Path.GetDirectoryName(hdrfile.Value));
                string[] lines = File.ReadAllLines(filepath);

                List<string> newlines;
                if (FixIncludes(basepath, lines, includefixer, out newlines))
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

        static bool FixIncludes(string basepath, string[] lines, IncludeFixer includes, out List<string> outlines)
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
