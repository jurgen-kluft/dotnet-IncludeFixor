using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Glob
{
    public static class GlobExtensions
    {
        public static IEnumerable<DirectoryInfo> GlobDirectories(this DirectoryInfo di, string pattern)
        {
            var glob = new Glob(pattern, GlobOptions.Compiled);
            var truncateLength = di.FullName.Length + 1;
            if (!di.Exists)
                return Array.Empty<DirectoryInfo>();
            return di.EnumerateDirectories("*", SearchOption.AllDirectories).Where(info => glob.IsMatch(info.FullName.Remove(0, truncateLength)));
        }

        public static IEnumerable<FileInfo> GlobFiles(this DirectoryInfo di, string pattern)
        {
            var glob = new Glob(pattern, GlobOptions.Compiled);
            var truncateLength = di.FullName.Length + 1;
            if (!di.Exists)
                return Array.Empty<FileInfo>();
            return di.EnumerateFiles("*", SearchOption.AllDirectories).Where(info => glob.IsMatch(info.FullName.Remove(0, truncateLength)));
        }

        public static IEnumerable<FileSystemInfo> GlobFileSystemInfos(this DirectoryInfo di, string pattern)
        {
            var glob = new Glob(pattern, GlobOptions.Compiled);
            var truncateLength = di.FullName.Length + 1;
            if (!di.Exists)
                return Array.Empty<FileSystemInfo>();
            return di.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Where(info => glob.IsMatch(info.FullName.Remove(0, truncateLength)));
        }
    }
}
