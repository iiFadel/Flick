// fork: About credits the upstream project by name (Brand.Apply would turn "Filee" in the string itself into Flick).

using Filee.App.Services;

namespace Filee.App.ViewModels.Pages;

public sealed partial class AboutPageViewModel
{
    public string BasedOn => _loc.Format("about.based_on", Fork.UpstreamName);

    public bool UpstreamPrompts => Fork.UpstreamPrompts;
}
