using ProofPath.Application.Analysis;
using ProofPath.Application.Resumes;

namespace ProofPath.Worker;

public sealed class Worker(IServiceScopeFactory scopes, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPrivateFileCleanup>().RunAsync(stoppingToken);
                var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
                var lease = await queue.ClaimAsync(stoppingToken);
                if (lease is null) { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); continue; }
                var handler = scope.ServiceProvider.GetServices<IAnalysisHandler>().SingleOrDefault(item => item.Kind == lease.Kind);
                if (handler is null)
                {
                    await queue.FailAsync(lease, "HANDLER_NOT_CONFIGURED", false, stoppingToken);
                    continue;
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(4));
                var monitor = MonitorLeaseAsync(lease, timeout);
                try
                {
                    if (!await queue.IsCurrentAsync(lease, timeout.Token)) continue;
                    var output = await handler.ProcessAsync(lease, timeout.Token);
                    await queue.CompleteAsync(lease, output, stoppingToken);
                }
                catch (AnalysisFailure failure)
                {
                    await queue.FailAsync(lease, failure.Code, failure.Retryable, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    await queue.FailAsync(lease, "ANALYSIS_TIMEOUT", true, stoppingToken);
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    await queue.FailAsync(lease, "ANALYSIS_FAILED", false, stoppingToken);
                }
                finally { await timeout.CancelAsync(); await monitor; }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                // Do not log provider/database payloads that may contain private contents or credentials.
                logger.LogWarning("Analysis queue unavailable; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task MonitorLeaseAsync(AnalysisLease lease, CancellationTokenSource work)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(work.Token))
            {
                // A separate scope avoids concurrent operations on the handler's EF context.
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<IAnalysisQueue>().IsCurrentAsync(lease, work.Token)) continue;
                await work.CancelAsync(); return;
            }
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested) { }
        catch (Exception) { await work.CancelAsync(); }
    }
}
