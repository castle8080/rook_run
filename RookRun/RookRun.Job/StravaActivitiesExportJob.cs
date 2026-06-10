using CsvHelper;
using Microsoft.Extensions.Logging;
using RookRun.Strava.Repositories;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RookRun.Job
{
    public class StravaActivitiesExportJob : IJob
    {

        private readonly IStravaActivitiesRepository _stravaActivitiesRepository;
        private readonly ILogger<StravaActivitiesExportJob> _logger;

        public StravaActivitiesExportJob(
            IStravaActivitiesRepository stravaActivitiesRepository,
            ILogger<StravaActivitiesExportJob> logger)
        {
            _stravaActivitiesRepository = stravaActivitiesRepository ?? throw new ArgumentNullException(nameof(stravaActivitiesRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {

            var activities = await _stravaActivitiesRepository
                .SearchAsync(DateTimeOffset.UtcNow.AddDays(-365 * 10), DateTimeOffset.UtcNow.AddDays(10), cancellationToken);


            // Convert to json to view the structure dynamically.
            var activitiesJson = activities.Select(a => JsonSerializer.SerializeToNode(a) as JsonObject).ToList();

            string outputFile = "var/strava_activities.csv";
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

            using var writer = new StreamWriter(outputFile);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Get all unique keys in seen order.
            List<string> keys = new();
            HashSet<string> seenKeys = new();

            foreach (var activity in activitiesJson)
            {
                foreach (var property in activity!)
                {
                    if (seenKeys.Add(property.Key))
                    {
                        keys.Add(property.Key);
                    }
                }
            }

            // Write Header row
            foreach (var key in keys)
            {
                csv.WriteField(key);
            }
            csv.NextRecord();

            // Write data rows
            foreach (var activity in activitiesJson)
            {
                foreach (var key in keys)
                {
                    activity!.TryGetPropertyValue(key, out var value);
                    csv.WriteField(value != null ? value.ToString() : "");
                }
                csv.NextRecord();
            }

            _logger.LogInformation("Write data to {OutputFile}", outputFile);
        }
    }
}
