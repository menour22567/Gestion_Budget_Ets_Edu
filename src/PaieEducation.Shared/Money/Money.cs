using System.Globalization;

namespace PaieEducation.Shared.Money;

public readonly record struct Money(decimal Amount, string Currency)
{
    public static readonly Money Zero = new(0m, "DZD");

    public Money(decimal amount) : this(amount, "DZD") { }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            ThrowCurrencyMismatch(left.Currency, right.Currency);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            ThrowCurrencyMismatch(left.Currency, right.Currency);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal factor)
        => new(money.Amount * factor, money.Currency);

    public static Money operator -(Money money) => new(-money.Amount, money.Currency);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;
    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public Money Arrondir(int decimals)
        => new(Math.Round(Amount, decimals, MidpointRounding.AwayFromZero), Currency);

    public override string ToString()
        => $"{Amount.ToString("N2", CultureInfo.InvariantCulture)} {Currency}";

    private static void ThrowCurrencyMismatch(string c1, string c2)
        => throw new InvalidOperationException($"Devises incompatibles : {c1} ≠ {c2}.");
}
