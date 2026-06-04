using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.Job;
public interface IJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken);
}
