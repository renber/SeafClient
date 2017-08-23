namespace SeafClient
{
    public static class SeafConnectionFactory
    {
        private static readonly ISeafWebConnection DefaultConnection = new SeafHttpConnection();

        /// <summary>
        ///     Returns the default implementation for ISeafWebConnection
        /// </summary>
        public static ISeafWebConnection GetDefaultConnection()
        {
            return DefaultConnection;
        }
    }
}