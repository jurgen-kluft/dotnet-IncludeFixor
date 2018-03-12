using System;
using System.IO;
using System.Collections.Generic;
using Glob;

namespace CppRelativeIncludes
{
    public class IncludeHandler
    {
        // Example:
        // List<string> header_files = GlobFiles(Path.Combine(rootpath, "game"), "*.h *.hpp");
        // IncludeDirectory("game", header_files);
        protected class IncludeDirectory
        {
            public string IncludePath { get; set; }
            public List<string> HeaderFiles { get; set; }
            public HeaderIncludeTree HeaderIncludeTree { get; set; }
        }
        protected class HeaderIncludeTree
        {
            public HeaderIncludeTree(string foldername)
            {
                FolderName = foldername;
                OldFolderNames = new List<string>();
                HeaderFilenames = new List<string>();
                HeaderFilepaths= new List<string>();
                SubFolders = new List<HeaderIncludeTree>();
            }

            public string FolderName { get; set; }
            public List<string> OldFolderNames { get; set; }
            public List<string> HeaderFilenames { get; set; }
            public List<string> HeaderFilepaths { get; set; }
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
                    HeaderFilenames.Add(filename);
                    HeaderFilepaths.Add(fullheaderinclude);
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

            public static void AddHeaderFile(HeaderIncludeTree tree, string headerfilepath)
            {
                Stack<string> headerinclude_stack = new Stack<string>();
                string[] headerinclude_parts = headerfilepath.Split('/');
                int n = headerinclude_parts.Length;
                for (int i=n-1; i>=0; --i)
                    headerinclude_stack.Push(headerinclude_parts[i]);
                tree.AddHeaderFile(headerinclude_stack, headerfilepath);
            }

            private bool TryGetFilename(string headerfilename, out string corrected_headerfilename)
            {
                int index = 0;
                foreach (string filename in HeaderFilenames)
                {
                    if (AreFilenamesEqual(headerfilename, filename))
                    {
                        corrected_headerfilename = HeaderFilepaths[index];
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

            private bool FindIncludeFile(Stack<string> headerinclude, Stack<string> corrected_headerinclude_stack)
            {
                string part = headerinclude.Pop();
                if (headerinclude.Count == 0)
                {
                    string filename = part;
                    string corrected_headerinclude;
                    if (TryGetFilename(filename, out corrected_headerinclude))
                    {
                        corrected_headerinclude_stack.Push(filename);
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
                        corrected_headerinclude_stack.Push(foldername);
                        return folder.FindIncludeFile(headerinclude, corrected_headerinclude_stack);
                    }
                }
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
                Stack<string> corrected_headerinclude_stack = new Stack<string>();
                if (tree.FindIncludeFile(headerinclude_stack, corrected_headerinclude_stack))
                {
                    while (corrected_headerinclude_stack.Count > 0)
                    {
                        string part = corrected_headerinclude_stack.Pop();
                        corrected_headerinclude = Path.Combine(part, corrected_headerinclude);
                    }
                    return true;
                }
                return false;
            }
        }

        List<IncludeDirectory> mIncludes;

        public IncludeHandler()
        {
            mIncludes = new List<IncludeDirectory>();
            PathSeperator = '/';
        }

        public char PathSeperator { get; set; }

        private void AddIncludePath(string includepath, HeaderIncludeTree headerfiletree, List<string> headerfiles)
        {
            IncludeDirectory id = new IncludeDirectory() { IncludePath = includepath, HeaderIncludeTree = headerfiletree, HeaderFiles = headerfiles };
            mIncludes.Add(id);
        }

        public void AddFolderRename(string includepath, string original, string renamed)
        {

            string[] original_parts = FixPath(original).Split(PathSeperator);
            string[] renamed_parts = FixPath(renamed).Split(PathSeperator);

            foreach(IncludeDirectory dir in mIncludes)
            {
                if (dir.IncludePath == includepath)
                {
                    HeaderIncludeTree headertree = dir.HeaderIncludeTree;
                    for (int i = 0; i < original_parts.Length && i < renamed_parts.Length; ++i)
                    {
                        headertree = headertree.AddFolderRename(original_parts[i], renamed_parts[i]);
                    }
                    break;
                }
            }
        }

        public void RegisterIncludePath(string includepath, params string[] headerfile_extensions)
        {
            // Glob header files
            var rootdirinfo = new DirectoryInfo(includepath);
            char old_path_seperator = OtherPathSeperator(PathSeperator);

            List<string> headerfiles = new List<string>();
            foreach (string extension in headerfile_extensions)
            {
                var globbed = rootdirinfo.GlobFiles("**/" + extension);
                foreach (FileInfo fi in globbed)
                {
                    string filepath = MakeRelative(includepath, fi.FullName);
                    filepath = filepath.Replace(old_path_seperator, PathSeperator);
                    headerfiles.Add(filepath);
                }
            }

            HeaderIncludeTree headerincludetree = new HeaderIncludeTree(includepath);
            foreach (string hdr in headerfiles)
            {
                HeaderIncludeTree.AddHeaderFile(headerincludetree, hdr);
            }

            AddIncludePath(includepath, headerincludetree, headerfiles);
        }

        private List<string> GetRenamesOf(string hdr)
        {
            List<string> renames = new List<string>();
            renames.Add(hdr);

            return renames;
        }


        public List<string> GetAllHeaderFiles()
        {
            List<string> hdrfiles = new List<string>();
            foreach (IncludeDirectory include in mIncludes)
            {
                hdrfiles.AddRange(include.HeaderFiles);
            }
            return hdrfiles;
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
