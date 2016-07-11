# SeafClient

This project is a client implementation for the [Seafile](https://www.seafile.com) Web API  for .Net as portable class library (PCL).
It can be used in desktop apps, Windows Universal Platform Projects (UWP), Windows Store applications as well as in apps for Windows Phone 7.5+, 8.x and on Windows 10 Mobile.

The aim is to create a library to easily access a Seafile server and the files stored there through a .Net application in a strong-typed fashion (no custom JSON parsing and with meaningful error messages, etc.) The library uses async/await methods for requests to the Seafile server.

The current stable release of the SeafClient library is available on NuGet [here](https://www.nuget.org/packages/SeafClient/).

This repository is automatically built and tested with AppVeyor: <br/>
![build status](https://ci.appveyor.com/api/projects/status/github/renber/seafclient?svg=true) <br/>
![test status](http://teststatusbadge.azurewebsites.net/api/status/renber/seafclient)

## Usage example (C#)

```C#
using SeafClient;

async Task Demo()
{
    Uri serverUri= new Uri("https://seacloud.cc", UriKind.Absolute);
    string username = "testuser@internet.com";
    char[] password = new char[] { 't', 'e', 's', 't' };

    try {
        // authenticate with the Seafile server and retrieve a Session
        SeafSession session = await SeafSession.Establish(serverUri, username, password);
        
        // connection was successful
        // btw: the password array is now empty (all elements are now char 0)
        // now retrieve some information about the account
        var accountInfo = await session.CheckAccountInfo();
        Debug.WriteLine(String.Format("Nickname: {0}\nUsed Storage: {1:d} Bytes\nQuota: {2}",
            accountInfo.Nickname, accountInfo.Usage, accountInfo.Usage, accountInfo.HasUnlimitedSpace ? "unlimited" : 
            (accountInfo.Quota.ToString() + "Bytes")));
        // get the url of the user avatar
        var userAvatar = await session.GetUserAvatar(128);
        Debug.WriteLine("Url to user's avatar: " + userAvatar.Url);

        // get the libraries
        var libraries = await session.ListLibraries();
        foreach (var lib in libraries)
            Debug.WriteLine(lib.Name + " " + lib.Timestamp.ToString());

        // list root contents of the first library
        var firstLib = libraries.FirstOrDefault();
        if (firstLib != null)
        {
            var content = await session.ListDirectory(firstLib, "/");
            foreach (var dirEntry in content)
                if (dirEntry.Type == SeafClient.Types.DirEntryType.File)
                    Debug.WriteLine(dirEntry.Name + " - " + dirEntry.Size.ToString() + " Bytes");
                else
                    Debug.WriteLine(dirEntry.Name);
        }

    } catch (SeafException e)
    {
                Debug.WriteLine(String.Format("An error occured: {0} (ErrorCode: {1} ({2}))", e.Message, 
                e.SeafError.SeafErrorCode, e.SeafError.HttpStatusCode));
    }
}
```

## Currently implemented requests
See the [official Seafile Web API documentation](http://manual.seafile.com/develop/web_api.html) for a list of all available requests. The following requests are currently implemented:

* Authentication
* Ping (with and without authentication)
* Get Server Info
* Check Account Info
* Get User Avatar
* List Libraries / List Shared Libraries
* List Directory Entries
* Create Directory
* Delete Directory / File
* Rename Directory / File
* Copy / Move File
* Get Thumbnail Image
* Get Download Link
* Get Upload Link / Upload file
* Starred files (list / star / unstar)
* Decrypt an encrypted library (allowing to download files from the encrypted library)
