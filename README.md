# N2O.NET

## Build & Run
Windows, Unix, Linux, Mac:

```cmd
..\n2o>dotnet run
```

## Notes
* No 3rd-party libs, just base default library
* Minimalism. Size of everything matters
* https://github.com/atemerev/skynet
* Make future for each connection to emulate CML lib of SML
* Avoid exceptions
* https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_servers
* https://hackernoon.com/implementing-a-websocket-server-with-node-js-d9b78ec5ffa8
* https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket?view=netframework-4.7.2
* https://docs.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket?view=netframework-4.7.2

## Measure
```
$ tcpkali --ws -T 20s -r 100000 -c 2 -m "PING" --latency-marker "PING" 127.0.0.1:8989
Destination: [127.0.0.1]:8989
Interface lo address [127.0.0.1]:0
Using interface lo to connect to [127.0.0.1]:8989
Ramped up to 2 connections.
Total data sent:     11.3 MiB (11797506 bytes)
Total data received: 3.8 MiB (3988306 bytes)
Bandwidth per channel: 3.157⇅ Mbps (394.6 kBps)
Aggregate bandwidth: 1.595↓, 4.719↑ Mbps
Packet rate estimate: 24167.5↓, 532.4↑ (1↓, 2↑ TCP MSS/op)
Message latency at percentiles: 15577.5/15884.7/15935.9 ms (95/99/99.5%)
Test duration: 20.0018 s.
```


## Road map
- [x] https://github.com/o1/n2o/commits/master/src/server.sml
- [ ] https://github.com/o1/n2o/blob/master/src/websocket.sml
- [ ] n2o
- [ ] nitro
