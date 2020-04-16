using System;

namespace SeafClient.Utils
{
    /// <summary>
    ///     Class for converting <see cref="DateTime"/> and Unix timestamps
    /// </summary>
    public static class SeafDateUtils
    {
        // the epoch has to be in UTC
        private static DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        ///     Converts a seafile timestamp (which is a UNIX timestamp) to a local DateTime
        /// </summary>
        /// <param name="seafTime">The timestamp from seafile (UNIX timestamp)</param>
        /// <returns>The timestamp in local time</returns>
        public static DateTime SeafileTimeToDateTime(long seafTime)
        {
            return _unixEpoch.AddSeconds(seafTime).ToLocalTime();
        }

        /// <summary>
        ///     Converts a DateTime to a seafile timestamp (which is a UNIX timestamp)
        /// </summary>
        public static long DateTimeToSeafileTime(DateTime dateTime)
        {
            return (long) dateTime.ToUniversalTime().Subtract(_unixEpoch).TotalSeconds;
        }
    }
}