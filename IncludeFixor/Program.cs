using System.Text.RegularExpressions;
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
			var includeFixer = new IncludeFixer();

			// Read the configuration
            if (args.Length == 0 || !Config.Read(args[0], out var config))
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

			var writers = new List<TextWriter> { Console.Out };
            var router = new TextWriterRouter(writers);
			Console.SetOut(router);

			FileStream logFile = null;
			StreamWriter logWriter = null;
			if (config.Settings.Log)
			{
				logFile = new FileStream(args[0] + ".log", FileMode.Create, FileAccess.ReadWrite);
				logWriter = new StreamWriter(logFile);
				logWriter.AutoFlush = true;
				router.AddWriter(logWriter);
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
				if (Verbose)
				{
					Console.WriteLine("Processing source file ... \"{0}\"", cppFile.Value);
				}

				//   Read in all lines
				var filePath = FixPath(Path.Combine(cppFile.Key, cppFile.Value));
				var basePath = FixPath(Path.GetDirectoryName(cppFile.Value));
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
                if (Verbose)
                {
                    Console.WriteLine("Processing header file ... \"{0}\"", relativeFilepath);
                }

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
			var includeGuardIfNotDefined = new Regex("\\s*#\\s*ifndef\\s*([ ])([^ ;/]+)", RegexOptions.Compiled);
			var includeGuardDefine = new Regex("\\s*#\\s*define\\s*([ ])([^ ;/]+)", RegexOptions.Compiled);

            void IncludeGuard(string rootPath, string relativeFilepath, string includeGuardPrefix)
            {
                if (Verbose)
                {
                    Console.WriteLine("Processing header file ... \"{0}\"", relativeFilepath);
                }

                //   Read in all lines
                var outlines = new List<string>();
                var filepath = FixPath(Path.Combine(rootPath, relativeFilepath));
                var basePath = FixPath(Path.GetDirectoryName(relativeFilepath));
                var lines = File.ReadAllLines(filepath);

                if (FixIncludeGuard(basePath, relativeFilepath, includeGuardPrefix, lines, includeFixer, includeGuardIfNotDefined, includeGuardDefine, out var newlines))
                {
                    // Write out all lines if there were any modifications
                    if (!config.Settings.DryRun)
                    {
                        File.WriteAllLines(filepath, newlines);
                    }
                }
            }

            includeFixer.ForeachHeaderFileThatNeedIncludeGuardFix(IncludeGuard);



			if (config.Settings.Log)
			{
				logWriter.Close();
				logFile.Close();
			}
			Console.SetOut(writers[0]);

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

		static bool FixIncludeDirectives(string basepath, string filename, string[] lines, IncludeFixer includes, Regex includeRegex, out List<string> outlines)
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
						if (includes.FindInclude(basepath, includeHdr, out var relativeIncludeHdr))
						{
							relativeIncludeHdr = FixPath(relativeIncludeHdr);

							const bool ignoreCase = true;
							lineIsModified = String.Compare(includeHdr, relativeIncludeHdr, ignoreCase) != 0;
							if (lineIsModified)
							{
								modifiedLine = originalLine.Replace(includeHdr, relativeIncludeHdr);
								if (Verbose)
								{
									Console.WriteLine("    changed:\"{0}\", line({1}): \"{2}\"  -->  \"{3}\".", filename, lineNumber, originalLine, modifiedLine);
								}
							}
						}
						else
						{
							Console.WriteLine("    warning:\"{0}\", line({1}): Could not find matching include for \"{2}\".", filename, lineNumber, includeHdr);
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


		static bool FixIncludeGuard(string basepath, string filename, string includeguardPrefix, string[] lines, IncludeFixer includes, Regex includeguardIfndef, Regex includeguardDefine, out List<string> outlines)
		{
			outlines = new List<string>();

			var i= 0;
			var numberOfModifiedLines = 0;

			// Skip empty and comment lines at the top of the file
			while (i < lines.Length)
			{
				var line = lines[i];
				line = line.Trim();
				if (line == "" || line.StartsWith("//") || line.StartsWith(";"))
				{
					i += 1;
				}
				else
				{
					break;
				}
			}

			if (lines.Length >= (i + 2))
			{
				var ifndefMatch = includeguardIfndef.Match(lines[i]);
				if (ifndefMatch.Success)
				{
					var defineMatch = includeguardDefine.Match(lines[i+1]);
					if (defineMatch.Success)
					{
						var ifndefGroups = ifndefMatch.Groups;
						var defineGroups = defineMatch.Groups;
						if (ifndefGroups.Count >= 2 && defineGroups.Count >= 2)
						{
							var ifndefSymbol = ifndefGroups[2].Value.Trim();
							var defineSymbol = defineGroups[2].Value.Trim();
							if (ifndefSymbol == defineSymbol)
							{
								// Ok we have found an include guard like this:
								// #ifndef __SYMBOL__
								// #define __SYMBOL__
								outlines.Add(lines[i].Replace(ifndefSymbol, includeguardPrefix + ifndefSymbol));
								outlines.Add(lines[i+1].Replace(ifndefSymbol, includeguardPrefix + ifndefSymbol));
								i += 2;
								numberOfModifiedLines = 2;
							}
						}
					}
				}
			}

			// Copy lines to out, but when we have replaced the include guard start after those 2 lines
			for (; i < lines.Length; ++i)
			{
				outlines.Add(lines[i]);
			}

			return numberOfModifiedLines > 0;
		}



		static string MakeRelative(string root, string filepath)
		{
			filepath = filepath.Substring(root.Length);
			return filepath;
		}

		static char OtherPathSeperator(char pathSeperator)
		{
			switch (pathSeperator)
			{
				case '/': return '\\';
				case '\\': return '/';
			}
			return pathSeperator;
		}
		static private string FixPath(string filepath)
		{
			filepath = filepath.Replace(OtherPathSeperator(PathSeparator), PathSeparator);
			return filepath;
		}

		static void GlobAllSourceFiles(string root, char pathSeperator, List<KeyValuePair<string, string>> allSourceFiles, params string[] extensions)
		{
			var rootdirinfo = new DirectoryInfo(root);
			var oldPathSeperator = OtherPathSeperator(pathSeperator);

			foreach (var ext in extensions)
			{
				var globbed = rootdirinfo.GlobFiles("**/" + ext);
				foreach (var fi in globbed)
				{
					var filepath = MakeRelative(root, fi.FullName);
					filepath = filepath.Replace(oldPathSeperator, pathSeperator);
					filepath = FixPath(filepath);
					allSourceFiles.Add(new KeyValuePair<string, string>(root, filepath));
				}
			}
		}

	}
}
