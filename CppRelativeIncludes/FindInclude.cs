using System;
using System.IO;
using System.Collections.Generic;
using Glob;

namespace CppRelativeIncludes
{
    public class IncludeFixer
    {
        // Example:
        // List<string> header_files = GlobFiles(Path.Combine(rootpath, "game"), "*.h *.hpp");
        // IncludeDirectory("game", header_files);
        protected class IncludeDirectory
        {
            public string IncludePath { get; set; }
            public bool IsReadonly { get; set; }
            public List<string> HeaderFiles { get; set; }
            public HeaderIncludeTree HeaderIncludeTree { get; set; }
            public Dictionary<string, List<string>> HeaderFilenameDB { get; set; }
        }
        protected class HeaderIncludeTree
        {
            public HeaderIncludeTree(string foldername)
            {
                FolderName = foldername;
                OldFolderNames = new List<string>();
                Filenames = new List<string>();
                OldFilenames = new List<string>();
                Filepaths = new List<string>();
                SubFolders = new List<HeaderIncludeTree>();
            }

            public string FolderName { get; set; }
            public List<string> OldFolderNames { get; set; }
            public List<string> Filenames { get; set; }
            public List<string> OldFilenames { get; set; }
            public List<string> Filepaths { get; set; }
            public List<HeaderIncludeTree> SubFolders { get; set; }

            private static bool AreFoldernamesEqual(string foldername, string other_foldername)
            {
                return String.Compare(foldername, other_foldername, true) == 0;
            }
            private static bool AreFilenamesEqual(string filename, string other_filename)
            {
                return String.Compare(filename, other_filename, true) == 0;
            }

            private bool IsFolderName(string foldername)
            {
                if (AreFoldernamesEqual(foldername, FolderName))
                    return true;
                foreach(string old in OldFolderNames)
                {
                    if (AreFoldernamesEqual(foldername, FolderName))
                        return true;
                }
                return false;
            }

            public HeaderIncludeTree AddFolderRename(string from, string name)
            {
                foreach(HeaderIncludeTree sub in SubFolders)
                {
                    if (sub.IsFolderName(name))
                    {
                        sub.OldFolderNames.Add(from);
                        return sub;
                    }
                }

                HeaderIncludeTree newsub = new HeaderIncludeTree(name);
                newsub.OldFolderNames.Add(from);
                SubFolders.Add(newsub);
                return newsub;
            }

            public bool AddHeaderFile(Stack<string> headerinclude, string fullheaderinclude)
            {
                string part = headerinclude.Pop();
                if (headerinclude.Count == 0)
                {
                    string filename = part;
                    Filenames.Add(filename);
                    Filepaths.Add(fullheaderinclude);
                    return true;
                }
                else
                {
                    string foldername = part;
                    HeaderIncludeTree folder;
                    if (!TryGetFolder(foldername, out folder))
                    {
                        folder = new HeaderIncludeTree(foldername);
                        SubFolders.Add(folder);
                    }
                    return folder.AddHeaderFile(headerinclude, fullheaderinclude);
                }
            }

            public static void AddHeaderFile(HeaderIncludeTree tree, string headerfilepath, string store_headerfilepath)
            {
                Stack<string> headerinclude_stack = new Stack<string>();
                string[] headerinclude_parts = headerfilepath.Split('/');
                int n = headerinclude_parts.Length;
                for (int i=n-1; i>=0; --i)
                    headerinclude_stack.Push(headerinclude_parts[i]);
                tree.AddHeaderFile(headerinclude_stack, store_headerfilepath);
            }

            private bool TryGetFilename(string headerfilename, out string corrected_headerfilename)
            {
                int index = 0;
                foreach (string filename in Filenames)
                {
                    if (AreFilenamesEqual(headerfilename, filename))
                    {
                        corrected_headerfilename = Filepaths[index];
                        return true;
                    }
                    index += 1;
                }
                corrected_headerfilename = headerfilename;
                return false;
            }
            private bool TryGetFolder(string foldername, out HeaderIncludeTree subfolder)
            {
                foreach (HeaderIncludeTree sub in SubFolders)
                {
                    if (sub.IsFolderName(foldername))
                    {
                        subfolder = sub;
                        return true;
                    }
                }
                subfolder = null;
                return false;
            }

            private bool FindIncludeFile(Stack<string> headerinclude, out string corrected_headerinclude)
            {
                string part = headerinclude.Pop();
                if (headerinclude.Count == 0)
                {
                    string filename = part;
                    if (TryGetFilename(filename, out corrected_headerinclude))
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    string foldername = part;
                    HeaderIncludeTree folder;
                    if (TryGetFolder(foldername, out folder))
                    {
                        return folder.FindIncludeFile(headerinclude, out corrected_headerinclude);
                    }
                }
                corrected_headerinclude = string.Empty;
                return false;
            }

            public static bool FindIncludeFile(HeaderIncludeTree tree, string headerinclude, out string corrected_headerinclude)
            {
                Stack<string> headerinclude_stack = new Stack<string>();
                string[] headerinclude_parts = headerinclude.Split('/');
                int n = headerinclude_parts.Length;
                for (int i = n - 1; i >= 0; --i)
                    headerinclude_stack.Push(headerinclude_parts[i]);

                corrected_headerinclude = string.Empty;
                if (tree.FindIncludeFile(headerinclude_stack, out corrected_headerinclude))
                {
                    return true;
                }
                return false;
            }
        }

        List<IncludeDirectory> mIncludes;

        public IncludeFixer()
        {
            mIncludes = new List<IncludeDirectory>();
            PathSeperator = '/';
        }

        public char PathSeperator { get; set; }

        private IncludeDirectory AddIncludePath(string includepath, List<string> headerfiles, HeaderIncludeTree headerfiletree, Dictionary<string, List<string>> headerFilenameDB)
        {
            IncludeDirectory id = new IncludeDirectory() {
                IncludePath = includepath,
                HeaderFiles = headerfiles,
                HeaderIncludeTree = headerfiletree,
                HeaderFilenameDB = headerFilenameDB
            };
            mIncludes.Add(id);
            return id;
        }
        public bool AddFileRename(string includepath, string old, string current)
        {
            foreach (IncludeDirectory dir in mIncludes)
            {
                if (dir.IncludePath == includepath)
                {
                    string stored;
                    if (HeaderIncludeTree.FindIncludeFile(dir.HeaderIncludeTree, current, out stored))
                    {
                        if (!HeaderIncludeTree.FindIncludeFile(dir.HeaderIncludeTree, old, out stored))
                        {
                            // OK, current exists in the tree, which is correct, and old does not exist in the tree.
                            HeaderIncludeTree.AddHeaderFile(dir.HeaderIncludeTree, old, current);
                            return true;
                        }
                    }
                    break;
                }
            }
            return false;
        }

        public void AddFolderRename(string includepath, string original, string renamed)
        {
            foreach(IncludeDirectory dir in mIncludes)
            {
                if (dir.IncludePath == includepath)
                {
                    string[] original_parts = FixPath(original).Split(PathSeperator);
                    string[] renamed_parts = FixPath(renamed).Split(PathSeperator);

                    HeaderIncludeTree headertree;

                    // Traverse down the include tree folder by folder

                    // Find 'original' and get 'renamed'
                    headertree = dir.HeaderIncludeTree;
                    for (int i = 0; i < original_parts.Length && i < renamed_parts.Length; ++i)
                    {
                        headertree = headertree.AddFolderRename(original_parts[i], renamed_parts[i]);
                    }

                    // Find 'renamed' and get 'renamed'
                    headertree = dir.HeaderIncludeTree;
                    for (int i = 0; i < original_parts.Length && i < renamed_parts.Length; ++i)
                    {
                        headertree = headertree.AddFolderRename(renamed_parts[i], original_parts[i]);
                    }
                    break;
                }
            }
        }

        public void RegisterIncludePath(string includepath, bool isreadonly, string[] headerfile_extensions)
        {
            // Glob header files
            IncludeDirectory id = ProcessIncludePath(includepath, headerfile_extensions);
            id.IsReadonly = isreadonly;
        }

        private IncludeDirectory ProcessIncludePath(string includepath, params string[] file_extensions)
        {
            var rootdirinfo = new DirectoryInfo(includepath);
            char old_path_seperator = OtherPathSeperator(PathSeperator);

            List<string> headerfiles = new List<string>();
            foreach (string extension in file_extensions)
            {
                var globbed = rootdirinfo.GlobFiles("**/" + extension);
                foreach (FileInfo fi in globbed)
                {
                    string filepath = MakeRelative(includepath, fi.FullName);
                    filepath = filepath.Replace(old_path_seperator, PathSeperator);
                    headerfiles.Add(filepath);
                }
            }

            Dictionary<string, List<string>> headerincludedb = new Dictionary<string, List<string>>();
            HeaderIncludeTree headerincludetree = new HeaderIncludeTree(includepath);
            foreach (string hdr in headerfiles)
            {
                HeaderIncludeTree.AddHeaderFile(headerincludetree, hdr, hdr);

                string dbkey = AsDictionaryKey(Path.GetFileName(hdr));
                List<string> filepaths = new List<string>();
                if (!headerincludedb.TryGetValue(dbkey, out filepaths))
                {
                    filepaths = new List<string>();
                    headerincludedb.Add(dbkey, filepaths);
                }
                filepaths.Add(hdr);
            }

            return AddIncludePath(includepath, headerfiles, headerincludetree, headerincludedb);
        }

        private List<string> GetRenamesOf(string hdr)
        {
            List<string> renames = new List<string>();
            renames.Add(hdr);

            return renames;
        }


        public void GetAllHeaderFilesToFix(Action<string, string, bool> collect)
        {
            List<string> hdrfiles = new List<string>();
            foreach (IncludeDirectory include in mIncludes)
            {
                foreach (string hdr in include.HeaderFiles)
                {
                    collect(include.IncludePath, hdr, include.IsReadonly);
                }
            }
        }

        public bool FindInclude(string currentpath, string headerinclude, out string resulting_headerinclude)
        {
            resulting_headerinclude = headerinclude;

            //   1) Try to find it relative to currentpath
            string current_headerinclude = AsDictionaryKey(Path.Combine(currentpath, headerinclude));
            foreach (IncludeDirectory include in mIncludes)
            {
                if (HeaderIncludeTree.FindIncludeFile(include.HeaderIncludeTree, current_headerinclude, out resulting_headerinclude))
                {
                    resulting_headerinclude = FixPath(resulting_headerinclude);
                    return true;
                }
            }

            //   2) Try to find it in every include path
            current_headerinclude = AsDictionaryKey(headerinclude);
            foreach (IncludeDirectory include in mIncludes)
            {
                if (HeaderIncludeTree.FindIncludeFile(include.HeaderIncludeTree, current_headerinclude, out resulting_headerinclude))
                {
                    resulting_headerinclude = FixPath(resulting_headerinclude);
                    return true;
                }
            }

            //   3) Find the header file by name to 
            current_headerinclude = AsDictionaryKey(Path.GetFileName(headerinclude));
            foreach (IncludeDirectory include in mIncludes)
            {
                List<string> candidates;
                if (include.HeaderFilenameDB.TryGetValue(current_headerinclude, out candidates))
                {
                    resulting_headerinclude = FixPath(candidates[0]);
                    return true;
                }
            }

            // If nothing found report it as include-not-found
            return false;
        }

        private string FixPath(string filepath)
        {
            char old_path_seperator = OtherPathSeperator(PathSeperator);
            filepath = filepath.Replace(old_path_seperator, PathSeperator);
            return filepath;
        }

        private string AsDictionaryKey(string filepath)
        {
            filepath = FixPath(filepath);
            filepath = filepath.ToLower();
            return filepath;
        }

        private static int SortFilesByDepth(string t1, string t2)
        {
            string[] d1 = t1.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string[] d2 = t2.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (d1.Length < d2.Length)
                return 1;
            if (d1.Length > d2.Length)
                return -1;
            return 0;
        }

        private static char OtherPathSeperator(char path_seperator)
        {
            switch (path_seperator)
            {
                case '/': return '\\';
                case '\\': return '/';
            }
            return path_seperator;
        }

        private static string MakeRelative(string root, string filepath)
        {
            filepath = filepath.Substring(root.Length);
            return filepath;
        }
    }
}
