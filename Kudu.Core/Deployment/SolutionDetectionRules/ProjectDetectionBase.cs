using System;
using System.Globalization;
using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.SolutionDetectionRules
{
    public abstract class ProjectDetectionBase
    {
        protected static ISiteBuilder ResolveProject(SiteBuilderFactoryDetectionConfiguration detectionConfiguration, bool tryWebSiteProject = false, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (DeploymentHelper.IsProject(detectionConfiguration.Configuration.ProjectPath))
            {
                return DetermineProject(detectionConfiguration.RepositoryRoot, detectionConfiguration.Configuration.ProjectPath, detectionConfiguration);
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetProjects(detectionConfiguration.Configuration.ProjectPath, searchOption);
            if (projects.Count > 1)
            {
                // Can't determine which project to build
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_AmbiguousProjects,
                                                                  String.Join(", ", projects)));
            }
            else if (projects.Count == 1)
            {
                return DetermineProject(detectionConfiguration.RepositoryRoot, projects[0], detectionConfiguration);
            }

            if (tryWebSiteProject)
            {
                // Website projects need a solution to build so look for one in the repository path
                // that has this website in it.
                var solutions = VsHelper.FindContainingSolutions(detectionConfiguration.RepositoryRoot, detectionConfiguration.Configuration.ProjectPath);

                // More than one solution is ambiguous
                solutions.ThrowIfAmbigious();

                if (solutions.Count == 1)
                {
                    // Unambiguously pick the root
                    return new WebSiteBuilder(detectionConfiguration.PropertyProvider,
                                              detectionConfiguration.RepositoryRoot,
                                              detectionConfiguration.Configuration.ProjectPath,
                                              detectionConfiguration.Environment.TempPath,
                                              detectionConfiguration.Environment.NuGetCachePath,
                                              solutions[0].Path);
                }
            }

            // This should only ever happen if the user specifies an invalid directory.
            // The other case where the method is called we always resolve the path so it's a non issue there.
            if (false && !Directory.Exists(detectionConfiguration.Configuration.ProjectPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectDoesNotExist,
                                                                  detectionConfiguration.Configuration.ProjectPath));
            }

            // If there's none then use the basic builder (the site is xcopy deployable)
            return new BasicBuilder(detectionConfiguration.Configuration.ProjectPath, detectionConfiguration.Environment.TempPath, detectionConfiguration.Environment.ScriptPath);
        }


        private static ISiteBuilder DetermineProject(string repositoryRoot, string targetPath, SiteBuilderFactoryDetectionConfiguration detectionConfiguration)
        {
            if (!DeploymentHelper.IsDeployableProject(targetPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectNotDeployable,
                                                                  targetPath));
            }

            if (File.Exists(targetPath))
            {
                var solution = VsHelper.FindContainingSolution(repositoryRoot, targetPath);
                string solutionPath = solution != null ? solution.Path : null;

                return new WapBuilder(detectionConfiguration.Settings,
                                      detectionConfiguration.PropertyProvider,
                                      repositoryRoot,
                                      targetPath,
                                      detectionConfiguration.Environment.TempPath,
                                      detectionConfiguration.Environment.NuGetCachePath,
                                      solutionPath);
            }

            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                              Resources.Error_ProjectDoesNotExist,
                                                              targetPath));
        }

    }
}