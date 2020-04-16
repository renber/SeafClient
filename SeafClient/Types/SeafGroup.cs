using Newtonsoft.Json;
using SeafClient.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Types
{
    /// <summary>
    /// Represents a seafile group
    /// </summary>
    public class SeafGroup
    {
        /// <summary>
        /// The identifier of this group
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of this group
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The user who created this group
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// The number of message sin this group
        /// </summary>
        [JsonProperty("msgnum")]
        public int NumberOfMessages { get; set; }

        /// <summary>
        /// Time of the creation of this group
        /// </summary>
        [JsonProperty("ctime")]        
        public virtual ulong? CommitTime { get; set; }

        /// <summary>
        /// Time of the last modification of this group
        /// </summary>
        [JsonProperty("mtime")]
        [JsonConverter(typeof(SeafTimestampConverter))]
        public virtual DateTime? ModificationTime { get; set; }
    }

    /// <summary>
    /// A list of groups as returned by the ListGroupsRequest
    /// </summary>
    [JsonObject]
    public class SeafGroupList : IList<SeafGroup>
    {        
        [JsonProperty("replynum")]
        public int NumberOfReplies { get; set; }

        // *************************************
        // pass-through implementation for IList
        // *************************************

        public SeafGroup this[int index]
        {
	        get { return Groups[index]; }
	        set { Groups[index] = value; }
        }

        public IList<SeafGroup> Groups { get; private set; } = new List<SeafGroup>();

        public int Count => Groups.Count;

        public bool IsReadOnly => Groups.IsReadOnly;

        public void Add(SeafGroup item) => Groups.Add(item);

        public void Clear() => Groups.Clear();

        public bool Contains(SeafGroup item) => Groups.Contains(item);

        public void CopyTo(SeafGroup[] array, int arrayIndex) => Groups.CopyTo(array, arrayIndex);

        public IEnumerator<SeafGroup> GetEnumerator() => Groups.GetEnumerator();

        public int IndexOf(SeafGroup item) => Groups.IndexOf(item);

        public void Insert(int index, SeafGroup item) => Groups.Insert(index, item);

        public bool Remove(SeafGroup item) => Groups.Remove(item);

        public void RemoveAt(int index) => Groups.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => Groups.GetEnumerator();        
    }
}
