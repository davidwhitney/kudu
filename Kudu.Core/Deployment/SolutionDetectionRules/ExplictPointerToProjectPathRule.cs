using System;
using System.IO;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment.SolutionDetectionRules
{
    /// <summary>
    /// If the repository has an explicit pointer to a project path to be deployed then use it.
    /// </summary>
    public class ExplictPointerToProjectPathRule : ProjectDetectionBase, IProjectDetectionRules
    {
        public Tuple<bool, ISiteBuilder> Detected(SiteBuilderFactoryDetectionConfiguration detectionConfiguration, ITracer tracer)
        {
            if (String.IsNullOrEmpty(detectionConfiguration.Configuration.ProjectPath))
            {
                return new Tuple<bool, ISiteBuilder>(false, null);
            }

            tracer.Trace("Found .deployment file in repository");

            ISiteBuilder builder = ResolveProject(detectionConfiguration, tryWebSiteProject: true, searchOption: SearchOption.TopDirectoryOnly);
            return new Tuple<bool, ISiteBuilder>(true, builder);
        }

    }
}