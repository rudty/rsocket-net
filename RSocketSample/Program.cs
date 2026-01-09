namespace RSocketSample;

using RSocket;
using RSocket.Transports;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

class Program
{
	//TODO Connection Cleanup on Unsubscribe/failure/etc
	//TODO General Error handling -> OnError

	static async Task Main(string[] args)
	{
		//var loopback = new LoopbackTransport();
		//var server = new EchoServer(loopback.Beyond);
		//await server.ConnectAsync();

		var client = new RSocketClient(new SocketTransport("tcp://127.0.0.1:7000/"), new RSocketOptions() { InitialRequestSize = 3 });
		//var client = new RSocketClient(new WebSocketTransport("ws://localhost:9092/"), new RSocketOptions() { InitialRequestSize = 3 });
		//var client = new RSocketClient(loopback);
		await client.ConnectAsync();

		//Console.WriteLine("Requesting Raw Protobuf Stream...");

		//var persondata = new Person() { Id = 1234, Name = "Someone Person", Address = new Address() { Line1 = "123 Any Street", Line2 = "Somewhere, LOC" } };
		//var personmetadata = new Person() { Id = 567, Name = "Meta Person", Address = new Address() { Line1 = "", Line2 = "" } };

		//Make a Raw binary call just to show how it's done.
		// var stream = client.RequestStream(
		// resultmapper: result => (Data: ProtobufNetSerializer.Deserialize<Person>(result.data), Metadata: ProtobufNetSerializer.Deserialize<Person>(result.metadata)),
		// data: ProtobufNetSerializer.Serialize(persondata), metadata: ProtobufNetSerializer.Serialize(personmetadata));
		//var serializePersonData = ProtobufNetSerializer.Serialize(persondata);
		//var serializePersonMetaData = ProtobufNetSerializer.Serialize(personmetadata);

		//var res = await client.RequestResponse(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("Hello Client")), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("Hello Client")));
		//var d = Encoding.UTF8.GetString(res.data);
		//Console.WriteLine(d);

		var iter = await client.RequestStream(Encoding.UTF8.GetBytes("Hello Client"), Encoding.UTF8.GetBytes("Hello Client"));

		await foreach (var item in iter)
		{
			var d = Encoding.UTF8.GetString(item.data);
			var m = Encoding.UTF8.GetString(item.metadata);

			Console.WriteLine("RECEIVE STREAM DATA:" + d);
			Console.WriteLine("RECEIVE STREAM META:" + m);
		}

		Console.WriteLine("--END--");
		//Console.WriteLine("\nRequesting String Serializer Stream...");
		//var serializePersonData = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("Hello Client"));
		//var serializePersonMetaData = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("Hello Client"));

		//await client.RequestStream(new A(), serializePersonData, serializePersonMetaData);

		//await foreach (var persons in stream)
		//{
		//	Console.WriteLine($"RawDemo.OnNext===>[{persons.Metadata}]{persons.Data}");
		//}

		//Console.WriteLine("\nRequesting String Serializer Stream...");

		//var stringclient = new RSocketClient.ForStrings(client);    //A simple client that uses UTF8 strings instead of bytes.
		//var demoResponse = stringclient.RequestStream("A Demo Payload");
		//await foreach (var result in demoResponse)
		//{
		//	Console.WriteLine($"StringDemo.OnNext===>{result}");
		//}

		Console.ReadKey();

		//var sender = from index in Observable.Interval(TimeSpan.FromSeconds(1)) select new Person() { Id = (int)index, Name = $"Person #{index:0000}" };
		//using (personclient.RequestChannel(obj).Subscribe(
		//	onNext: value => Console.WriteLine($"RequestChannel.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestChannel.OnComplete!")))
		//{
		//	Console.ReadKey();
		//}
	}
}

class A : System.IObserver<DataAndMetadata>
{
	public void OnCompleted()
	{
		Console.WriteLine("OnCompleted");
	}

	public void OnError(Exception error)
	{
		Console.WriteLine("OnError" + error);
	}

	public void OnNext(DataAndMetadata value)
	{
		Console.WriteLine("12313232"+value);
		try
		{
			var d = Encoding.UTF8.GetString(value.data);
			Console.WriteLine(d);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Exception"+ex);
		}
		//var person = ProtobufNetSerializer.Deserialize<Person>(value.data);
		//var metadata = ProtobufNetSerializer.Deserialize<Person>(value.metadata);
		//Console.WriteLine($"OnNext===>[{metadata}]{person}");
	}
}

class EchoServer : RSocketServer
{
	public EchoServer(IRSocketTransport transport, RSocketOptions options = default, int echoes = 2) : base(transport, options)
	{
		// Stream(request => request,
		// 	request => AsyncEnumerable.Repeat(request, echoes),
		// 	result => result);
	}
}
