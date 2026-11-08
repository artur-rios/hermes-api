namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// Tests that replace the console or set environment variables change state the whole process shares, so they
/// cannot run alongside one another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ProcessStateCollection
{
    public const string Name = "Process state";
}
