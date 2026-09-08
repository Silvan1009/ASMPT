using Microsoft.AspNetCore.Components;
using MudBlazor;
using UI.Web.ApiClient;

namespace UI.Web.Components.Production;

/// <summary>
/// Shared behaviour of the order, board and component create/edit dialogs: the name/description fields,
/// form validation before saving, closing with the saved DTO, and mapping a failed save onto the dialog
/// (session expiry, server-side field errors, other problems). The derived dialog supplies the markup, its
/// extra fields and the API call.
/// </summary>
public abstract class EntityDialogBase : ComponentBase
{
    private IDictionary<string, ICollection<string>>? _fieldErrors;
    private ILogger? _logger;

    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    protected IServiceApiClient Api { get; set; } = default!;

    [Inject]
    private ILoggerFactory LoggerFactory { get; set; } = default!;

    /// <summary>Logs under the derived dialog's type name.</summary>
    protected ILogger Logger => _logger ??= LoggerFactory.CreateLogger(GetType());

    protected MudForm Form { get; set; } = default!;

    protected string Name { get; set; } = "";

    protected string? Description { get; set; }

    /// <summary>Message shown above the form, when set.</summary>
    protected string? Error { get; set; }

    protected bool Saving { get; private set; }

    /// <summary>Singular, lower case, for log messages: "order".</summary>
    protected abstract string EntityName { get; }

    /// <summary>The request property names this dialog renders a field for; server errors on any other key
    /// are shown in the message above the form instead of being lost.</summary>
    protected abstract IReadOnlyCollection<string> KnownFields { get; }

    /// <summary>The name as sent to the API.</summary>
    protected string TrimmedName => Name.Trim();

    /// <summary>The description as sent to the API: null when blank.</summary>
    protected string? TrimmedDescription => string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

    /// <summary>Sends the create or update request and returns the saved DTO.</summary>
    protected abstract Task<object> SaveCoreAsync();

    protected void Cancel() => MudDialog.Cancel();

    protected async Task SaveAsync()
    {
        await Form.ValidateAsync();
        if (!Form.IsValid)
        {
            return;
        }

        Saving = true;
        Error = null;
        _fieldErrors = null;
        try
        {
            var saved = await SaveCoreAsync();
            MudDialog.Close(DialogResult.Ok(saved));
        }
        catch (Exception ex) when (ApiErrors.IsApiFailure(ex))
        {
            HandleSaveFailure(ex);
        }
        finally
        {
            Saving = false;
        }
    }

    protected bool HasError(string field) => ErrorText(field) is not null;

    /// <summary>The server's first validation message for <paramref name="field"/> (case-insensitive), if any.</summary>
    protected string? ErrorText(string field) => _fieldErrors?
        .FirstOrDefault(e => string.Equals(e.Key, field, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();

    private void HandleSaveFailure(Exception ex)
    {
        if (ApiErrors.IsSessionExpired(ex))
        {
            Logger.LogInformation("Session expired while saving the {Entity}", EntityName);
            Error = "Your session has expired. Reload the page to sign in again.";
            return;
        }

        if (ApiErrors.TryGetFieldErrors(ex) is { } fieldErrors)
        {
            _fieldErrors = fieldErrors;
            Logger.LogInformation("Saving the {Entity} was rejected by validation on {Fields}", EntityName, fieldErrors.Keys);
            var unmatched = fieldErrors
                .Where(e => !KnownFields.Contains(e.Key, StringComparer.OrdinalIgnoreCase))
                .SelectMany(e => e.Value)
                .ToList();
            Error = unmatched.Count > 0 ? string.Join(" ", unmatched) : "Please correct the highlighted fields.";
            return;
        }

        Logger.LogError(ex, "Saving the {Entity} failed", EntityName);
        Error = $"{ApiErrors.Describe(ex, "Saving failed. Please try again.")} Error reference: {TraceReference.For(ex)}.";
    }
}
