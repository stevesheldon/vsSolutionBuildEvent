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

using EnvDTE80;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using net.r_eg.vsSBE.MSBuild.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace net.r_eg.vsSBE
{
    public class Environment: IEnvironment
    {
        /// <summary>
        /// Simple list of names from EnvDTE projects
        /// </summary>
        public List<string> ProjectsList
        {
            get
            {
                try {
                    return DTEProjects.Select(p => getProjectNameFrom(p)).ToList<string>();
                }
                catch(Exception ex) {
                    Log.nlog.Error("Failed getting project from EnvDTE: {0}", ex.Message);
                }
                return new List<string>();
            }
        }

        /// <summary>
        /// Events in the extensibility model
        /// </summary>
        public EnvDTE.Events Events
        {
            get { return Dte2.Events; }
        }

        /// <summary>
        /// Contains all of the commands in the environment
        /// </summary>
        public EnvDTE.Commands Commands
        {
            get { return Dte2.Commands; }
        }

        /// <summary>
        /// Active configuration for current solution
        /// </summary>
        public SolutionConfiguration2 SolutionActiveCfg
        {
            get { return (SolutionConfiguration2)Dte2.Solution.SolutionBuild.ActiveConfiguration; }
        }

        /// <summary>
        /// Formatted string with active configuration for current solution
        /// </summary>
        public string SolutionActiveCfgString
        {
            get {
                return SolutionCfgFormat(SolutionActiveCfg);
            }
        }

        /// <summary>
        /// All configurations for current solution
        /// </summary>
        public IEnumerable<SolutionConfiguration2> SolutionConfigurations
        {
            get {
                foreach(SolutionConfiguration2 cfg in Dte2.Solution.SolutionBuild.SolutionConfigurations) {
                    yield return cfg;
                }
            }
        }

        /// <summary>
        /// Getting name from "Set as StartUp Project"
        /// </summary>
        public virtual string StartupProjectString
        {
            get
            {
                foreach(string project in (Array)Dte2.Solution.SolutionBuild.StartupProjects)
                {
                    if(String.IsNullOrEmpty(project)) {
                        continue;
                    }
                    return project;
                }
                return null;
            }
        }

        /// <summary>
        /// Solution path from the DTE-context
        /// </summary>
        public string SolutionPath
        {
            get {
                if(solutionPath != null) {
                    return solutionPath;
                }
                solutionPath = extractPath(Dte2);
                return solutionPath;
            }
        }
        protected string solutionPath = null;

        /// <summary>
        /// Name of used solution file without extension
        /// </summary>
        public string SolutionFileName
        {
            get {
                if(solutionFileName != null) {
                    return solutionFileName;
                }
                solutionFileName = extractFileName(Dte2);
                return solutionFileName;
            }
        }
        protected string solutionFileName = null;

        /// <summary>
        /// Access to OutputWindowPane through IOW
        /// </summary>
        public IOW OutputWindowPane
        {
            get
            {
                if(outputWindowPane == null) {
                    outputWindowPane = new OWP(Dte2);
                }
                return outputWindowPane;
            }
        }
        protected IOW outputWindowPane;

        /// <summary>
        /// DTE2 context
        /// </summary>
        protected DTE2 Dte2 { get; set; }

        /// <summary>
        /// Getting projects from DTE
        /// </summary>
        protected virtual IEnumerable<EnvDTE.Project> DTEProjects
        {
            get
            {
                foreach(EnvDTE.Project project in DTEProjectsRaw)
                {
                    if(project.Kind == "{67294A52-A4F0-11D2-AA88-00C04F688DDE}" || project.ConfigurationManager == null) {
                        Log.nlog.Debug("Unloaded project '{0}' has ignored", project.Name);
                        continue; // skip for all unloaded projects
                    }
                    yield return project;
                }
            }
        }

        protected IEnumerable<EnvDTE.Project> DTEProjectsRaw
        {
            get
            {
                foreach(EnvDTE.Project project in Dte2.Solution.Projects)
                {
                    if(project.Kind != ProjectKinds.vsProjectKindSolutionFolder) {
                        yield return project;
                        continue;
                    }

                    foreach(EnvDTE.Project subproject in listSubProjectsDTE(project)) {
                        yield return subproject;
                    }
                }
            }
        }

        /// <summary>
        /// Gets instance of the Build.Evaluation.Project for accessing to properties etc.
        /// 
        /// Used the Startup-project in the list or the first with the same Configuration & Platform if the name contains null value
        /// Note: we work with DTE because the ProjectCollection.GlobalProjectCollection can be is empty
        /// https://bitbucket.org/3F/vssolutionbuildevent/issue/8/
        /// </summary>
        /// <param name="name">Project name</param>
        /// <returns>Microsoft.Build.Evaluation.Project</returns>
        public virtual Project getProject(string name = null)
        {
            EnvDTE.Project selected = null;
            string startup          = StartupProjectString;

            if(name == null) {
                Log.nlog.Debug("default project is a '{0}'", startup);
            }

            foreach(EnvDTE.Project dteProject in DTEProjects)
            {
                if(name == null && !String.IsNullOrEmpty(startup) && !dteProject.UniqueName.Equals(startup)) {
                    continue;
                }
                else if(name != null && !getProjectNameFrom(dteProject).Equals(name)) {
                    continue;
                }
                selected = dteProject;
                Log.nlog.Trace("selected = dteProject: '{0}'", dteProject.FullName);

                foreach(Project eProject in ProjectCollection.GlobalProjectCollection.LoadedProjects)
                {
                    if(isEquals(dteProject, eProject)) {
                        return eProject;
                    }
                }
                break; // selected & LoadedProjects is empty
            }

            if(selected != null) {
                Log.nlog.Debug("getProject->selected '{0}'", selected.FullName);
                return tryLoadPCollection(selected);
            }
            throw new MSBProjectNotFoundException("not found project: '{0}' [sturtup: '{1}']", name, startup);
        }

        /// <summary>
        /// Getting global(general for all existing projects) property
        /// </summary>
        /// <param name="name">Property name</param>
        public string getSolutionProperty(string name)
        {
            if(String.IsNullOrEmpty(name)) {
                return null;
            }

            if(name.Equals("Configuration")) {
                return SolutionActiveCfg.Name;
            }

            if(name.Equals("Platform")) {
                return SolutionActiveCfg.PlatformName;
            }

            return null;
        }

        /// <summary>
        /// Compatible format: 'configname'|'platformname'
        /// http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.shell.interop.ivscfg.get_displayname.aspx
        /// </summary>
        public string SolutionCfgFormat(SolutionConfiguration2 cfg)
        {
            return String.Format("{0}|{1}", cfg.Name, cfg.PlatformName);
        }

        /// <summary>
        /// Execute command with DTE
        /// </summary>
        /// <param name="name">Command name</param>
        /// <param name="args">Command arguments</param>
        public virtual void exec(string name, string args = "")
        {
            ((EnvDTE.DTE)Dte2).ExecuteCommand(name, (args == null)? String.Empty : args);
        }

        public Environment(DTE2 dte2)
        {
            Dte2 = dte2;
        }

        /// <summary>
        /// Extracts the solution path from the DTE-context
        /// </summary>
        /// <param name="dte2">DTE2 context</param>
        protected virtual string extractPath(DTE2 dte2)
        {
            string dir = Path.GetDirectoryName(_getFullPathFrom(dte2));

            if(dir.ElementAt(dir.Length - 1) != Path.DirectorySeparatorChar) {
                dir += Path.DirectorySeparatorChar;
            }
            return dir;
        }

        /// <summary>
        /// Extracts the solution file name from the DTE-context
        /// </summary>
        /// <param name="dte2">DTE2 context</param>
        /// <returns>File name without extension</returns>
        protected virtual string extractFileName(DTE2 dte2)
        {
            return Path.GetFileNameWithoutExtension(_getFullPathFrom(dte2));
        }

        protected bool isEquals(EnvDTE.Project dteProject, Project eProject)
        {
            string ePrgName         = getProjectNameFrom(eProject);
            string ePrgCfg          = eProject.GetPropertyValue("Configuration");
            string ePrgPlatform     = eProject.GetPropertyValue("Platform");

            string dtePrgName       = getProjectNameFrom(dteProject);
            string dtePrgCfg        = dteProject.ConfigurationManager.ActiveConfiguration.ConfigurationName;
            string dtePrgPlatform   = dteProject.ConfigurationManager.ActiveConfiguration.PlatformName;

            Log.nlog.Trace("isEquals for '{0}' : '{1}' [{2} = {3} ; {4} = {5}]",
                            eProject.FullPath, dtePrgName, dtePrgCfg, ePrgCfg, dtePrgPlatform, ePrgPlatform);

            // see MS Connect Issue #503935 & https://bitbucket.org/3F/vssolutionbuildevent/issue/14/empty-property-outdir
            bool isEqualPlatforms = ePrgPlatform.Replace(" ", "") == dtePrgPlatform.Replace(" ", "");

            if(dtePrgName.Equals(ePrgName) && dtePrgCfg.Equals(ePrgCfg) && isEqualPlatforms)
            {
                Log.nlog.Trace("isEquals: matched");
                return true;
            }
            return false;
        }

        /// <summary>
        /// This solution for similar problems - MS Connect Issue #508628:
        /// http://connect.microsoft.com/VisualStudio/feedback/details/508628/
        /// </summary>
        protected Project tryLoadPCollection(EnvDTE.Project dteProject)
        {
            Dictionary<string, string> prop = getGlobalProperties(dteProject);

            Log.nlog.Debug("tryLoadPCollection :: '{0}' [{1} ; {2}]", dteProject.FullName, prop["Configuration"], prop["Platform"]);
            //ProjectCollection.GlobalProjectCollection.LoadProject(dteProject.FullName, prop, null);
            return new Project(dteProject.FullName, prop, null, ProjectCollection.GlobalProjectCollection);
        }

        /// <summary>
        /// Gets project name from EnvDTE.Project
        /// </summary>
        /// <param name="dteProject"></param>
        /// <returns></returns>
        protected virtual string getProjectNameFrom(EnvDTE.Project dteProject)
        {
            //return dteProject.Name; // can be as 'AppName' and 'AppName_2013' for different .sln

            IVsSolution sln = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
            IVsHierarchy hr;
            sln.GetProjectOfUniqueName(dteProject.FullName, out hr);

            Guid id;
            hr.GetGuidProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ProjectIDGuid, out id);

            foreach(Project eProject in ProjectCollection.GlobalProjectCollection.LoadedProjects)
            {
                string guidString = eProject.GetPropertyValue("ProjectGuid");
                if(!String.IsNullOrEmpty(guidString) && id == (new Guid(guidString))) {
                    return getProjectNameFrom(eProject);
                }
            }
            return getProjectNameFrom(tryLoadPCollection(dteProject));
        }

        /// <summary>
        /// Gets project name from Microsoft.Build.Evaluation.Project
        /// </summary>
        /// <param name="eProject"></param>
        /// <returns></returns>
        protected virtual string getProjectNameFrom(Project eProject)
        {
            return eProject.GetPropertyValue("ProjectName");
            //note: we can to define as unified name for all .sln files in project file, e.g.: <PropertyGroup> <ProjectName>YourName</ProjectName> </PropertyGroup>
        }

        protected Dictionary<string, string> getGlobalProperties(EnvDTE.Project dteProject)
        {
            Dictionary<string, string> prop = new Dictionary<string, string>(ProjectCollection.GlobalProjectCollection.GlobalProperties); // copy from ProjectCollection
            
            if(!prop.ContainsKey("Configuration")) {
                prop["Configuration"] = dteProject.ConfigurationManager.ActiveConfiguration.ConfigurationName;
            }

            if(!prop.ContainsKey("Platform")) {
                prop["Platform"] = dteProject.ConfigurationManager.ActiveConfiguration.PlatformName;

                // Bug with $(OutDir) - see MS Connect Issue #503935 & https://bitbucket.org/3F/vssolutionbuildevent/issue/14/empty-property-outdir
                if(prop["Platform"] == "Any CPU") {
                    prop["Platform"] = "AnyCPU";
                }
            }
            
            if(!prop.ContainsKey("BuildingInsideVisualStudio")) {
                // by default(can be changed in other components) set as "true" in Microsoft.VisualStudio.Project.ProjectNode :: DoMSBuildSubmission & SetupProjectGlobalPropertiesThatAllProjectSystemsMustSet
                prop["BuildingInsideVisualStudio"] = "true";
            }
            if(!prop.ContainsKey("DevEnvDir"))
            {
                // http://technet.microsoft.com/en-us/microsoft.visualstudio.shell.interop.__vsspropid%28v=vs.71%29.aspx

                object dirObject = null;
                IVsShell shell = (IVsShell)Package.GetGlobalService(typeof(SVsShell));
                shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out dirObject);

                string dir              = (string)dirObject;
                const string vDefault   = "*Undefined*";

                if(String.IsNullOrEmpty(dir)) {
                    prop["DevEnvDir"] = vDefault;
                }
                else if(dir.ElementAt(dir.Length - 1) != Path.DirectorySeparatorChar) {
                    dir += Path.DirectorySeparatorChar;
                }
                prop["DevEnvDir"] = dir;
            }

            if(!prop.ContainsKey("SolutionDir")  
               || !prop.ContainsKey("SolutionName")
               || !prop.ContainsKey("SolutionFileName")
               || !prop.ContainsKey("SolutionExt")
               || !prop.ContainsKey("SolutionPath"))
            {
                string dir, file, opts;
                IVsSolution solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
                solution.GetSolutionInfo(out dir, out file, out opts);

                string fname                = Path.GetFileName(file);
                string name                 = Path.GetFileNameWithoutExtension(file);
                string ext                  = Path.GetExtension(file);
                const string vDefault       = "*Undefined*";

                prop["SolutionDir"]         = dir != null ? dir : vDefault;
                prop["SolutionName"]        = name != null ? name : vDefault;
                prop["SolutionFileName"]    = fname != null ? fname : vDefault;
                prop["SolutionExt"]         = ext != null ? ext : vDefault;
                prop["SolutionPath"]        = file != null ? file : vDefault;
            }

            if(!prop.ContainsKey("RunCodeAnalysisOnce")) {
                // by default set as "false" in Microsoft.VisualStudio.Package.GlobalPropertyHandler
                prop["RunCodeAnalysisOnce"] = "false";
            }

            return prop;
        }

        protected IEnumerable<EnvDTE.Project> listSubProjectsDTE(EnvDTE.Project project)
        {
            foreach(EnvDTE.ProjectItem item in project.ProjectItems)
            {
                if(item.SubProject == null) {
                    continue; //e.g. project is incompatible with used version of visual studio
                }

                if(item.SubProject.Kind != ProjectKinds.vsProjectKindSolutionFolder) {
                    yield return item.SubProject;
                    continue;
                }

                foreach(EnvDTE.Project subproject in listSubProjectsDTE(item.SubProject)) {
                    yield return subproject;
                }
            }
        }

        /// <summary>
        /// Gets full path to solution file
        /// </summary>
        /// <param name="dte2">DTE2 context</param>
        /// <returns></returns>
        private string _getFullPathFrom(DTE2 dte2)
        {
            string path = dte2.Solution.FullName; // empty if used the new solution 
            if(String.IsNullOrEmpty(path)) {
                return dte2.Solution.Properties.Item("Path").Value.ToString();
            }
            return path;
        }
    }
}
