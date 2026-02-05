using LeoQ.Core.Models;

namespace LeoQ.Core.Abstractions;

public interface ILatencyModel
{
    string Name { get; }
    RunResult Run(ScenarioConfig scenario);
}
