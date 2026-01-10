namespace Test;

using System.IO.Pipelines;

public class UnitTest1
{
	[Fact]
	public void Test1()
	{
		var p = new Pipe();
		var w = p.Writer;
		w.GetMemory();
		
	}
}
