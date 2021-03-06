﻿/*
 * Copyright (c) 2013-2015  Denis Kuzmin (reg) <entry.reg@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace net.r_eg.vsSBE
{
    /// <summary>
    /// Basic work with .sln for getting list of projects, available configurations etc.
    /// Please note: it's necessary for work without DTE-context/IDE mode, for example with isolated enviroment.
    ///              Use the EnvDTE & ProjectCollection if it's possible!
    /// 
    /// Another variants:
    /// * Using deprecated Microsoft.Build.BuildEngine.Project - http://msdn.microsoft.com/en-us/library/microsoft.build.buildengine.project%28v=vs.100%29.aspx
    /// * Or reflect the internal SolutionParser from Microsoft.Build.BuildEngine.Shared, for example for getting all projects:
    ///   -> void ParseProject(string firstLine)
    ///      -> void ParseFirstProjectLine(string firstLine, ProjectInSolution proj)
    ///      -> crackProjectLine -> PROJECTNAME & RELATIVEPATH
    /// </summary>
    public class SolutionParser
    {
        /// <summary>
        /// Properties of project in solution file
        /// </summary>
        public struct Project
        {
            /// <summary>
            /// Project type GUID
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Project name
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Relative path to project
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// Full path to project 
            /// </summary>
            public string FullPath { get; set; }

            /// <summary>
            /// Project GUID
            /// </summary>
            public string Guid { get; set; }
        }

        /// <summary>
        /// Configuration & Platform
        /// </summary>
        public struct SolutionCfg
        {
            public string Configuration { get; set; }
            public string Platform { get; set; }
        }

        /// <summary>
        /// Provides the result of work
        /// </summary>
        public class Result
        {
            /// <summary>
            /// Configurations with platforms in solution file
            /// </summary>
            public List<SolutionCfg> configs;

            /// <summary>
            /// All found projects for solution
            /// </summary>
            public List<Project> projects;
        }

        /// <summary>
        /// Full path to root solution directory
        /// </summary>
        private string _solutionDir;

        
        /// <summary>
        /// Parse of selected .sln file
        /// </summary>
        /// <param name="sln">Solution file</param>
        /// <returns></returns>
        public Result parse(string sln)
        {
            _solutionDir = getPathFrom(sln);

            Result Data = new Result() {
                configs  = new List<SolutionCfg>(),
                projects = new List<Project>()
            };

            using(StreamReader reader = new StreamReader(sln, Encoding.Default))
            {
                string line;
                while((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if(line.StartsWith("Project(", StringComparison.Ordinal)) {
                        hProject(line, ref Data.projects);
                    }

                    if(line.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.Ordinal)) {
                        hConfiguration(reader, ref Data.configs);
                    }
                }
            }
            return Data;
        }

        protected void hConfiguration(StreamReader reader, ref List<SolutionCfg> configuration)
        {
            string line;
            while((line = reader.ReadLine()) != null && line.Trim() != "EndGlobalSection")
            {
                string left = line.Split('=')[0].Trim(); // Debug|Win32 = Debug|Win32
                if(string.Compare(left, "DESCRIPTION", StringComparison.OrdinalIgnoreCase) == 0) {
                    Log.nlog.Trace("SolutionParser: Configuration has been ignored for line '{0}'", line);
                    continue;
                }

                string[] cfg = left.Split('|');
                if(cfg.Length < 2) {
                    continue;
                }

                Log.nlog.Trace("SolutionParser: Configuration ->['{0}' ; '{1}']", cfg[0], cfg[1]);
                configuration.Add(new SolutionCfg() {
                    Configuration   = cfg[0],
                    Platform        = cfg[1]
                });
            }
        }

        protected void hProject(string line, ref List<Project> projects)
        {
            // Pattern from Microsoft.Build.BuildEngine.Shared.SolutionParser !
            string pattern = "^Project\\(\"(?<PROJECTTYPEGUID>.*)\"\\)\\s*=\\s*\"(?<PROJECTNAME>.*)\"\\s*,\\s*\"(?<RELATIVEPATH>.*)\"\\s*,\\s*\"(?<PROJECTGUID>.*)\"$";
            Match m = Regex.Match(line, pattern);
            if(!m.Success) {
                Log.nlog.Debug("SolutionParser: incorrect line for pattern :: '{0}'", line);
                return;
            }

            string pType = m.Groups["PROJECTTYPEGUID"].Value.Trim();
            
            if(String.Equals("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", pType, StringComparison.OrdinalIgnoreCase)) {
                Log.nlog.Trace("SolutionParser: ignored as SolutionFolder");
                return;
            }

            string pName = m.Groups["PROJECTNAME"].Value.Trim();
            string pPath = m.Groups["RELATIVEPATH"].Value.Trim();
            string pGuid = m.Groups["PROJECTGUID"].Value.Trim();

            string fullPath;
            if(Path.IsPathRooted(pPath)) {
                fullPath = pPath;
            }
            else {
                fullPath = (!String.IsNullOrEmpty(pPath))? Path.Combine(_solutionDir, pPath) : pPath;
            }

            Log.nlog.Trace("SolutionParser: project ->[Type: '{0}'; Name: '{1}'; Path: '{2}'; GUID: '{3}'; FullPath: '{4}']",
                                                            pType, pName, pPath, pGuid, fullPath);
            projects.Add(new Project() {
                Type        = pType,
                Name        = pName,
                Path        = pPath,
                FullPath    = fullPath,
                Guid        = pGuid
            });
        }

        protected string getPathFrom(string file)
        {
            string dir = Path.GetDirectoryName(file);
            if(dir.ElementAt(dir.Length - 1) != Path.DirectorySeparatorChar) {
                dir += Path.DirectorySeparatorChar;
            }
            return dir;
        }
    }
}
