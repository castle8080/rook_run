using Microsoft.Extensions.Logging;
using RookRun.Common;
using RookRun.GoogleHealth.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnitsNet;

namespace RookRun.GoogleHealth
{
    public class GHExportActivityExtractor
    {
        private readonly ILogger<GHExportActivityExtractor> _logger;

        public GHExportActivityExtractor(ILogger<GHExportActivityExtractor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExtractActivitiesAsync(string exportFilePath)
        {
            using var archive = ZipFile.OpenRead(exportFilePath);

            var exercises = await ReadExerciseRecordsAsync(archive);

            foreach (var kvp in exercises.CountBy(e => e.ActivityName))
            {
                _logger.LogInformation("Exercise: {ActivityName}, Count: {Count}", kvp.Key, kvp.Value);
            }

            // ./Google Health/Global Export Data/distance-####-##-##.json
            // ./Google Health/Global Export Data/heart_rate-####-##-##.json
            // ./Google Health/Global Export Data/steps -####-##-##.json
            // ./Google Health/Global Export Data/calories-####-##-##.json
            // ./Google Health/Global Export Data/altitude-####-##-##.json

            var distances = (await ReadDistanceDataPointsAsync(archive))
                .OrderBy(d => d.Timestamp)
                .ToList();

            var runs = exercises.Where(e => e.ActivityName.Contains("Run")).OrderBy(e => e.Start).ToList();

            foreach (var exercise in runs)
            {

                int startIndex = distances.LowerBound(exercise.GetStartTZ(), d => d.Timestamp);
                int endIndex = distances.UpperBound(exercise.GetEndTZ(), d => d.Timestamp);

                var exerciseDistances = distances.Range(startIndex, endIndex);
                var totalDistance = exerciseDistances.Select(d => d.Distance).Sum();

                var distanceMiles = Length.FromMeters(totalDistance).Miles;


                var span = exercise.End - exercise.Start;
                _logger.LogInformation(
                    "Run: {Start} -> {End}: {span}  UtcOffset: {UtcOffset}   ->  TotalDistance: {distanceMiles}",
                    exercise.Start, exercise.End, span, exercise.UtcOffset, distanceMiles);

                /*
                foreach (var d in distances.Skip(startIndex-5).Take(endIndex - startIndex + 10))
                {
                    _logger.LogInformation("    Distance: {Timestamp} -> {Distance}", d.Timestamp, d.Distance);
                }
                */
            }

            _logger.LogInformation("Total activities extracted: {TotalCount}", exercises.Count);
        }

        private async Task<IList<GHDistanceDataPoint>> ReadDistanceDataPointsAsync(ZipArchive archive)
        {
            return await ReadCSVDataPoints<GHDistanceDataPoint>(archive, new Regex(@"distance_.*.csv"));
        }

        private async Task<IList<GHExerciseRecord>> ReadExerciseRecordsAsync(ZipArchive archive)
        {
            return await ReadCSVDataPoints<GHExerciseRecord>(archive, new Regex(@"UserExercises_.*.csv"));
        }

        private async Task<IList<T>> ReadCSVDataPoints<T>(ZipArchive archive, Regex fileRegex)
        {
            List<T> dataPoints = new List<T>();

            foreach (var entry in archive.Entries)
            {
                if (fileRegex.IsMatch(entry.FullName))
                {
                    using var stream = await entry.OpenAsync();
                    using var reader = new StreamReader(stream);
                    using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
                    var records = csv.GetRecords<T>();
                    dataPoints.AddRange(records);
                }
            }

            return dataPoints;
        }
    }
}
