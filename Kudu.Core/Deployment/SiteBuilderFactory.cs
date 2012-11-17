using System.Collections.Generic;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.SolutionDetectionRules;

namespace Kudu.Core.Deployment
{
    public class SiteBuilderFactory : ISiteBuilderFactory
    {
        private readonly SiteBuilderFactoryDetectionConfiguration _perProjectConfig;

        public SiteBuilderFactory(IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _perProjectConfig = new SiteBuilderFactoryDetectionConfiguration(environment, settings, propertyProvider);
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger)
        {
            var detectionRules = new List<IProjectDetectionRules>
                {
                    new CustomDeploymentFileRule(),
                    new ExplictPointerToProjectPathRule(),
                    new DetectFirstSolutionRule()
                };

            foreach (var detected in detectionRules.Select(rule => rule.Detected(_perProjectConfig, tracer)).Where(detected => detected.Item1))
            {
                return detected.Item2;
            }

            return new BasicBuilder(_perProjectConfig.Environment.RepositoryPath, 
                                    _perProjectConfig.Environment.TempPath,
                                    _perProjectConfig.Environment.ScriptPath);
        }

    }
}
