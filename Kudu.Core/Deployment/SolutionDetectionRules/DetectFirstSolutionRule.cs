using System;
using System.IO;
using System.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.SolutionDetectionRules
{
    public class DetectFirstSolutionRule : ProjectDetectionBase, IProjectDetectionRules
    {
        public Tuple<bool, ISiteBuilder> Detected(SiteBuilderFactoryDetectionConfiguration detectionConfiguration, ITracer tracer)
        {
            // Get all solutions in the current repository path
            var solutions = VsHelper.GetSolutions(detectionConfiguration.RepositoryRoot).ToList();

            if (!solutions.Any())
            {
                var bldr = ResolveProject(detectionConfiguration, searchOption: SearchOption.AllDirectories);
                return new Tuple<bool, ISiteBuilder>(true, bldr);
            }

            solutions.ThrowIfAmbigious();

            VsSolution discoveredSolution = solutions[0];

            // We need to determine what project to deploy so get a list of all web projects and
            // figure out with some heuristic, which one to deploy. 

            // TODO: Pick only 1 and throw if there's more than one
            VsSolutionProject project = discoveredSolution.Projects.FirstOrDefault(p => p.IsWap || p.IsWebSite);

            if(project == null)
            {
                return new Tuple<bool, ISiteBuilder>(false, null);
            }

            var builder = project.IsWap
                              ? (ISiteBuilder) new WapBuilder(detectionConfiguration.Settings,
                                                              detectionConfiguration.PropertyProvider,
                                                              detectionConfiguration.RepositoryRoot,
                                                              project.AbsolutePath,
                                                              detectionConfiguration.Environment.TempPath,
                                                              detectionConfiguration.Environment.NuGetCachePath,
                                                              discoveredSolution.Path)

                              : new WebSiteBuilder(detectionConfiguration.PropertyProvider,
                                                   detectionConfiguration.RepositoryRoot,
                                                   project.AbsolutePath,
                                                   detectionConfiguration.Environment.TempPath,
                                                   detectionConfiguration.Environment.NuGetCachePath,
                                                   discoveredSolution.Path);

            return new Tuple<bool, ISiteBuilder>(true, builder);

        }
    }
}