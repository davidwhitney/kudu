using System;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment.SolutionDetectionRules
{
    /// <summary>
    /// If there's a custom deployment file then let that take over
    /// </summary>
    public class CustomDeploymentFileRule : IProjectDetectionRules
    {
        public Tuple<bool, ISiteBuilder> Detected(SiteBuilderFactoryDetectionConfiguration detectionConfiguration, ITracer tracer)
        {
            return String.IsNullOrEmpty(detectionConfiguration.Configuration.Command)
                       ? new Tuple<bool, ISiteBuilder>(false, null)
                       : new Tuple<bool, ISiteBuilder>(true,
                                                       new CustomBuilder(detectionConfiguration.Environment.RepositoryPath,
                                                                         detectionConfiguration.Environment.TempPath, detectionConfiguration.Configuration.Command,
                                                                         detectionConfiguration.PropertyProvider));
        }
    }
}