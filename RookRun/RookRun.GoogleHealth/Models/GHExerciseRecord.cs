using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.GoogleHealth.Models
{
    public sealed record GHExerciseRecord
    {
        [Name("exercise_id")]
        public required string Id { get; init; }

        [Name("exercise_start")]
        public required DateTime Start { get; init; }

        [Name("exercise_end")]
        public required DateTime End { get; init; }

        [Name("utc_offset")]
        public required TimeSpan UtcOffset { get; init; }

        [Name("activity_name")]
        public required string ActivityName { get; init; }
    }
}
