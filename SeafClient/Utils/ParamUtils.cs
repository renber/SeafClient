using System;

namespace SeafClient.Utils
{
    public static class ParamUtils
    {
        /// <summary>
        ///     Throws a <see cref="ArgumentNullException"/> if the given object is null
        /// </summary>
        /// <param name="param"></param>
        /// <param name="parameterName">The parameterName to use in the <see cref="ArgumentNullException"/></param>
        public static void ThrowOnNull(this object param, string parameterName)
        {
            if (param == null)
                throw new ArgumentNullException("The parameter " + parameterName + " must not be null.");
        }
    }
}