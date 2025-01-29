# StorageConnector: A Unified Interface for Cloud Storage.
**StorageConnector** is an open-source C# class library that provides a wrapper around existing services that are used to verify Mobile numbers and send messages.
## Features

- Covers Twilio, Plivo (possible to cover more if needed)
- Usage information is stored in your own MongoDB instance for audit reasons


## Contributing

We welcome contributions! If you find a bug, have an idea for improvement, please submit an issue or a pull request on GitHub.

## Getting Started

### [NuGet Package](https://www.nuget.org/packages/StorageConnector)

To include **StorageConnector** in your project, [install the NuGet package](https://www.nuget.org/packages/StorageConnector):

```bash
dotnet add package StorageConnector
```
Then in your `appsettings.json` add the following sample configuration and change the values to match the details of your credentials to the various services.
```json
   "StorageConnectors": {
    "Azure": {
      "CountryIsoCodeMapToAccountName": {
        "IN": "globolpayprofilepicseu"
      },
      "Accounts": [
        {
          "AccountName": "globolpayprofilepicseu",
          "AccountKey": "59fLrSFyFDvGaEL+eJhMugkX/mgSfIHzrs74mG2WfJsX6VAlVfepZmI55QnnhWqYQz7SeAqLCj+AStQHx8DQ==",
          "ContainerName": "profilepics"
        }
      ]
    }
  }
  ```

After the above is done, you can just Dependency inject the `StorageConnector` in your C# class.

#### For example:



```csharp
TODO

```

### GitHub Repository
Visit our GitHub repository for the latest updates, documentation, and community contributions.
https://github.com/prmeyn/StorageConnector


## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE.

Happy coding! 🚀🌐📚


