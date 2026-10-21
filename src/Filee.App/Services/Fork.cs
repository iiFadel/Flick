// fork: switches for what Flick leaves out of upstream Filee.

namespace Filee.App.Services;

public static class Fork
{
    /// <summary>
    /// Upstream's update checks, "star us" and sponsor prompts, and the "Request this format" card all lead to the Filee
    /// project (an update would install Filee over Flick), so Flick shows none of them.
    /// </summary>
    public static bool UpstreamPrompts => false;

    /// <summary>The project Flick is based on, credited on the About page.</summary>
    public const string UpstreamName = "Filee";
}
