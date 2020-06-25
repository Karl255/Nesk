using K6502Emu;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Timers;

namespace Nesk
{
	public sealed class Nesk
	{
		public ChannelReader<byte[]> VideoOutputChannelReader => VideoOutputChannel.Reader;

		private Channel<byte[]> VideoOutputChannel;
		private K6502 Cpu;
		private Timer ClockGen;
		private readonly byte[] BlankBuffer;

		public Nesk()
		{
			BlankBuffer = Shared.Resources.BlankBitmap;

			var bus = new Bus()
			{
				//TODO: add components
			};

			Cpu = new K6502(bus);

			VideoOutputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(32)
			{
				SingleWriter = true,
				SingleReader = true
			});

			ClockGen = new Timer(0.000558659217877095);
			ClockGen.Elapsed += async (s, e) =>
			{
				await Tick();
			};
		}

		public void Start() => ClockGen.Start();
		public void Stop() => ClockGen.Stop();

		public async Task Tick()
		{
			//TODO: implement proper ticking
			//Cpu.Tick();

			//create random noise and write it to the VideoOutputChannel
			byte[] buffer = (byte[])BlankBuffer.Clone();
			int start = buffer[0x0A];
			var rand = new Random();

			for (int y = 0; y < 240; y++)
			{
				for (int x = 0; x < 256; x++)
				{
					buffer[start + (y * 256 + x) * 3 + 0] = (byte)rand.Next();
					buffer[start + (y * 256 + x) * 3 + 1] = (byte)rand.Next();
					buffer[start + (y * 256 + x) * 3 + 2] = (byte)rand.Next();
				}
			}

			await VideoOutputChannel.Writer.WriteAsync(buffer);
		}
	}
}
