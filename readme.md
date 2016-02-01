# OrleansHttp

> This project is alpha quality, and is published to collect community feedback.

An HTTP API into Microsoft Orleans.

This is an HTTP listener hosted as a bootstrap provider in an orleans silo. It uses reflection to 
send messages to grains, and returns the results as JSON.

## Installation

Nuget is currently not available (coming soon).

* Build this project.
* Copy the assemblies to the location of your Orleans host.
* Add this bootstrap provider to your Orleans configuration:

```xml
<?xml version="1.0" encoding="utf-8"?>
<OrleansConfiguration xmlns="urn:orleans">
  <Globals>
    <BootstrapProviders>
      <Provider Type="OrleansHttp.Bootstrap" Name="Http" />
    </BootstrapProviders>
    ...
```

* Open this url in your browser: [`http://localhost:8080`](http://localhost:8080)

You can then call grains using this scheme:

```
http://localhost:8080/grain/IGrainInterface/PrimaryKey/MethodName/?argument=value
```

you can optionally supply a class prefix:

```
http://localhost:8080/grain/IGrainInterface/PrimaryKey/MethodName/ClassPrefix/?argument=value
```

grains with compound keys should supply the keys with comma separation:

```
http://localhost:8080/grain/IGrainInterface/1234,abc/MethodName/ClassPrefix/?argument=value
```

For example:

```
http://localhost:8080/grain/IManagementGrain/0/GetTotalActivationCount

http://localhost:8080/grain/IManagementGrain/0/GetHosts?onlyActive=true
```

Note, you can use `GET`, `POST` or `PUT` to send messages to your grains.

## Configuration

The API supports the following attributes in the configuration:

* `Port` : Set the the number for the api to listen on.
* `Username` : Set a username for accessing the api (basic auth).
* `Password` : Set a password for accessing the api (basic auth).

```xml
<BootstrapProviders>
    <Provider Type="OrleansHttp.Bootstrap" Name="Http" Port="1234" Username="my_username" Password="my_password" />
</BootstrapProviders>
```

## License

MIT
