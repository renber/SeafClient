using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Types
{
    /// <summary>
    /// Possible status codes returned by the Seafile API
    /// These are in fact HTTP status codes
    /// </summary>
    public enum SeafStatusCode
    {
        Ok = 200,
        Created = 201,
        Accepted = 202,
        Moved_Permanently = 301,
        Bad_Request = 400,
        Forbidden = 403,
        Not_Found = 404,
        Conflict = 409,
        Too_Many_Requests = 429,
        Repository_Password_Required = 440,
        Repository_Password_Magic_Required = 441,
        Internal_Server_Error = 500,
        Operation_Failed = 520
    }
}
