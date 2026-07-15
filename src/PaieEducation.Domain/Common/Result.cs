namespace PaieEducation.Domain.Common;

/// <summary>
/// Résultat du Domain de calcul. Copie minimale du patron <c>Result</c> — voir
/// <see cref="Error"/> pour la justification (pas de dépendance à Shared).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error.Code != "")
            throw new InvalidOperationException("Un résultat réussi ne peut pas porter d'erreur.");
        if (!isSuccess && error.Code == "")
            throw new InvalidOperationException("Un résultat en échec doit porter une erreur.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, new Error("", ""));
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, new Error("", ""));
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;
    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Impossible d'accéder à la valeur d'un résultat en échec.");

    public static implicit operator Result<T>(T value) => Success(value);
}
