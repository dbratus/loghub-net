# LogHub client for .NET

To learn about LogHub, visit [LogHub repository](https://github.com/dbratus/loghub).

## Getting the client

The simplest way to obtain the client is to get it from NuGet.

```
PM> Install-Package LogHub
```

## Using the client

You can access logs by using LogHubClient class. Instances of this class are thread-safe, so you can use a single instance of LogHubClient to write logs from multiple sources in a single application domain.

Writing logs.

```C#
using LogHub;

...

var options = new ClientOptions 
{
	MaxConnections = 1,
	User = "username",
	Password = "secret",
	UseTls = true
}

//Connecting to a log at hostname:10000.
using (var cli = new LogHubClient("hostname", 10000, options)) 
{
	//Logging errors.
	cli.Error += 
		(sender, args) => Console.WriteLine(args.Exception);
	
	//Writing an entry with severity 1.
	cli.Write(1, "Source", "Message.");
}

```

LogHubClient implements reading and management operations asynchronously by using channels from [NChannels library](https://github.com/dbratus/NChannels).

Reading logs.

```C#
using LogHub;
using NChannels;

...

var options = new ClientOptions 
{
	MaxConnections = 1,
	User = "username",
	Password = "secret",
	UseTls = true
}

//Connecting to a log or hub at hostname:10000.
using (var cli = new LogHubClient("hostname", 10000, options)) 
{
	var entriesChan = cli.Read(DateTime.Now - TimeSpan.FromSeconds(5), DateTime.Now, 0, 10, "Source");

	entriesChan.ForEach(ent => 
		... // Process entry.
	).Wait();
}
```

Truncating logs.

```C#
using LogHub;
using NChannels;

...

var options = new ClientOptions 
{
	MaxConnections = 1,
	User = "username",
	Password = "secret",
	UseTls = true
}

//Connecting to a log or hub at hostname:10000.
using (var cli = new LogHubClient("hostname", 10000, options)) 
{
	cli.Truncate(DateTime.Now).Wait();
}
```

Getting log stats.

```C#
using LogHub;
using NChannels;

...

var options = new ClientOptions 
{
	MaxConnections = 1,
	User = "username",
	Password = "secret",
	UseTls = true
}

//Connecting to a log or hub at hostname:10000.
using (var cli = new LogHubClient("hostname", 10000, options)) 
{
	var statChan = cli.Stat();

	statChan.ForEach(s => 
		... // Process stat.
	).Wait();
}
```