namespace FormPlatform.Domain.Abstractions;

public interface IDomainEvent { DateTime OccurredAtUtc { get; } }
public abstract record DomainEvent(DateTime OccurredAtUtc) : IDomainEvent;
public abstract class Entity<TId>(TId id) where TId : notnull
{
	public TId Id { get; protected set; } = id;
	public override bool Equals(object? obj) => obj is Entity<TId> other && GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
	public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id) where TId : notnull
{
	private readonly List<IDomainEvent> _domainEvents = [];
	public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
	protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
	public void ClearDomainEvents() => _domainEvents.Clear();
}
public abstract class ValueObject
{
	protected abstract IEnumerable<object?> GetEqualityComponents();
	public override bool Equals(object? obj) => obj is ValueObject other && GetType() == other.GetType() && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
	public override int GetHashCode() => GetEqualityComponents().Aggregate(17, (hash, item) => HashCode.Combine(hash, item));
}
