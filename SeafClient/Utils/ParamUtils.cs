using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Utils
{
    public static class ParamUtils
    {
        /// <summary>
        /// Throws a ArgumentNullException if the given object is null
        /// </summary>
        /// <param name="param"></param>
        /// <param name="parameterName">The parameterName to use in the ArgumentNullException</param>
        public static void ThrowOnNull(this Object param, String parameterName)
        {
            if (param == null)
                throw new ArgumentNullException("The parameter " + parameterName + " must not be null.");
        }

    }
}
