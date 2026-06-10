using Microsoft.Extensions.Logging;
using RookRun.GoogleHealth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.Job
{
    public class ProcessGoogleHealthExportJob : IJob
    {

        private readonly ILogger<ProcessGoogleHealthExportJob> _logger;
        private readonly GHExportActivityExtractor _ghExportActivityExtractor;

        public ProcessGoogleHealthExportJob(
            ILogger<ProcessGoogleHealthExportJob> logger,
            GHExportActivityExtractor ghExportActivityExtractor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ghExportActivityExtractor = ghExportActivityExtractor ?? throw new ArgumentNullException(nameof(ghExportActivityExtractor));
        }

        /// <summary>
        /// Processes google health export data.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            const string fileName = @"C:\Users\bryan\source\repos\rook_run\RookRun\RookRun\var\takeout-20260607T115315Z-3-001.zip";

            _logger.LogInformation("Starting Process Google Health Export job.");

            await _ghExportActivityExtractor.ExtractActivitiesAsync(fileName);
        }
    }
}
