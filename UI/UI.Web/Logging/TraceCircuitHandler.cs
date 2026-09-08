using System.Diagnostics;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Serilog.Context;

namespace UI.Web.Logging;

/// <summary>
/// Gives every user interaction inside a Blazor circuit its own W3C trace id. SignalR clears Activity.Current
/// around each hub invocation (an invocation is not treated as a child of the long-lived /_blazor connection
/// request), so without this handler nothing inside a circuit would carry a trace id: log events would have
/// none, and HttpClient would send no traceparent header to the Service (it only injects one when
/// Activity.Current is set). This handler starts a root Activity around every inbound circuit activity —
/// browser events, navigation, JS interop callbacks, and the initial root-component render — and stamps
/// CircuitId on the events written meanwhile. The resulting trace id is the {TraceId} column in both apps'
/// logs and the "error reference" shown to the user (see ApiClient/TraceReference.cs).
/// </summary>
internal sealed class TraceCircuitHandler(ILogger<TraceCircuitHandler> logger) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Circuit {CircuitId} opened", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Circuit {CircuitId} closed", circuit.Id);
        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
        => async context =>
        {
            // Defensive: Activity.Current is already null under SignalR. If a future runtime stopped clearing
            // it, a non-null current activity would make the new one its child and the whole circuit would
            // share the /_blazor request's single trace id instead of one id per interaction.
            Activity.Current = null;
            using var activity = new Activity("UI.CircuitInbound")
            {
                ActivityTraceFlags = ActivityTraceFlags.Recorded,   // survives a future parent-based sampler on the Service
            };
            activity.Start();   // W3C ids are assigned even with no listener.

            using (LogContext.PushProperty("CircuitId", context.Circuit.Id))
            {
                await next(context);
            }
        };
}
