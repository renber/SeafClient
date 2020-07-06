using System;
using Newtonsoft.Json;
using SeafClient.Converters;

namespace SeafClient.Types
{
    public class SeafShareLinkPermissions
    {
        [JsonProperty("can_edit")]
        public virtual bool CanEdit { get; set; }

        [JsonProperty("can_download")]
        public virtual bool CanDownload { get; set; }
    }

    public class SeafShareLinkRequest
    {
        [JsonProperty("repo_id")]
        public virtual string LibraryId { get; set; }

        [JsonProperty("path")]
        public virtual string Path { get; set; }

        [JsonProperty("permissions")]
        public virtual SeafShareLinkPermissions Permission { get; set; }

        [JsonProperty("expire_days")]
        public virtual int ExpireDays { get; set; }
    }


    public class SeafShareLink
    {
        [JsonProperty("repo_id")]
        public virtual string LibraryId { get; set; }

        [JsonProperty("path")]
        public virtual string Path { get; set; }


        [JsonProperty("username")]
        public virtual string UserName { get; set; }
        
        [JsonProperty("ctime")]
        public virtual DateTime? CreationDate { get; set; }

        [JsonProperty("expire_date")]
        public virtual DateTime? ExpiryDate { get; set; }

        [JsonProperty("token")]
        public virtual string Id { get; set; }

        [JsonProperty("view_cnt")]
        public virtual int ViewCounter { get; set; }

        [JsonProperty("link")]
        public virtual string Link { get; set; }

        [JsonProperty("obj_name")]
        public virtual string Name { get; set; }

        [JsonProperty("is_dir")]
        public virtual bool IsDirectory { get; set; }

        [JsonProperty("is_expired")]
        public virtual bool IsExpired { get; set; }

        [JsonProperty("repo_name")]
        public virtual string LibraryName { get; set; }

        [JsonProperty("permissions")]
        public virtual SeafShareLinkPermissions Permission { get; set; }

    }
}
