using ProCargo.Domain.Exceptions;

namespace ProCargo.Domain.DomainRules;

/// <summary>
/// A small, explicit state machine: the only allowed moves are the ones listed.
/// Used by services before calling a status-changing stored procedure.
/// </summary>
public sealed class StatusTransitionMap<TStatus> where TStatus : struct, Enum
{
    private readonly Dictionary<TStatus, HashSet<TStatus>> _allowed = new();
    private readonly string _entityName;

    public StatusTransitionMap(string entityName)
    {
        _entityName = entityName;
    }

    public StatusTransitionMap<TStatus> Allow(TStatus from, params TStatus[] to)
    {
        if (!_allowed.TryGetValue(from, out var targets))
        {
            targets = [];
            _allowed[from] = targets;
        }

        foreach (var target in to)
        {
            targets.Add(target);
        }

        return this;
    }

    public bool CanTransition(TStatus from, TStatus to) =>
        _allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public IReadOnlyCollection<TStatus> AllowedFrom(TStatus from) =>
        _allowed.TryGetValue(from, out var targets) ? targets : [];

    public bool IsTerminal(TStatus status) => AllowedFrom(status).Count == 0;

    public void EnsureCanTransition(TStatus from, TStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidStatusTransitionException(_entityName, from.ToString(), to.ToString());
        }
    }
}
