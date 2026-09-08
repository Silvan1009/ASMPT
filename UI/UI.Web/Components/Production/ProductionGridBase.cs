using Microsoft.AspNetCore.Components;
using MudBlazor;
using UI.Web.ApiClient;

namespace UI.Web.Components.Production;

/// <summary>
/// Shared behaviour of the Orders, Boards and Components grids: loading with the debounced search term,
/// opening the create/edit dialog, single and batch deletion with confirmation, and API error handling.
/// The derived grid supplies the markup and the entity-specific API calls.
/// </summary>
public abstract class ProductionGridBase<TItem> : ComponentBase, IAsyncDisposable
    where TItem : class, IProductionItem
{
    private CancellationTokenSource? _loadCts;
    private ILogger? _logger;

    [Inject]
    protected IServiceApiClient Api { get; set; } = default!;

    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private ILoggerFactory LoggerFactory { get; set; } = default!;

    /// <summary>Logs under the derived grid's type name.</summary>
    protected ILogger Logger => _logger ??= LoggerFactory.CreateLogger(GetType());

    protected List<TItem> Items { get; set; } = [];

    protected HashSet<TItem> Selected { get; set; } = [];

    /// <summary>The search box value; bound with a debounce, each change reloads the grid.</summary>
    protected string? Search { get; set; }

    protected bool Loading { get; set; } = true;

    protected bool SessionExpired { get; private set; }

    protected string? Error { get; private set; }

    protected string? ErrorReference { get; private set; }

    /// <summary>Singular, lower case, for messages: "order".</summary>
    protected abstract string EntityName { get; }

    /// <summary>Plural, lower case, for messages: "orders".</summary>
    protected abstract string EntityNamePlural { get; }

    /// <summary>Appended to the delete confirmation, e.g. what other entities the deletion affects. Empty by default.</summary>
    protected virtual string DeleteWarning => "";

    protected abstract Task<ICollection<TItem>> FetchAsync(string? search, CancellationToken cancellationToken);

    protected abstract Task DeleteOneAsync(Guid id);

    protected abstract Task DeleteManyAsync(BatchDeleteRequest request);

    /// <summary>Shows the create (null) or edit dialog and returns its reference.</summary>
    protected abstract Task<IDialogReference> ShowDialogAsync(TItem? item);

    // The page is prerendered: loading there would only be repeated once the circuit starts, so the data is
    // fetched exactly once, in the interactive instance. The prerendered grid shows its loading state.
    protected override Task OnInitializedAsync() => RendererInfo.IsInteractive ? LoadAsync() : Task.CompletedTask;

    /// <summary>Reloads the grid with the current search term, cancelling a load that is still in flight so a
    /// slower, older response can never overwrite a newer one.</summary>
    protected async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = _loadCts = new CancellationTokenSource();

        Loading = true;
        Error = null;
        ErrorReference = null;
        try
        {
            var items = await FetchAsync(string.IsNullOrWhiteSpace(Search) ? null : Search, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            Items = [.. items];
            Selected = [];
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ApiErrors.IsApiFailure(ex))
        {
            if (!cts.IsCancellationRequested)
            {
                ShowError(ex, $"Loading {EntityNamePlural} failed.");
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                Loading = false;
            }
        }
    }

    protected async Task OpenDialogAsync(TItem? item)
    {
        var dialog = await ShowDialogAsync(item);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            Snackbar.Add(item is null ? $"{Capitalized(EntityName)} created." : $"{Capitalized(EntityName)} saved.", Severity.Success);
            await LoadAsync();
        }
    }

    protected async Task DeleteAsync(TItem item)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            $"Delete {EntityName}", $"Delete \"{item.Name}\"?{DeleteWarning}", yesText: "Delete", cancelText: "Cancel");
        if (confirmed != true)
        {
            return;
        }

        try
        {
            await DeleteOneAsync(item.Id);
            Snackbar.Add($"{Capitalized(EntityName)} deleted.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception ex) when (ApiErrors.IsApiFailure(ex))
        {
            ShowError(ex, $"Deleting the {EntityName} failed.");
        }
    }

    protected async Task DeleteSelectedAsync()
    {
        var ids = Selected.Select(item => item.Id).ToList();
        var confirmed = await DialogService.ShowMessageBoxAsync(
            $"Delete {EntityNamePlural}", $"Delete {ids.Count} selected {EntityName}(s)?{DeleteWarning}", yesText: "Delete", cancelText: "Cancel");
        if (confirmed != true)
        {
            return;
        }

        try
        {
            await DeleteManyAsync(new BatchDeleteRequest { Ids = ids });
            Snackbar.Add($"{ids.Count} {EntityName}(s) deleted.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception ex) when (ApiErrors.IsApiFailure(ex))
        {
            ShowError(ex, $"Deleting the {EntityNamePlural} failed.");
        }
    }

    /// <summary>Records a failed call: an expired session switches the grid to its reload hint, anything else
    /// is logged and shown as <paramref name="message"/>.</summary>
    protected void ShowError(Exception ex, string message)
    {
        if (ApiErrors.IsSessionExpired(ex))
        {
            Logger.LogInformation("Session expired while working with {Entity}; asking the user to reload", EntityNamePlural);
            SessionExpired = true;
            return;
        }

        ErrorReference = TraceReference.For(ex);
        Logger.LogError(ex, "Service call failed ({Entity}): {UserMessage}", EntityNamePlural, message);
        Error = message;
    }

    public virtual ValueTask DisposeAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        return ValueTask.CompletedTask;
    }

    private static string Capitalized(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
