namespace EF10_NewFeaturesDbLibrary;

using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class LoggingCommandInterceptor : DbCommandInterceptor
{
    private readonly ILogger _logger;
    private readonly bool _writeCalloutToConsole;

    public LoggingCommandInterceptor(ILoggerFactory loggerFactory, bool writeCalloutToConsole = false)
    {
        _logger = loggerFactory.CreateLogger("EFCustomInterceptor");
        _writeCalloutToConsole = writeCalloutToConsole;
    }

    private void WriteCallout(string callbackName)
    {
        if (_writeCalloutToConsole)
        {
            Console.WriteLine($">>> DbCommandInterceptor.{callbackName} CALLED <<<");
        }
    }

    public override InterceptionResult<DbCommand> CommandCreating(
        CommandCorrelatedEventData eventData,
        InterceptionResult<DbCommand> result)
    {
        WriteCallout(nameof(CommandCreating));
        _logger.LogInformation("[LoggingCommandInterceptor] Creating command for context {Context}", eventData.Context?.GetType().Name);
        return base.CommandCreating(eventData, result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        WriteCallout(nameof(NonQueryExecuting));
        _logger.LogInformation("[LoggingCommandInterceptor] Executing NonQuery: {CommandText}", command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        WriteCallout(nameof(NonQueryExecutingAsync));
        _logger.LogInformation("[LoggingCommandInterceptor] Executing NonQueryAsync: {CommandText}", command.CommandText);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        WriteCallout(nameof(ReaderExecuting));
        _logger.LogInformation("[LoggingCommandInterceptor] Executing Query: {CommandText}", command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        WriteCallout(nameof(ReaderExecutingAsync));
        _logger.LogInformation("[LoggingCommandInterceptor] Executing Query (Async): {CommandText}", command.CommandText);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

