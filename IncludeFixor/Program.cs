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

			List<TextWriter> writers = new List<TextWriter>();
			writers.Add(Console.Out);
			TextWriterRouter router = new TextWriterRouter(writers);
			Console.SetOut(router);

			FileStream logfile = null;
			StreamWriter logwriter = null;
			if (config.Settings.Log)
			{
				logfile = new FileStream(args[0] + ".log", FileMode.Create, FileAccess.ReadWrite);
				logwriter = new StreamWriter(logfile);
				logwriter.AutoFlush = true;
				router.AddWriter(logwriter);
			}

			foreach (Include inc in config.Includes)
			{
				includefixer.RegisterIncludePath(inc);

				foreach (Rename rn in inc.FileRenames)
					includefixer.AddFileRename(inc.ScannerPath, rn.From, rn.To);
				foreach (Rename rn in inc.FolderRenames)
					includefixer.AddFolderRename(inc.ScannerPath, rn.From, rn.To);
			}

			// Build list of source files (*.c, *.cpp)
			List<KeyValuePair<string, string>> all_source_files = new List<KeyValuePair<string, string>>();
			foreach (Source src in config.Sources)
			{
				GlobAllSourceFiles(src.ScannerPath, config.Settings.PathSeparator, all_source_files, src.Extensions);
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
				if (FixIncludeDirectives(basepath, cppfile.Value, lines, includefixer, include_regex, out newlines))
				{
					// Write out all lines if there where any modifications
					if (!config.Settings.DryRun)
					{
						File.WriteAllLines(filepath, newlines);
					}
				}
			}

			// For every header file that is not read-only fix their include directives
			Action<string, string> fix_includedir = delegate (string rootpath, string relative_filepath)
			{
				if (Verbose)
				{
					Console.WriteLine("Processing header file ... \"{0}\"", relative_filepath);
				}

				//   Read in all lines
				List<string> outlines = new List<string>();
				string filepath = FixPath(Path.Combine(rootpath, relative_filepath));
				string basepath = FixPath(Path.GetDirectoryName(relative_filepath));
				string[] lines = File.ReadAllLines(filepath);

				List<string> newlines;
				if (FixIncludeDirectives(basepath, relative_filepath, lines, includefixer, include_regex, out newlines))
				{
					// Write out all lines if there where any modifications
					if (!config.Settings.DryRun)
					{
						File.WriteAllLines(filepath, newlines);
					}
				}
			};
			includefixer.ForeachHeaderFileThatNeedIncludeDirFix(fix_includedir);


			// For every header file that is not read-only fix their include guards
			Regex includeguard_ifndef = new Regex("\\s*#\\s*ifndef\\s*([ ])([^ ;/]+)", RegexOptions.Compiled);
			Regex includeguard_define = new Regex("\\s*#\\s*define\\s*([ ])([^ ;/]+)", RegexOptions.Compiled);

			Action<string, string, string> fix_includeguard = delegate (string rootpath, string relative_filepath, string includeguard_prefix)
			{
				if (Verbose)
				{
					Console.WriteLine("Processing header file ... \"{0}\"", relative_filepath);
				}

				//   Read in all lines
				List<string> outlines = new List<string>();
				string filepath = FixPath(Path.Combine(rootpath, relative_filepath));
				string basepath = FixPath(Path.GetDirectoryName(relative_filepath));
				string[] lines = File.ReadAllLines(filepath);

				List<string> newlines;
				if (FixIncludeGuard(basepath, relative_filepath, includeguard_prefix, lines, includefixer, includeguard_ifndef, includeguard_define, out newlines))
				{
					// Write out all lines if there where any modifications
					if (!config.Settings.DryRun)
					{
						File.WriteAllLines(filepath, newlines);
					}
				}
			};
			includefixer.ForeachHeaderFileThatNeedIncludeGuardFix(fix_includeguard);



			if (config.Settings.Log)
			{
				logwriter.Close();
				logfile.Close();
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

		static bool FixIncludeDirectives(string basepath, string filename, string[] lines, IncludeFixer includes, Regex include_regex, out List<string> outlines)
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


		static bool FixIncludeGuard(string basepath, string filename, string includeguard_prefix, string[] lines, IncludeFixer includes, Regex includeguard_ifndef, Regex includeguard_define, out List<string> outlines)
		{
			outlines = new List<string>();

			int i= 0;
			int number_of_modified_lines = 0;

			// Skip empty and comment lines at the top of the file
			while (i < lines.Length)
			{
				string line = lines[i];
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
				Match ifndef_match = includeguard_ifndef.Match(lines[i]);
				if (ifndef_match.Success)
				{
					Match define_match = includeguard_define.Match(lines[i+1]);
					if (define_match.Success)
					{
						var ifndef_groups = ifndef_match.Groups;
						var define_groups = define_match.Groups;
						if (ifndef_groups.Count >= 2 && define_groups.Count >= 2)
						{
							string ifndef_symbol = ifndef_groups[2].Value.Trim();
							string define_symbol = define_groups[2].Value.Trim();
							if (ifndef_symbol == define_symbol)
							{
								// Ok we have found an include guard like this:
								// #ifndef __SYMBOL__
								// #define __SYMBOL__
								outlines.Add(lines[i].Replace(ifndef_symbol, includeguard_prefix + ifndef_symbol));
								outlines.Add(lines[i+1].Replace(ifndef_symbol, includeguard_prefix + ifndef_symbol));
								i += 2;
								number_of_modified_lines = 2;
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
					all_source_files.Add(new KeyValuePair<string, string>(root, filepath));
				}
			}
		}

	}
}
