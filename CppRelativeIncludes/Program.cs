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
            // Root folder is ROOT
            // Should we make backups of the cpp/c files that we modify ?

            // Build database of include files (*.h, *.hpp, *.inl)
            string hdr_root = @"e:\Dev.Go\src\github.com\jurgen-kluft\TRCDC\source\main\include\cdc\runtime\";
            List<string> all_header_files = GlobAllHeaderFiles(hdr_root);

            Dictionary<string, string> db_header_files = new Dictionary<string, string>();
            foreach(string hdr in all_header_files)
            {
                string fulldbkey = MakeDbKey(hdr);
                string[] dbkeyparts = fulldbkey.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string dbkey = string.Empty;
                for (int i = dbkeyparts.Length - 1; i >= 0; --i)
                {
                    if (String.IsNullOrEmpty(dbkey))
                    {
                        dbkey = dbkeyparts[i];
                        if (db_header_files.ContainsKey(dbkey))
                        {
                            // Project contains duplicate header, so make sure we are not
                            // matching by purely the filename
                            db_header_files.Remove(dbkey);
                            continue;
                        }
                    }
                    else
                    {
                        dbkey = dbkeyparts[i] + "/" + dbkey;
                    }
                    if (!db_header_files.ContainsKey(dbkey))
                    {
                        db_header_files.Add(dbkey, hdr);
                    }
                }
            }

            // Build list of source files (*.c, *.cpp)
            string cpp_root = @"e:\Dev.Go\src\github.com\jurgen-kluft\TRCDC\source\main\cpp\cdc\runtime\";
            List<string> all_source_files = GlobAllSourceFiles(cpp_root);

            // For every source file:
            foreach (string cppfile in all_source_files)
            {
                //   Read in all lines
                List<string> outlines = new List<string>();
                string filepath = Path.Combine(cpp_root, cppfile);
                string[] lines = File.ReadAllLines(filepath);

                int modifiedcnt = 0;
                string include = "#include";
                foreach (string l in lines)
                {
                    //   Analyze line for  #include "
                    string line = l.Trim(' ');
                    bool modified = false;
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
                            string dbkey_include_hdr = MakeDbKey(include_hdr);
                            if (db_header_files.TryGetValue(dbkey_include_hdr, out relative_include_hdr))
                            {
                                line = l.Replace(include_hdr, relative_include_hdr);
                                modified = true;
                            }
                            else
                            {
                                // Decompose the include path and try to find a header that matches
                                string[] dbkeyparts = dbkey_include_hdr.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                for (int i = 0; i < dbkeyparts.Length; i++)
                                {
                                    string dbkey = dbkeyparts[dbkeyparts.Length - 1];
                                    for (int j = dbkeyparts.Length - 2; j >= i; j--)
                                    {
                                        dbkey = dbkeyparts[j] + "/" + dbkey;
                                    }
                                    if (db_header_files.TryGetValue(dbkey, out relative_include_hdr))
                                    {
                                        line = l.Replace(include_hdr, relative_include_hdr);
                                        modified = true;
                                        break;
                                    }
                                }

                                Console.WriteLine("Could not find \"{0}\" in the header db", include_hdr);
                            }
                        }
                    }

                    if (modified)
                    {
                        modifiedcnt += 1;
                        outlines.Add(line);
                    }
                    else
                    {
                        outlines.Add(l);
                    }
                }

                if (modifiedcnt > 0)
                {
                    // Write out all lines if there where any modifications
                    File.WriteAllLines(filepath, outlines);
                }
            }

            // For every header file:
            foreach (string hdrfile in all_header_files)
            {
                //   Read in all lines
                List<string> outlines = new List<string>();
                string filepath = Path.Combine(hdr_root, hdrfile);
                string[] lines = File.ReadAllLines(filepath);

                int modifiedcnt = 0;
                string include = "#include";
                foreach (string l in lines)
                {
                    //   Analyze line for  #include "
                    string line = l.Trim(' ');
                    bool modified = false;
                    if (line.StartsWith(include))
                    {
                        line = line.Substring(include.Length);
                        line = line.Trim(' ');
                        if (line.StartsWith("\""))
                        {
                            // Skip the '"'
                            line = line.Substring(1);
                            string include_hdr = line.Substring(0, line.IndexOf('"'));
                            string dbkey_include_hdr = MakeDbKey(include_hdr);
                            string relative_include_hdr;
                            if (db_header_files.TryGetValue(dbkey_include_hdr, out relative_include_hdr))
                            {
                                line = l.Replace(include_hdr, relative_include_hdr);
                                modified = true;
                            }
                            else
                            {
                                // Decompose the include path and try to find a header that matches
                                string[] dbkeyparts = dbkey_include_hdr.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                for (int i = 0; i < dbkeyparts.Length; i++)
                                {
                                    string dbkey = dbkeyparts[dbkeyparts.Length - 1];
                                    for (int j = dbkeyparts.Length - 2; j >= i; j--)
                                    {
                                        dbkey = dbkeyparts[j] + "/" + dbkey;
                                    }
                                    if (db_header_files.TryGetValue(dbkey, out relative_include_hdr))
                                    {
                                        line = l.Replace(include_hdr, relative_include_hdr);
                                        modified = true;
                                        break;
                                    }
                                }

                                Console.WriteLine("Could not find \"{0}\" in the header db", include_hdr);
                            }
                        }
                    }

                    if (modified)
                    {
                        modifiedcnt += 1;
                        outlines.Add(line);
                    }
                    else
                    {
                        outlines.Add(l);
                    }
                }

                if (modifiedcnt > 0)
                {
                    // Write out all lines if there where any modifications
                    File.WriteAllLines(filepath, outlines);
                }
            }

            // REPORT
            // Report any header files that could not be detected
        }


        static string FixSlashes(string filepath)
        {
            filepath = filepath.Replace('\\', '/');
            return filepath;
        }

        static string MakeDbKey(string str)
        {
            str = str.ToLower().Replace('\\', '/');
            return str;
        }

        static string MakeRelative(string root, string filepath)
        {
            filepath = filepath.Substring(root.Length);
            return filepath;
        }

        static List<string> GlobAllHeaderFiles(string root)
        {
            var rootdirinfo = new DirectoryInfo(root);

            List<string> all = new List<string>();
            var globbed = rootdirinfo.GlobFiles("**/*.h");
            foreach (FileInfo fi in globbed)
            {
                all.Add(MakeRelative(root, fi.FullName));
            }
            globbed = rootdirinfo.GlobFiles("**/*.H");
            foreach (FileInfo fi in globbed)
            {
                all.Add(MakeRelative(root, fi.FullName));
            }
            globbed = rootdirinfo.GlobFiles("**/*.hpp");
            foreach (FileInfo fi in globbed)
            {
                all.Add(MakeRelative(root, fi.FullName));
            }
            return all;
        }

        static List<string> GlobAllSourceFiles(string root)
        {
            var rootdirinfo = new DirectoryInfo(root);

            List<string> all = new List<string>();
            var globbed = rootdirinfo.GlobFiles("**/*.c");
            foreach (FileInfo fi in globbed)
            {
                all.Add(MakeRelative(root, fi.FullName));
            }
            globbed = rootdirinfo.GlobFiles("**/*.cpp");
            foreach (FileInfo fi in globbed)
            {
                all.Add(MakeRelative(root, fi.FullName));
            }
            return all;
        }

    }
}
