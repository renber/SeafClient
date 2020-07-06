using SeafClient.Types;
using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace SeafClient.Requests.ShareLinks
{
    public class ListShareLinksRequest : SessionRequest<List<SeafShareLink>>
    {
        public string LibraryId {get;set;}
        public string Path { get; set; }

        public override string CommandUri
        {
            get
            {
                var tmp = new List<string>();
                if (LibraryId!="")
                {
                    tmp.Add("repo_id=" + LibraryId);
                }
                if (Path != "")
                {
                    tmp.Add("path=" + WebUtility.UrlEncode(Path));
                }

                var add = "";
                if (tmp.Count > 0)
                {
                    add = "?" + String.Join("&", tmp);
                }

                return "api/v2.1/share-links/"+add;
            }
        }

        public ListShareLinksRequest(string authToken, string pLibraryId="", string pPath="")
       : base(authToken)
        {
            ParamUtils.ThrowOnNull(pLibraryId, "pLibraryId");
            ParamUtils.ThrowOnNull(pPath, "pPath");

            LibraryId = pLibraryId;
            Path = pPath;
        }


    }
}

