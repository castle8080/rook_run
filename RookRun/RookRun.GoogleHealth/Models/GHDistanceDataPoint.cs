using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace RookRun.GoogleHealth.Models;

public record GHDistanceDataPoint
{
    [Name("timestamp")]
    public required DateTime Timestamp { get; init; }

    [Name("distance")]
    public required double Distance { get; init; }

    [Name("data source")]
    public required string DataSource { get; init; }
}
