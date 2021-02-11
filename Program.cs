using System;
using System.Threading.Tasks;
using LogRecord;

namespace LogTest
{
	class Program
	{
		static async Task Main(string[] args)
		{
			using Log Test = new(@"\Log.txt");
			for (int i = 0; i < 1000; i++)
			{
				Test.Message(i.ToString());
			}
			// 忙しい処理
			await Task.Delay(12000);
		}
	}
}
