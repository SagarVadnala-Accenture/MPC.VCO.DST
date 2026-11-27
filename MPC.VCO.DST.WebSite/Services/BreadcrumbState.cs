using MudBlazor;

namespace MPC.VCO.DST.WebSite.Services;

/// <summary>Lets any page contribute breadcrumb segments beyond the shared "Home" root that <c>VcoLayout</c> renders.</summary>
public sealed class BreadcrumbState
{
    public IReadOnlyList<BreadcrumbItem> Trail { get; private set; } = [];

    public event Action? Changed;

    public void SetTrail(params BreadcrumbItem[] items)
    {
        Trail = items;
        Changed?.Invoke();
    }

    public void Clear() => SetTrail();
}
