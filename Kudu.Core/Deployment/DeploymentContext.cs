﻿using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class DeploymentContext
    {
        /// <summary>
        /// Path to the manifest file.
        /// </summary>
        public string ManifestPath { get; set; }

        /// <summary>
        /// Writes diagnostic output to the trace.
        /// </summary>
        public ITracer Tracer { get; set; }

        /// <summary>
        /// The logger for the current operation (building, copying files etc.)
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// The logger for the entire deployment operation.
        /// </summary>
        public ILogger GlobalLogger { get; set; }

        public string OutputPath { get; set; }
    }
}
