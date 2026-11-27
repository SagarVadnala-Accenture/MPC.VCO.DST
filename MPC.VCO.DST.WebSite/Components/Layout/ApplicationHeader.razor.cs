using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MPC.VCO.DST.WebSite.Components.Layout
{
    public partial class ApplicationHeader
    {
        [Inject]
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

        private IDisposable? _clockTimer;
        private DateTimeOffset _now;

        private string DisplayName = "DST User";
        private string UserInitials = "DU";

        private string DisplayDate
        {
            get
            {
                var day = _now.Day;
                var suffix = day % 100 is 11 or 12 or 13 ? "th" : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
                return $"{_now:dddd} - {_now:MMMM} {day}{suffix}, {_now:yyyy}";
            }
        }

        private string CurrentDateIso => _now.ToString("yyyy-MM-dd");

        protected override void OnInitialized()
        {
            _now = TimeProvider.GetLocalNow();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await UpdateUserAsync();

                _clockTimer = TimeProvider.CreateTimer(
                    callback: _ =>
                    {
                        _ = InvokeAsync(() =>
                        {
                            _now = TimeProvider.GetLocalNow();
                            StateHasChanged();
                        });
                    },
                    state: null,
                    dueTime: TimeSpan.FromMinutes(1),
                    period: TimeSpan.FromMinutes(1));
            }
        }

        private async Task UpdateUserAsync()
        {
            if (AuthenticationStateTask is null)
            {
                return;
            }

            AuthenticationState authState = await AuthenticationStateTask;
            ClaimsPrincipal user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                return;
            }

            string? name =
                user.FindFirst("name")?.Value
                ?? user.Identity.Name
                ?? user.FindFirst(ClaimTypes.Upn)?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value;

            if (!string.IsNullOrWhiteSpace(name))
            {
                DisplayName = name.Trim();
                UserInitials = DeriveInitials(DisplayName);
                await InvokeAsync(StateHasChanged);
            }
        }

        private static string DeriveInitials(string displayName)
        {
            string[] segments = displayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length == 0)
            {
                return "DU";
            }

            if (segments.Length == 1)
            {
                return segments[0][0].ToString().ToUpperInvariant();
            }

            return string.Concat(segments[0][0], segments[^1][0]).ToUpperInvariant();
        }

        public void Dispose()
        {
            _clockTimer?.Dispose();
        }
    }
}

