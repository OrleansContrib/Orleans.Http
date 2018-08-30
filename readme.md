# OrleansHttp

[![Build status](https://ci.appveyor.com/api/projects/status/06ps8j8tsh9s7qfx?svg=true)](https://ci.appveyor.com/project/richorama/orleanshttp)

> This project is alpha quality, and is published to collect community feedback.

An HTTP API into Microsoft Orleans.

This is an HTTP listener hosted as a bootstrap provider in an orleans silo. It uses reflection to
send messages to grains, and returns the results as JSON.

## Installation

Using Nuget command line:

```
PM> Install-Package OrleansHttp
```

Then register the bootstrap provider:

```c#
siloHost.Config.Globals.RegisterBootstrapProvider<Bootstrap>("http");
```

Alternatively you can do this with Orleans configuration:

```xml
<?xml version="1.0" encoding="utf-8"?>
<OrleansConfiguration xmlns="urn:orleans">
  <Globals>
    <BootstrapProviders>
      <Provider Type="OrleansHttp.Bootstrap" Name="Http" />
    </BootstrapProviders>
    ...
```

## Usage

Open this url in your browser: [`http://localhost:8080`](http://localhost:8080)

You can then call grains using this scheme:

```
http://localhost:8080/grain/IGrainInterface/PrimaryKey/MethodName/?parameter=value
```

you can optionally supply a class prefix:

```
http://localhost:8080/grain/IGrainInterface/PrimaryKey/MethodName/ClassPrefix/?parameter=value
```

parameters are serialized as JSON.

compound keys should supply be supplied with comma separation (I hope you don't use commas in your key name!):

```
http://localhost:8080/grain/IGrainInterface/1234,abc/MethodName/ClassPrefix/?argument=value
```

For example:

```
http://localhost:8080/grain/IManagementGrain/0/GetTotalActivationCount

http://localhost:8080/grain/IManagementGrain/0/GetHosts?onlyActive=true
```

Note, you can use `GET`, `POST` or `PUT` to send messages to your grains.

## Benchmarking

Using [benchmark.net](https://github.com/dotnet/BenchmarkDotNet) my results look like this (running in `Release` mode):

```
           Method |      Mean |    StdErr |    StdDev |
----------------- |---------- |---------- |---------- |
 DirectConnection | 1.5810 ms | 0.0161 ms | 0.1055 ms |
   HttpConnection | 2.8601 ms | 0.0329 ms | 0.1317 ms |
```

## Configuration

The API supports the following attributes in the configuration:

* `Port` : Set the number for the api to listen on.
* `Username` : Set a username for accessing the api (basic auth).
* `Password` : Set a password for accessing the api (basic auth).

```xml
<BootstrapProviders>
    <Provider Type="OrleansHttp.Bootstrap" Name="Http" Port="1234" Username="my_username" Password="my_password" />
</BootstrapProviders>
```

## License

MIT
