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
        private readonly DeploymentConfiguration _config;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly IBuildPropertyProvider _propertyProvider;

        private string RepositoryRoot { get { return _environment.RepositoryPath; } }
        
        public SiteBuilderFactory(IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _settings = settings;
            _propertyProvider = propertyProvider;
            _environment = environment;
            _config = new DeploymentConfiguration(RepositoryRoot);
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger)
        {
            var artifactDetectionRules = new Dictionary<ProjectConfigurationType, Func<bool>>
                {
                    {ProjectConfigurationType.CustomDeploymentCommandSpecified, ()=> _config.Command.IsConfigured()},
                    {ProjectConfigurationType.ProjectPathSpecifiedInConfiguration, ()=> _config.ProjectPath.IsConfigured() && DeploymentHelper.IsProject(_config.ProjectPath)},
                    {ProjectConfigurationType.SingleVsSolution,()=> FindFirstSolution() != null},
                    {ProjectConfigurationType.SingleLooseProjectFile, CheckForLooseProjects},
                    {ProjectConfigurationType.NoBuildableArtifactsDetected, ()=>true},
                };

            var detectedArtifactType = artifactDetectionRules.Where(rule => rule.Value()).Select(rule => rule.Key).FirstOrDefault();

            var buildProcessMappings = new Dictionary<ProjectConfigurationType, Func<ISiteBuilder>>
                {
                    {ProjectConfigurationType.CustomDeploymentCommandSpecified, () => new CustomBuilder(_environment.RepositoryPath, _environment.TempPath, _config.Command, _propertyProvider)},
                    {ProjectConfigurationType.ProjectPathSpecifiedInConfiguration, DetermineDeployableProject},
                    {ProjectConfigurationType.SingleVsSolution, WebBuilder},
                    {ProjectConfigurationType.SingleLooseProjectFile,()=> ResolveProject()},
                    {ProjectConfigurationType.NoBuildableArtifactsDetected, () => new BasicBuilder(_environment.RepositoryPath, _environment.TempPath, _environment.ScriptPath)},
                };


            return buildProcessMappings[detectedArtifactType]();
        }

        private bool CheckForLooseProjects()
        {
            var projects = DeploymentHelper.GetProjects(_config.ProjectPath);
            projects.ThrowIfMultipleFound(); // Multiple loose projects not supported.
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
            if (DeploymentHelper.IsProject(_config.ProjectPath))
            {
                return DetermineDeployableProject();
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetProjects(_config.ProjectPath, searchOption);
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
                var solutions = VsHelper.FindContainingSolutions(RepositoryRoot, _config.ProjectPath);
                solutions.ThrowIfMultipleSolutionsFound();

                if (solutions.Count == 1)
                {
                    // Unambiguously pick the root
                    return new WebSiteBuilder(_propertyProvider,
                                              RepositoryRoot,
                                              _config.ProjectPath,
                                              _environment.TempPath,
                                              _environment.NuGetCachePath,
                                              solutions[0].Path);
                }
            }

            // If there's none then use the basic builder (the site is xcopy deployable)
            return new BasicBuilder(_config.ProjectPath, _environment.TempPath, _environment.ScriptPath);
        }

        private ISiteBuilder DetermineDeployableProject()
        {
            if (!DeploymentHelper.IsDeployableProject(_config.ProjectPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectNotDeployable,
                                                                  _config.ProjectPath));
            }

            return DetermineProject(RepositoryRoot, _config.ProjectPath);
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
            CustomDeploymentCommandSpecified,
            ProjectPathSpecifiedInConfiguration,
            SingleVsSolution,
            SingleLooseProjectFile,
            NoBuildableArtifactsDetected,
        }
    }

}
