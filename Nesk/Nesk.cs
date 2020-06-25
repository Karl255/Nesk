using K6502Emu;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nesk
{
	public sealed class Nesk
	{
		public ChannelReader<byte[]> VideoOutputChannelReader => VideoOutputChannel.Reader;

		private Channel<byte[]> VideoOutputChannel;
		private K6502 Cpu;
		private readonly byte[] BlankBuffer;
		private long TickCounter = 0;

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

		}

		public async Task Run(CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				long t1 = Stopwatch.GetTimestamp();
				await Tick();

				/*
				 * 0.5 ms delay, although it should be 0.558659217877095 us (microseconds)
				 * the method returns the amount of ticks counted at a frequency of 10 MHz
				 * (at least on my machine, TODO: make this universal), so every tick is
				 * 100 ns or 0.1 us
				 */
				while (Stopwatch.GetTimestamp() - t1 < 5)
					Thread.Sleep(0);
			}
		}

		public async Task Tick()
		{
			TickCounter++;
			if (TickCounter < 16_639) //create new frames at ~60 Hz
				return;

			TickCounter = 0;
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
