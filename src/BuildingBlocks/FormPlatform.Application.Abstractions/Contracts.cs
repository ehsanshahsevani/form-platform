namespace FormPlatform.Application.Abstractions;

public sealed record Error(string Code, string Description);
public class Result
{
	protected Result(bool isSuccess, Error? error)
	{
		if (isSuccess == (error is not null))
		{
			throw new ArgumentException("A successful result cannot contain an error, and a failed result must contain one.");
		}

		IsSuccess = isSuccess;
		Error = error;
	}

	public bool IsSuccess { get; }
	public Error? Error { get; }
	public static Result Success() => new(true, null);
	public static Result Failure(Error error) => new(false, ValidateError(error));

	protected static Error ValidateError(Error error)
	{
		ArgumentNullException.ThrowIfNull(error);
		if (string.IsNullOrWhiteSpace(error.Code) || string.IsNullOrWhiteSpace(error.Description))
		{
			throw new ArgumentException("An error must have a code and description.", nameof(error));
		}

		return error;
	}
}
public sealed class Result<T> : Result
{
	private readonly T? _value;

	private Result(T value) : base(true, null)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value), "A successful result must contain a value.");
		}

		_value = value;
	}

	private Result(Error error) : base(false, ValidateError(error))
	{
	}

	public T Value
	{
		get
		{
			if (!IsSuccess)
			{
				throw new InvalidOperationException("A failed result has no value.");
			}

			return _value is not null ? _value : throw new InvalidOperationException("A successful result has no value.");
		}
	}
	public static Result<T> Success(T value) => new(value);
	public new static Result<T> Failure(Error error) => new(error);
}
public interface ICurrentUser { Guid UserId { get; } }
public interface IClock { DateTime UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
