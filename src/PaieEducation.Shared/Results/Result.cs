namespace PaieEducation.Shared.Results;

/// <summary>
/// Résultat d'une opération sans valeur de retour. Encapsule succès/échec et une
/// <see cref="Error"/>, afin d'éviter l'usage des exceptions pour les cas métier attendus.
/// </summary>
public class Result
{
    /// <summary>Construit un résultat en garantissant la cohérence succès/erreur.</summary>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Un résultat réussi ne peut pas porter d'erreur.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Un résultat en échec doit porter une erreur.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Vrai si l'opération a réussi.</summary>
    public bool IsSuccess { get; }

    /// <summary>Vrai si l'opération a échoué.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Erreur associée (<see cref="Error.None"/> si succès).</summary>
    public Error Error { get; }

    /// <summary>Crée un résultat réussi sans valeur.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Crée un résultat en échec.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Crée un résultat réussi porteur d'une valeur.</summary>
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);

    /// <summary>Crée un résultat en échec typé.</summary>
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

/// <summary>Résultat d'une opération porteuse d'une valeur de type <typeparamref name="T"/>.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
        => _value = value;

    /// <summary>Valeur du résultat ; lève une exception si le résultat est en échec.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Impossible d'accéder à la valeur d'un résultat en échec.");

    /// <summary>Conversion implicite d'une valeur en résultat réussi.</summary>
    public static implicit operator Result<T>(T value) => Success(value);
}
