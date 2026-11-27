using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Components.Pages.Shared
{
    public partial class CreateModelDrawer
    {
        [Parameter] public bool Open { get; set; }
        [Parameter] public EventCallback<bool> OpenChanged { get; set; }
        [Parameter] public EventCallback<ModelUpsertRequest> ModelCreated { get; set; }

        private readonly ModelUpsertRequest _configuration = new()
        {
            Status = "Draft"
        };

        private bool _saving;

        private async Task Create()
        {
            if (_saving)
            {
                return;
            }

            _saving = true;

            try
            {
                await ModelCreated.InvokeAsync(_configuration);
            }
            finally
            {
                _saving = false;
            }
        }

        private Task Close() => OpenChanged.InvokeAsync(false);

        private Task HandleKey(KeyboardEventArgs args) => args.Key == "Escape" ? Close() : Task.CompletedTask;
    }
}
