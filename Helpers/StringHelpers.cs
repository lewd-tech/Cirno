﻿namespace Cliptok.Helpers
{
    public class StringHelpers
    {
        // https://stackoverflow.com/a/2776689
        public static string Truncate(string value, int maxLength, bool elipsis = false)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var strOut = value.Length <= maxLength ? value : value[..maxLength];
            if (elipsis && value.Length > maxLength)
                return strOut + '…';
            else
                return strOut;
        }

        public static string Pad(long id)
        {
            if (id < 0)
                return id.ToString("D4");
            else
                return id.ToString("D5");
        }
    }
}
