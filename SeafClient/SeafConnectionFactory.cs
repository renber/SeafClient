namespace SeafClient
{
    public static class SeafConnectionFactory
    {
        private static readonly ISeafWebConnection DefaultConnection;

        /// <summary>
        /// Returns an instance of the default implementation for ISeafWebConnection
        /// </summary>
        public static ISeafWebConnection GetDefaultConnection()
        {
            return new SeafHttpConnection();
        }
    }
}