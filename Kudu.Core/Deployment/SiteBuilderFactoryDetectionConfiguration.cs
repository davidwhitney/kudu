using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment
{
    public class SiteBuilderFactoryDetectionConfiguration
    {
        public DeploymentConfiguration Configuration { get; set; }
        public IDeploymentSettingsManager Settings { get; set; }
        public IEnvironment Environment { get; set; }
        public IBuildPropertyProvider PropertyProvider { get; set; }

        public string RepositoryRoot { get { return Environment.RepositoryPath; } }

        public SiteBuilderFactoryDetectionConfiguration(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider)
        {
            Environment = environment;
            Settings = settings;
            PropertyProvider = propertyProvider;

            string repositoryRoot = environment.RepositoryPath;
            Configuration = new DeploymentConfiguration(repositoryRoot);
        }
    }
}