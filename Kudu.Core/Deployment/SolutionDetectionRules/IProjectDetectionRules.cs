using System;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment.SolutionDetectionRules
{
    public interface IProjectDetectionRules
    {
        Tuple<bool, ISiteBuilder> Detected(SiteBuilderFactoryDetectionConfiguration detectionConfiguration, ITracer tracer);
    }
}