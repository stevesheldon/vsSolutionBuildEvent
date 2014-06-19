﻿/* 
 * Boost Software License - Version 1.0 - August 17th, 2003
 * 
 * Copyright (c) 2013 Developed by reg <entry.reg@gmail.com>
 * 
 * Permission is hereby granted, free of charge, to any person or organization
 * obtaining a copy of the software and accompanying documentation covered by
 * this license (the "Software") to use, reproduce, display, distribute,
 * execute, and transmit the Software, and to prepare derivative works of the
 * Software, and to permit third-parties to whom the Software is furnished to
 * do so, all subject to the following:
 * 
 * The copyright notices in the Software and this entire statement, including
 * the above license grant, this restriction and the following disclaimer,
 * must be included in all copies of the Software, in whole or in part, and
 * all derivative works of the Software, unless such copies or derivative
 * works are solely in the form of machine-executable object code generated by
 * a source language processor.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
 * SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
 * FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE. 
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE80;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace net.r_eg.vsSBE
{
    public class MSBuildParser: IMSBuildProperty, ISBEParserScript
    {
        private Object _eLock = new Object();

        /// <summary>
        /// MSBuild Property from default Project
        /// </summary>
        /// <param name="name">key property</param>
        /// <returns>evaluated value</returns>
        public string getProperty(string name)
        {
            return getProperty(name, null);
        }

        /// <summary>
        /// MSBuild Property from specific project
        /// </summary>
        /// <param name="name">key property</param>
        /// <param name="projectName">project name</param>
        /// <returns>evaluated value</returns>
        public string getProperty(string name, string projectName)
        {
            Project project         = getProject(projectName);
            ProjectProperty prop    = project.GetProperty(name);

            if(prop != null) {
                return prop.EvaluatedValue;
            }
            throw new MSBuildParserProjectPropertyNotFoundException(String.Format("variable - '{0}' : project - '{1}'", name, (projectName == null) ? "<default>" : projectName));
        }

        public List<MSBuildPropertyItem> listProperties(string projectName = null)
        {
            List<MSBuildPropertyItem> properties = new List<MSBuildPropertyItem>();

            Project project = getProject(projectName);
            foreach(ProjectProperty property in project.AllEvaluatedProperties) {
                properties.Add(new MSBuildPropertyItem(property.Name, property.EvaluatedValue));
            }
            return properties;
        }

        public List<string> listProjects()
        {
            List<string> projects           = new List<string>();
            IEnumerator<Project> eprojects  = _loadedProjects();

            while(eprojects.MoveNext()) {
                string projectName = eprojects.Current.GetPropertyValue("ProjectName");

                if(projectName != null && _isActiveConfiguration(eprojects.Current)) {
                    projects.Add(projectName);
                }
            }
            return projects;
        }

        public string evaluateVariableWithMSBuild(string unevaluatedValue, Project project)
        {
            lock(_eLock) {
                project.SetProperty("vsSBE_latestEvaluated", string.Format("$({0})", unevaluatedValue));
            }
            return project.GetProperty("vsSBE_latestEvaluated").EvaluatedValue;
        }

        /// <summary>
        /// Simple handler properties of MSBuild environment
        /// </summary>
        /// <remarks>deprecated</remarks>
        /// <param name="data">text with $(ident) data</param>
        /// <returns>text with evaluated properties</returns>
        public string parseVariablesMSBuildSimple(string data)
        {
            return Regex.Replace(data, @"
                                         (?<!\$)\$
                                         \(
                                           (?:
                                             (
                                               [^\:\r\n)]+?
                                             )
                                             \:
                                             (
                                               [^)\r\n]+?
                                             )
                                             |
                                             (
                                               [^)]*?
                                             )
                                           )
                                         \)", delegate(Match m)
            {
                // 3   -> $(name)
                // 1,2 -> $(name:project)

                if(m.Groups[3].Success) {
                    return getProperty(m.Groups[3].Value);
                }
                return getProperty(m.Groups[1].Value, m.Groups[2].Value);

            }, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace).Replace("$$(", "$(");
        }

        public virtual string parseVariablesMSBuild(string data)
        {
            /*
                    (
                      \${1,2}
                    )
                    (?=
                      (
                        \(
                          (?>
                            [^()]
                            |
                            (?2)
                          )*
                        \)
                      )
                    )            -> for .NET: v             
             */
            return Regex.Replace(data,  @"(
                                            \${1,2}
                                          )
                                          (
                                            \(
                                              (?>
                                                [^()]
                                                |
                                                \((?<R>)
                                                |
                                                \)(?<-R>)
                                              )*
                                              (?(R)(?!))
                                            \)
                                          )", delegate(Match m)
            {
                // 1 - $ or $$
                // 2 - (name) or (name:project) or ([MSBuild]::MakeRelative($(path1), ...):project) .. 
                //      http://msdn.microsoft.com/en-us/library/vstudio/dd633440%28v=vs.120%29.aspx

                if(m.Groups[1].Value.Length > 1) { //escape
                    return m.Value.Substring(1);
                }

                string unevaluated  = m.Groups[2].Value;
                string projectName  = _splitGeneralProjectAttr(ref unevaluated);

                if(_isSimpleProperty(ref unevaluated)) {
                    return getProperty(unevaluated, projectName);
                }
                return evaluateVariableWithMSBuild(unevaluated, getProject(projectName));
            }, RegexOptions.IgnorePatternWhitespace);
        }


        /// <summary>
        /// All variables which are not included in MSBuild environment.
        /// Customization for our data
        /// </summary>
        /// <param name="data">where to look</param>
        /// <param name="name">we're looking for..</param>
        /// <param name="value">replace with this value if found</param>
        /// <returns></returns>
        public string parseCustomVariable(string data, string name, string value)
        {
            return Regex.Replace(data,  @"(
                                            \${1,2}
                                          )
                                          \(
                                            (
                                              [^)]+?
                                            )
                                          \)", delegate(Match m)
            {
                if(m.Groups[2].Value != name || m.Groups[1].Value.Length > 1) {
                    return m.Value;
                }
                return (value == null)? "" : value;
            }, RegexOptions.IgnorePatternWhitespace);
        }
        
        public MSBuildParser()
        {
#if DEBUG
            string unevaluated = "(name:project)";
            Debug.Assert(_splitGeneralProjectAttr(ref unevaluated).CompareTo("project") == 0);
            Debug.Assert(unevaluated.CompareTo("name") == 0);

            unevaluated = "(name)";
            Debug.Assert(_splitGeneralProjectAttr(ref unevaluated) == null);
            Debug.Assert(unevaluated.CompareTo("name") == 0);

            unevaluated = "([class]::func($(path:project), $([class]::func2($(path2)):project)):project)";
            Debug.Assert(_splitGeneralProjectAttr(ref unevaluated).CompareTo("project") == 0);
            Debug.Assert(unevaluated.CompareTo("[class]::func($(path:project), $([class]::func2($(path2)):project))") == 0);

            unevaluated = "([class]::func($(path:project), $([class]::func2($(path2)):project)):project))";
            Debug.Assert(_splitGeneralProjectAttr(ref unevaluated) == null);
            Debug.Assert(unevaluated.CompareTo("[class]::func($(path:project), $([class]::func2($(path2)):project)):project)") == 0);
#endif
        }

        /// <summary>
        /// get default project for access to properties etc.
        /// first in the list at Configuration & Platform
        /// </summary>
        /// <returns>Microsoft.Build.Evaluation.Project</returns>
        protected Project getProjectDefault()
        {
            IEnumerator<Project> eprojects = _loadedProjects();
            while(eprojects.MoveNext()) {
                if(_isActiveConfiguration(eprojects.Current)) {
                    return eprojects.Current;
                }
            }
            throw new MSBuildParserProjectNotFoundException("not found project: <default>");
        }

        protected Project getProject(string project)
        {
            if(project == null) {
                return getProjectDefault();
            }

            IEnumerator<Project> eprojects = _loadedProjects();
            while(eprojects.MoveNext()) {
                if(eprojects.Current.GetPropertyValue("ProjectName").Equals(project) && _isActiveConfiguration(eprojects.Current)) {
                    return eprojects.Current;
                }
            }
            throw new MSBuildParserProjectNotFoundException(String.Format("not found project: '{0}'", project));
        }

        // TODO:
        private TRuntimeSettings _getRuntimeSettings()
        {
            // TODO: settings in Runtime... Where are placed the individual ?
            string xml = ProjectCollection.GlobalProjectCollection.GetGlobalProperty("CurrentSolutionConfigurationContents").EvaluatedValue;

            // ProjectConfiguration
            Match m = Regex.Match(xml, @"ProjectConfiguration[^>]*>(\S+?)\|(\S+?)<", RegexOptions.IgnoreCase);
            if(!m.Success){
                throw new MSBuildParserProjectPropertyNotFoundException("Runtime settings - 'ProjectConfiguration'");
            }
            return new TRuntimeSettings(m.Groups[1].Value, m.Groups[2].Value);
        }

        private bool _isActiveConfiguration(Project project)
        {
            TRuntimeSettings runtime    = _getRuntimeSettings();
            string configuration        = project.GetPropertyValue("Configuration");
            string platform             = project.GetPropertyValue("Platform");

            if(configuration.Equals(runtime.configuration) && platform.Equals(runtime.platform)) {
                return true;
            }
            return false;
        }

        private IEnumerator<Project> _loadedProjects()
        {
            return ((ICollection<Project>)ProjectCollection.GlobalProjectCollection.LoadedProjects).GetEnumerator();
        }

        /// <summary>
        /// Getting the project name and format unevaluated variable
        /// ~ (variable:project) -> variable & project
        /// </summary>
        /// <param name="unevaluated">to be formatted after handling</param>
        /// <returns>project name or null if not present</returns>
        private string _splitGeneralProjectAttr(ref string unevaluated)
        {
            unevaluated = unevaluated.Substring(1, unevaluated.Length - 2);
            int pos     = unevaluated.LastIndexOf(':');

            if(pos == -1) {
                return null; //(ProjectOutputFolder.Substring(0, 1)), (OS), (OS.Length)
            }
            if(unevaluated.ElementAt(pos - 1).CompareTo(':') == 0) {
                return null; //([System.DateTime]::Now), ([System.Guid]::NewGuid())
            }
            if(unevaluated.IndexOf(')', pos) != -1) {
                return null; // allow only for latest block (general option)  :project)) | :project) ... )-> :project)
            }

            string project  = unevaluated.Substring(pos + 1, unevaluated.Length - pos - 1);
            unevaluated     = unevaluated.Substring(0, pos);

            return project;
        }

        private bool _isSimpleProperty(ref string unevaluated)
        {
            if(unevaluated.IndexOfAny(new char[]{'.', ':', '(', ')', '\'', '"'}) != -1) {
                return false;
            }
            return true;
        }

        private struct TRuntimeSettings
        {
            public string configuration;
            public string platform;

            public TRuntimeSettings(string configuration, string platform)
            {
                this.configuration  = configuration;
                this.platform       = platform;
            }
        }
    }

    /// <summary>
    /// item of property: name = value
    /// </summary>
    public sealed class MSBuildPropertyItem
    {
        public string name;
        public string value;

        public MSBuildPropertyItem(string name, string value)
        {
            this.name  = name;
            this.value = value;
        }
    }

    //TODO:
    public struct SBECustomVariable
    {
        public const string OWP_BUILD           = "vsSBE_OWPBuild";
        public const string OWP_BUILD_WARNINGS  = "vsSBE_OWPBuildWarnings";
        public const string OWP_BUILD_ERRORS    = "vsSBE_OWPBuildErrors";
    }
}