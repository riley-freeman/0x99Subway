using Crayon.Box;

var uri = new Uri($"wss://{Config.ServerAddress}:{Config.ServerPort}/station-clock-in");
var station = new Client(uri, null, null);
station.Run().Wait();
station.Dispose();

