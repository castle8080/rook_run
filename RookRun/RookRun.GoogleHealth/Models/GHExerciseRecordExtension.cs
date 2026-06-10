using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.GoogleHealth.Models
{
    public static class GHExerciseRecordExtension
    {

        public static TimeSpan GetDuration(this GHExerciseRecord record)
        {
            return record.End - record.Start;
        }

        public static DateTimeOffset GetStartTZ(this GHExerciseRecord record)
        {
            return new DateTimeOffset(record.Start, record.UtcOffset);
        }

        public static DateTimeOffset GetEndTZ(this GHExerciseRecord record)
        {
            return new DateTimeOffset(record.End, record.UtcOffset);
        }
    }
}
