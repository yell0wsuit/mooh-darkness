namespace Mooh.SimulationRunner
{
    /// <summary>Identifies the category of a failed scenario run.</summary>
    public enum ScenarioFailureKind
    {
        /// <summary>The source scenario is invalid.</summary>
        Validation,

        /// <summary>Observed behavior differs from an expectation.</summary>
        Expectation,
    }

    /// <summary>Reports the outcome of one self-verifying scenario.</summary>
    /// <param name="Name">The scenario name.</param>
    /// <param name="Source">The scenario source name.</param>
    /// <param name="Passed">Whether every expectation passed.</param>
    /// <param name="FailureKind">The failure category, if unsuccessful.</param>
    /// <param name="Diagnostic">A focused failure diagnostic, or an empty string on success.</param>
    public sealed record ScenarioRunResult(
        string Name,
        string Source,
        bool Passed,
        ScenarioFailureKind? FailureKind,
        string Diagnostic);
}
