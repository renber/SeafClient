using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    public static class SeafDateUtils
    {

        static DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();

        /// <summary>
        /// Converts a seafile timestamp (which is a UNIX timestamp) to DateTime
        /// </summary>
        /// <param name="seafTime"></param>
        /// <returns></returns>
        public static DateTime SeafileTimeToDateTime(long seafTime)
        {
            return unixEpoch.AddSeconds(seafTime);            
        }

    }
}
