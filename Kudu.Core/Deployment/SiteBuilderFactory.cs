using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class SiteBuilderFactory : ISiteBuilderFactory
    {
        private readonly DeploymentConfiguration _configuration;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly IBuildPropertyProvider _propertyProvider;

        private string RepositoryRoot { get { return _environment.RepositoryPath; } }
        
        public SiteBuilderFactory(IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _settings = settings;
            _propertyProvider = propertyProvider;
            _environment = environment;
            _configuration = new DeploymentConfiguration(RepositoryRoot);
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger)
        {
            var detectionRules = new Dictionary<ProjectConfigurationType, Func<bool>>
                {
                    {ProjectConfigurationType.CustomDeploymentFile, ()=> !String.IsNullOrEmpty(_configuration.Command)},
                    {ProjectConfigurationType.ExplicitPointerToProjectPath, ()=> !String.IsNullOrEmpty(_configuration.ProjectPath)},
                    {ProjectConfigurationType.FindSingleVisualStudioSolutionFileRule,()=> FindFirstSolution() != null},
                    {ProjectConfigurationType.FindFirstProjectFile, SingleLooseProjectFileDetected},
                    {ProjectConfigurationType.NoBuildableFormatsDetected, ()=>true},
                };

            var detectedProjectType = detectionRules.Where(rule => rule.Value()).Select(rule => rule.Key).FirstOrDefault();

            var buildersForProjectTypes = new Dictionary<ProjectConfigurationType, Func<ISiteBuilder>>
                {
                    {ProjectConfigurationType.CustomDeploymentFile, () => new CustomBuilder(_environment.RepositoryPath, _environment.TempPath, _configuration.Command, _propertyProvider)},
                    {ProjectConfigurationType.ExplicitPointerToProjectPath, ()=> ResolveProject(true, SearchOption.TopDirectoryOnly)},
                    {ProjectConfigurationType.FindSingleVisualStudioSolutionFileRule, WebBuilder},
                    {ProjectConfigurationType.FindFirstProjectFile,()=> ResolveProject()},
                    {ProjectConfigurationType.NoBuildableFormatsDetected, () => new BasicBuilder(_environment.RepositoryPath, _environment.TempPath, _environment.ScriptPath)},
                };


            return buildersForProjectTypes[detectedProjectType]();
        }

        private bool SingleLooseProjectFileDetected()
        {
            if (DeploymentHelper.IsProject(_configuration.ProjectPath))
            {
                return true;
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetProjects(_configuration.ProjectPath);
            if (projects.Count > 1)
            {
                // Can't determine which project to build
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_AmbiguousProjects,
                                                                  String.Join(", ", projects)));
            }

            return projects.Count == 1;
        }

        private VsSolution FindFirstSolution()
        {
            // Get all solutions in the current repository path
            var solutions = VsHelper.GetSolutions(RepositoryRoot).ToList();

            if (!solutions.Any())
            {
                return null;
            }

            solutions.ThrowIfMultipleSolutionsFound();

            return solutions[0];
        }

        private ISiteBuilder WebBuilder()
        {
            var firstSolution = FindFirstSolution();

            var project = firstSolution.Projects.FirstOrDefault(x => x.IsWap || x.IsWebSite);

            var builder = project.IsWap
                              ? (ISiteBuilder)new WapBuilder(_settings,
                                                             _propertyProvider,
                                                             RepositoryRoot,
                                                             project.AbsolutePath,
                                                             _environment.TempPath,
                                                             _environment.NuGetCachePath,
                                                             firstSolution.Path)

                              : new WebSiteBuilder(_propertyProvider,
                                                   RepositoryRoot,
                                                   project.AbsolutePath,
                                                   _environment.TempPath,
                                                   _environment.NuGetCachePath,
                                                   firstSolution.Path);

            return builder;
        }

        private ISiteBuilder ResolveProject(bool tryWebSiteProject = false, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (DeploymentHelper.IsProject(_configuration.ProjectPath))
            {
                if (!DeploymentHelper.IsDeployableProject(_configuration.ProjectPath))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_ProjectNotDeployable, _configuration.ProjectPath));
                }

                return DetermineProject(RepositoryRoot, _configuration.ProjectPath);
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetProjects(_configuration.ProjectPath, searchOption);
            if (projects.Count > 1)
            {
                // Can't determine which project to build
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_AmbiguousProjects, String.Join(", ", projects)));
            }
            
            if (projects.Count == 1)
            {
                if (!DeploymentHelper.IsDeployableProject(projects[0]))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_ProjectNotDeployable, projects[0]));
                }

                return DetermineProject(RepositoryRoot, projects[0]);
            }

            if (tryWebSiteProject)
            {
                // Website projects need a solution to build so look for one in the repository path
                // that has this website in it.
                var solutions = VsHelper.FindContainingSolutions(RepositoryRoot, _configuration.ProjectPath);
                solutions.ThrowIfMultipleSolutionsFound();

                if (solutions.Count == 1)
                {
                    // Unambiguously pick the root
                    return new WebSiteBuilder(_propertyProvider,
                                              RepositoryRoot,
                                              _configuration.ProjectPath,
                                              _environment.TempPath,
                                              _environment.NuGetCachePath,
                                              solutions[0].Path);
                }
            }

            // If there's none then use the basic builder (the site is xcopy deployable)
            return new BasicBuilder(_configuration.ProjectPath, _environment.TempPath, _environment.ScriptPath);
        }


        private ISiteBuilder DetermineProject(string repositoryRoot, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_ProjectDoesNotExist, targetPath));
            }

            var solution = VsHelper.FindContainingSolution(repositoryRoot, targetPath);
            string solutionPath = solution != null ? solution.Path : null;

            return new WapBuilder(_settings,
                                  _propertyProvider,
                                  repositoryRoot,
                                  targetPath,
                                  _environment.TempPath,
                                  _environment.NuGetCachePath,
                                  solutionPath);
        }

        private enum ProjectConfigurationType
        {
            CustomDeploymentFile,
            ExplicitPointerToProjectPath,
            FindSingleVisualStudioSolutionFileRule,
            FindFirstProjectFile,
            NoBuildableFormatsDetected,
        }
    }

}
