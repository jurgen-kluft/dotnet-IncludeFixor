using System;
using System.IO;
using System.Collections.Generic;
using Glob;

namespace IncludeFixor
{
    public class IncludeFixer
    {
        // Example:
        // List<string> header_files = GlobFiles(Path.Combine(rootPath, "game"), "*.h *.hpp");
        // IncludeDirectory("game", header_files);
        private class IncludeDirectory
        {
			public Include Include { get; set; }
			public List<string> HeaderFiles { get; set; } = new ();
			public HeaderIncludeTree HeaderIncludeTree { get; set; }
            public Dictionary<string, List<string>> HeaderFilenameDb { get; set; }
        }
        protected class HeaderIncludeTree
        {
            public HeaderIncludeTree(string folderName)
            {
                FolderName = folderName;
                OldFolderNames = new List<string>();
                Filenames = new List<string>();
                OldFilenames = new List<string>();
                FilePaths = new List<string>();
                SubFolders = new List<HeaderIncludeTree>();
            }

            private string FolderName { get; set; }
            private List<string> OldFolderNames { get; set; }
            private List<string> Filenames { get; set; }
            public List<string> OldFilenames { get; set; }
            private List<string> FilePaths { get; set; }
            private List<HeaderIncludeTree> SubFolders { get; set; }

            private static bool AreFolderNamesEqual(string folderName, string otherFolderName)
            {
                return string.Compare(folderName, otherFolderName, StringComparison.OrdinalIgnoreCase) == 0;
            }
            private static bool AreFileNamesEqual(string filename, string otherFilename)
            {
                return string.Compare(filename, otherFilename, StringComparison.OrdinalIgnoreCase) == 0;
            }

            private bool IsFolderName(string folderName)
            {
                if (AreFolderNamesEqual(folderName, FolderName))
                    return true;
                foreach(var old in OldFolderNames)
                {
                    if (AreFolderNamesEqual(folderName, FolderName))
                        return true;
                }
                return false;
            }

            public HeaderIncludeTree AddFolderRename(string from, string name)
            {
                foreach(var sub in SubFolders)
                {
                    if (sub.IsFolderName(name))
                    {
                        sub.OldFolderNames.Add(from);
                        return sub;
                    }
                }

                var newSub = new HeaderIncludeTree(name);
                newSub.OldFolderNames.Add(from);
                SubFolders.Add(newSub);
                return newSub;
            }

            private bool AddHeaderFile(Stack<string> headerinclude, string fullheaderinclude)
            {
                var part = headerinclude.Pop();
                if (headerinclude.Count == 0)
                {
                    Filenames.Add(part);
                    FilePaths.Add(fullheaderinclude);
                    return true;
                }
                else
                {
                    if (!TryGetFolder(part, out var folder))
                    {
                        folder = new HeaderIncludeTree(part);
                        SubFolders.Add(folder);
                    }
                    return folder.AddHeaderFile(headerinclude, fullheaderinclude);
                }
            }

            public static void AddHeaderFile(HeaderIncludeTree tree, string headerFilePath, string storeHeaderFilePath)
            {
                var headerIncludeStack = new Stack<string>();
                var headerIncludeParts = headerFilePath.Split('/');
                var n = headerIncludeParts.Length;
                for (var i=n-1; i>=0; --i)
                    headerIncludeStack.Push(headerIncludeParts[i]);
                tree.AddHeaderFile(headerIncludeStack, storeHeaderFilePath);
            }

            private bool TryGetFilename(string headerFilename, out string correctedHeaderFilename)
            {
                var index = 0;
                foreach (var filename in Filenames)
                {
                    if (AreFileNamesEqual(headerFilename, filename))
                    {
                        correctedHeaderFilename = FilePaths[index];
                        return true;
                    }
                    index += 1;
                }
                correctedHeaderFilename = headerFilename;
                return false;
            }
            private bool TryGetFolder(string foldername, out HeaderIncludeTree subfolder)
            {
                foreach (var sub in SubFolders)
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

            private bool FindIncludeFile(Stack<string> headerinclude, out string correctedHeaderinclude)
            {
                var part = headerinclude.Pop();
                if (headerinclude.Count == 0)
                {
                    if (TryGetFilename(part, out correctedHeaderinclude))
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    if (TryGetFolder(part, out var folder))
                    {
                        return folder.FindIncludeFile(headerinclude, out correctedHeaderinclude);
                    }
                }
                correctedHeaderinclude = string.Empty;
                return false;
            }

            public static bool FindIncludeFile(HeaderIncludeTree tree, string headerinclude, out string correctedHeaderinclude)
            {
                var headerIncludeStack = new Stack<string>();
                var headerIncludeParts = headerinclude.Split('/');
                var n = headerIncludeParts.Length;
                for (var i = n - 1; i >= 0; --i)
                    headerIncludeStack.Push(headerIncludeParts[i]);

                correctedHeaderinclude = string.Empty;
                return tree.FindIncludeFile(headerIncludeStack, out correctedHeaderinclude);
            }
        }

        List<IncludeDirectory> _mIncludes = new();

        private char PathSeparator { get; set; } = '/';

        private IncludeDirectory AddIncludePath(Include include, List<string> headerfiles, HeaderIncludeTree headerfiletree, Dictionary<string, List<string>> headerFilenameDb)
        {
            var id = new IncludeDirectory() {
                Include = include,
                HeaderFiles = headerfiles,
                HeaderIncludeTree = headerfiletree,
                HeaderFilenameDb = headerFilenameDb
            };

            _mIncludes.Add(id);
            return id;
        }
        public bool AddFileRename(string includePath, string old, string current)
        {
            foreach (var dir in _mIncludes)
            {
                if (dir.Include.IncludePath == includePath)
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

        public void AddFolderRename(string includePath, string original, string renamed)
        {
            foreach(var dir in _mIncludes)
            {
                if (dir.Include.IncludePath == includePath)
                {
                    var originalParts = FixPath(original).Split(PathSeparator);
                    var renamedParts = FixPath(renamed).Split(PathSeparator);

                    // Traverse down the include tree folder by folder

                    // Find 'original' and get 'renamed'
                    var headerTree = dir.HeaderIncludeTree;
                    for (var i = 0; i < originalParts.Length && i < renamedParts.Length; ++i)
                    {
                        headerTree = headerTree.AddFolderRename(originalParts[i], renamedParts[i]);
                    }

                    // Find 'renamed' and get 'renamed'
                    headerTree = dir.HeaderIncludeTree;
                    for (var i = 0; i < originalParts.Length && i < renamedParts.Length; ++i)
                    {
                        headerTree = headerTree.AddFolderRename(renamedParts[i], originalParts[i]);
                    }
                    break;
                }
            }
        }

        public void RegisterIncludePath(Include include)
        {
			var scannerPath = include.ScannerPath;
			var includePath = include.IncludePath;
			var fileExtensions = include.Extensions;

			var rootDirInfo = new DirectoryInfo(Path.Join(Environment.CurrentDirectory, scannerPath));
            var oldPathSeparator = OtherPathSeparator(PathSeparator);

            var headerFiles = new List<string>();
            foreach (var extension in fileExtensions)
            {
                var globbed = rootDirInfo.GlobFiles("**/" + extension);
                foreach (var fi in globbed)
                {
                    var filepath = MakeRelative(rootDirInfo.FullName, fi.FullName);
					filepath = filepath.Replace(oldPathSeparator, PathSeparator);
                    headerFiles.Add(filepath);
                }
            }

            var headerIncludeDb = new Dictionary<string, List<string>>();
            var headerIncludeTree = new HeaderIncludeTree(scannerPath);
            foreach (var originalHdr in headerFiles)
            {
				var updatedHdr = Join(includePath, originalHdr);

				HeaderIncludeTree.AddHeaderFile(headerIncludeTree, originalHdr, updatedHdr);

                var dbKey = AsDictionaryKey(Path.GetFileName(originalHdr));
                if (!headerIncludeDb.TryGetValue(dbKey, out var filePaths))
                {
                    filePaths = new List<string>();
                    headerIncludeDb.Add(dbKey, filePaths);
                }
                filePaths.Add(originalHdr);
            }

			AddIncludePath(include, headerFiles, headerIncludeTree, headerIncludeDb);
		}

		private List<string> GetRenamesOf(string hdr)
        {
            var renames = new List<string> { hdr };
            return renames;
        }

		public void ForeachHeaderFile(Action<string, string> action)
		{
			//var hdrFiles = new List<string>();
			foreach (var include in _mIncludes)
			{
				foreach (var hdr in include.HeaderFiles)
				{
					action(include.Include.ScannerPath, hdr);
				}
			}
		}

		public void ForeachHeaderFileThatNeedIncludeDirFix(Action<string, string> action)
		{
			foreach (var include in _mIncludes)
			{
				if (include.Include.ReadOnly == false)
				{
					foreach (var hdr in include.HeaderFiles)
                    {
                        if (!File.Exists(Path.Join(include.Include.ScannerPath, hdr)))
                            continue;

						action(include.Include.ScannerPath, hdr);
					}
				}
			}
		}
		public void ForeachHeaderFileThatNeedIncludeGuardFix(bool verbose, Action<string, string, bool, IncludeGuards> action)
		{
			foreach (var include in _mIncludes)
			{
				if (include.Include.IncludeGuards != null && !String.IsNullOrEmpty(include.Include.IncludeGuards.Prefix))
				{
					var includeGuardPrefix = include.Include.IncludeGuards.Prefix;
					foreach (var hdr in include.HeaderFiles)
					{
                        if (!File.Exists(Path.Join(include.Include.ScannerPath, hdr)))
                            continue;
						action(include.Include.ScannerPath, hdr, verbose, include.Include.IncludeGuards);
					}
				}
			}
		}

		public bool FindInclude(string currentPath, string headerInclude, out string resultingHeaderInclude)
        {
            resultingHeaderInclude = headerInclude;

            //   1) Try to find it relative to currentPath
            var currentHeaderInclude = AsDictionaryKey(Path.Combine(currentPath, headerInclude));
            foreach (var include in _mIncludes)
            {
                if (HeaderIncludeTree.FindIncludeFile(include.HeaderIncludeTree, currentHeaderInclude, out resultingHeaderInclude))
                {
                    resultingHeaderInclude = FixPath(resultingHeaderInclude);
                    return true;
                }
            }

            //   2) Try to find it in every include path
            currentHeaderInclude = AsDictionaryKey(headerInclude);
            foreach (var include in _mIncludes)
            {
                if (HeaderIncludeTree.FindIncludeFile(include.HeaderIncludeTree, currentHeaderInclude, out resultingHeaderInclude))
                {
                    resultingHeaderInclude = FixPath(resultingHeaderInclude);
                    return true;
                }
            }

            //   3) Find the header file by name to
            currentHeaderInclude = AsDictionaryKey(Path.GetFileName(headerInclude));
            foreach (var include in _mIncludes)
            {
                if (include.HeaderFilenameDb.TryGetValue(currentHeaderInclude, out var candidates))
                {
                    resultingHeaderInclude = FixPath(candidates[0]);
                    return true;
                }
            }

            // If nothing found report it as include-not-found
            return false;
        }

        private string FixPath(string filepath)
        {
            var oldPathSeparator = OtherPathSeparator(PathSeparator);
            filepath = filepath.Replace(oldPathSeparator, PathSeparator);
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
            var d1 = t1.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var d2 = t2.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (d1.Length < d2.Length)
                return 1;
            if (d1.Length > d2.Length)
                return -1;
            return 0;
        }

        private static char OtherPathSeparator(char pathSeparator)
        {
            switch (pathSeparator)
            {
                case '/': return '\\';
                case '\\': return '/';
            }
            return pathSeparator;
        }

		private static string MakeRelative(string root, string filepath)
		{
			filepath = filepath.Substring(root.Length);
			return filepath;
		}
		private static string Join(string rootPath, string filepath)
		{
			var newPath = Path.Combine(rootPath, filepath);
			return newPath;
		}
	}
}
