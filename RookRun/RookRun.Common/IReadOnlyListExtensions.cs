using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.Common;

public static class IReadOnlyListExtensions
{

    public static IEnumerable<T> Range<T>(
        this IReadOnlyList<T> list,
        int start,
        int endExclusive)
    {
        for (int i = start; i < endExclusive; i++)
        {
            yield return list[i];
        }
    }
}
