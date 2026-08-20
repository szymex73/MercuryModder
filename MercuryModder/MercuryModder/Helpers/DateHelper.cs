namespace MercuryModder.Helpers;

public class DateHelper
{
    public static int DateTimeToNum(DateTimeOffset? date)
    {
        if (date == null) return 0;

        return date.Value.Year * 1000000
            + date.Value.Month * 10000
            + date.Value.Day * 100
            + date.Value.Offset.Hours;
    }
}
