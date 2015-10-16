using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient
{
    public static class SeafConnectionFactory
    {
        static ISeafWebConnection defaultConnection = new SeafHttpConnection();

        /// <summary>
        /// Returns the default implementation for ISeafWebConnection
        /// </summary>        
        public static ISeafWebConnection GetDefaultConnection()
        {
            return defaultConnection;
        }
    }
}
