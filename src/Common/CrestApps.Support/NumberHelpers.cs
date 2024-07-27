namespace CrestApps.Support;

public class NumberHelpers
{
    private const string _numericChars = "1234567890";

    public static long GetRandomNumber(int length = 10)
    {
        var random = new Random();
        var values = Enumerable.Repeat(_numericChars, length).Select(s => s[random.Next(s.Length)]).ToArray();

        return Convert.ToInt64(new string(values));
    }

    public static string NumberOnly(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        return new string(input.Where(char.IsDigit).ToArray());
    }

    public static decimal Truncate(decimal value, int precision)
    {
        var step = (decimal)Math.Pow(10, precision);
        var tmp = Math.Truncate(step * value);

        return tmp / step;
    }
}
